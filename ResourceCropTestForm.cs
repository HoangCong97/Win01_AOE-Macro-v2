using System.Diagnostics;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Text;
using AOEKeyboardMacroPro.Models;
using AOEKeyboardMacroPro.Services;

namespace AOEKeyboardMacroPro;

public partial class ResourceCropTestForm : Form
{
    private readonly System.Windows.Forms.Timer _refreshTimer = new();
    private ResourceCropSettings _settings;
    private Bitmap? _lastCapturedSource;
    private Bitmap? _loadedOfflineBitmap;
    private Bitmap? _currentTemplateCropBmp;
    private bool _isLiveMode = true;

    private readonly string _templatesDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Templates", "Digits");
    private readonly ResourceOcrService _ocrService = new();

    // Card UI references for 4 resources
    private class ResourceCardUI
    {
        public string Key { get; set; } = "";
        public string Title { get; set; } = "";
        public Color ThemeColor { get; set; }
        public PixelPictureBox PicPreview { get; set; } = null!;
        public Label LblOcrResult { get; set; } = null!;
        public NumericUpDown NumX { get; set; } = null!;
        public NumericUpDown NumY { get; set; } = null!;
        public NumericUpDown NumW { get; set; } = null!;
        public NumericUpDown NumH { get; set; } = null!;
        public Label LblSizeInfo { get; set; } = null!;
        public Panel CardPanel { get; set; } = null!;
    }

    private readonly Dictionary<string, ResourceCardUI> _cardUIs = new();

    // Digit Slots UI (0 -> 9)
    private class DigitSlotUI
    {
        public int Digit { get; set; }
        public PixelPictureBox PicSlot { get; set; } = null!;
        public Label LblSize { get; set; } = null!;
        public Button BtnAssign { get; set; } = null!;
        public Button BtnClear { get; set; } = null!;
        public Panel SlotPanel { get; set; } = null!;
    }

    private readonly DigitSlotUI[] _digitSlots = new DigitSlotUI[10];

    public ResourceCropTestForm()
    {
        InitializeComponent();

        DoubleBuffered = true;
        SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint, true);

        // Ensure Templates directory exists
        try { Directory.CreateDirectory(_templatesDir); } catch { }

        // Load configuration
        var appSettings = ConfigService.LoadSettings();
        _settings = appSettings.ResourceCrop ?? new ResourceCropSettings();

        // Build 4 Resource Cards UI
        BuildResourceCards();

        // Build 10 Digit Slots UI
        BuildDigitSlots();

        // Setup Hold-to-Repeat on Global Adjust Buttons
        SetupGlobalHoldToRepeat();

        // Setup Hold-to-Repeat on Template Tool Buttons
        SetupTemplateToolHoldToRepeat();

        // Setup controls initial states
        chkAutoRefresh.Checked = _settings.AutoRefresh;
        chkTopMost.Checked = _settings.TopMost;
        this.TopMost = _settings.TopMost;

        int zoomIdx = Math.Clamp(_settings.ZoomLevel - 1, 0, 3);
        cboZoom.SelectedIndex = zoomIdx;
        cboTargetDigit.SelectedIndex = 0;

        _refreshTimer.Interval = 500; // 0.5s
        _refreshTimer.Tick += RefreshTimer_Tick;

        // Hook coordinate change events on template tool
        numTplX.ValueChanged += (s, e) => UpdateTemplatePreview();
        numTplY.ValueChanged += (s, e) => UpdateTemplatePreview();
        numTplW.ValueChanged += (s, e) => UpdateTemplatePreview();
        numTplH.ValueChanged += (s, e) => UpdateTemplatePreview();

        if (File.Exists("app_icon.ico"))
        {
            try { this.Icon = new Icon("app_icon.ico"); } catch { }
        }

        // Load existing templates from disk
        LoadExistingTemplates();
    }

    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);
        if (chkAutoRefresh.Checked)
        {
            _refreshTimer.Start();
        }
        PerformCaptureAndRefresh();
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        _refreshTimer.Stop();
        (this.Owner as MainForm)?.ReloadOcrSettings();
        base.OnFormClosing(e);
    }

    private void BtnBack_Click(object? sender, EventArgs e)
    {
        this.Close();
    }

    #region Hold to Repeat Mechanism

    /// <summary>
    /// Gắn cơ chế ấn giữ lặp lại liên tục (Hold to repeat) cho Button.
    /// Nhấn 1 lần: thực thi ngay lập tức 1 bước.
    /// Giữ quá 250ms: tự động lặp lại liên tục mỗi 40ms.
    /// </summary>
    private static void AttachHoldToRepeat(Button button, Action action, int initialDelay = 250, int repeatInterval = 40)
    {
        System.Windows.Forms.Timer repeatTimer = new() { Interval = repeatInterval };
        System.Windows.Forms.Timer initialTimer = new() { Interval = initialDelay };

        initialTimer.Tick += (s, e) =>
        {
            initialTimer.Stop();
            repeatTimer.Start();
        };

        repeatTimer.Tick += (s, e) =>
        {
            action();
        };

        button.MouseDown += (s, e) =>
        {
            if (e.Button == MouseButtons.Left)
            {
                action(); // Bước đầu tiên tức thì
                initialTimer.Start();
            }
        };

        void StopAll()
        {
            initialTimer.Stop();
            repeatTimer.Stop();
        }

        button.MouseUp += (s, e) => StopAll();
        button.MouseLeave += (s, e) => StopAll();
    }

    private void SetupGlobalHoldToRepeat()
    {
        AttachHoldToRepeat(btnGlobalLeft1, () => NudgeAll(-1, 0));
        AttachHoldToRepeat(btnGlobalRight1, () => NudgeAll(1, 0));
        AttachHoldToRepeat(btnGlobalUp1, () => NudgeAll(0, -1));
        AttachHoldToRepeat(btnGlobalDown1, () => NudgeAll(0, 1));

        AttachHoldToRepeat(btnGlobalLeft5, () => NudgeAll(-5, 0));
        AttachHoldToRepeat(btnGlobalRight5, () => NudgeAll(5, 0));
        AttachHoldToRepeat(btnGlobalUp5, () => NudgeAll(0, -5));
        AttachHoldToRepeat(btnGlobalDown5, () => NudgeAll(0, 5));

        AttachHoldToRepeat(btnGlobalWMinus, () => ResizeAll(-2, 0));
        AttachHoldToRepeat(btnGlobalWPlus, () => ResizeAll(2, 0));
        AttachHoldToRepeat(btnGlobalHMinus, () => ResizeAll(0, -2));
        AttachHoldToRepeat(btnGlobalHPlus, () => ResizeAll(0, 2));
    }

    private void SetupTemplateToolHoldToRepeat()
    {
        AttachHoldToRepeat(btnTplLeft, () => { if (numTplX.Value > numTplX.Minimum) numTplX.Value--; });
        AttachHoldToRepeat(btnTplRight, () => { if (numTplX.Value < numTplX.Maximum) numTplX.Value++; });
        AttachHoldToRepeat(btnTplUp, () => { if (numTplY.Value > numTplY.Minimum) numTplY.Value--; });
        AttachHoldToRepeat(btnTplDown, () => { if (numTplY.Value < numTplY.Maximum) numTplY.Value++; });

        AttachHoldToRepeat(btnTplWMinus, () => { if (numTplW.Value - 1 >= numTplW.Minimum) numTplW.Value--; });
        AttachHoldToRepeat(btnTplWPlus, () => { if (numTplW.Value + 1 <= numTplW.Maximum) numTplW.Value++; });
        AttachHoldToRepeat(btnTplHMinus, () => { if (numTplH.Value - 1 >= numTplH.Minimum) numTplH.Value--; });
        AttachHoldToRepeat(btnTplHPlus, () => { if (numTplH.Value + 1 <= numTplH.Maximum) numTplH.Value++; });
    }

    #endregion

    #region Resource Cards UI

    private void BuildResourceCards()
    {
        tblResources.SuspendLayout();
        tblResources.Controls.Clear();
        _cardUIs.Clear();

        var definitions = new[]
        {
            ("Wood", "🪵 GỖ (WOOD)", Color.FromArgb(70, 210, 130), _settings.Wood),
            ("Food", "🥩 THỊT (FOOD)", Color.FromArgb(255, 95, 95), _settings.Food),
            ("Gold", "🪙 VÀNG (GOLD)", Color.FromArgb(255, 215, 60), _settings.Gold),
            ("Stone", "🪨 ĐÁ (STONE)", Color.FromArgb(100, 210, 255), _settings.Stone)
        };

        int col = 0;
        foreach (var (key, title, color, box) in definitions)
        {
            var cardUI = CreateResourceCard(key, title, color, box);
            _cardUIs[key] = cardUI;
            tblResources.Controls.Add(cardUI.CardPanel, col, 0);
            col++;
        }

        tblResources.ResumeLayout(true);
    }

    private ResourceCardUI CreateResourceCard(string key, string title, Color themeColor, ResourceCropBox box)
    {
        var panel = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(28, 28, 34),
            BorderStyle = BorderStyle.FixedSingle,
            Margin = new Padding(3),
            Padding = new Padding(6)
        };

        // Title Header
        var pnlTitle = new Panel
        {
            Dock = DockStyle.Top,
            Height = 26,
            BackColor = Color.FromArgb(36, 36, 44)
        };

        var lblColorBar = new Label
        {
            Dock = DockStyle.Left,
            Width = 5,
            BackColor = themeColor
        };

        var lblTitle = new Label
        {
            Dock = DockStyle.Fill,
            Text = title,
            Font = new Font("Segoe UI", 9F, FontStyle.Bold),
            ForeColor = themeColor,
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(5, 0, 0, 0)
        };
        pnlTitle.Controls.Add(lblTitle);
        pnlTitle.Controls.Add(lblColorBar);

        // Size info label
        var lblSizeInfo = new Label
        {
            Dock = DockStyle.Top,
            Height = 20,
            Font = new Font("Segoe UI", 8F),
            ForeColor = Color.DarkGray,
            Text = $"Tọa độ: ({box.X}, {box.Y}) | {box.Width}x{box.Height}px",
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(2, 1, 0, 0)
        };

        // Preview PictureBox
        var picPreview = new PixelPictureBox
        {
            Dock = DockStyle.Top,
            Height = 78,
            BackColor = Color.Black,
            BorderStyle = BorderStyle.FixedSingle,
            SizeMode = PictureBoxSizeMode.CenterImage,
            Cursor = Cursors.Hand,
            Margin = new Padding(0, 2, 0, 2)
        };

        // Click on preview to center template crop box onto this resource
        picPreview.Click += (s, e) =>
        {
            numTplX.Value = Math.Clamp(box.X, numTplX.Minimum, numTplX.Maximum);
            numTplY.Value = Math.Clamp(box.Y, numTplY.Minimum, numTplY.Maximum);
            UpdateTemplatePreview();
        };

        // Inputs Container (X, Y, W, H)
        var pnlInputs = new Panel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            Padding = new Padding(0, 4, 0, 0)
        };

        int currentY = 2;

        // X Input
        var lblX = new Label { Text = "X:", ForeColor = Color.White, Location = new Point(2, currentY + 3), AutoSize = true, Font = new Font("Segoe UI", 8.5F) };
        var numX = new NumericUpDown
        {
            Location = new Point(25, currentY),
            Width = 60,
            Maximum = 3840,
            Minimum = 0,
            Value = box.X,
            BackColor = Color.FromArgb(45, 45, 55),
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 8.5F, FontStyle.Bold)
        };
        var btnLeft = new Button { Text = "◀", Location = new Point(88, currentY), Size = new Size(24, 23), FlatStyle = FlatStyle.Flat, ForeColor = Color.White, BackColor = Color.FromArgb(50, 50, 60) };
        var btnRight = new Button { Text = "▶", Location = new Point(114, currentY), Size = new Size(24, 23), FlatStyle = FlatStyle.Flat, ForeColor = Color.White, BackColor = Color.FromArgb(50, 50, 60) };

        AttachHoldToRepeat(btnLeft, () => { if (numX.Value > numX.Minimum) numX.Value--; });
        AttachHoldToRepeat(btnRight, () => { if (numX.Value < numX.Maximum) numX.Value++; });

        pnlInputs.Controls.AddRange(new Control[] { lblX, numX, btnLeft, btnRight });
        currentY += 26;

        // Y Input
        var lblY = new Label { Text = "Y:", ForeColor = Color.White, Location = new Point(2, currentY + 3), AutoSize = true, Font = new Font("Segoe UI", 8.5F) };
        var numY = new NumericUpDown
        {
            Location = new Point(25, currentY),
            Width = 60,
            Maximum = 2160,
            Minimum = 0,
            Value = box.Y,
            BackColor = Color.FromArgb(45, 45, 55),
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 8.5F, FontStyle.Bold)
        };
        var btnUp = new Button { Text = "▲", Location = new Point(88, currentY), Size = new Size(24, 23), FlatStyle = FlatStyle.Flat, ForeColor = Color.White, BackColor = Color.FromArgb(50, 50, 60) };
        var btnDown = new Button { Text = "▼", Location = new Point(114, currentY), Size = new Size(24, 23), FlatStyle = FlatStyle.Flat, ForeColor = Color.White, BackColor = Color.FromArgb(50, 50, 60) };

        AttachHoldToRepeat(btnUp, () => { if (numY.Value > numY.Minimum) numY.Value--; });
        AttachHoldToRepeat(btnDown, () => { if (numY.Value < numY.Maximum) numY.Value++; });

        pnlInputs.Controls.AddRange(new Control[] { lblY, numY, btnUp, btnDown });
        currentY += 26;

        // Width Input
        var lblW = new Label { Text = "W:", ForeColor = Color.White, Location = new Point(2, currentY + 3), AutoSize = true, Font = new Font("Segoe UI", 8.5F) };
        var numW = new NumericUpDown
        {
            Location = new Point(25, currentY),
            Width = 60,
            Maximum = 500,
            Minimum = 5,
            Value = box.Width,
            BackColor = Color.FromArgb(45, 45, 55),
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 8.5F)
        };
        var btnWMinus = new Button { Text = "-", Location = new Point(88, currentY), Size = new Size(24, 23), FlatStyle = FlatStyle.Flat, ForeColor = Color.White, BackColor = Color.FromArgb(50, 50, 60) };
        var btnWPlus = new Button { Text = "+", Location = new Point(114, currentY), Size = new Size(24, 23), FlatStyle = FlatStyle.Flat, ForeColor = Color.White, BackColor = Color.FromArgb(50, 50, 60) };

        AttachHoldToRepeat(btnWMinus, () => { if (numW.Value - 1 >= numW.Minimum) numW.Value--; });
        AttachHoldToRepeat(btnWPlus, () => { if (numW.Value + 1 <= numW.Maximum) numW.Value++; });

        pnlInputs.Controls.AddRange(new Control[] { lblW, numW, btnWMinus, btnWPlus });
        currentY += 26;

        // Height Input
        var lblH = new Label { Text = "H:", ForeColor = Color.White, Location = new Point(2, currentY + 3), AutoSize = true, Font = new Font("Segoe UI", 8.5F) };
        var numH = new NumericUpDown
        {
            Location = new Point(25, currentY),
            Width = 60,
            Maximum = 300,
            Minimum = 5,
            Value = box.Height,
            BackColor = Color.FromArgb(45, 45, 55),
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 8.5F)
        };
        var btnHMinus = new Button { Text = "-", Location = new Point(88, currentY), Size = new Size(24, 23), FlatStyle = FlatStyle.Flat, ForeColor = Color.White, BackColor = Color.FromArgb(50, 50, 60) };
        var btnHPlus = new Button { Text = "+", Location = new Point(114, currentY), Size = new Size(24, 23), FlatStyle = FlatStyle.Flat, ForeColor = Color.White, BackColor = Color.FromArgb(50, 50, 60) };

        AttachHoldToRepeat(btnHMinus, () => { if (numH.Value - 1 >= numH.Minimum) numH.Value--; });
        AttachHoldToRepeat(btnHPlus, () => { if (numH.Value + 1 <= numH.Maximum) numH.Value++; });

        pnlInputs.Controls.AddRange(new Control[] { lblH, numH, btnHMinus, btnHPlus });

        void OnValueChanged(object? s, EventArgs e)
        {
            box.X = (int)numX.Value;
            box.Y = (int)numY.Value;
            box.Width = (int)numW.Value;
            box.Height = (int)numH.Value;
            lblSizeInfo.Text = $"Tọa độ: ({box.X}, {box.Y}) | {box.Width}x{box.Height}px";
            UpdatePreviewsFromLastCapture();
        }

        numX.ValueChanged += OnValueChanged;
        numY.ValueChanged += OnValueChanged;
        numW.ValueChanged += OnValueChanged;
        numH.ValueChanged += OnValueChanged;

        // OCR Recognition Result Label
        var lblOcrResult = new Label
        {
            Dock = DockStyle.Top,
            Height = 22,
            Font = new Font("Segoe UI", 9F, FontStyle.Bold),
            ForeColor = Color.Gold,
            Text = "🎯 Nhận diện: --",
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(4, 1, 0, 0),
            BackColor = Color.FromArgb(28, 28, 35)
        };

        panel.Controls.Add(pnlInputs);
        panel.Controls.Add(lblOcrResult);
        panel.Controls.Add(picPreview);
        panel.Controls.Add(lblSizeInfo);
        panel.Controls.Add(pnlTitle);

        return new ResourceCardUI
        {
            Key = key,
            Title = title,
            ThemeColor = themeColor,
            PicPreview = picPreview,
            LblOcrResult = lblOcrResult,
            NumX = numX,
            NumY = numY,
            NumW = numW,
            NumH = numH,
            LblSizeInfo = lblSizeInfo,
            CardPanel = panel
        };
    }

    #endregion

    #region Digit Slots & Template Management

    private void BuildDigitSlots()
    {
        tblDigitSlots.SuspendLayout();
        tblDigitSlots.Controls.Clear();

        for (int i = 0; i < 10; i++)
        {
            int digit = i;
            var panel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(32, 32, 40),
                BorderStyle = BorderStyle.FixedSingle,
                Margin = new Padding(2),
                Padding = new Padding(2)
            };

            var lblHeader = new Label
            {
                Dock = DockStyle.Top,
                Height = 20,
                Text = $"Số {digit}",
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                ForeColor = Color.FromArgb(220, 200, 255),
                TextAlign = ContentAlignment.MiddleCenter
            };

            var picSlot = new PixelPictureBox
            {
                Dock = DockStyle.Top,
                Height = 65,
                BackColor = Color.Black,
                BorderStyle = BorderStyle.FixedSingle,
                SizeMode = PictureBoxSizeMode.CenterImage,
                Margin = new Padding(0, 2, 0, 2)
            };

            var lblSize = new Label
            {
                Dock = DockStyle.Top,
                Height = 18,
                Text = "Trống",
                Font = new Font("Segoe UI", 7.5F),
                ForeColor = Color.Gray,
                TextAlign = ContentAlignment.MiddleCenter
            };

            var btnAssign = new Button
            {
                Dock = DockStyle.Top,
                Height = 24,
                Text = "Gán",
                Font = new Font("Segoe UI", 8F, FontStyle.Bold),
                BackColor = Color.FromArgb(50, 90, 60),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            btnAssign.FlatAppearance.BorderSize = 0;
            btnAssign.Click += (s, e) => AssignCurrentToDigit(digit);

            var btnClear = new Button
            {
                Dock = DockStyle.Top,
                Height = 22,
                Text = "Xóa",
                Font = new Font("Segoe UI", 7.5F),
                BackColor = Color.FromArgb(60, 40, 40),
                ForeColor = Color.LightGray,
                FlatStyle = FlatStyle.Flat
            };
            btnClear.FlatAppearance.BorderSize = 0;
            btnClear.Click += (s, e) => ClearDigitTemplate(digit);

            panel.Controls.Add(btnClear);
            panel.Controls.Add(btnAssign);
            panel.Controls.Add(lblSize);
            panel.Controls.Add(picSlot);
            panel.Controls.Add(lblHeader);

            _digitSlots[digit] = new DigitSlotUI
            {
                Digit = digit,
                PicSlot = picSlot,
                LblSize = lblSize,
                BtnAssign = btnAssign,
                BtnClear = btnClear,
                SlotPanel = panel
            };

            tblDigitSlots.Controls.Add(panel, digit, 0);
        }

        tblDigitSlots.ResumeLayout(true);
    }

    private void LoadExistingTemplates()
    {
        try
        {
            for (int i = 0; i < 10; i++)
            {
                string path = Path.Combine(_templatesDir, $"{i}.png");
                if (File.Exists(path))
                {
                    using var fs = new FileStream(path, FileMode.Open, FileAccess.Read);
                    using var img = Image.FromStream(fs);
                    Bitmap bmp = new(img);
                    SetDigitSlotImage(i, bmp);
                }
            }
        }
        catch { }
    }

    private void SetDigitSlotImage(int digit, Bitmap bmp)
    {
        if (digit < 0 || digit > 9) return;
        var slot = _digitSlots[digit];

        // Zoom 3x for slot preview
        int zoom = 3;
        Bitmap displayBmp = new(bmp.Width * zoom, bmp.Height * zoom);
        using (Graphics g = Graphics.FromImage(displayBmp))
        {
            g.InterpolationMode = InterpolationMode.NearestNeighbor;
            g.PixelOffsetMode = PixelOffsetMode.Half;
            g.DrawImage(bmp, new Rectangle(0, 0, displayBmp.Width, displayBmp.Height), new Rectangle(0, 0, bmp.Width, bmp.Height), GraphicsUnit.Pixel);
        }

        var old = slot.PicSlot.Image;
        slot.PicSlot.Image = displayBmp;
        old?.Dispose();

        slot.LblSize.Text = $"{bmp.Width}x{bmp.Height} px";
        slot.LblSize.ForeColor = Color.LightGreen;
    }

    private void AssignCurrentToDigit(int digit)
    {
        if (_currentTemplateCropBmp == null)
        {
            MessageBox.Show(this, "Chưa có ảnh cắt để gán! Hãy chụp màn hình hoặc chọn ảnh trước.", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        try
        {
            string filePath = Path.Combine(_templatesDir, $"{digit}.png");
            _currentTemplateCropBmp.Save(filePath, ImageFormat.Png);

            // Đồng bộ sang thư mục gốc dự án Templates/Digits nếu có
            try
            {
                string projectTplDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "Templates", "Digits");
                if (Directory.Exists(projectTplDir))
                {
                    _currentTemplateCropBmp.Save(Path.Combine(projectTplDir, $"{digit}.png"), ImageFormat.Png);
                }
            }
            catch { }

            SetDigitSlotImage(digit, _currentTemplateCropBmp);

            _ocrService.LoadTemplates();
            (this.Owner as MainForm)?.ReloadOcrSettings();
            UpdatePreviewsFromLastCapture();

            lblStatus.Text = $"✅ Đã lưu template Số {digit} thành công vào: {filePath}";
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"Lỗi khi lưu template: {ex.Message}", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void ClearDigitTemplate(int digit)
    {
        try
        {
            string filePath = Path.Combine(_templatesDir, $"{digit}.png");
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }

            var slot = _digitSlots[digit];
            var old = slot.PicSlot.Image;
            slot.PicSlot.Image = null;
            old?.Dispose();

            slot.LblSize.Text = "Trống";
            slot.LblSize.ForeColor = Color.Gray;

            _ocrService.LoadTemplates();
            (this.Owner as MainForm)?.ReloadOcrSettings();
            UpdatePreviewsFromLastCapture();

            lblStatus.Text = $"Đã xóa template Số {digit}.";
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"Lỗi khi xóa template: {ex.Message}", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void DisposeDigitSlots()
    {
        for (int i = 0; i < 10; i++)
        {
            var old = _digitSlots[i]?.PicSlot?.Image;
            old?.Dispose();
        }
    }

    private void BtnAssignCurrent_Click(object? sender, EventArgs e)
    {
        int targetDigit = cboTargetDigit.SelectedIndex;
        if (targetDigit >= 0 && targetDigit <= 9)
        {
            AssignCurrentToDigit(targetDigit);
        }
    }

    private void BtnOpenFolder_Click(object? sender, EventArgs e)
    {
        try
        {
            Directory.CreateDirectory(_templatesDir);
            Process.Start(new ProcessStartInfo("explorer.exe", _templatesDir) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"Không thể mở thư mục: {ex.Message}", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void ChkTemplateBinarize_CheckedChanged(object? sender, EventArgs e)
    {
        UpdateTemplatePreview();
    }

    private void BtnToggleTemplates_Click(object? sender, EventArgs e)
    {
        pnlTemplates.Visible = !pnlTemplates.Visible;
        btnToggleTemplates.Text = pnlTemplates.Visible ? "✂️ Mẫu 0-9 (Hiện)" : "✂️ Mẫu 0-9 (Ẩn)";
    }

    private void BtnAutoExtract_Click(object? sender, EventArgs e)
    {
        if (_lastCapturedSource == null)
        {
            MessageBox.Show(this, "Vui lòng chụp màn hình hoặc nạp ảnh trước khi trích xuất!", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        try
        {
            // Tọa độ chuẩn các số có sẵn từ ảnh mẫu topbar AOE:
            // '1' tại X=25, Y=4, W=4, H=12
            // '9' tại X=29, Y=4, W=5, H=12
            // '7' tại X=35, Y=4, W=5, H=12
            // '0' tại X=45, Y=4, W=5, H=12
            // '5' tại X=86, Y=4, W=5, H=12
            var presets = new (int digit, int x, int y, int w, int h)[]
            {
                (1, 25, 4, 4, 12),
                (9, 29, 4, 5, 12),
                (7, 35, 4, 5, 12),
                (0, 45, 4, 5, 12),
                (5, 86, 4, 5, 12)
            };

            int count = 0;
            foreach (var (d, x, y, w, h) in presets)
            {
                if (x + w <= _lastCapturedSource.Width && y + h <= _lastCapturedSource.Height)
                {
                    using Bitmap sub = new(w, h);
                    using (Graphics g = Graphics.FromImage(sub))
                    {
                        g.DrawImage(_lastCapturedSource, new Rectangle(0, 0, w, h), new Rectangle(x, y, w, h), GraphicsUnit.Pixel);
                    }

                    using Bitmap processed = chkTemplateBinarize.Checked ? Binarize(sub, 180) : new Bitmap(sub);
                    string path = Path.Combine(_templatesDir, $"{d}.png");
                    processed.Save(path, ImageFormat.Png);
                    SetDigitSlotImage(d, processed);
                    count++;
                }
            }

            _ocrService.LoadTemplates();
            (this.Owner as MainForm)?.ReloadOcrSettings();
            UpdatePreviewsFromLastCapture();

            MessageBox.Show(this, $"Đã tự động trích xuất và lưu {count} mẫu số (0, 1, 5, 7, 9) vào thư mục Templates!", "Thành công", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"Lỗi khi tự động trích xuất: {ex.Message}", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void UpdateTemplatePreview()
    {
        if (_lastCapturedSource == null) return;

        int tplX = (int)numTplX.Value;
        int tplY = (int)numTplY.Value;
        int tplW = (int)numTplW.Value;
        int tplH = (int)numTplH.Value;

        int srcW = _lastCapturedSource.Width;
        int srcH = _lastCapturedSource.Height;

        tplX = Math.Clamp(tplX, 0, Math.Max(0, srcW - 1));
        tplY = Math.Clamp(tplY, 0, Math.Max(0, srcH - 1));
        tplW = Math.Clamp(tplW, 1, Math.Max(1, srcW - tplX));
        tplH = Math.Clamp(tplH, 1, Math.Max(1, srcH - tplY));

        Rectangle rect = new(tplX, tplY, tplW, tplH);

        using Bitmap rawCrop = new(tplW, tplH);
        using (Graphics g = Graphics.FromImage(rawCrop))
        {
            g.DrawImage(_lastCapturedSource, new Rectangle(0, 0, tplW, tplH), rect, GraphicsUnit.Pixel);
        }

        Bitmap finalCrop = chkTemplateBinarize.Checked ? Binarize(rawCrop, 180) : new Bitmap(rawCrop);

        var oldCrop = _currentTemplateCropBmp;
        _currentTemplateCropBmp = finalCrop;
        oldCrop?.Dispose();

        // Zoom 4x for preview
        int zoom = 4;
        Bitmap previewBmp = new(tplW * zoom, tplH * zoom);
        using (Graphics g = Graphics.FromImage(previewBmp))
        {
            g.InterpolationMode = InterpolationMode.NearestNeighbor;
            g.PixelOffsetMode = PixelOffsetMode.Half;
            g.DrawImage(_currentTemplateCropBmp, new Rectangle(0, 0, previewBmp.Width, previewBmp.Height), new Rectangle(0, 0, tplW, tplH), GraphicsUnit.Pixel);

            using Pen p = new(Color.Yellow, 1);
            g.DrawRectangle(p, 0, 0, previewBmp.Width - 1, previewBmp.Height - 1);
        }

        var oldPreview = picTemplatePreview.Image;
        picTemplatePreview.Image = previewBmp;
        oldPreview?.Dispose();

        lblTplCoord.Text = $"({tplX}, {tplY}) | {tplW}x{tplH}px";

        // Redraw overview to include template crop box
        UpdateOverviewStrip();
    }

    private static Bitmap Binarize(Bitmap src, int threshold = 180)
    {
        Bitmap dest = new(src.Width, src.Height);
        for (int y = 0; y < src.Height; y++)
        {
            for (int x = 0; x < src.Width; x++)
            {
                Color c = src.GetPixel(x, y);
                int brightness = (int)(c.R * 0.299 + c.G * 0.587 + c.B * 0.114);
                if (brightness >= threshold || (c.R > threshold && c.G > threshold && c.B > threshold))
                {
                    dest.SetPixel(x, y, Color.White);
                }
                else
                {
                    dest.SetPixel(x, y, Color.Black);
                }
            }
        }
        return dest;
    }

    #endregion

    #region Capture & Refresh

    private void RefreshTimer_Tick(object? sender, EventArgs e)
    {
        if (IsDisposed) return;
        PerformCaptureAndRefresh();
    }

    private void PerformCaptureAndRefresh()
    {
        try
        {
            Bitmap? captured = null;
            string statusText = "";

            if (!_isLiveMode && _loadedOfflineBitmap != null)
            {
                captured = new Bitmap(_loadedOfflineBitmap);
                statusText = "🔵 Đang hiển thị ảnh tĩnh nạp từ file | Sẵn sàng điều chỉnh tọa độ & cắt mẫu";
            }
            else
            {
                IntPtr aoeHwnd = FindAoeWindow();
                int originX = 0;
                int originY = 0;
                int screenW = 1920;
                int screenH = 1080;

                if (aoeHwnd != IntPtr.Zero)
                {
                    StringBuilder sb = new(256);
                    NativeMethods.GetWindowText(aoeHwnd, sb, sb.Capacity);
                    string title = sb.ToString();

                    NativeMethods.GetClientRect(aoeHwnd, out NativeMethods.RECT clientRect);
                    NativeMethods.POINT pt = new() { X = 0, Y = 0 };
                    NativeMethods.ClientToScreen(aoeHwnd, ref pt);

                    originX = pt.X;
                    originY = pt.Y;
                    screenW = clientRect.Right - clientRect.Left;
                    screenH = clientRect.Bottom - clientRect.Top;

                    statusText = $"🟢 Tìm thấy AOE: \"{title}\" (HWND: 0x{aoeHwnd.ToInt64():X}) | Gốc: ({originX}, {originY}) | Kích thước: {screenW}x{screenH}";
                }
                else
                {
                    statusText = "🟡 Không tìm thấy cửa sổ AOE, đang chụp tại góc trên màn hình chính (0, 0)";
                }

                // Calculate required capture width and height
                int maxRequiredX = Math.Max(500, Math.Max(_settings.Wood.X + _settings.Wood.Width,
                    Math.Max(_settings.Food.X + _settings.Food.Width,
                    Math.Max(_settings.Gold.X + _settings.Gold.Width, _settings.Stone.X + _settings.Stone.Width)))) + 20;

                int maxRequiredY = Math.Max(40, Math.Max(_settings.Wood.Y + _settings.Wood.Height,
                    Math.Max(_settings.Food.Y + _settings.Food.Height,
                    Math.Max(_settings.Gold.Y + _settings.Gold.Height, _settings.Stone.Y + _settings.Stone.Height)))) + 10;

                captured = new Bitmap(maxRequiredX, maxRequiredY);
                using (Graphics g = Graphics.FromImage(captured))
                {
                    g.CopyFromScreen(originX, originY, 0, 0, new Size(maxRequiredX, maxRequiredY));
                }
            }

            var oldSource = _lastCapturedSource;
            _lastCapturedSource = captured;
            oldSource?.Dispose();

            UpdatePreviewsFromLastCapture();
            UpdateTemplatePreview();

            lblStatus.Text = statusText;
            lblLastUpdate.Text = $"Cập nhật: {DateTime.Now:HH:mm:ss.fff} (0.5s)";
        }
        catch (Exception ex)
        {
            lblStatus.Text = $"⚠️ Lỗi khi chụp màn hình: {ex.Message}";
        }
    }

    private void UpdatePreviewsFromLastCapture()
    {
        if (_lastCapturedSource == null) return;

        int zoom = Math.Clamp(cboZoom.SelectedIndex + 1, 1, 4);

        // Update 4 resource cards preview images
        UpdateCardCrop("Wood", _settings.Wood, zoom);
        UpdateCardCrop("Food", _settings.Food, zoom);
        UpdateCardCrop("Gold", _settings.Gold, zoom);
        UpdateCardCrop("Stone", _settings.Stone, zoom);

        // Run OCR recognition and display results live on each card
        var res = _ocrService.RecognizeFromBitmap(_lastCapturedSource, _settings);
        UpdateCardOcrResult("Wood", res.Wood);
        UpdateCardOcrResult("Food", res.Food);
        UpdateCardOcrResult("Gold", res.Gold);
        UpdateCardOcrResult("Stone", res.Stone);

        // Update Overview Panorama Strip with colored bounding boxes
        UpdateOverviewStrip();
    }

    private void UpdateCardOcrResult(string key, int? value)
    {
        if (!_cardUIs.TryGetValue(key, out var cardUI)) return;

        if (value.HasValue)
        {
            cardUI.LblOcrResult.Text = $"🎯 Nhận diện: {value.Value:N0}";
            cardUI.LblOcrResult.ForeColor = Color.FromArgb(70, 240, 140); // Bright green
        }
        else
        {
            cardUI.LblOcrResult.Text = "⚠️ Chưa khớp mẫu số";
            cardUI.LblOcrResult.ForeColor = Color.FromArgb(250, 110, 100); // Soft red
        }
    }

    private void UpdateCardCrop(string key, ResourceCropBox box, int zoom)
    {
        if (!_cardUIs.TryGetValue(key, out var cardUI) || _lastCapturedSource == null) return;

        int srcW = _lastCapturedSource.Width;
        int srcH = _lastCapturedSource.Height;

        int cropX = Math.Clamp(box.X, 0, Math.Max(0, srcW - 1));
        int cropY = Math.Clamp(box.Y, 0, Math.Max(0, srcH - 1));
        int cropW = Math.Clamp(box.Width, 1, Math.Max(1, srcW - cropX));
        int cropH = Math.Clamp(box.Height, 1, Math.Max(1, srcH - cropY));

        Rectangle rect = new(cropX, cropY, cropW, cropH);

        Bitmap croppedBmp = new(cropW * zoom, cropH * zoom);
        using (Graphics g = Graphics.FromImage(croppedBmp))
        {
            g.InterpolationMode = InterpolationMode.NearestNeighbor;
            g.PixelOffsetMode = PixelOffsetMode.Half;
            g.DrawImage(_lastCapturedSource, new Rectangle(0, 0, croppedBmp.Width, croppedBmp.Height), rect, GraphicsUnit.Pixel);

            using Pen p = new(cardUI.ThemeColor, 1);
            g.DrawRectangle(p, 0, 0, croppedBmp.Width - 1, croppedBmp.Height - 1);
        }

        var oldImg = cardUI.PicPreview.Image;
        cardUI.PicPreview.Image = croppedBmp;
        oldImg?.Dispose();
    }

    private void UpdateOverviewStrip()
    {
        if (_lastCapturedSource == null) return;

        int overviewW = Math.Min(_lastCapturedSource.Width, 500);
        int overviewH = Math.Min(_lastCapturedSource.Height, 40);

        Bitmap overviewBmp = new(overviewW, overviewH);
        using (Graphics g = Graphics.FromImage(overviewBmp))
        {
            g.DrawImage(_lastCapturedSource, new Rectangle(0, 0, overviewW, overviewH), new Rectangle(0, 0, overviewW, overviewH), GraphicsUnit.Pixel);

            // Draw colored bounding boxes for 4 resources
            DrawBoxOnOverview(g, _settings.Wood, Color.FromArgb(70, 210, 130), "Wood");
            DrawBoxOnOverview(g, _settings.Food, Color.FromArgb(255, 95, 95), "Food");
            DrawBoxOnOverview(g, _settings.Gold, Color.FromArgb(255, 215, 60), "Gold");
            DrawBoxOnOverview(g, _settings.Stone, Color.FromArgb(100, 210, 255), "Stone");

            // Draw yellow bounding box for Template tool crop box if template panel is visible
            if (pnlTemplates.Visible)
            {
                int tX = (int)numTplX.Value;
                int tY = (int)numTplY.Value;
                int tW = (int)numTplW.Value;
                int tH = (int)numTplH.Value;

                using Pen tplPen = new(Color.Yellow, 1.5F) { DashStyle = DashStyle.Dash };
                g.DrawRectangle(tplPen, tX, tY, tW, tH);
            }
        }

        var oldOverview = picOverview.Image;
        picOverview.Image = overviewBmp;
        oldOverview?.Dispose();
    }

    private static void DrawBoxOnOverview(Graphics g, ResourceCropBox box, Color color, string label)
    {
        using Pen pen = new(color, 1.5F);
        g.DrawRectangle(pen, box.X, box.Y, box.Width, box.Height);

        using Font font = new("Segoe UI", 7F, FontStyle.Bold);
        using SolidBrush brush = new(color);
        g.DrawString(label, font, brush, box.X + 2, Math.Max(0, box.Y - 10));
    }

    private void PicOverview_MouseDown(object? sender, MouseEventArgs e)
    {
        if (picOverview.Image == null) return;

        int? px = GetOverviewPixelX(e.X);
        int? py = GetOverviewPixelY(e.Y);

        if (px.HasValue && py.HasValue)
        {
            // Center template crop box onto this pixel
            int w = (int)numTplW.Value;
            int h = (int)numTplH.Value;

            int targetX = Math.Clamp(px.Value - w / 2, (int)numTplX.Minimum, (int)numTplX.Maximum);
            int targetY = Math.Clamp(py.Value - h / 2, (int)numTplY.Minimum, (int)numTplY.Maximum);

            numTplX.Value = targetX;
            numTplY.Value = targetY;
            UpdateTemplatePreview();
        }
    }

    private void PicOverview_MouseMove(object? sender, MouseEventArgs e)
    {
        int? px = GetOverviewPixelX(e.X);
        int? py = GetOverviewPixelY(e.Y);

        if (px.HasValue && py.HasValue)
        {
            lblMouseCoord.Text = $"Tọa độ chuột trên ảnh: X = {px.Value}, Y = {py.Value}";
        }
    }

    private void PicOverview_MouseLeave(object? sender, EventArgs e)
    {
        lblMouseCoord.Text = "Tọa độ chuột trên ảnh: X = -, Y = -";
    }

    private int? GetOverviewPixelX(int mouseX)
    {
        if (picOverview.Image == null) return null;
        GetRenderMetrics(out float renderW, out float _, out float offsetX, out float _, out int imgW, out int _);
        float relX = mouseX - offsetX;
        if (relX >= 0 && relX <= renderW)
        {
            return (int)(relX * imgW / renderW);
        }
        return null;
    }

    private int? GetOverviewPixelY(int mouseY)
    {
        if (picOverview.Image == null) return null;
        GetRenderMetrics(out float _, out float renderH, out float _, out float offsetY, out int _, out int imgH);
        float relY = mouseY - offsetY;
        if (relY >= 0 && relY <= renderH)
        {
            return (int)(relY * imgH / renderH);
        }
        return null;
    }

    private void GetRenderMetrics(out float renderW, out float renderH, out float offsetX, out float offsetY, out int imgW, out int imgH)
    {
        imgW = picOverview.Image!.Width;
        imgH = picOverview.Image!.Height;

        float boxRatio = (float)picOverview.Width / picOverview.Height;
        float imgRatio = (float)imgW / imgH;

        if (boxRatio > imgRatio)
        {
            renderH = picOverview.Height;
            renderW = imgRatio * renderH;
            offsetX = (picOverview.Width - renderW) / 2f;
            offsetY = 0;
        }
        else
        {
            renderW = picOverview.Width;
            renderH = renderW / imgRatio;
            offsetX = 0;
            offsetY = (picOverview.Height - renderH) / 2f;
        }
    }

    #endregion

    #region Adjustments & Toolbar Actions

    private void NudgeAll(int deltaX, int deltaY)
    {
        foreach (var card in _cardUIs.Values)
        {
            if (deltaX != 0)
            {
                decimal newX = Math.Clamp(card.NumX.Value + deltaX, card.NumX.Minimum, card.NumX.Maximum);
                card.NumX.Value = newX;
            }
            if (deltaY != 0)
            {
                decimal newY = Math.Clamp(card.NumY.Value + deltaY, card.NumY.Minimum, card.NumY.Maximum);
                card.NumY.Value = newY;
            }
        }
    }

    private void ResizeAll(int deltaW, int deltaH)
    {
        foreach (var card in _cardUIs.Values)
        {
            if (deltaW != 0)
            {
                decimal newW = Math.Clamp(card.NumW.Value + deltaW, card.NumW.Minimum, card.NumW.Maximum);
                card.NumW.Value = newW;
            }
            if (deltaH != 0)
            {
                decimal newH = Math.Clamp(card.NumH.Value + deltaH, card.NumH.Minimum, card.NumH.Maximum);
                card.NumH.Value = newH;
            }
        }
    }

    private void ChkAutoRefresh_CheckedChanged(object? sender, EventArgs e)
    {
        if (chkAutoRefresh.Checked)
        {
            _refreshTimer.Start();
            lblStatus.Text = $"Đã bật tự động làm mới ({_refreshTimer.Interval}ms).";
        }
        else
        {
            _refreshTimer.Stop();
            lblStatus.Text = "Đã tạm dừng tự động làm mới.";
        }
    }

    private void NumInterval_ValueChanged(object? sender, EventArgs e)
    {
        _refreshTimer.Interval = (int)numInterval.Value;
    }

    private void ChkTopMost_CheckedChanged(object? sender, EventArgs e)
    {
        this.TopMost = chkTopMost.Checked;
    }

    private void CboZoom_SelectedIndexChanged(object? sender, EventArgs e)
    {
        UpdatePreviewsFromLastCapture();
    }

    private void BtnRefreshNow_Click(object? sender, EventArgs e)
    {
        PerformCaptureAndRefresh();
    }

    private void BtnLoadImage_Click(object? sender, EventArgs e)
    {
        using OpenFileDialog ofd = new()
        {
            Title = "Chọn ảnh chụp màn hình game AOE để test crop",
            Filter = "Ảnh (*.png;*.jpg;*.jpeg;*.bmp)|*.png;*.jpg;*.jpeg;*.bmp|Tất cả tệp (*.*)|*.*",
            InitialDirectory = AppDomain.CurrentDomain.BaseDirectory
        };
        if (File.Exists(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "sample_aoe.jpg")))
        {
            ofd.FileName = "sample_aoe.jpg";
        }

        if (ofd.ShowDialog(this) == DialogResult.OK)
        {
            try
            {
                using var fs = new FileStream(ofd.FileName, FileMode.Open, FileAccess.Read);
                using var src = Image.FromStream(fs);
                var oldOffline = _loadedOfflineBitmap;
                _loadedOfflineBitmap = new Bitmap(src);
                oldOffline?.Dispose();

                _isLiveMode = false;
                _refreshTimer.Stop();
                chkAutoRefresh.Checked = false;

                PerformCaptureAndRefresh();
                MessageBox.Show(this, $"Đã nạp ảnh thành công: {Path.GetFileName(ofd.FileName)} ({_loadedOfflineBitmap.Width}x{_loadedOfflineBitmap.Height})", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"Không thể mở ảnh: {ex.Message}", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }

    private void BtnUseLive_Click(object? sender, EventArgs e)
    {
        _isLiveMode = true;
        _loadedOfflineBitmap?.Dispose();
        _loadedOfflineBitmap = null;
        chkAutoRefresh.Checked = true;
        _refreshTimer.Start();
        PerformCaptureAndRefresh();
    }

    private void BtnSave_Click(object? sender, EventArgs e)
    {
        var appSettings = ConfigService.LoadSettings();
        _settings.AutoRefresh = chkAutoRefresh.Checked;
        _settings.TopMost = chkTopMost.Checked;
        _settings.ZoomLevel = cboZoom.SelectedIndex + 1;

        appSettings.ResourceCrop = _settings;
        ConfigService.SaveSettings(appSettings);

        (this.Owner as MainForm)?.ReloadOcrSettings();

        MessageBox.Show(this, "Đã lưu cài đặt tọa độ crop thành công vào config.json!", "Thành công", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private void BtnReset_Click(object? sender, EventArgs e)
    {
        if (MessageBox.Show(this, "Bạn có muốn reset tọa độ về thông số gốc không?\n\nWood: (73, 12)\nFood: (174, 12)\nGold: (276, 12)\nStone: (377, 12)", "Xác nhận", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
        {
            if (_cardUIs.TryGetValue("Wood", out var wood)) { wood.NumX.Value = 73; wood.NumY.Value = 12; wood.NumW.Value = 70; wood.NumH.Value = 20; }
            if (_cardUIs.TryGetValue("Food", out var food)) { food.NumX.Value = 174; food.NumY.Value = 12; food.NumW.Value = 70; food.NumH.Value = 20; }
            if (_cardUIs.TryGetValue("Gold", out var gold)) { gold.NumX.Value = 276; gold.NumY.Value = 12; gold.NumW.Value = 70; gold.NumH.Value = 20; }
            if (_cardUIs.TryGetValue("Stone", out var stone)) { stone.NumX.Value = 377; stone.NumY.Value = 12; stone.NumW.Value = 70; stone.NumH.Value = 20; }
            PerformCaptureAndRefresh();
        }
    }

    private static IntPtr FindAoeWindow()
    {
        IntPtr fgHwnd = NativeMethods.GetForegroundWindow();
        if (fgHwnd != IntPtr.Zero && IsAoeWindow(fgHwnd))
        {
            return fgHwnd;
        }

        try
        {
            foreach (var proc in Process.GetProcesses())
            {
                try
                {
                    string name = proc.ProcessName.ToLowerInvariant();
                    if (name.Contains("empires") || name.Contains("aoe") || name.Contains("age"))
                    {
                        if (proc.MainWindowHandle != IntPtr.Zero)
                        {
                            return proc.MainWindowHandle;
                        }
                    }
                }
                catch { }
            }
        }
        catch { }

        IntPtr found = IntPtr.Zero;
        try
        {
            NativeMethods.EnumWindows((hwnd, lParam) =>
            {
                if (NativeMethods.IsWindowVisible(hwnd))
                {
                    StringBuilder sb = new(256);
                    NativeMethods.GetWindowText(hwnd, sb, sb.Capacity);
                    string title = sb.ToString();
                    if (IsAoeTitle(title))
                    {
                        found = hwnd;
                        return false;
                    }
                }
                return true;
            }, IntPtr.Zero);
        }
        catch { }

        return found;
    }

    private static bool IsAoeWindow(IntPtr hwnd)
    {
        StringBuilder sb = new(256);
        NativeMethods.GetWindowText(hwnd, sb, sb.Capacity);
        return IsAoeTitle(sb.ToString());
    }

    private static bool IsAoeTitle(string title)
    {
        if (string.IsNullOrWhiteSpace(title)) return false;
        string t = title.ToLowerInvariant();
        return t.Contains("empire") ||
               t.Contains("age of empires") ||
               t.Contains("aoe") ||
               t.Contains("definitive edition");
    }

    #endregion
}

public class PixelPictureBox : PictureBox
{
    public PixelPictureBox()
    {
        DoubleBuffered = true;
    }

    protected override void OnPaint(PaintEventArgs pe)
    {
        pe.Graphics.InterpolationMode = InterpolationMode.NearestNeighbor;
        pe.Graphics.PixelOffsetMode = PixelOffsetMode.Half;
        base.OnPaint(pe);
    }
}
