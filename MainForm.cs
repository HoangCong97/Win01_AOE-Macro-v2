using System.Data;
using System.Drawing.Text;
using AOEKeyboardMacroPro.Models;
using AOEKeyboardMacroPro.Services;

namespace AOEKeyboardMacroPro;

public partial class MainForm : Form
{
    private readonly ControlEngine _controlEngine = new();
    private bool _isDarkMode = false;
    private readonly List<KeyMapItem> _keyMappings = new();
    private readonly ResourceOcrService _ocrService = new();
    private readonly PopOcrService _popOcrService = new();
    private readonly TimerOcrService _timerOcrService = new();
    private MiniHudForm? _hudForm;
    private volatile TimerValues _currentTimer = new();
    private readonly ResourceValues _lastKnownResources = new();
    private readonly PopValues _lastKnownPop = new();
    private readonly TimerValues _lastKnownTimer = new();
    private bool _isDataDimmed = true;
    private DateTime _popSuppressedUntil = DateTime.MinValue;

    public MainForm()
    {
        InitializeComponent();

        // Bật Double Buffering toàn diện để cửa sổ kéo mượt mà, triệt tiêu khựng giật
        this.DoubleBuffered = true;
        SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint, true);
        EnableDoubleBuffer(pnlOuterBorder);
        EnableDoubleBuffer(pnlKeyboardGrid);
        EnableDoubleBuffer(pnlResourceRow);
        pnlResourceRow.Paint += PnlResourceRow_Paint;
        foreach (Control c in pnlResourceRow.Controls)
        {
            c.Visible = false;
        }

        if (File.Exists("app_icon.ico"))
        {
            try { this.Icon = new Icon("app_icon.ico"); } catch { }
        }
        var settings = ConfigService.LoadSettings();
        int initialInterval = Math.Clamp(settings.FarmTimerInterval, (int)numFarmInterval.Minimum, (int)numFarmInterval.Maximum);
        numFarmInterval.Value = initialInterval;
        _controlEngine.SetFarmTimerInterval(initialInterval);
        numFarmInterval.Leave += NumFarmInterval_Leave;
        numFarmInterval.KeyUp += NumFarmInterval_KeyUp;

        // Click vào bất kỳ đâu trên form/panel sẽ bỏ focus khỏi ô nhập liệu
        pnlOuterBorder.Click += (s, e) => { btnToggleMacro.Focus(); };
        this.Click += (s, e) => { btnToggleMacro.Focus(); };
        rtbLog.Click += (s, e) => { rtbLog.Focus(); };

        InitializeKeyMappings();
        BuildKeyboardGridUI();

        _controlEngine.StateChanged += OnEngineStateChanged;
        _controlEngine.LogRequested += AppendLog;
        _controlEngine.FarmTimerUpdated += OnFarmTimerUpdated;
        _controlEngine.HouseBeBuildingTriggered += OnHouseBeBuildingTriggered;
        _ocrService.ResourcesUpdated += OnResourcesUpdated;
        _ocrService.InGameStatusChanged += OnInGameStatusChangedFromResource;
        _popOcrService.PopUpdated += OnPopUpdated;
        _timerOcrService.TimerUpdated += OnTimerUpdated;

        ApplyTheme();
        UpdateStatusUI(_controlEngine.CurrentState);
        UpdateFarmTimerUI(-1, -1);
    }

    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);
        RestoreWindowPosition();
        _controlEngine.Start();
        _ocrService.Start();
        _popOcrService.Start();
        _timerOcrService.Start();

        var hudSettings = ConfigService.LoadSettings().Hud ?? new HudSettings();
        _hudForm = new MiniHudForm(hudSettings);
        _hudForm.SetDimmed(true);
        if (hudSettings.Enabled)
        {
            _hudForm.Show();
        }
        UpdateHudButtonText();
        ApplyResourceLabelColors();
        RenderAllDataText();
    }

    private void RestoreWindowPosition()
    {
        try
        {
            var settings = ConfigService.LoadSettings();
            if (settings.WindowX.HasValue && settings.WindowY.HasValue)
            {
                int x = settings.WindowX.Value;
                int y = settings.WindowY.Value;

                bool isVisibleOnAnyScreen = false;
                foreach (var screen in Screen.AllScreens)
                {
                    if (screen.WorkingArea.IntersectsWith(new Rectangle(x, y, Width, Height)))
                    {
                        isVisibleOnAnyScreen = true;
                        break;
                    }
                }

                if (isVisibleOnAnyScreen)
                {
                    StartPosition = FormStartPosition.Manual;
                    Location = new Point(x, y);
                }
            }
        }
        catch { }
    }

    private void SaveWindowPosition()
    {
        try
        {
            var appSettings = ConfigService.LoadSettings();
            appSettings.FarmTimerInterval = (int)numFarmInterval.Value;
            if (WindowState == FormWindowState.Normal)
            {
                appSettings.WindowX = Location.X;
                appSettings.WindowY = Location.Y;
            }
            ConfigService.SaveSettings(appSettings);
        }
        catch { }
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        SaveWindowPosition();

        _hudForm?.SavePosition();
        _hudForm?.Close();
        _hudForm?.Dispose();

        _ocrService.Stop();
        _ocrService.Dispose();

        _popOcrService.Stop();
        _popOcrService.Dispose();

        _timerOcrService.Stop();
        _timerOcrService.Dispose();

        _controlEngine.Stop();
        _controlEngine.Dispose();
        base.OnFormClosing(e);
    }

    protected override CreateParams CreateParams
    {
        get
        {
            CreateParams cp = base.CreateParams;
            cp.Style |= 0x02000000; // WS_CLIPCHILDREN: Loại bỏ vùng control con khi vẽ nền, tránh repaint thừa
            return cp;
        }
    }

    protected override void WndProc(ref Message m)
    {
        switch (m.Msg)
        {
            case NativeMethods.WM_ENTERSIZEMOVE:
                // Tắt vẽ GDI trong suốt quá trình kéo cửa sổ
                // Windows DWM sẽ tự động di chuyển window frame bằng GPU siêu mượt, 0% CPU
                NativeMethods.SendMessage(Handle, NativeMethods.WM_SETREDRAW, IntPtr.Zero, IntPtr.Zero);
                SuspendLayout();
                break;

            case NativeMethods.WM_EXITSIZEMOVE:
                // Bật lại vẽ GDI và layout ngay khi thả chuột
                NativeMethods.SendMessage(Handle, NativeMethods.WM_SETREDRAW, (IntPtr)1, IntPtr.Zero);
                ResumeLayout(true);
                Invalidate(true);
                Update();
                SaveWindowPosition();
                break;
        }

        base.WndProc(ref m);
    }

    private void InitializeKeyMappings()
    {
        // Row 0
        _keyMappings.Add(new KeyMapItem("Q", "C", "Xin dân C", 0, 0));
        _keyMappings.Add(new KeyMapItem("W", "H", "Về nhà chính H", 0, 1));
        _keyMappings.Add(new KeyMapItem("E", "E", "Xây nhà E", 0, 2));
        _keyMappings.Add(new KeyMapItem("R", "S", "Xây nhà S", 0, 3));
        _keyMappings.Add(new KeyMapItem("T", "G", "Xây nhà G", 0, 4));
        _keyMappings.Add(new KeyMapItem("Y", "", "", 0, 5));
        _keyMappings.Add(new KeyMapItem("U", "", "", 0, 6));
        _keyMappings.Add(new KeyMapItem("I", "", "", 0, 7));

        // Row 1
        _keyMappings.Add(new KeyMapItem("A", "A", "Xây/Duyệt nhà A", 1, 0));
        _keyMappings.Add(new KeyMapItem("S", "L", "Xây/Duyệt nhà L", 1, 1));
        _keyMappings.Add(new KeyMapItem("D", "B", "Xây/Duyệt nhà B", 1, 2));
        _keyMappings.Add(new KeyMapItem("F", "F", "Ruộng 1 (BF)", 1, 3));
        _keyMappings.Add(new KeyMapItem("G", "F", "Ruộng 2 (BF)", 1, 4));
        _keyMappings.Add(new KeyMapItem("H", "", "", 1, 5));
        _keyMappings.Add(new KeyMapItem("J", "", "", 1, 6));
        _keyMappings.Add(new KeyMapItem("K", "", "", 1, 7));

        // Row 2
        _keyMappings.Add(new KeyMapItem("Z", "K", "Xây/Duyệt nhà K", 2, 0));
        _keyMappings.Add(new KeyMapItem("X", "Y", "Xây/Duyệt nhà Y", 2, 1));
        _keyMappings.Add(new KeyMapItem("C", "P", "Xây/Duyệt nhà P", 2, 2));
        _keyMappings.Add(new KeyMapItem("V", "M", "Xây nhà M", 2, 3));
        _keyMappings.Add(new KeyMapItem("B", "C", "Xây nhà C", 2, 4));
        _keyMappings.Add(new KeyMapItem("N", "", "", 2, 5));
        _keyMappings.Add(new KeyMapItem("M", "", "", 2, 6));
        _keyMappings.Add(new KeyMapItem(",", "", "", 2, 7));
    }

    private void BuildKeyboardGridUI()
    {
        pnlKeyboardGrid.SuspendLayout();
        pnlKeyboardGrid.Controls.Clear();

        foreach (var item in _keyMappings)
        {
            var cellPanel = CreateKeyCellPanel(item);
            pnlKeyboardGrid.Controls.Add(cellPanel, item.Column, item.Row);
        }

        pnlKeyboardGrid.ResumeLayout();
    }

    private Panel CreateKeyCellPanel(KeyMapItem item)
    {
        Panel panel = new()
        {
            Dock = DockStyle.Fill,
            Margin = new Padding(0),
            Name = "cellPanel"
        };

        Label lblPhysical = new()
        {
            Text = item.PhysicalKey,
            Font = new Font("Segoe UI", 9.5F, FontStyle.Regular, GraphicsUnit.Point, 0),
            AutoSize = true,
            Name = "lblPhysical",
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
            Location = new Point(panel.Width - 22, 2)
        };

        panel.SizeChanged += (s, e) =>
        {
            lblPhysical.Location = new Point(panel.Width - 24, 2);
        };

        Label lblMapped = new()
        {
            Text = item.MappedKey,
            Font = new Font("Segoe UI", 18F, FontStyle.Bold, GraphicsUnit.Point, 0),
            Dock = DockStyle.Fill,
            Name = "lblMapped",
            TextAlign = ContentAlignment.MiddleCenter
        };

        panel.Controls.Add(lblPhysical);
        panel.Controls.Add(lblMapped);
        lblPhysical.BringToFront();

        return panel;
    }

    private void BtnThemeToggle_Click(object? sender, EventArgs e)
    {
        _isDarkMode = !_isDarkMode;
        ApplyTheme();
        AppendLog($"Đã chuyển sang giao diện: {(_isDarkMode ? "Tối (Dark Theme)" : "Sáng (Light Theme)")}", _isDarkMode ? Color.Cyan : Color.Blue);
    }

    private void BtnToggleHud_Click(object? sender, EventArgs e)
    {
        if (_hudForm == null || _hudForm.IsDisposed)
        {
            var hudSettings = ConfigService.LoadSettings().Hud ?? new HudSettings();
            _hudForm = new MiniHudForm(hudSettings);
            _hudForm.SetDimmed(_isDataDimmed);
            _hudForm.UpdateResources(_lastKnownResources);
            _hudForm.UpdatePop(_lastKnownPop);
            _hudForm.UpdateTimer(_lastKnownTimer);
            if (_popSuppressedUntil > DateTime.UtcNow)
            {
                int remaining = (int)Math.Ceiling((_popSuppressedUntil - DateTime.UtcNow).TotalSeconds);
                _hudForm.SuppressPopWarning(remaining);
            }
        }

        if (_hudForm.Visible)
        {
            _hudForm.Hide();
            var s = ConfigService.LoadSettings();
            s.Hud ??= new HudSettings();
            s.Hud.Enabled = false;
            ConfigService.SaveSettings(s);
        }
        else
        {
            _hudForm.Show();
            var s = ConfigService.LoadSettings();
            s.Hud ??= new HudSettings();
            s.Hud.Enabled = true;
            ConfigService.SaveSettings(s);
        }
        UpdateHudButtonText();
    }

    private void UpdateHudButtonText()
    {
        bool isShown = _hudForm != null && !_hudForm.IsDisposed && _hudForm.Visible;
        btnToggleHud.Text = isShown ? "🖥️ Mini HUD: Bật" : "🖥️ Mini HUD: Tắt";
    }

    private void NumFarmInterval_ValueChanged(object? sender, EventArgs e)
    {
        int val = (int)numFarmInterval.Value;
        _controlEngine.SetFarmTimerInterval(val);
        var s = ConfigService.LoadSettings();
        s.FarmTimerInterval = val;
        ConfigService.SaveSettings(s);
    }

    private void NumFarmInterval_Leave(object? sender, EventArgs e)
    {
        if (decimal.TryParse(numFarmInterval.Text, out decimal typedVal))
        {
            decimal clampedVal = Math.Clamp(typedVal, numFarmInterval.Minimum, numFarmInterval.Maximum);
            if (numFarmInterval.Value != clampedVal)
            {
                numFarmInterval.Value = clampedVal;
            }
        }
        int val = (int)numFarmInterval.Value;
        _controlEngine.SetFarmTimerInterval(val);
        var s = ConfigService.LoadSettings();
        s.FarmTimerInterval = val;
        ConfigService.SaveSettings(s);
    }

    private void NumFarmInterval_KeyUp(object? sender, KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Enter)
        {
            pnlOuterBorder.Focus();
            e.Handled = true;
            e.SuppressKeyPress = true;
        }
    }

    private void ApplyTheme()
    {
        Color bgColor = _isDarkMode ? Color.FromArgb(18, 18, 18) : Color.White;
        Color panelBg = _isDarkMode ? Color.FromArgb(30, 30, 30) : Color.White;
        Color textColor = _isDarkMode ? Color.White : Color.Black;
        Color subTextColor = _isDarkMode ? Color.FromArgb(180, 180, 180) : Color.FromArgb(80, 80, 80);
        Color borderColor = _isDarkMode ? Color.FromArgb(80, 80, 80) : Color.Black;
        Color cellBg = _isDarkMode ? Color.FromArgb(40, 40, 45) : Color.White;
        Color logBg = _isDarkMode ? Color.FromArgb(24, 24, 28) : Color.White;

        BackColor = bgColor;
        pnlOuterBorder.BackColor = panelBg;
        pnlOuterBorder.BorderStyle = BorderStyle.FixedSingle;

        lblTitle.ForeColor = textColor;
        lblStatusTitle.ForeColor = textColor;
        lblFarmTimerSeparator.ForeColor = _isDarkMode ? Color.Gray : Color.LightGray;
        lblFarmIntervalConfig.ForeColor = textColor;
        lblSecondsUnit.ForeColor = textColor;
        lblGridTitle.ForeColor = textColor;
        lblLogTitle.ForeColor = textColor;
        chkAutoScroll.ForeColor = textColor;

        numFarmInterval.BackColor = cellBg;
        numFarmInterval.ForeColor = textColor;

        btnThemeToggle.Text = _isDarkMode ? "☀️ Giao diện sáng" : "🌙 Giao diện tối";
        btnThemeToggle.BackColor = cellBg;
        btnThemeToggle.ForeColor = textColor;
        btnThemeToggle.FlatAppearance.BorderColor = borderColor;

        btnToggleHud.BackColor = cellBg;
        btnToggleHud.ForeColor = textColor;
        btnToggleHud.FlatAppearance.BorderColor = borderColor;
        UpdateHudButtonText();

        btnToggleMacro.BackColor = cellBg;
        btnToggleMacro.ForeColor = textColor;
        btnToggleMacro.FlatAppearance.BorderColor = borderColor;

        btnClearLog.BackColor = cellBg;
        btnClearLog.ForeColor = textColor;
        btnClearLog.FlatAppearance.BorderColor = borderColor;

        rtbLog.BackColor = logBg;
        rtbLog.ForeColor = textColor;

        Color resPanelBg = _isDarkMode ? Color.FromArgb(28, 28, 34) : Color.FromArgb(245, 246, 250);
        pnlResourceRow.BackColor = resPanelBg;
        pnlResourceRow.BorderStyle = BorderStyle.FixedSingle;

        ApplyResourceLabelColors();

        foreach (Control ctrl in pnlKeyboardGrid.Controls)
        {
            if (ctrl is Panel cell)
            {
                cell.BackColor = cellBg;
                foreach (Control subCtrl in cell.Controls)
                {
                    if (subCtrl.Name == "lblPhysical")
                        subCtrl.ForeColor = subTextColor;
                    else if (subCtrl.Name == "lblMapped")
                        subCtrl.ForeColor = textColor;
                }
            }
        }
    }

    public void ReloadOcrSettings()
    {
        var s = ConfigService.LoadSettings().ResourceCrop ?? new ResourceCropSettings();
        _ocrService.UpdateSettings(s);
        _ocrService.LoadTemplates();

        var popSettings = ConfigService.LoadSettings().PopCrop ?? new PopCropSettings();
        _popOcrService.UpdateSettings(popSettings);
        _popOcrService.LoadTemplates();
    }

    private void OnInGameStatusChangedFromResource(bool inGame)
    {
        _controlEngine.UpdateInGameStatus(inGame);

        if (IsDisposed) return;
        if (InvokeRequired)
        {
            try
            {
                BeginInvoke(new Action<bool>(UpdateInGameUI), inGame);
            }
            catch { }
        }
        else
        {
            UpdateInGameUI(inGame);
        }
    }

    private void UpdateInGameUI(bool inGame)
    {
        _isDataDimmed = !inGame;
        if (!inGame)
        {
            _currentTimer = new TimerValues();
        }
        _hudForm?.SetDimmed(_isDataDimmed);
        ApplyResourceLabelColors();
        RenderAllDataText();
    }

    private void ApplyResourceLabelColors()
    {
        pnlResourceRow.Invalidate();
    }

    private void RenderAllDataText()
    {
        RenderResourceText();
        RenderPopText();
        RenderTimerText();
    }

    private void RenderResourceText()
    {
        lblResourceWood.Text = $"🪵 Gỗ: {ResourceValues.Format(_lastKnownResources.Wood)}";
        lblResourceFood.Text = $"🥩 Thịt: {ResourceValues.Format(_lastKnownResources.Food)}";
        lblResourceGold.Text = $"🪙 Vàng: {ResourceValues.Format(_lastKnownResources.Gold)}";
        lblResourceStone.Text = $"🪨 Đá: {ResourceValues.Format(_lastKnownResources.Stone)}";
        pnlResourceRow.Invalidate();
    }

    private void RenderPopText()
    {
        lblResourcePop.Text = $"👥 POP: {(_lastKnownPop.IsValid ? $"{_lastKnownPop.CurrentPop}/{_lastKnownPop.MaxPop}" : (_lastKnownPop.CurrentPop.HasValue ? $"{_lastKnownPop.CurrentPop}/--" : "--/--"))}";
        pnlResourceRow.Invalidate();
    }

    private void RenderTimerText()
    {
        lblResourceRate.Text = $"⏱️ {(_lastKnownTimer.IsValid ? _lastKnownTimer.RawText : "--:--")}";
        pnlResourceRow.Invalidate();
    }

    private void PnlResourceRow_Paint(object? sender, PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

        Color woodColor = _isDarkMode ? Color.FromArgb(70, 210, 130) : Color.FromArgb(46, 125, 50);
        Color foodColor = _isDarkMode ? Color.FromArgb(255, 95, 95) : Color.FromArgb(198, 40, 40);
        Color goldColor = _isDarkMode ? Color.FromArgb(255, 215, 60) : Color.FromArgb(210, 115, 25);
        Color stoneColor = _isDarkMode ? Color.FromArgb(100, 210, 255) : Color.FromArgb(25, 118, 210);
        Color popColor = _isDarkMode ? Color.FromArgb(235, 130, 255) : Color.FromArgb(142, 36, 170);
        Color timerColor = _isDarkMode ? Color.FromArgb(100, 210, 255) : Color.FromArgb(0, 130, 220);
        Color sepColor = _isDarkMode ? Color.FromArgb(80, 85, 95) : Color.LightGray;
        Color dimmedValColor = _isDarkMode ? Color.FromArgb(120, 125, 135) : Color.FromArgb(145, 150, 158);

        using var font = new Font("Segoe UI", 10F, FontStyle.Bold);
        using var sepFont = new Font("Segoe UI", 9F, FontStyle.Regular);

        int y = 5;

        void DrawItemAt(int startX, string title, string val, Color itemColor)
        {
            // 1. Đại lượng (luôn giữ màu sắc đặc trưng, KHÔNG bị làm mờ)
            TextRenderer.DrawText(g, title, font, new Point(startX, y), itemColor, TextFormatFlags.NoPadding);
            Size titleSz = TextRenderer.MeasureText(g, title, font, Size.Empty, TextFormatFlags.NoPadding);

            // 2. Giá trị (chỉ giá trị bị làm mờ khi ngoài trận / timeout 1s)
            Color valColor = _isDataDimmed ? dimmedValColor : itemColor;
            TextRenderer.DrawText(g, val, font, new Point(startX + titleSz.Width, y), valColor, TextFormatFlags.NoPadding);
        }

        void DrawSepAt(int sepX)
        {
            TextRenderer.DrawText(g, "|", sepFont, new Point(sepX, y + 1), sepColor, TextFormatFlags.NoPadding);
        }

        // 1. Gỗ (X = 10, Phân cách = 120)
        DrawItemAt(10, "🪵 Gỗ: ", ResourceValues.Format(_lastKnownResources.Wood), woodColor);
        DrawSepAt(120);

        // 2. Thịt (X = 135, Phân cách = 250)
        DrawItemAt(135, "🥩 Thịt: ", ResourceValues.Format(_lastKnownResources.Food), foodColor);
        DrawSepAt(250);

        // 3. Vàng (X = 265, Phân cách = 385)
        DrawItemAt(265, "🪙 Vàng: ", ResourceValues.Format(_lastKnownResources.Gold), goldColor);
        DrawSepAt(385);

        // 4. Đá (X = 400, Phân cách = 515)
        DrawItemAt(400, "🪨 Đá: ", ResourceValues.Format(_lastKnownResources.Stone), stoneColor);
        DrawSepAt(515);

        // 5. POP (X = 530)
        string popVal = _lastKnownPop.IsValid
            ? $"{_lastKnownPop.CurrentPop}/{_lastKnownPop.MaxPop}"
            : (_lastKnownPop.CurrentPop.HasValue ? $"{_lastKnownPop.CurrentPop}/--" : "--/--");
        DrawItemAt(530, "👥 POP: ", popVal, popColor);

        // 6. Timer (Vẽ sát mép phải)
        string timerTitle = "⏱️ ";
        string timerVal = _lastKnownTimer.IsValid ? _lastKnownTimer.RawText! : "--:--";
        Size timerTitleSz = TextRenderer.MeasureText(g, timerTitle, font, Size.Empty, TextFormatFlags.NoPadding);
        Size timerValSz = TextRenderer.MeasureText(g, timerVal, font, Size.Empty, TextFormatFlags.NoPadding);
        int timerTotalW = timerTitleSz.Width + timerValSz.Width;
        int timerX = pnlResourceRow.Width - timerTotalW - 14;

        TextRenderer.DrawText(g, timerTitle, font, new Point(timerX, y), timerColor, TextFormatFlags.NoPadding);
        Color timerValColor = _isDataDimmed ? dimmedValColor : timerColor;
        TextRenderer.DrawText(g, timerVal, font, new Point(timerX + timerTitleSz.Width, y), timerValColor, TextFormatFlags.NoPadding);
    }

    private void OnResourcesUpdated(ResourceValues res)
    {
        _controlEngine.TryTriggerAutoFastStart(res, _timerOcrService.CurrentRealtimeTimer);

        if (IsDisposed) return;

        if (InvokeRequired)
        {
            try
            {
                BeginInvoke(new Action<ResourceValues>(UpdateResourceUI), res);
            }
            catch { }
        }
        else
        {
            UpdateResourceUI(res);
        }
    }

    private void UpdateResourceUI(ResourceValues res)
    {
        if (res != null && !res.IsEmpty)
        {
            if (res.Wood.HasValue) _lastKnownResources.Wood = res.Wood;
            if (res.Food.HasValue) _lastKnownResources.Food = res.Food;
            if (res.Gold.HasValue) _lastKnownResources.Gold = res.Gold;
            if (res.Stone.HasValue) _lastKnownResources.Stone = res.Stone;
        }

        RenderResourceText();
        _hudForm?.UpdateResources(res);
    }

    private void OnHouseBeBuildingTriggered()
    {
        if (IsDisposed) return;

        if (InvokeRequired)
        {
            try
            {
                BeginInvoke(new Action(OnHouseBeBuildingTriggered));
            }
            catch { }
            return;
        }

        _popSuppressedUntil = DateTime.UtcNow.AddSeconds(20);
        _hudForm?.SuppressPopWarning(20);
    }

    private void OnPopUpdated(PopValues pop)
    {
        if (IsDisposed) return;

        if (InvokeRequired)
        {
            try
            {
                BeginInvoke(new Action<PopValues>(UpdatePopUI), pop);
            }
            catch { }
        }
        else
        {
            UpdatePopUI(pop);
        }
    }

    private void UpdatePopUI(PopValues pop)
    {
        if (pop != null && (pop.IsValid || pop.CurrentPop.HasValue))
        {
            if (pop.CurrentPop.HasValue) _lastKnownPop.CurrentPop = pop.CurrentPop;
            if (pop.MaxPop.HasValue) _lastKnownPop.MaxPop = pop.MaxPop;
        }

        RenderPopText();
        _hudForm?.UpdatePop(pop);
    }

    private void OnTimerUpdated(TimerValues timer)
    {
        _currentTimer = timer;

        if (IsDisposed) return;

        if (InvokeRequired)
        {
            try
            {
                BeginInvoke(new Action<TimerValues>(UpdateTimerUI), timer);
            }
            catch { }
        }
        else
        {
            UpdateTimerUI(timer);
        }
    }

    private void UpdateTimerUI(TimerValues timer)
    {
        if (timer != null && timer.IsValid)
        {
            _lastKnownTimer.RawText = timer.RawText;
        }

        RenderTimerText();
        _hudForm?.UpdateTimer(timer);
    }

    private void OnFarmTimerUpdated(int rem1, int rem2)
    {
        if (IsDisposed) return;

        if (InvokeRequired)
        {
            Invoke(new Action<int, int>(UpdateFarmTimerUI), rem1, rem2);
        }
        else
        {
            UpdateFarmTimerUI(rem1, rem2);
        }
    }

    private void UpdateFarmTimerUI(int rem1, int rem2)
    {
        // Ruộng 1
        if (rem1 > 0)
        {
            lblFarmTimer1.Text = $"🌾 Ruộng 1 (Ctrl+F): {rem1} giây";
            lblFarmTimer1.ForeColor = Color.FromArgb(39, 174, 96); // Green
            lblFarmTimer1.Font = new Font("Segoe UI", 10.5F, FontStyle.Bold);
        }
        else if (rem1 == 0)
        {
            lblFarmTimer1.Text = "🌾 Ruộng 1: ⚠️ HẾT HẠN! (Shift+F)";
            lblFarmTimer1.ForeColor = Color.Red;
            lblFarmTimer1.Font = new Font("Segoe UI", 10.5F, FontStyle.Bold);
        }
        else
        {
            lblFarmTimer1.Text = "🌾 Ruộng 1 (Ctrl+F): Chưa bật";
            lblFarmTimer1.ForeColor = _isDarkMode ? Color.Gray : Color.DarkGray;
            lblFarmTimer1.Font = new Font("Segoe UI", 10.5F, FontStyle.Regular);
        }

        // Ruộng 2
        if (rem2 > 0)
        {
            lblFarmTimer2.Text = $"🌾 Ruộng 2 (Ctrl+G): {rem2} giây";
            lblFarmTimer2.ForeColor = Color.FromArgb(39, 174, 96); // Green
            lblFarmTimer2.Font = new Font("Segoe UI", 10.5F, FontStyle.Bold);
        }
        else if (rem2 == 0)
        {
            lblFarmTimer2.Text = "🌾 Ruộng 2: ⚠️ HẾT HẠN! (Shift+G)";
            lblFarmTimer2.ForeColor = Color.Red;
            lblFarmTimer2.Font = new Font("Segoe UI", 10.5F, FontStyle.Bold);
        }
        else
        {
            lblFarmTimer2.Text = "🌾 Ruộng 2 (Ctrl+G): Chưa bật";
            lblFarmTimer2.ForeColor = _isDarkMode ? Color.Gray : Color.DarkGray;
            lblFarmTimer2.Font = new Font("Segoe UI", 10.5F, FontStyle.Regular);
        }
    }

    private void OnEngineStateChanged(MacroState state)
    {
        bool isAppEnabled = (state != MacroState.Disabled);
        _ocrService.SetEnabled(isAppEnabled);
        _popOcrService.SetEnabled(isAppEnabled);
        _timerOcrService.SetEnabled(isAppEnabled);

        if (IsDisposed) return;

        if (InvokeRequired)
        {
            Invoke(new Action<MacroState>(UpdateStatusUI), state);
        }
        else
        {
            UpdateStatusUI(state);
        }
    }

    private void UpdateStatusUI(MacroState state)
    {
        switch (state)
        {
            case MacroState.Disabled:
                lblStatusValue.Text = "Tắt";
                lblStatusValue.ForeColor = Color.FromArgb(127, 140, 141); // Gray
                btnToggleMacro.Text = "Bật Macro";
                break;

            case MacroState.Active:
                lblStatusValue.Text = "Hoạt động";
                lblStatusValue.ForeColor = Color.FromArgb(39, 174, 96); // Green
                btnToggleMacro.Text = "Tắt Macro";
                break;

            case MacroState.SuspendedChat:
                lblStatusValue.Text = "Tạm dừng (Chat)";
                lblStatusValue.ForeColor = Color.FromArgb(230, 126, 34); // Orange
                btnToggleMacro.Text = "Tắt Macro";
                break;

            case MacroState.SuspendedOutOfGame:
            default:
                lblStatusValue.Text = "Tạm dừng (Ngoài game)";
                lblStatusValue.ForeColor = Color.FromArgb(231, 76, 60); // Red
                btnToggleMacro.Text = "Tắt Macro";
                break;
        }
    }

    private void BtnToggleMacro_Click(object? sender, EventArgs e)
    {
        _controlEngine.ToggleEnable();
    }

    private void MainForm_KeyDown(object? sender, KeyEventArgs e)
    {
    }

    public void AppendLog(string message, Color color)
    {
        if (rtbLog.IsDisposed) return;

        if (InvokeRequired)
        {
            Invoke(new Action<string, Color>(AppendLog), message, color);
            return;
        }

        string timestamp = DateTime.Now.ToString("HH:mm:ss");
        rtbLog.SelectionStart = rtbLog.TextLength;
        rtbLog.SelectionLength = 0;
        rtbLog.SelectionColor = _isDarkMode ? Color.Gray : Color.DarkGray;
        rtbLog.AppendText($"[{timestamp}] ");

        rtbLog.SelectionColor = color;
        rtbLog.AppendText($"{message}\n");

        if (chkAutoScroll.Checked)
        {
            rtbLog.SelectionStart = rtbLog.TextLength;
            rtbLog.ScrollToCaret();
        }
    }

    private void BtnClearLog_Click(object? sender, EventArgs e)
    {
        rtbLog.Clear();
        AppendLog("Đã xóa nhật ký.", Color.Gray);
    }

    private static void EnableDoubleBuffer(Control ctrl)
    {
        try
        {
            typeof(Control).GetProperty("DoubleBuffered", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.SetValue(ctrl, true, null);
        }
        catch { }
    }
}
