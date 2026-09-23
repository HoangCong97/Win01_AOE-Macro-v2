using System.Data;
using AOEKeyboardMacroPro.Models;
using AOEKeyboardMacroPro.Services;

namespace AOEKeyboardMacroPro;

public partial class MainForm : Form
{
    private readonly ControlEngine _controlEngine = new();
    private bool _isDarkMode = false;
    private readonly List<KeyMapItem> _keyMappings = new();
    private ResourceCropTestForm? _cropTestForm;
    private readonly ResourceOcrService _ocrService = new();

    public MainForm()
    {
        InitializeComponent();

        // Bật Double Buffering toàn diện để cửa sổ kéo mượt mà, triệt tiêu khựng giật
        this.DoubleBuffered = true;
        SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint, true);
        EnableDoubleBuffer(pnlOuterBorder);
        EnableDoubleBuffer(pnlKeyboardGrid);

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
        _ocrService.ResourcesUpdated += OnResourcesUpdated;

        ApplyTheme();
        UpdateStatusUI(_controlEngine.CurrentState);
        UpdateFarmTimerUI(-1, -1);
    }

    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);
        _controlEngine.Start();
        _ocrService.Start();
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        var appSettings = ConfigService.LoadSettings();
        appSettings.FarmTimerInterval = (int)numFarmInterval.Value;
        ConfigService.SaveSettings(appSettings);

        _ocrService.Stop();
        _ocrService.Dispose();

        _cropTestForm?.Close();
        _cropTestForm?.Dispose();

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

    private void BtnCropTest_Click(object? sender, EventArgs e)
    {
        if (_cropTestForm == null || _cropTestForm.IsDisposed)
        {
            _cropTestForm = new ResourceCropTestForm();
            _cropTestForm.Show(this);
        }
        else
        {
            if (_cropTestForm.WindowState == FormWindowState.Minimized)
            {
                _cropTestForm.WindowState = FormWindowState.Normal;
            }
            _cropTestForm.BringToFront();
            _cropTestForm.Focus();
        }
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

        btnCropTest.BackColor = cellBg;
        btnCropTest.ForeColor = textColor;
        btnCropTest.FlatAppearance.BorderColor = borderColor;

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

        lblResourceWood.ForeColor = _isDarkMode ? Color.FromArgb(70, 210, 130) : Color.FromArgb(46, 125, 50);
        lblResourceFood.ForeColor = _isDarkMode ? Color.FromArgb(255, 95, 95) : Color.FromArgb(198, 40, 40);
        lblResourceGold.ForeColor = _isDarkMode ? Color.FromArgb(255, 215, 60) : Color.FromArgb(230, 124, 115);
        lblResourceStone.ForeColor = _isDarkMode ? Color.FromArgb(100, 210, 255) : Color.FromArgb(25, 118, 210);

        Color sepColor = _isDarkMode ? Color.Gray : Color.LightGray;
        lblResSep1.ForeColor = sepColor;
        lblResSep2.ForeColor = sepColor;
        lblResSep3.ForeColor = sepColor;
        lblResourceRate.ForeColor = _isDarkMode ? Color.Gray : Color.DarkGray;

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
    }

    private void OnResourcesUpdated(ResourceValues res)
    {
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
        lblResourceWood.Text = $"🪵 Gỗ: {(res.Wood.HasValue ? res.Wood.Value.ToString("N0") : "--")}";
        lblResourceFood.Text = $"🥩 Thịt: {(res.Food.HasValue ? res.Food.Value.ToString("N0") : "--")}";
        lblResourceGold.Text = $"🪙 Vàng: {(res.Gold.HasValue ? res.Gold.Value.ToString("N0") : "--")}";
        lblResourceStone.Text = $"🪨 Đá: {(res.Stone.HasValue ? res.Stone.Value.ToString("N0") : "--")}";
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
                btnToggleMacro.Text = "Bật Macro (F1)";
                break;

            case MacroState.Active:
                lblStatusValue.Text = "Hoạt động";
                lblStatusValue.ForeColor = Color.FromArgb(39, 174, 96); // Green
                btnToggleMacro.Text = "Tắt Macro (F1)";
                break;

            case MacroState.SuspendedChat:
                lblStatusValue.Text = "Tạm dừng (Chat)";
                lblStatusValue.ForeColor = Color.FromArgb(230, 126, 34); // Orange
                btnToggleMacro.Text = "Tắt Macro (F1)";
                break;

            case MacroState.SuspendedOutOfGame:
            default:
                lblStatusValue.Text = "Tạm dừng (Ngoài game)";
                lblStatusValue.ForeColor = Color.FromArgb(231, 76, 60); // Red
                btnToggleMacro.Text = "Tắt Macro (F1)";
                break;
        }
    }

    private void BtnToggleMacro_Click(object? sender, EventArgs e)
    {
        _controlEngine.ToggleF1();
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
