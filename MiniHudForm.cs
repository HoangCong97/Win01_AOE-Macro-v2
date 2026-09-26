using System.Drawing.Drawing2D;
using System.Drawing.Text;
using AOEKeyboardMacroPro.Models;
using AOEKeyboardMacroPro.Services;

namespace AOEKeyboardMacroPro;

public class MiniHudForm : Form
{
    private readonly HudSettings _settings;
    private readonly System.Windows.Forms.Timer _popBlinkTimer = new();
    private readonly ContextMenuStrip _contextMenu = new();

    private ResourceValues _resourceValues = new();
    private PopValues _popValues = new();
    private TimerValues _timerValues = new();

    private bool _shouldBlink = false;
    private bool _blinkPhase = false;
    private bool _isMaxPop = false;
    private bool _isDimmed = true;

    private bool _isDragging = false;
    private Point _dragStart;
    private Rectangle _closeBtnRect = new(128, 9, 16, 16);

    public bool IsDimmed => _isDimmed;

    public void SetDimmed(bool dimmed)
    {
        if (IsDisposed) return;
        if (InvokeRequired)
        {
            BeginInvoke(new Action<bool>(SetDimmed), dimmed);
            return;
        }

        if (_isDimmed != dimmed)
        {
            _isDimmed = dimmed;
            if (_isDimmed)
            {
                // Khi bị làm mờ: TẮT CẢNH BÁO HOÀN TOÀN!
                _shouldBlink = false;
                _blinkPhase = false;
                _popBlinkTimer.Stop();
            }
            else
            {
                CheckPopBlinkCondition();
            }
            Invalidate();
        }
    }

    public MiniHudForm(HudSettings? settings = null)
    {
        _settings = settings ?? ConfigService.LoadSettings().Hud ?? new HudSettings();

        // Cài đặt thuộc tính Form nổi (Gaming HUD)
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.Manual;
        TopMost = true;
        ShowInTaskbar = false;
        DoubleBuffered = true;
        Size = new Size(154, 235);
        BackColor = Color.FromArgb(16, 17, 22);

        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);

        // Khởi tạo vị trí & độ mờ
        LoadPositionAndState();

        // Timer nhấp nháy POP khi sắp chạm giới hạn nhà (chậm dân)
        _popBlinkTimer.Interval = 320;
        _popBlinkTimer.Tick += (s, e) =>
        {
            if (_shouldBlink)
            {
                _blinkPhase = !_blinkPhase;
                Invalidate();
            }
        };

        BuildContextMenu();
    }

    protected override bool ShowWithoutActivation => true;

    protected override CreateParams CreateParams
    {
        get
        {
            var cp = base.CreateParams;
            cp.ExStyle |= 0x08000000; // WS_EX_NOACTIVATE: Không giật focus khi click hoặc cập nhật dữ liệu game
            return cp;
        }
    }

    private void LoadPositionAndState()
    {
        int x = _settings.X;
        int y = _settings.Y;

        var screen = Screen.FromPoint(new Point(x, y));
        if (x < screen.WorkingArea.Left || x > screen.WorkingArea.Right - Width ||
            y < screen.WorkingArea.Top || y > screen.WorkingArea.Bottom - Height)
        {
            x = screen.WorkingArea.Left + 25;
            y = screen.WorkingArea.Top + 140;
        }

        Location = new Point(x, y);
        Opacity = Math.Clamp(_settings.Opacity, 0.35, 1.0);
    }

    public void SavePosition()
    {
        try
        {
            _settings.X = Location.X;
            _settings.Y = Location.Y;
            _settings.Opacity = Opacity;
            var appSettings = ConfigService.LoadSettings();
            appSettings.Hud = _settings;
            ConfigService.SaveSettings(appSettings);
        }
        catch { }
    }

    private void BuildContextMenu()
    {
        var itemLock = new ToolStripMenuItem("🔒 Khóa vị trí", null, (s, e) =>
        {
            _settings.Locked = !_settings.Locked;
            ((ToolStripMenuItem)s!).Checked = _settings.Locked;
            SavePosition();
        })
        {
            Checked = _settings.Locked
        };

        var menuOpacity = new ToolStripMenuItem("🌓 Độ mờ (Opacity)");
        foreach (int op in new[] { 100, 90, 80, 65, 50 })
        {
            var opItem = new ToolStripMenuItem($"{op}%", null, (s, e) =>
            {
                Opacity = op / 100.0;
                _settings.Opacity = Opacity;
                SavePosition();
                foreach (ToolStripMenuItem sub in menuOpacity.DropDownItems)
                {
                    sub.Checked = (sub == s);
                }
            })
            {
                Checked = Math.Abs(Opacity - (op / 100.0)) < 0.05
            };
            menuOpacity.DropDownItems.Add(opItem);
        }

        var itemReset = new ToolStripMenuItem("🎯 Đặt lại vị trí mặc định", null, (s, e) =>
        {
            Location = new Point(30, 150);
            SavePosition();
        });

        var itemHide = new ToolStripMenuItem("✕ Ẩn Mini HUD", null, (s, e) =>
        {
            Hide();
        });

        _contextMenu.Items.AddRange(new ToolStripItem[]
        {
            new ToolStripLabel("AOE MINI HUD") { Font = new Font("Segoe UI", 9F, FontStyle.Bold), ForeColor = Color.DarkGray },
            new ToolStripSeparator(),
            itemLock,
            menuOpacity,
            itemReset,
            new ToolStripSeparator(),
            itemHide
        });

        ContextMenuStrip = _contextMenu;
    }

    public void UpdateResources(ResourceValues? res)
    {
        if (IsDisposed) return;
        if (InvokeRequired)
        {
            BeginInvoke(new Action<ResourceValues?>(UpdateResources), res);
            return;
        }

        if (res != null && !res.IsEmpty)
        {
            if (res.Wood.HasValue) _resourceValues.Wood = res.Wood;
            if (res.Food.HasValue) _resourceValues.Food = res.Food;
            if (res.Gold.HasValue) _resourceValues.Gold = res.Gold;
            if (res.Stone.HasValue) _resourceValues.Stone = res.Stone;
        }
        Invalidate();
    }

    public void UpdateTimer(TimerValues? timer)
    {
        if (IsDisposed) return;
        if (InvokeRequired)
        {
            BeginInvoke(new Action<TimerValues?>(UpdateTimer), timer);
            return;
        }

        if (timer != null && timer.IsValid)
        {
            _timerValues.RawText = timer.RawText;
        }
        Invalidate();
    }

    public void UpdatePop(PopValues? pop)
    {
        if (IsDisposed) return;
        if (InvokeRequired)
        {
            BeginInvoke(new Action<PopValues?>(UpdatePop), pop);
            return;
        }

        if (pop != null && (pop.IsValid || pop.CurrentPop.HasValue))
        {
            if (pop.CurrentPop.HasValue) _popValues.CurrentPop = pop.CurrentPop;
            if (pop.MaxPop.HasValue) _popValues.MaxPop = pop.MaxPop;
        }

        CheckPopBlinkCondition();
        Invalidate();
    }

    private void CheckPopBlinkCondition()
    {
        if (_isDimmed)
        {
            _isMaxPop = false;
            _shouldBlink = false;
            _blinkPhase = false;
            _popBlinkTimer.Stop();
            return;
        }

        if (_popValues.IsValid)
        {
            int x = _popValues.CurrentPop!.Value;
            int y = _popValues.MaxPop!.Value;
            int diff = y - x;

            if (x >= 200)
            {
                // Khi POP >= 200: tô nền, bỏ nhấp nháy
                _isMaxPop = true;
                _shouldBlink = false;
            }
            else
            {
                _isMaxPop = false;

                // Các khoảng phân tầng độc lập, tuyệt đối không để khoảng sau đè lên khoảng trước
                if (x < 26)
                {
                    _shouldBlink = (diff <= 2);
                }
                else if (x < 50)
                {
                    _shouldBlink = (diff <= 4);
                }
                else if (x < 100)
                {
                    _shouldBlink = (diff <= 8);
                }
                else // 100 <= x < 200
                {
                    _shouldBlink = (diff <= 16);
                }
            }

            if (_shouldBlink)
            {
                if (!_popBlinkTimer.Enabled)
                {
                    _blinkPhase = true;
                    _popBlinkTimer.Start();
                }
            }
            else
            {
                _blinkPhase = false;
                _popBlinkTimer.Stop();
            }
        }
        else
        {
            _isMaxPop = false;
            _shouldBlink = false;
            _blinkPhase = false;
            _popBlinkTimer.Stop();
        }
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button == MouseButtons.Left)
        {
            if (_closeBtnRect.Contains(e.Location))
            {
                Hide();
                return;
            }

            if (!_settings.Locked)
            {
                _isDragging = true;
                _dragStart = e.Location;
            }
        }
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (_isDragging && !_settings.Locked)
        {
            Location = new Point(Left + (e.X - _dragStart.X), Top + (e.Y - _dragStart.Y));
        }
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        if (e.Button == MouseButtons.Left && _isDragging)
        {
            _isDragging = false;
            SavePosition();
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        Graphics g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

        // Nền form chính
        using (var bgBrush = new SolidBrush(Color.FromArgb(16, 17, 22)))
        {
            g.FillRectangle(bgBrush, ClientRectangle);
        }

        // Viền dày 8px bao quanh Mini HUD (nhấp nháy đồng bộ khi có cảnh báo POP)
        Color hudBorderColor;
        if (_isDimmed)
        {
            hudBorderColor = Color.FromArgb(42, 45, 55); // Viền mờ tối khi dimmed, không nhấp nháy
        }
        else if (_shouldBlink)
        {
            hudBorderColor = _blinkPhase ? Color.FromArgb(245, 40, 60) : Color.FromArgb(65, 25, 35);
        }
        else if (_isMaxPop)
        {
            hudBorderColor = Color.FromArgb(145, 45, 175);
        }
        else
        {
            hudBorderColor = Color.FromArgb(52, 56, 70);
        }

        const float borderThickness = 8f;
        RectangleF borderRect = new(
            borderThickness / 2f,
            borderThickness / 2f,
            Width - borderThickness,
            Height - borderThickness);

        using (GraphicsPath borderPath = CreateRoundedRectangle(borderRect, 6f))
        {
            using var borderPen = new Pen(hudBorderColor, borderThickness)
            {
                LineJoin = LineJoin.Round
            };
            g.DrawPath(borderPen, borderPath);
        }

        // Header nhỏ phía trên (Grip dots và nút tắt)
        using (var gripFont = new Font("Segoe UI", 7.5f, FontStyle.Bold))
        {
            TextRenderer.DrawText(g, "::: HUD", gripFont, new Point(12, 10), Color.FromArgb(120, 125, 140));
        }

        // Nút '×' tắt nhanh
        using (var closeFont = new Font("Segoe UI", 8.5f, FontStyle.Bold))
        {
            TextRenderer.DrawText(g, "×", closeFont, _closeBtnRect, Color.FromArgb(140, 145, 160),
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        }

        int rowX = 11;
        int rowW = Width - 22;
        int rowH = 30;

        // 1. THỜI GIAN (Hàng đầu tiên)
        Rectangle timerRect = new(rowX, 28, rowW, rowH);
        DrawTimerRow(g, timerRect);

        // 2. POP (Hàng thứ hai)
        Rectangle popRect = new(rowX, 61, rowW, rowH);
        DrawPopRow(g, popRect);

        // 3. GỖ (Wood)
        Rectangle woodRect = new(rowX, 94, rowW, rowH);
        DrawResourceRow(g, woodRect, "🪵 Gỗ", _resourceValues.Wood,
            normalText: Color.FromArgb(110, 215, 140),
            normalBg: Color.FromArgb(22, 28, 24),
            normalBorder: Color.FromArgb(38, 55, 42),
            l1Bg: Color.FromArgb(24, 65, 38), l1Border: Color.FromArgb(48, 125, 72), l1Text: Color.FromArgb(145, 255, 165),
            l2Bg: Color.FromArgb(32, 105, 55), l2Border: Color.FromArgb(65, 200, 110),
            l3Bg: Color.FromArgb(40, 148, 70), l3Border: Color.FromArgb(100, 255, 140));

        // 4. THỰC (Food)
        Rectangle foodRect = new(rowX, 127, rowW, rowH);
        DrawResourceRow(g, foodRect, "🥩 Thực", _resourceValues.Food,
            normalText: Color.FromArgb(245, 120, 125),
            normalBg: Color.FromArgb(28, 22, 24),
            normalBorder: Color.FromArgb(55, 38, 42),
            l1Bg: Color.FromArgb(70, 28, 35), l1Border: Color.FromArgb(135, 50, 62), l1Text: Color.FromArgb(255, 150, 155),
            l2Bg: Color.FromArgb(122, 35, 45), l2Border: Color.FromArgb(220, 65, 80),
            l3Bg: Color.FromArgb(188, 35, 52), l3Border: Color.FromArgb(255, 100, 118));

        // 5. VÀNG (Gold)
        Rectangle goldRect = new(rowX, 160, rowW, rowH);
        DrawResourceRow(g, goldRect, "🪙 Vàng", _resourceValues.Gold,
            normalText: Color.FromArgb(245, 210, 75),
            normalBg: Color.FromArgb(28, 27, 20),
            normalBorder: Color.FromArgb(55, 52, 36),
            l1Bg: Color.FromArgb(70, 60, 24), l1Border: Color.FromArgb(135, 115, 42), l1Text: Color.FromArgb(255, 230, 95),
            l2Bg: Color.FromArgb(128, 102, 28), l2Border: Color.FromArgb(230, 190, 52),
            l3Bg: Color.FromArgb(195, 148, 22), l3Border: Color.FromArgb(255, 225, 65));

        // 6. ĐÁ (Stone)
        Rectangle stoneRect = new(rowX, 193, rowW, rowH);
        DrawResourceRow(g, stoneRect, "🪨 Đá", _resourceValues.Stone,
            normalText: Color.FromArgb(120, 200, 250),
            normalBg: Color.FromArgb(22, 27, 32),
            normalBorder: Color.FromArgb(38, 50, 62),
            l1Bg: Color.FromArgb(24, 55, 82), l1Border: Color.FromArgb(48, 108, 155), l1Text: Color.FromArgb(145, 225, 255),
            l2Bg: Color.FromArgb(28, 88, 140), l2Border: Color.FromArgb(65, 170, 245),
            l3Bg: Color.FromArgb(25, 128, 215), l3Border: Color.FromArgb(100, 215, 255));
    }

    private void DrawTimerRow(Graphics g, Rectangle rect)
    {
        string textVal = _timerValues.IsValid ? _timerValues.RawText! : "--:--";

        Color bgColor;
        Color borderColor;
        Color textColor;

        if (_isDimmed)
        {
            bgColor = Color.FromArgb(18, 20, 26);
            borderColor = Color.FromArgb(28, 32, 40);
            textColor = Color.FromArgb(100, 115, 125);
        }
        else
        {
            bgColor = Color.FromArgb(20, 26, 34);
            borderColor = Color.FromArgb(35, 52, 70);
            textColor = Color.FromArgb(100, 205, 255);
        }

        using (GraphicsPath path = CreateRoundedRectangle(rect, 5))
        {
            using var b = new SolidBrush(bgColor);
            g.FillPath(b, path);
            using var p = new Pen(borderColor, 1f);
            g.DrawPath(p, path);
        }

        // Tên mục bên trái (ĐẠI LƯỢNG) - Luôn giữ màu xanh lam đặc trưng, không bị làm mờ
        Color labelColor = Color.FromArgb(100, 205, 255);
        using (var labelFont = new Font("Segoe UI", 9f, FontStyle.Regular))
        {
            TextRenderer.DrawText(g, "⏱️ Giờ", labelFont,
                new Rectangle(rect.X + 6, rect.Y, 52, rect.Height),
                labelColor, TextFormatFlags.VerticalCenter | TextFormatFlags.Left);
        }

        // Giá trị thời gian bên phải (GIÁ TRỊ) - Chỉ giá trị bị làm mờ
        Color valColor = _isDimmed ? Color.FromArgb(105, 115, 125) : Color.FromArgb(100, 205, 255);
        using (var valFont = new Font("Segoe UI", 9.5f, FontStyle.Bold))
        {
            TextRenderer.DrawText(g, textVal, valFont,
                new Rectangle(rect.X + 54, rect.Y, rect.Width - 60, rect.Height),
                valColor, TextFormatFlags.VerticalCenter | TextFormatFlags.Right);
        }
    }

    private void DrawPopRow(Graphics g, Rectangle rect)
    {
        string textVal;
        if (_popValues.IsValid)
        {
            textVal = $"{_popValues.CurrentPop}/{_popValues.MaxPop}";
        }
        else if (_popValues.CurrentPop.HasValue)
        {
            textVal = $"{_popValues.CurrentPop}/--";
        }
        else
        {
            textVal = "--/--";
        }

        Color bgColor;
        Color borderColor;
        Color textColor;
        bool isBold;

        if (_isDimmed)
        {
            // Khi bị làm mờ: màu xám dịu, tắt cảnh báo
            bgColor = Color.FromArgb(18, 20, 26);
            borderColor = Color.FromArgb(28, 32, 40);
            textColor = Color.FromArgb(115, 110, 125);
            isBold = false;
        }
        else if (_isMaxPop)
        {
            // Khi POP >= 200: tô nền, bỏ nhấp nháy
            bgColor = Color.FromArgb(140, 32, 168);
            borderColor = Color.FromArgb(215, 75, 250);
            textColor = Color.White;
            isBold = true;
        }
        else if (_shouldBlink)
        {
            // Nhấp nháy cảnh báo sắp đè dân / cần xây nhà BE
            if (_blinkPhase)
            {
                bgColor = Color.FromArgb(225, 30, 48);
                borderColor = Color.FromArgb(255, 95, 110);
                textColor = Color.White;
            }
            else
            {
                bgColor = Color.FromArgb(34, 25, 40);
                borderColor = Color.FromArgb(75, 45, 88);
                textColor = Color.FromArgb(250, 150, 255);
            }
            isBold = true;
        }
        else
        {
            bgColor = Color.FromArgb(25, 24, 32);
            borderColor = Color.FromArgb(50, 44, 62);
            textColor = Color.FromArgb(235, 130, 255);
            isBold = false;
        }

        using (GraphicsPath path = CreateRoundedRectangle(rect, 5))
        {
            using var b = new SolidBrush(bgColor);
            g.FillPath(b, path);
            using var p = new Pen(borderColor, 1f);
            g.DrawPath(p, path);
        }

        // Tên mục bên trái (ĐẠI LƯỢNG) - Luôn giữ màu tím đặc trưng, không bị làm mờ
        Color labelColor = Color.FromArgb(235, 130, 255);
        using (var labelFont = new Font("Segoe UI", 9f, FontStyle.Regular))
        {
            TextRenderer.DrawText(g, "👥 POP", labelFont,
                new Rectangle(rect.X + 6, rect.Y, 52, rect.Height),
                labelColor, TextFormatFlags.VerticalCenter | TextFormatFlags.Left);
        }

        // Giá trị bên phải (GIÁ TRỊ) - Chỉ giá trị bị làm mờ
        using (var valFont = new Font("Segoe UI", 9.5f, isBold ? FontStyle.Bold : FontStyle.Regular))
        {
            TextRenderer.DrawText(g, textVal, valFont,
                new Rectangle(rect.X + 54, rect.Y, rect.Width - 60, rect.Height),
                textColor, TextFormatFlags.VerticalCenter | TextFormatFlags.Right);
        }
    }

    private void DrawResourceRow(Graphics g, Rectangle rect, string label, int? val,
        Color normalText, Color normalBg, Color normalBorder,
        Color l1Bg, Color l1Border, Color l1Text,
        Color l2Bg, Color l2Border,
        Color l3Bg, Color l3Border)
    {
        string textVal = val.HasValue ? val.Value.ToString("N0") : "--";
        int amount = val ?? 0;

        Color bgColor;
        Color borderColor;
        Color textColor;
        bool isBold;

        if (_isDimmed)
        {
            // Làm mờ khi ngoài game/không đọc được, không tô đậm mức L1, L2, L3
            bgColor = Color.FromArgb(18, 20, 26);
            borderColor = Color.FromArgb(28, 32, 40);
            textColor = Color.FromArgb(120, 125, 135);
            isBold = false;
        }
        else if (amount > 2000)
        {
            bgColor = l3Bg;
            borderColor = l3Border;
            textColor = Color.White;
            isBold = true;
        }
        else if (amount > 1000)
        {
            bgColor = l2Bg;
            borderColor = l2Border;
            textColor = Color.White;
            isBold = true;
        }
        else if (amount > 500)
        {
            bgColor = l1Bg;
            borderColor = l1Border;
            textColor = l1Text;
            isBold = true;
        }
        else
        {
            bgColor = normalBg;
            borderColor = normalBorder;
            textColor = normalText;
            isBold = false;
        }

        using (GraphicsPath path = CreateRoundedRectangle(rect, 5))
        {
            using var b = new SolidBrush(bgColor);
            g.FillPath(b, path);
            using var p = new Pen(borderColor, 1f);
            g.DrawPath(p, path);
        }

        // Nhãn biểu tượng & tên tài nguyên bên trái (ĐẠI LƯỢNG) - Luôn giữ màu đặc trưng, không bị làm mờ
        using (var labelFont = new Font("Segoe UI", 9f, FontStyle.Regular))
        {
            TextRenderer.DrawText(g, label, labelFont,
                new Rectangle(rect.X + 6, rect.Y, 52, rect.Height),
                normalText, TextFormatFlags.VerticalCenter | TextFormatFlags.Left);
        }

        // Số lượng tài nguyên bên phải (GIÁ TRỊ) - Chỉ giá trị bị làm mờ
        using (var valFont = new Font("Segoe UI", 9.5f, isBold ? FontStyle.Bold : FontStyle.Regular))
        {
            TextRenderer.DrawText(g, textVal, valFont,
                new Rectangle(rect.X + 54, rect.Y, rect.Width - 60, rect.Height),
                textColor, TextFormatFlags.VerticalCenter | TextFormatFlags.Right);
        }
    }

    private static GraphicsPath CreateRoundedRectangle(Rectangle rect, int radius)
    {
        GraphicsPath path = new();
        int d = radius * 2;
        path.AddArc(rect.X, rect.Y, d, d, 180, 90);
        path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
        path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
        path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }

    private static GraphicsPath CreateRoundedRectangle(RectangleF rect, float radius)
    {
        GraphicsPath path = new();
        float d = radius * 2f;
        path.AddArc(rect.X, rect.Y, d, d, 180, 90);
        path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
        path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
        path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _popBlinkTimer.Stop();
            _popBlinkTimer.Dispose();
            _contextMenu.Dispose();
        }
        base.Dispose(disposing);
    }
}
