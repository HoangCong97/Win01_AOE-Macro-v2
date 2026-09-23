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

    // POP Settings & OCR Service
    private PopCropSettings _popSettings;
    private readonly string _popTemplatesDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Templates", "Pop");
    private readonly PopOcrService _popOcrService = new();

    // Tab Navigation
    private Panel pnlTabBar = null!;
    private Button btnTabResources = null!;
    private Button btnTabPop = null!;
    private Panel pnlTabResources = null!;
    private Panel pnlTabPop = null!;
    private int _activeTab = 0;

    // POP Overview Strip
    private PixelPictureBox picPopOverview = null!;
    private Label lblPopMouseCoord = null!;

    // POP Card UI Controls
    private PixelPictureBox picPopRawPreview = null!;
    private PixelPictureBox picPopFilteredPreview = null!;
    private Label lblPopSizeInfo = null!;
    private Label lblPopOcrResult = null!;
    private NumericUpDown numPopX = null!;
    private NumericUpDown numPopY = null!;
    private NumericUpDown numPopW = null!;
    private NumericUpDown numPopH = null!;
    private NumericUpDown numPopBrightness = null!;
    private NumericUpDown numPopSaturation = null!;

    // POP Template Tool
    private Panel pnlPopTemplates = null!;
    private PixelPictureBox picPopTemplatePreview = null!;
    private Label lblPopTplCoord = null!;
    private NumericUpDown numPopTplX = null!;
    private NumericUpDown numPopTplY = null!;
    private NumericUpDown numPopTplW = null!;
    private NumericUpDown numPopTplH = null!;
    private ComboBox cboPopTargetGlyph = null!;
    private Bitmap? _currentPopTemplateCropBmp;

    // POP 11 Glyph Slots (0..9 and '/')
    private readonly DigitSlotUI[] _popGlyphSlots = new DigitSlotUI[11];
    private TableLayoutPanel tblPopGlyphSlots = null!;

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
        _popSettings = appSettings.PopCrop ?? new PopCropSettings();

        // Ensure POP templates directory exists
        try { Directory.CreateDirectory(_popTemplatesDir); } catch { }

        // Build 4 Resource Cards UI
        BuildResourceCards();

        // Build 10 Digit Slots UI
        BuildDigitSlots();

        // Setup Hold-to-Repeat on Global Adjust Buttons
        SetupGlobalHoldToRepeat();

        // Setup Hold-to-Repeat on Template Tool Buttons
        SetupTemplateToolHoldToRepeat();

        // Build Tab Navigation & POP Tab UI
        BuildTabsAndWrap();

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
        LoadExistingPopTemplates();
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

    #region POP UI & Tab Management

    private void BuildTabsAndWrap()
    {
        this.SuspendLayout();

        // Tăng chiều cao ClientSize thêm 34px đúng bằng thanh TabBar để không làm giảm dù chỉ 1px diện tích của thẻ Tài nguyên
        this.ClientSize = new Size(1008, 824);
        this.MinimumSize = new Size(980, 784);

        // 1. Tab Bar
        pnlTabBar = new Panel
        {
            Dock = DockStyle.Top,
            Height = 34,
            BackColor = Color.FromArgb(24, 24, 30),
            Padding = new Padding(8, 2, 8, 2)
        };

        btnTabResources = new Button
        {
            Text = "🪵 TÀI NGUYÊN (Wood, Food, Gold, Stone)",
            Dock = DockStyle.Left,
            Width = 260,
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(45, 90, 160),
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 9F, FontStyle.Bold),
            Cursor = Cursors.Hand
        };
        btnTabResources.FlatAppearance.BorderSize = 0;
        btnTabResources.Click += (s, e) => SwitchTab(0);

        btnTabPop = new Button
        {
            Text = "👥 DÂN SỐ (POP: Dân / Max)",
            Dock = DockStyle.Left,
            Width = 240,
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(32, 32, 40),
            ForeColor = Color.FromArgb(170, 170, 190),
            Font = new Font("Segoe UI", 9F, FontStyle.Bold),
            Cursor = Cursors.Hand
        };
        btnTabPop.FlatAppearance.BorderSize = 0;
        btnTabPop.Click += (s, e) => SwitchTab(1);

        pnlTabBar.Controls.Add(btnTabPop);
        pnlTabBar.Controls.Add(btnTabResources);

        // 2. Tab Resource Page - Chứa nguyên vẹn 100% các control gốc của màn hình Tài nguyên
        pnlTabResources = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(20, 20, 24)
        };

        // Chuyển toàn bộ các thành phần gốc vào pnlTabResources theo đúng thứ tự khởi tạo ban đầu
        this.Controls.Remove(tblResources);
        this.Controls.Remove(pnlGlobalAdjust);
        this.Controls.Remove(pnlTemplates);
        this.Controls.Remove(pnlOverview);
        this.Controls.Remove(pnlToolbar);

        pnlTabResources.Controls.Add(tblResources);
        pnlTabResources.Controls.Add(pnlGlobalAdjust);
        pnlTabResources.Controls.Add(pnlTemplates);
        pnlTabResources.Controls.Add(pnlOverview);
        pnlTabResources.Controls.Add(pnlToolbar);

        // 3. Tab POP Page
        pnlTabPop = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(20, 20, 26),
            Visible = false
        };
        BuildPopTabUI();

        // 4. Thêm Tab Pages & Tab Bar vào Form
        this.Controls.Add(pnlTabPop);
        this.Controls.Add(pnlTabResources);
        this.Controls.Add(pnlTabBar);

        this.Controls.SetChildIndex(pnlHeader, 0);
        this.Controls.SetChildIndex(pnlTabBar, 1);
        this.Controls.SetChildIndex(pnlStatus, 2);
        this.Controls.SetChildIndex(pnlTabResources, 3);
        this.Controls.SetChildIndex(pnlTabPop, 4);

        this.ResumeLayout(true);
    }

    private void SwitchTab(int tabIndex)
    {
        _activeTab = tabIndex;
        if (_activeTab == 0)
        {
            btnTabResources.BackColor = Color.FromArgb(45, 90, 160);
            btnTabResources.ForeColor = Color.White;
            btnTabPop.BackColor = Color.FromArgb(32, 32, 40);
            btnTabPop.ForeColor = Color.FromArgb(170, 170, 190);

            lblHeaderTitle.Text = "CÔNG CỤ CÂN CHỈNH TỌA ĐỘ VÀ KIỂM TRA OCR 4 LOẠI TÀI NGUYÊN";
            lblHeaderSubtitle.Text = "Tự động chụp góc trên màn hình AOE mỗi 0.5s để kiểm tra crop và template matching";

            pnlTabResources.Visible = true;
            pnlTabPop.Visible = false;
        }
        else
        {
            btnTabPop.BackColor = Color.FromArgb(45, 90, 160);
            btnTabPop.ForeColor = Color.White;
            btnTabResources.BackColor = Color.FromArgb(32, 32, 40);
            btnTabResources.ForeColor = Color.FromArgb(170, 170, 190);

            lblHeaderTitle.Text = "CÔNG CỤ CÂN CHỈNH TỌA ĐỘ VÀ KIỂM TRA OCR DÂN SỐ (POP)";
            lblHeaderSubtitle.Text = "Tự động lọc tách màu địa hình bản đồ (cỏ, nước, cát) và so khớp mẫu 11 ký tự (0-9 và /)";

            pnlTabPop.Visible = true;
            pnlTabResources.Visible = false;
        }
    }

    private void BuildPopTabUI()
    {
        pnlTabPop.SuspendLayout();
        pnlTabPop.Controls.Clear();

        // 1. POP Overview Strip at Top
        Panel pnlPopOverview = new()
        {
            Dock = DockStyle.Top,
            Height = 78,
            BackColor = Color.FromArgb(28, 28, 34),
            BorderStyle = BorderStyle.FixedSingle,
            Margin = new Padding(3),
            Padding = new Padding(6)
        };

        Panel pnlPopOverviewHeader = new()
        {
            Dock = DockStyle.Top,
            Height = 22
        };

        Label lblPopOverviewTitle = new()
        {
            Text = "🔍 DẢI ẢNH TOÀN CẢNH TOPBAR (X: 550 → 800, Y: 15 → 55) - Click để di chuyển tâm crop template",
            Font = new Font("Segoe UI", 8.5F, FontStyle.Bold),
            ForeColor = Color.FromArgb(100, 220, 255),
            Dock = DockStyle.Left,
            AutoSize = true
        };

        lblPopMouseCoord = new Label
        {
            Text = "Tọa độ chuột trên ảnh: X = -, Y = -",
            Font = new Font("Segoe UI", 8F),
            ForeColor = Color.FromArgb(180, 180, 195),
            Dock = DockStyle.Right,
            AutoSize = true
        };

        pnlPopOverviewHeader.Controls.Add(lblPopOverviewTitle);
        pnlPopOverviewHeader.Controls.Add(lblPopMouseCoord);

        picPopOverview = new PixelPictureBox
        {
            Dock = DockStyle.Fill,
            BackColor = Color.Black,
            BorderStyle = BorderStyle.FixedSingle,
            SizeMode = PictureBoxSizeMode.Zoom,
            Cursor = Cursors.Cross
        };
        picPopOverview.MouseDown += PicPopOverview_MouseDown;
        picPopOverview.MouseMove += PicPopOverview_MouseMove;
        picPopOverview.MouseLeave += (s, e) => lblPopMouseCoord.Text = "Tọa độ chuột trên ảnh: X = -, Y = -";

        pnlPopOverview.Controls.Add(picPopOverview);
        pnlPopOverview.Controls.Add(pnlPopOverviewHeader);

        // 2. POP Template Drawer at Bottom
        pnlPopTemplates = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 185,
            BackColor = Color.FromArgb(24, 24, 30),
            BorderStyle = BorderStyle.FixedSingle,
            Padding = new Padding(4)
        };

        Panel pnlPopTemplatesHeader = new()
        {
            Dock = DockStyle.Top,
            Height = 28,
            BackColor = Color.FromArgb(32, 32, 40),
            Padding = new Padding(6, 2, 6, 2)
        };

        Label lblPopTemplatesTitle = new()
        {
            Text = "✂️ CẮT TEMPLATE MẪU SỐ (0-9) VÀ DẤU GẠCH CHÉO (/) CHO POP (TỰ ĐỘNG TÁCH NỀN BẢN ĐỒ)",
            Font = new Font("Segoe UI", 9F, FontStyle.Bold),
            ForeColor = Color.FromArgb(255, 220, 100),
            Dock = DockStyle.Left,
            AutoSize = true
        };

        Button btnOpenPopFolder = new()
        {
            Text = "📂 Thư mục Templates/Pop",
            Font = new Font("Segoe UI", 8F),
            BackColor = Color.FromArgb(50, 50, 65),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Dock = DockStyle.Right,
            Width = 175,
            Height = 24
        };
        btnOpenPopFolder.FlatAppearance.BorderSize = 0;
        btnOpenPopFolder.Click += BtnOpenPopFolder_Click;

        pnlPopTemplatesHeader.Controls.Add(lblPopTemplatesTitle);
        pnlPopTemplatesHeader.Controls.Add(btnOpenPopFolder);

        Panel pnlPopTemplatesContent = new()
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(2)
        };

        // Left tool panel
        Panel pnlPopTemplateTool = new()
        {
            Dock = DockStyle.Left,
            Width = 330,
            BackColor = Color.FromArgb(30, 30, 38),
            BorderStyle = BorderStyle.FixedSingle,
            Padding = new Padding(6)
        };

        picPopTemplatePreview = new PixelPictureBox
        {
            Location = new Point(6, 6),
            Size = new Size(68, 68),
            BackColor = Color.Black,
            BorderStyle = BorderStyle.FixedSingle,
            SizeMode = PictureBoxSizeMode.CenterImage
        };

        lblPopTplCoord = new Label
        {
            Location = new Point(80, 6),
            Size = new Size(240, 16),
            Text = "(650, 27) | 7x12px",
            Font = new Font("Segoe UI", 8F, FontStyle.Bold),
            ForeColor = Color.Yellow
        };

        Label lblInfoTip = new()
        {
            Location = new Point(80, 24),
            Size = new Size(240, 48),
            Text = "Di chuyển khung crop đến từng số hoặc dấu / rồi chọn ô đích và bấm Lưu mẫu.",
            Font = new Font("Segoe UI", 7.5F),
            ForeColor = Color.LightGray
        };

        pnlPopTemplateTool.Controls.Add(picPopTemplatePreview);
        pnlPopTemplateTool.Controls.Add(lblPopTplCoord);
        pnlPopTemplateTool.Controls.Add(lblInfoTip);

        // Coordinates controls for POP template
        Label lblTplX = new() { Text = "X:", Location = new Point(6, 80), Size = new Size(18, 20), ForeColor = Color.LightGray, Font = new Font("Segoe UI", 8F) };
        numPopTplX = new NumericUpDown { Location = new Point(26, 78), Size = new Size(54, 22), Minimum = 0, Maximum = 1920, Value = 650, BackColor = Color.FromArgb(40, 40, 50), ForeColor = Color.White };
        Button btnPopTplLeft = CreateSmallBtn("◀", new Point(82, 78));
        Button btnPopTplRight = CreateSmallBtn("▶", new Point(108, 78));

        Label lblTplY = new() { Text = "Y:", Location = new Point(140, 80), Size = new Size(18, 20), ForeColor = Color.LightGray, Font = new Font("Segoe UI", 8F) };
        numPopTplY = new NumericUpDown { Location = new Point(160, 78), Size = new Size(54, 22), Minimum = 0, Maximum = 1080, Value = 27, BackColor = Color.FromArgb(40, 40, 50), ForeColor = Color.White };
        Button btnPopTplUp = CreateSmallBtn("▲", new Point(216, 78));
        Button btnPopTplDown = CreateSmallBtn("▼", new Point(242, 78));

        Label lblTplW = new() { Text = "W:", Location = new Point(6, 106), Size = new Size(18, 20), ForeColor = Color.LightGray, Font = new Font("Segoe UI", 8F) };
        numPopTplW = new NumericUpDown { Location = new Point(26, 104), Size = new Size(54, 22), Minimum = 1, Maximum = 100, Value = 7, BackColor = Color.FromArgb(40, 40, 50), ForeColor = Color.White };
        Button btnPopTplWMinus = CreateSmallBtn("-", new Point(82, 104));
        Button btnPopTplWPlus = CreateSmallBtn("+", new Point(108, 104));

        Label lblTplH = new() { Text = "H:", Location = new Point(140, 106), Size = new Size(18, 20), ForeColor = Color.LightGray, Font = new Font("Segoe UI", 8F) };
        numPopTplH = new NumericUpDown { Location = new Point(160, 104), Size = new Size(54, 22), Minimum = 1, Maximum = 100, Value = 12, BackColor = Color.FromArgb(40, 40, 50), ForeColor = Color.White };
        Button btnPopTplHMinus = CreateSmallBtn("-", new Point(216, 104));
        Button btnPopTplHPlus = CreateSmallBtn("+", new Point(242, 104));

        Label lblTarget = new() { Text = "Ô đích:", Location = new Point(6, 132), Size = new Size(46, 20), ForeColor = Color.LightGray, Font = new Font("Segoe UI", 8F) };
        cboPopTargetGlyph = new ComboBox { Location = new Point(54, 130), Size = new Size(106, 22), DropDownStyle = ComboBoxStyle.DropDownList, BackColor = Color.FromArgb(40, 40, 50), ForeColor = Color.White };
        for (int d = 0; d < 10; d++) cboPopTargetGlyph.Items.Add($"Số {d}");
        cboPopTargetGlyph.Items.Add("/ (Gạch chéo)");
        cboPopTargetGlyph.SelectedIndex = 0;

        Button btnPopAssign = new()
        {
            Location = new Point(166, 129),
            Size = new Size(150, 24),
            Text = "💾 Lưu mẫu vào ô",
            Font = new Font("Segoe UI", 8F, FontStyle.Bold),
            BackColor = Color.FromArgb(40, 110, 60),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat
        };
        btnPopAssign.FlatAppearance.BorderSize = 0;
        btnPopAssign.Click += (s, e) => AssignCurrentToPopGlyph(cboPopTargetGlyph.SelectedIndex);

        pnlPopTemplateTool.Controls.AddRange(new Control[] {
            lblTplX, numPopTplX, btnPopTplLeft, btnPopTplRight,
            lblTplY, numPopTplY, btnPopTplUp, btnPopTplDown,
            lblTplW, numPopTplW, btnPopTplWMinus, btnPopTplWPlus,
            lblTplH, numPopTplH, btnPopTplHMinus, btnPopTplHPlus,
            lblTarget, cboPopTargetGlyph, btnPopAssign
        });

        // Right glyph slots table (11 columns)
        Panel pnlPopSlotsContainer = new() { Dock = DockStyle.Fill, Padding = new Padding(4, 0, 0, 0) };
        tblPopGlyphSlots = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 11,
            RowCount = 1,
            BackColor = Color.FromArgb(24, 24, 30)
        };
        for (int c = 0; c < 11; c++)
        {
            tblPopGlyphSlots.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f / 11f));
        }
        tblPopGlyphSlots.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
        BuildPopGlyphSlots(tblPopGlyphSlots);
        pnlPopSlotsContainer.Controls.Add(tblPopGlyphSlots);

        pnlPopTemplatesContent.Controls.Add(pnlPopSlotsContainer);
        pnlPopTemplatesContent.Controls.Add(pnlPopTemplateTool);

        pnlPopTemplates.Controls.Add(pnlPopTemplatesContent);
        pnlPopTemplates.Controls.Add(pnlPopTemplatesHeader);

        // 3. POP Main Panel (Center Fill)
        Panel pnlPopMain = new()
        {
            Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(20, 20, 26),
            Padding = new Padding(8)
        };

        Panel pnlPopCard = new()
        {
            Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(28, 28, 36),
            BorderStyle = BorderStyle.FixedSingle,
            Padding = new Padding(10)
        };

        Panel pnlCardHeader = new()
        {
            Dock = DockStyle.Top,
            Height = 32,
            BackColor = Color.FromArgb(36, 36, 48),
            Padding = new Padding(8, 6, 8, 4)
        };

        Label lblCardTitle = new()
        {
            Text = "👥 HIỆU CHỈNH ĐỌC DÂN SỐ (POPULATION) - TÁCH NỀN BẢN ĐỒ & TỌA ĐỘ VÙNG POP",
            Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
            ForeColor = Color.FromArgb(100, 220, 255),
            Dock = DockStyle.Left,
            AutoSize = true
        };

        Button btnPopSave = new()
        {
            Text = "💾 Lưu cài đặt POP",
            Font = new Font("Segoe UI", 8.5F, FontStyle.Bold),
            BackColor = Color.FromArgb(40, 110, 60),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Dock = DockStyle.Right,
            Width = 130
        };
        btnPopSave.FlatAppearance.BorderSize = 0;
        btnPopSave.Click += (s, e) =>
        {
            var appSettings = ConfigService.LoadSettings();
            appSettings.PopCrop = _popSettings;
            ConfigService.SaveSettings(appSettings);
            MessageBox.Show(this, "Đã lưu cài đặt tọa độ & bộ lọc POP thành công vào config.json!", "Thành công", MessageBoxButtons.OK, MessageBoxIcon.Information);
        };

        Button btnPopReset = new()
        {
            Text = "↩️ Reset POP",
            Font = new Font("Segoe UI", 8.5F),
            BackColor = Color.FromArgb(55, 55, 68),
            ForeColor = Color.LightGray,
            FlatStyle = FlatStyle.Flat,
            Dock = DockStyle.Right,
            Width = 95
        };
        btnPopReset.FlatAppearance.BorderSize = 0;
        btnPopReset.Click += (s, e) =>
        {
            if (MessageBox.Show(this, "Bạn có muốn reset tọa độ Dân số (POP) về mặc định (650, 27) | 63x13px không?", "Xác nhận", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
            {
                numPopX.Value = 650;
                numPopY.Value = 27;
                numPopW.Value = 63;
                numPopH.Value = 13;
                numPopBrightness.Value = 175;
                numPopSaturation.Value = 35;
                PerformCaptureAndRefresh();
            }
        };

        Button btnPopToggleTemplates = new()
        {
            Text = "✂️ Mẫu POP (Hiện)",
            Font = new Font("Segoe UI", 8.5F),
            BackColor = Color.FromArgb(55, 55, 68),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Dock = DockStyle.Right,
            Width = 115
        };
        btnPopToggleTemplates.FlatAppearance.BorderSize = 0;
        btnPopToggleTemplates.Click += (s, e) =>
        {
            pnlPopTemplates.Visible = !pnlPopTemplates.Visible;
            btnPopToggleTemplates.Text = pnlPopTemplates.Visible ? "✂️ Mẫu POP (Hiện)" : "✂️ Mẫu POP (Ẩn)";
        };

        pnlCardHeader.Controls.Add(lblCardTitle);
        pnlCardHeader.Controls.Add(btnPopToggleTemplates);
        pnlCardHeader.Controls.Add(btnPopReset);
        pnlCardHeader.Controls.Add(btnPopSave);

        Panel pnlCardBody = new()
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            Padding = new Padding(6)
        };

        // Row 1: Previews & OCR result table layout (Height 125)
        TableLayoutPanel tblPreviewRow = new()
        {
            Dock = DockStyle.Top,
            Height = 125,
            ColumnCount = 3,
            RowCount = 1,
            BackColor = Color.Transparent,
            Margin = new Padding(0, 0, 0, 8)
        };
        tblPreviewRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 270f));
        tblPreviewRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 270f));
        tblPreviewRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        tblPreviewRow.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

        // Subpanel 1: Raw Preview
        Panel pnlSubRaw = new() { Dock = DockStyle.Fill, BackColor = Color.FromArgb(32, 32, 42), BorderStyle = BorderStyle.FixedSingle, Margin = new Padding(3), Padding = new Padding(4) };
        Label lblSubRawTitle = new() { Text = "1. ẢNH GỐC CHỤP MÀN HÌNH (4X)", Font = new Font("Segoe UI", 8F, FontStyle.Bold), ForeColor = Color.FromArgb(100, 220, 255), Dock = DockStyle.Top, Height = 20 };
        picPopRawPreview = new PixelPictureBox { Dock = DockStyle.Fill, BackColor = Color.Black, SizeMode = PictureBoxSizeMode.CenterImage };
        pnlSubRaw.Controls.Add(picPopRawPreview);
        pnlSubRaw.Controls.Add(lblSubRawTitle);

        // Subpanel 2: Filtered Preview
        Panel pnlSubFiltered = new() { Dock = DockStyle.Fill, BackColor = Color.FromArgb(32, 32, 42), BorderStyle = BorderStyle.FixedSingle, Margin = new Padding(3), Padding = new Padding(4) };
        Label lblSubFilteredTitle = new() { Text = "2. SAU KHI LỌC TÁCH NỀN (4X)", Font = new Font("Segoe UI", 8F, FontStyle.Bold), ForeColor = Color.FromArgb(70, 240, 140), Dock = DockStyle.Top, Height = 20 };
        picPopFilteredPreview = new PixelPictureBox { Dock = DockStyle.Fill, BackColor = Color.Black, SizeMode = PictureBoxSizeMode.CenterImage };
        pnlSubFiltered.Controls.Add(picPopFilteredPreview);
        pnlSubFiltered.Controls.Add(lblSubFilteredTitle);

        // Subpanel 3: Live Result
        Panel pnlSubOcr = new() { Dock = DockStyle.Fill, BackColor = Color.FromArgb(22, 22, 28), BorderStyle = BorderStyle.FixedSingle, Margin = new Padding(3), Padding = new Padding(8) };
        Label lblSubOcrTitle = new() { Text = "3. KẾT QUẢ NHẬN DIỆN THỰC TẾ (OCR)", Font = new Font("Segoe UI", 8F, FontStyle.Bold), ForeColor = Color.LightGray, Dock = DockStyle.Top, Height = 20 };
        lblPopOcrResult = new Label { Text = "🎯 POP nhận diện: -- / --", Font = new Font("Segoe UI", 15F, FontStyle.Bold), ForeColor = Color.FromArgb(70, 240, 140), Dock = DockStyle.Top, Height = 36 };
        lblPopSizeInfo = new Label { Text = "Tọa độ: (650, 27) | 63x13 px | Ngưỡng sáng: 175 | Sắc độ: ≤35", Font = new Font("Segoe UI", 8.5F), ForeColor = Color.LightGray, Dock = DockStyle.Top, Height = 24 };
        pnlSubOcr.Controls.Add(lblPopSizeInfo);
        pnlSubOcr.Controls.Add(lblPopOcrResult);
        pnlSubOcr.Controls.Add(lblSubOcrTitle);

        tblPreviewRow.Controls.Add(pnlSubRaw, 0, 0);
        tblPreviewRow.Controls.Add(pnlSubFiltered, 1, 0);
        tblPreviewRow.Controls.Add(pnlSubOcr, 2, 0);

        // Row 2: Settings and Adjustments (Height 210)
        TableLayoutPanel tblSettingsRow = new()
        {
            Dock = DockStyle.Top,
            Height = 210,
            ColumnCount = 2,
            RowCount = 1,
            BackColor = Color.Transparent,
            Margin = new Padding(0, 8, 0, 0)
        };
        tblSettingsRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
        tblSettingsRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
        tblSettingsRow.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

        // Group A: Coordinates adjustments
        Panel pnlCoordGroup = new() { Dock = DockStyle.Fill, BackColor = Color.FromArgb(32, 32, 42), BorderStyle = BorderStyle.FixedSingle, Margin = new Padding(3), Padding = new Padding(8) };
        Label lblCoordGroupTitle = new() { Text = "📍 ĐIỀU CHỈNH TỌA ĐỘ & KÍCH THƯỚC VÙNG POP", Font = new Font("Segoe UI", 9F, FontStyle.Bold), ForeColor = Color.FromArgb(100, 220, 255), Dock = DockStyle.Top, Height = 24 };

        Panel pnlCoordControls = new() { Dock = DockStyle.Fill, BackColor = Color.Transparent };

        // X Controls
        Label lblPopX = new() { Text = "Tọa độ X:", Location = new Point(8, 8), Size = new Size(65, 20), ForeColor = Color.LightGray, Font = new Font("Segoe UI", 8.5F) };
        numPopX = new NumericUpDown { Location = new Point(78, 6), Size = new Size(62, 23), Minimum = 0, Maximum = 1920, Value = _popSettings.PopBox.X, BackColor = Color.FromArgb(40, 40, 50), ForeColor = Color.White };
        Button btnPopLeft1 = CreateSmallBtn("◀ 1", new Point(146, 6), 38, 23);
        Button btnPopRight1 = CreateSmallBtn("1 ▶", new Point(188, 6), 38, 23);
        Button btnPopLeft5 = CreateSmallBtn("◀ 5", new Point(230, 6), 38, 23);
        Button btnPopRight5 = CreateSmallBtn("5 ▶", new Point(272, 6), 38, 23);

        // Y Controls
        Label lblPopY = new() { Text = "Tọa độ Y:", Location = new Point(8, 38), Size = new Size(65, 20), ForeColor = Color.LightGray, Font = new Font("Segoe UI", 8.5F) };
        numPopY = new NumericUpDown { Location = new Point(78, 36), Size = new Size(62, 23), Minimum = 0, Maximum = 1080, Value = _popSettings.PopBox.Y, BackColor = Color.FromArgb(40, 40, 50), ForeColor = Color.White };
        Button btnPopUp1 = CreateSmallBtn("▲ 1", new Point(146, 36), 38, 23);
        Button btnPopDown1 = CreateSmallBtn("1 ▼", new Point(188, 36), 38, 23);
        Button btnPopUp5 = CreateSmallBtn("▲ 5", new Point(230, 36), 38, 23);
        Button btnPopDown5 = CreateSmallBtn("5 ▼", new Point(272, 36), 38, 23);

        // W Controls
        Label lblPopW = new() { Text = "Chiều Rộng:", Location = new Point(8, 68), Size = new Size(68, 20), ForeColor = Color.LightGray, Font = new Font("Segoe UI", 8.5F) };
        numPopW = new NumericUpDown { Location = new Point(78, 66), Size = new Size(62, 23), Minimum = 10, Maximum = 300, Value = _popSettings.PopBox.Width, BackColor = Color.FromArgb(40, 40, 50), ForeColor = Color.White };
        Button btnPopWMinus = CreateSmallBtn("Rộng -2", new Point(146, 66), 62, 23);
        Button btnPopWPlus = CreateSmallBtn("Rộng +2", new Point(212, 66), 62, 23);

        // H Controls
        Label lblPopH = new() { Text = "Chiều Cao:", Location = new Point(8, 98), Size = new Size(68, 20), ForeColor = Color.LightGray, Font = new Font("Segoe UI", 8.5F) };
        numPopH = new NumericUpDown { Location = new Point(78, 96), Size = new Size(62, 23), Minimum = 5, Maximum = 100, Value = _popSettings.PopBox.Height, BackColor = Color.FromArgb(40, 40, 50), ForeColor = Color.White };
        Button btnPopHMinus = CreateSmallBtn("Cao -2", new Point(146, 96), 62, 23);
        Button btnPopHPlus = CreateSmallBtn("Cao +2", new Point(212, 96), 62, 23);

        pnlCoordControls.Controls.AddRange(new Control[] {
            lblPopX, numPopX, btnPopLeft1, btnPopRight1, btnPopLeft5, btnPopRight5,
            lblPopY, numPopY, btnPopUp1, btnPopDown1, btnPopUp5, btnPopDown5,
            lblPopW, numPopW, btnPopWMinus, btnPopWPlus,
            lblPopH, numPopH, btnPopHMinus, btnPopHPlus
        });

        pnlCoordGroup.Controls.Add(pnlCoordControls);
        pnlCoordGroup.Controls.Add(lblCoordGroupTitle);

        // Group B: Map Filter Parameters
        Panel pnlFilterGroup = new() { Dock = DockStyle.Fill, BackColor = Color.FromArgb(32, 32, 42), BorderStyle = BorderStyle.FixedSingle, Margin = new Padding(3), Padding = new Padding(8) };
        Label lblFilterGroupTitle = new() { Text = "🎨 BỘ LỌC MÀU TÁCH NỀN ĐỊA HÌNH (TERRAIN MAP FILTER)", Font = new Font("Segoe UI", 9F, FontStyle.Bold), ForeColor = Color.FromArgb(255, 200, 100), Dock = DockStyle.Top, Height = 24 };

        Panel pnlFilterControls = new() { Dock = DockStyle.Fill, BackColor = Color.Transparent };

        Label lblBright = new() { Text = "Ngưỡng sáng chữ (100 - 255):", Location = new Point(8, 8), Size = new Size(185, 20), ForeColor = Color.LightGray, Font = new Font("Segoe UI", 8.5F) };
        numPopBrightness = new NumericUpDown { Location = new Point(196, 6), Size = new Size(62, 23), Minimum = 100, Maximum = 255, Value = _popSettings.BrightnessThreshold, BackColor = Color.FromArgb(40, 40, 50), ForeColor = Color.White };
        Label lblBrightExplain = new() { Text = "Mặc định: 175. Giữ lại các điểm sáng trắng/xám của chữ số POP.", Location = new Point(8, 30), Size = new Size(380, 20), ForeColor = Color.Gray, Font = new Font("Segoe UI", 7.8F) };

        Label lblSat = new() { Text = "Ngưỡng sắc độ màu max (5 - 100):", Location = new Point(8, 56), Size = new Size(185, 20), ForeColor = Color.LightGray, Font = new Font("Segoe UI", 8.5F) };
        numPopSaturation = new NumericUpDown { Location = new Point(196, 54), Size = new Size(62, 23), Minimum = 5, Maximum = 150, Value = _popSettings.MaxSaturation, BackColor = Color.FromArgb(40, 40, 50), ForeColor = Color.White };
        Label lblSatExplain = new() { Text = "Mặc định: 35. max(R,G,B) - min(R,G,B) ≤ 35. Triệt tiêu hoàn toàn màu cỏ xanh, nước lam, bùn đất!", Location = new Point(8, 78), Size = new Size(380, 36), ForeColor = Color.Gray, Font = new Font("Segoe UI", 7.8F) };

        pnlFilterControls.Controls.AddRange(new Control[] {
            lblBright, numPopBrightness, lblBrightExplain,
            lblSat, numPopSaturation, lblSatExplain
        });

        pnlFilterGroup.Controls.Add(pnlFilterControls);
        pnlFilterGroup.Controls.Add(lblFilterGroupTitle);

        tblSettingsRow.Controls.Add(pnlCoordGroup, 0, 0);
        tblSettingsRow.Controls.Add(pnlFilterGroup, 1, 0);

        pnlCardBody.Controls.Add(tblSettingsRow);
        pnlCardBody.Controls.Add(tblPreviewRow);

        pnlPopCard.Controls.Add(pnlCardBody);
        pnlPopCard.Controls.Add(pnlCardHeader);

        pnlPopMain.Controls.Add(pnlPopCard);

        pnlTabPop.Controls.Add(pnlPopMain);
        pnlTabPop.Controls.Add(pnlPopOverview);
        pnlTabPop.Controls.Add(pnlPopTemplates);

        pnlTabPop.Controls.SetChildIndex(pnlPopOverview, 0);
        pnlTabPop.Controls.SetChildIndex(pnlPopTemplates, 1);
        pnlTabPop.Controls.SetChildIndex(pnlPopMain, 2);

        // Setup Hold to Repeat for POP
        SetupPopHoldToRepeat(
            btnPopLeft1, btnPopRight1, btnPopUp1, btnPopDown1,
            btnPopLeft5, btnPopRight5, btnPopUp5, btnPopDown5,
            btnPopWMinus, btnPopWPlus, btnPopHMinus, btnPopHPlus,
            btnPopTplLeft, btnPopTplRight, btnPopTplUp, btnPopTplDown,
            btnPopTplWMinus, btnPopTplWPlus, btnPopTplHMinus, btnPopTplHPlus
        );

        // Hook ValueChanged events
        numPopX.ValueChanged += (s, e) => { _popSettings.PopBox.X = (int)numPopX.Value; UpdatePopPreviewsFromCapture(); };
        numPopY.ValueChanged += (s, e) => { _popSettings.PopBox.Y = (int)numPopY.Value; UpdatePopPreviewsFromCapture(); };
        numPopW.ValueChanged += (s, e) => { _popSettings.PopBox.Width = (int)numPopW.Value; UpdatePopPreviewsFromCapture(); };
        numPopH.ValueChanged += (s, e) => { _popSettings.PopBox.Height = (int)numPopH.Value; UpdatePopPreviewsFromCapture(); };
        numPopBrightness.ValueChanged += (s, e) => { _popSettings.BrightnessThreshold = (int)numPopBrightness.Value; UpdatePopPreviewsFromCapture(); };
        numPopSaturation.ValueChanged += (s, e) => { _popSettings.MaxSaturation = (int)numPopSaturation.Value; UpdatePopPreviewsFromCapture(); };

        numPopTplX.ValueChanged += (s, e) => UpdatePopTemplatePreview();
        numPopTplY.ValueChanged += (s, e) => UpdatePopTemplatePreview();
        numPopTplW.ValueChanged += (s, e) => UpdatePopTemplatePreview();
        numPopTplH.ValueChanged += (s, e) => UpdatePopTemplatePreview();

        pnlTabPop.ResumeLayout(true);
    }

    private static Button CreateSmallBtn(string text, Point loc, int w = 26, int h = 22)
    {
        Button btn = new()
        {
            Text = text,
            Location = loc,
            Size = new Size(w, h),
            Font = new Font("Segoe UI", 7.5F, FontStyle.Bold),
            BackColor = Color.FromArgb(48, 48, 60),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat
        };
        btn.FlatAppearance.BorderSize = 0;
        return btn;
    }

    private void SetupPopHoldToRepeat(
        Button btnLeft1, Button btnRight1, Button btnUp1, Button btnDown1,
        Button btnLeft5, Button btnRight5, Button btnUp5, Button btnDown5,
        Button btnWMinus, Button btnWPlus, Button btnHMinus, Button btnHPlus,
        Button btnTplLeft, Button btnTplRight, Button btnTplUp, Button btnTplDown,
        Button btnTplWMinus, Button btnTplWPlus, Button btnTplHMinus, Button btnTplHPlus)
    {
        AttachHoldToRepeat(btnLeft1, () => { if (numPopX.Value > numPopX.Minimum) numPopX.Value--; });
        AttachHoldToRepeat(btnRight1, () => { if (numPopX.Value < numPopX.Maximum) numPopX.Value++; });
        AttachHoldToRepeat(btnUp1, () => { if (numPopY.Value > numPopY.Minimum) numPopY.Value--; });
        AttachHoldToRepeat(btnDown1, () => { if (numPopY.Value < numPopY.Maximum) numPopY.Value++; });

        AttachHoldToRepeat(btnLeft5, () => { numPopX.Value = Math.Max(numPopX.Minimum, numPopX.Value - 5); });
        AttachHoldToRepeat(btnRight5, () => { numPopX.Value = Math.Min(numPopX.Maximum, numPopX.Value + 5); });
        AttachHoldToRepeat(btnUp5, () => { numPopY.Value = Math.Max(numPopY.Minimum, numPopY.Value - 5); });
        AttachHoldToRepeat(btnDown5, () => { numPopY.Value = Math.Min(numPopY.Maximum, numPopY.Value + 5); });

        AttachHoldToRepeat(btnWMinus, () => { numPopW.Value = Math.Max(numPopW.Minimum, numPopW.Value - 2); });
        AttachHoldToRepeat(btnWPlus, () => { numPopW.Value = Math.Min(numPopW.Maximum, numPopW.Value + 2); });
        AttachHoldToRepeat(btnHMinus, () => { numPopH.Value = Math.Max(numPopH.Minimum, numPopH.Value - 2); });
        AttachHoldToRepeat(btnHPlus, () => { numPopH.Value = Math.Min(numPopH.Maximum, numPopH.Value + 2); });

        AttachHoldToRepeat(btnTplLeft, () => { if (numPopTplX.Value > numPopTplX.Minimum) numPopTplX.Value--; });
        AttachHoldToRepeat(btnTplRight, () => { if (numPopTplX.Value < numPopTplX.Maximum) numPopTplX.Value++; });
        AttachHoldToRepeat(btnTplUp, () => { if (numPopTplY.Value > numPopTplY.Minimum) numPopTplY.Value--; });
        AttachHoldToRepeat(btnTplDown, () => { if (numPopTplY.Value < numPopTplY.Maximum) numPopTplY.Value++; });

        AttachHoldToRepeat(btnTplWMinus, () => { if (numPopTplW.Value > numPopTplW.Minimum) numPopTplW.Value--; });
        AttachHoldToRepeat(btnTplWPlus, () => { if (numPopTplW.Value < numPopTplW.Maximum) numPopTplW.Value++; });
        AttachHoldToRepeat(btnTplHMinus, () => { if (numPopTplH.Value > numPopTplH.Minimum) numPopTplH.Value--; });
        AttachHoldToRepeat(btnTplHPlus, () => { if (numPopTplH.Value < numPopTplH.Maximum) numPopTplH.Value++; });
    }

    private void BuildPopGlyphSlots(TableLayoutPanel tbl)
    {
        tbl.SuspendLayout();
        tbl.Controls.Clear();

        for (int i = 0; i < 11; i++)
        {
            int idx = i;
            var panel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(32, 32, 40),
                BorderStyle = BorderStyle.FixedSingle,
                Margin = new Padding(2),
                Padding = new Padding(2)
            };

            string headerText = idx < 10 ? $"Số {idx}" : "Dấu /";
            var lblHeader = new Label
            {
                Dock = DockStyle.Top,
                Height = 20,
                Text = headerText,
                Font = new Font("Segoe UI", 8.5F, FontStyle.Bold),
                ForeColor = idx < 10 ? Color.FromArgb(220, 200, 255) : Color.FromArgb(255, 215, 100),
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
            btnAssign.Click += (s, e) => AssignCurrentToPopGlyph(idx);

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
            btnClear.Click += (s, e) => ClearPopGlyphTemplate(idx);

            panel.Controls.Add(btnClear);
            panel.Controls.Add(btnAssign);
            panel.Controls.Add(lblSize);
            panel.Controls.Add(picSlot);
            panel.Controls.Add(lblHeader);

            _popGlyphSlots[idx] = new DigitSlotUI
            {
                Digit = idx,
                PicSlot = picSlot,
                LblSize = lblSize,
                BtnAssign = btnAssign,
                BtnClear = btnClear,
                SlotPanel = panel
            };

            tbl.Controls.Add(panel, idx, 0);
        }

        tbl.ResumeLayout(true);
    }

    private void SetPopGlyphSlotImage(int idx, Bitmap bmp)
    {
        if (idx < 0 || idx >= 11) return;
        var slot = _popGlyphSlots[idx];
        if (slot?.PicSlot == null) return;

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

    private void LoadExistingPopTemplates()
    {
        try
        {
            for (int i = 0; i < 10; i++)
            {
                string path = Path.Combine(_popTemplatesDir, $"{i}.png");
                if (File.Exists(path))
                {
                    using var fs = new FileStream(path, FileMode.Open, FileAccess.Read);
                    using var img = Image.FromStream(fs);
                    Bitmap bmp = new(img);
                    SetPopGlyphSlotImage(i, bmp);
                }
            }

            string slashPath = Path.Combine(_popTemplatesDir, "slash.png");
            if (!File.Exists(slashPath)) slashPath = Path.Combine(_popTemplatesDir, "10.png");
            if (!File.Exists(slashPath)) slashPath = Path.Combine(_popTemplatesDir, "div.png");

            if (File.Exists(slashPath))
            {
                using var fs = new FileStream(slashPath, FileMode.Open, FileAccess.Read);
                using var img = Image.FromStream(fs);
                Bitmap bmp = new(img);
                SetPopGlyphSlotImage(10, bmp);
            }
        }
        catch { }
    }

    private void AssignCurrentToPopGlyph(int idx)
    {
        if (_currentPopTemplateCropBmp == null)
        {
            MessageBox.Show(this, "Chưa có ảnh cắt template POP để gán! Hãy chụp màn hình hoặc nạp ảnh trước.", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        try
        {
            string filename = (idx == 10) ? "slash.png" : $"{idx}.png";
            string filePath = Path.Combine(_popTemplatesDir, filename);
            _currentPopTemplateCropBmp.Save(filePath, ImageFormat.Png);

            // Đồng bộ sang thư mục gốc dự án Templates/Pop nếu có
            try
            {
                string projectTplDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "Templates", "Pop");
                if (Directory.Exists(projectTplDir))
                {
                    _currentPopTemplateCropBmp.Save(Path.Combine(projectTplDir, filename), ImageFormat.Png);
                }
            }
            catch { }

            SetPopGlyphSlotImage(idx, _currentPopTemplateCropBmp);

            _popOcrService.LoadTemplates();
            (this.Owner as MainForm)?.ReloadOcrSettings();
            UpdatePopPreviewsFromCapture();

            string name = (idx == 10) ? "Dấu /" : $"Số {idx}";
            lblStatus.Text = $"✅ Đã lưu template POP {name} thành công vào: {filePath}";
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"Lỗi khi lưu template POP: {ex.Message}", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void ClearPopGlyphTemplate(int idx)
    {
        try
        {
            string filename = (idx == 10) ? "slash.png" : $"{idx}.png";
            string filePath = Path.Combine(_popTemplatesDir, filename);
            if (File.Exists(filePath)) File.Delete(filePath);

            if (idx == 10)
            {
                string p10 = Path.Combine(_popTemplatesDir, "10.png");
                if (File.Exists(p10)) File.Delete(p10);
                string pdiv = Path.Combine(_popTemplatesDir, "div.png");
                if (File.Exists(pdiv)) File.Delete(pdiv);
            }

            var slot = _popGlyphSlots[idx];
            if (slot?.PicSlot != null)
            {
                var old = slot.PicSlot.Image;
                slot.PicSlot.Image = null;
                old?.Dispose();
                slot.LblSize.Text = "Trống";
                slot.LblSize.ForeColor = Color.Gray;
            }

            _popOcrService.LoadTemplates();
            (this.Owner as MainForm)?.ReloadOcrSettings();
            UpdatePopPreviewsFromCapture();

            string name = (idx == 10) ? "Dấu /" : $"Số {idx}";
            lblStatus.Text = $"Đã xóa template POP {name}.";
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"Lỗi khi xóa template: {ex.Message}", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void DisposePopSlots()
    {
        for (int i = 0; i < 11; i++)
        {
            var old = _popGlyphSlots[i]?.PicSlot?.Image;
            old?.Dispose();
        }
        _currentPopTemplateCropBmp?.Dispose();
    }

    private void UpdatePopPreviewsFromCapture()
    {
        if (_lastCapturedSource == null || picPopRawPreview == null || picPopFilteredPreview == null) return;

        int boxX = _popSettings.PopBox.X;
        int boxY = _popSettings.PopBox.Y;
        int boxW = _popSettings.PopBox.Width;
        int boxH = _popSettings.PopBox.Height;

        int srcW = _lastCapturedSource.Width;
        int srcH = _lastCapturedSource.Height;

        boxX = Math.Clamp(boxX, 0, Math.Max(0, srcW - 1));
        boxY = Math.Clamp(boxY, 0, Math.Max(0, srcH - 1));
        boxW = Math.Clamp(boxW, 1, Math.Max(1, srcW - boxX));
        boxH = Math.Clamp(boxH, 1, Math.Max(1, srcH - boxY));

        // 1. Raw Preview (Zoom 4x)
        int zoom = 4;
        Bitmap rawZoomed = new(boxW * zoom, boxH * zoom);
        using (Graphics g = Graphics.FromImage(rawZoomed))
        {
            g.InterpolationMode = InterpolationMode.NearestNeighbor;
            g.PixelOffsetMode = PixelOffsetMode.Half;
            g.DrawImage(_lastCapturedSource, new Rectangle(0, 0, rawZoomed.Width, rawZoomed.Height), new Rectangle(boxX, boxY, boxW, boxH), GraphicsUnit.Pixel);
            using Pen p = new(Color.FromArgb(100, 220, 255), 1);
            g.DrawRectangle(p, 0, 0, rawZoomed.Width - 1, rawZoomed.Height - 1);
        }
        var oldRaw = picPopRawPreview.Image;
        picPopRawPreview.Image = rawZoomed;
        oldRaw?.Dispose();

        // 2. Filtered Preview (Zoom 4x)
        using Bitmap rawCrop = new(boxW, boxH);
        using (Graphics g = Graphics.FromImage(rawCrop))
        {
            g.DrawImage(_lastCapturedSource, new Rectangle(0, 0, boxW, boxH), new Rectangle(boxX, boxY, boxW, boxH), GraphicsUnit.Pixel);
        }

        using Bitmap binarized = PopOcrService.CreateFilteredPreviewBitmap(rawCrop, 0, 0, boxW, boxH, _popSettings.BrightnessThreshold, _popSettings.MaxSaturation);
        Bitmap filteredZoomed = new(boxW * zoom, boxH * zoom);
        using (Graphics g = Graphics.FromImage(filteredZoomed))
        {
            g.InterpolationMode = InterpolationMode.NearestNeighbor;
            g.PixelOffsetMode = PixelOffsetMode.Half;
            g.DrawImage(binarized, new Rectangle(0, 0, filteredZoomed.Width, filteredZoomed.Height), new Rectangle(0, 0, boxW, boxH), GraphicsUnit.Pixel);
            using Pen p = new(Color.FromArgb(70, 240, 140), 1);
            g.DrawRectangle(p, 0, 0, filteredZoomed.Width - 1, filteredZoomed.Height - 1);
        }
        var oldFiltered = picPopFilteredPreview.Image;
        picPopFilteredPreview.Image = filteredZoomed;
        oldFiltered?.Dispose();

        // 3. Live OCR Result
        var popRes = _popOcrService.RecognizeFromBitmap(_lastCapturedSource, _popSettings);
        if (popRes.IsValid)
        {
            lblPopOcrResult.Text = $"🎯 POP nhận diện: {popRes.CurrentPop} / {popRes.MaxPop}";
            lblPopOcrResult.ForeColor = Color.FromArgb(70, 240, 140);
        }
        else
        {
            lblPopOcrResult.Text = "⚠️ Chưa nhận diện được (Kiểm tra template hoặc ngưỡng)";
            lblPopOcrResult.ForeColor = Color.FromArgb(250, 110, 100);
        }

        lblPopSizeInfo.Text = $"Vùng POP: ({boxX}, {boxY}) | Kích thước: {boxW}x{boxH} px | Ngưỡng sáng: {_popSettings.BrightnessThreshold} | Sắc độ tối đa: ≤{_popSettings.MaxSaturation}";

        // 4. Update Overview Strip
        UpdatePopOverviewStrip();
    }

    private void UpdatePopOverviewStrip()
    {
        if (_lastCapturedSource == null || picPopOverview == null) return;

        int srcW = _lastCapturedSource.Width;
        int srcH = _lastCapturedSource.Height;

        int stripX = Math.Min(550, Math.Max(0, srcW - 50));
        int stripY = Math.Min(15, Math.Max(0, srcH - 10));
        int stripW = Math.Min(260, srcW - stripX);
        int stripH = Math.Min(45, srcH - stripY);

        if (stripW <= 0 || stripH <= 0) return;

        Bitmap overviewBmp = new(stripW, stripH);
        using (Graphics g = Graphics.FromImage(overviewBmp))
        {
            g.DrawImage(_lastCapturedSource, new Rectangle(0, 0, stripW, stripH), new Rectangle(stripX, stripY, stripW, stripH), GraphicsUnit.Pixel);

            // Draw Cyan bounding box for POP box
            int relBoxX = _popSettings.PopBox.X - stripX;
            int relBoxY = _popSettings.PopBox.Y - stripY;
            using Pen boxPen = new(Color.FromArgb(100, 220, 255), 1.5F);
            g.DrawRectangle(boxPen, relBoxX, relBoxY, _popSettings.PopBox.Width, _popSettings.PopBox.Height);

            using Font font = new("Segoe UI", 7F, FontStyle.Bold);
            using SolidBrush brush = new(Color.FromArgb(100, 220, 255));
            g.DrawString("POP", font, brush, relBoxX + 2, Math.Max(0, relBoxY - 10));

            // Draw Dashed Yellow bounding box for POP template tool if visible
            if (pnlPopTemplates != null && pnlPopTemplates.Visible && numPopTplX != null)
            {
                int tX = (int)numPopTplX.Value - stripX;
                int tY = (int)numPopTplY.Value - stripY;
                int tW = (int)numPopTplW.Value;
                int tH = (int)numPopTplH.Value;

                using Pen tplPen = new(Color.Yellow, 1.5F) { DashStyle = DashStyle.Dash };
                g.DrawRectangle(tplPen, tX, tY, tW, tH);
            }
        }

        var oldImg = picPopOverview.Image;
        picPopOverview.Image = overviewBmp;
        oldImg?.Dispose();
    }

    private void UpdatePopTemplatePreview()
    {
        if (_lastCapturedSource == null || numPopTplX == null || picPopTemplatePreview == null) return;

        int tplX = (int)numPopTplX.Value;
        int tplY = (int)numPopTplY.Value;
        int tplW = (int)numPopTplW.Value;
        int tplH = (int)numPopTplH.Value;

        int srcW = _lastCapturedSource.Width;
        int srcH = _lastCapturedSource.Height;

        tplX = Math.Clamp(tplX, 0, Math.Max(0, srcW - 1));
        tplY = Math.Clamp(tplY, 0, Math.Max(0, srcH - 1));
        tplW = Math.Clamp(tplW, 1, Math.Max(1, srcW - tplX));
        tplH = Math.Clamp(tplH, 1, Math.Max(1, srcH - tplY));

        using Bitmap rawCrop = new(tplW, tplH);
        using (Graphics g = Graphics.FromImage(rawCrop))
        {
            g.DrawImage(_lastCapturedSource, new Rectangle(0, 0, tplW, tplH), new Rectangle(tplX, tplY, tplW, tplH), GraphicsUnit.Pixel);
        }

        Bitmap finalCrop = PopOcrService.CreateFilteredPreviewBitmap(rawCrop, 0, 0, tplW, tplH, _popSettings.BrightnessThreshold, _popSettings.MaxSaturation);

        var oldCrop = _currentPopTemplateCropBmp;
        _currentPopTemplateCropBmp = finalCrop;
        oldCrop?.Dispose();

        int zoom = 4;
        Bitmap previewBmp = new(tplW * zoom, tplH * zoom);
        using (Graphics g = Graphics.FromImage(previewBmp))
        {
            g.InterpolationMode = InterpolationMode.NearestNeighbor;
            g.PixelOffsetMode = PixelOffsetMode.Half;
            g.DrawImage(_currentPopTemplateCropBmp, new Rectangle(0, 0, previewBmp.Width, previewBmp.Height), new Rectangle(0, 0, tplW, tplH), GraphicsUnit.Pixel);

            using Pen p = new(Color.Yellow, 1);
            g.DrawRectangle(p, 0, 0, previewBmp.Width - 1, previewBmp.Height - 1);
        }

        var oldPreview = picPopTemplatePreview.Image;
        picPopTemplatePreview.Image = previewBmp;
        oldPreview?.Dispose();

        lblPopTplCoord.Text = $"({tplX}, {tplY}) | {tplW}x{tplH}px";

        UpdatePopOverviewStrip();
    }

    private void PicPopOverview_MouseDown(object? sender, MouseEventArgs e)
    {
        if (picPopOverview.Image == null) return;

        int? px = GetPopOverviewPixelX(e.X);
        int? py = GetPopOverviewPixelY(e.Y);

        if (px.HasValue && py.HasValue)
        {
            int w = (int)numPopTplW.Value;
            int h = (int)numPopTplH.Value;

            int targetX = Math.Clamp(px.Value - w / 2, (int)numPopTplX.Minimum, (int)numPopTplX.Maximum);
            int targetY = Math.Clamp(py.Value - h / 2, (int)numPopTplY.Minimum, (int)numPopTplY.Maximum);

            numPopTplX.Value = targetX;
            numPopTplY.Value = targetY;
            UpdatePopTemplatePreview();
        }
    }

    private void PicPopOverview_MouseMove(object? sender, MouseEventArgs e)
    {
        int? px = GetPopOverviewPixelX(e.X);
        int? py = GetPopOverviewPixelY(e.Y);

        if (px.HasValue && py.HasValue)
        {
            lblPopMouseCoord.Text = $"Tọa độ chuột trên ảnh: X = {px.Value}, Y = {py.Value}";
        }
    }

    private int? GetPopOverviewPixelX(int mouseX)
    {
        if (picPopOverview.Image == null) return null;
        GetRenderMetricsFor(picPopOverview, out float renderW, out float _, out float offsetX, out float _, out int imgW, out int _);
        float relX = mouseX - offsetX;
        if (relX >= 0 && relX <= renderW)
        {
            int stripX = Math.Min(550, Math.Max(0, (_lastCapturedSource?.Width ?? 800) - 50));
            return stripX + (int)(relX * imgW / renderW);
        }
        return null;
    }

    private int? GetPopOverviewPixelY(int mouseY)
    {
        if (picPopOverview.Image == null) return null;
        GetRenderMetricsFor(picPopOverview, out float _, out float renderH, out float _, out float offsetY, out int _, out int imgH);
        float relY = mouseY - offsetY;
        if (relY >= 0 && relY <= renderH)
        {
            int stripY = Math.Min(15, Math.Max(0, (_lastCapturedSource?.Height ?? 60) - 10));
            return stripY + (int)(relY * imgH / renderH);
        }
        return null;
    }

    private static void GetRenderMetricsFor(PictureBox pic, out float renderW, out float renderH, out float offsetX, out float offsetY, out int imgW, out int imgH)
    {
        imgW = pic.Image!.Width;
        imgH = pic.Image!.Height;

        float boxRatio = (float)pic.Width / pic.Height;
        float imgRatio = (float)imgW / imgH;

        if (boxRatio > imgRatio)
        {
            renderH = pic.Height;
            renderW = imgRatio * renderH;
            offsetX = (pic.Width - renderW) / 2f;
            offsetY = 0;
        }
        else
        {
            renderW = pic.Width;
            renderH = renderW / imgRatio;
            offsetX = 0;
            offsetY = (pic.Height - renderH) / 2f;
        }
    }

    private void BtnOpenPopFolder_Click(object? sender, EventArgs e)
    {
        try
        {
            Directory.CreateDirectory(_popTemplatesDir);
            Process.Start(new ProcessStartInfo("explorer.exe", _popTemplatesDir) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"Không thể mở thư mục: {ex.Message}", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
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

                // Calculate required capture width and height (covers both resources and POP area up to X=800, Y=60)
                int maxRequiredX = Math.Max(800, Math.Max(_popSettings.PopBox.X + _popSettings.PopBox.Width + 40,
                    Math.Max(_settings.Wood.X + _settings.Wood.Width,
                    Math.Max(_settings.Food.X + _settings.Food.Width,
                    Math.Max(_settings.Gold.X + _settings.Gold.Width, _settings.Stone.X + _settings.Stone.Width))))) + 20;

                int maxRequiredY = Math.Max(60, Math.Max(_popSettings.PopBox.Y + _popSettings.PopBox.Height + 20,
                    Math.Max(_settings.Wood.Y + _settings.Wood.Height,
                    Math.Max(_settings.Food.Y + _settings.Food.Height,
                    Math.Max(_settings.Gold.Y + _settings.Gold.Height, _settings.Stone.Y + _settings.Stone.Height))))) + 10;

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
            UpdatePopPreviewsFromCapture();
            UpdatePopTemplatePreview();

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
