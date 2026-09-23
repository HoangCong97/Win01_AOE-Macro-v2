using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Text;
using AOEKeyboardMacroPro.Models;

namespace AOEKeyboardMacroPro.Services;

public class GlyphTemplate
{
    public char Character { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    public bool[,] Matrix { get; set; } = null!;
    public int WhitePixelCount { get; set; }
    public int BlackPixelCount { get; set; }
}

public class PopOcrService : IDisposable
{
    private readonly List<GlyphTemplate> _templates = new();
    private readonly System.Windows.Forms.Timer _scanTimer = new();
    private PopCropSettings _cropSettings;
    private bool _isRunning = false;
    private bool _isScanning = false;

    public event Action<PopValues>? PopUpdated;

    public bool IsRunning => _isRunning;

    public PopOcrService(PopCropSettings? settings = null)
    {
        _cropSettings = settings ?? ConfigService.LoadSettings().PopCrop ?? new PopCropSettings();
        LoadTemplates();

        _scanTimer.Interval = 100; // 100ms
        _scanTimer.Tick += ScanTimer_Tick;
    }

    public void UpdateSettings(PopCropSettings settings)
    {
        _cropSettings = settings;
    }

    public void SetScanInterval(int intervalMs)
    {
        _scanTimer.Interval = Math.Clamp(intervalMs, 20, 5000);
    }

    public void LoadTemplates()
    {
        _templates.Clear();
        string[] searchDirs = new[]
        {
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Templates", "Pop"),
            Path.Combine(Directory.GetCurrentDirectory(), "Templates", "Pop")
        };

        string? validDir = searchDirs.FirstOrDefault(Directory.Exists);
        if (validDir == null) return;

        // Load 10 digits: 0..9
        for (int d = 0; d <= 9; d++)
        {
            string path = Path.Combine(validDir, $"{d}.png");
            LoadSingleTemplate(path, (char)('0' + d));
        }

        // Load slash '/' (slash.png or 10.png or div.png)
        string slashPath = Path.Combine(validDir, "slash.png");
        if (!File.Exists(slashPath)) slashPath = Path.Combine(validDir, "10.png");
        if (!File.Exists(slashPath)) slashPath = Path.Combine(validDir, "div.png");
        LoadSingleTemplate(slashPath, '/');
    }

    private void LoadSingleTemplate(string path, char character)
    {
        if (!File.Exists(path)) return;
        try
        {
            using var fs = new FileStream(path, FileMode.Open, FileAccess.Read);
            using var bmp = new Bitmap(fs);

            int w = bmp.Width;
            int h = bmp.Height;
            bool[,] matrix = new bool[w, h];
            int whiteCount = 0;

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    Color c = bmp.GetPixel(x, y);
                    bool isWhite = (c.R > 150 && c.G > 150 && c.B > 150) || (c.R > 180);
                    matrix[x, y] = isWhite;
                    if (isWhite) whiteCount++;
                }
            }

            _templates.Add(new GlyphTemplate
            {
                Character = character,
                Width = w,
                Height = h,
                Matrix = matrix,
                WhitePixelCount = whiteCount,
                BlackPixelCount = w * h - whiteCount
            });
        }
        catch { }
    }

    public void Start()
    {
        if (_isRunning) return;
        _isRunning = true;
        _scanTimer.Start();
    }

    public void Stop()
    {
        _isRunning = false;
        _scanTimer.Stop();
    }

    private void ScanTimer_Tick(object? sender, EventArgs e)
    {
        if (!_isRunning || _isScanning || _templates.Count == 0) return;

        _isScanning = true;
        try
        {
            var res = CaptureAndRecognize();
            if (res != null)
            {
                PopUpdated?.Invoke(res);
            }
        }
        catch { }
        finally
        {
            _isScanning = false;
        }
    }

    public PopValues? CaptureAndRecognize()
    {
        IntPtr hwnd = FindAoeWindow();
        int originX = 0;
        int originY = 0;

        if (hwnd != IntPtr.Zero)
        {
            NativeMethods.POINT pt = new() { X = 0, Y = 0 };
            NativeMethods.ClientToScreen(hwnd, ref pt);
            originX = pt.X;
            originY = pt.Y;
        }

        int maxX = Math.Max(750, _cropSettings.PopBox.X + _cropSettings.PopBox.Width + 20);
        int maxY = Math.Max(60, _cropSettings.PopBox.Y + _cropSettings.PopBox.Height + 10);

        using Bitmap capture = new(maxX, maxY, PixelFormat.Format32bppArgb);
        using (Graphics g = Graphics.FromImage(capture))
        {
            g.CopyFromScreen(originX, originY, 0, 0, new Size(maxX, maxY));
        }

        return RecognizeFromBitmap(capture, _cropSettings);
    }

    public PopValues RecognizeFromBitmap(Bitmap bmp, PopCropSettings settings)
    {
        return RecognizeCrop(bmp, settings.PopBox, settings.BrightnessThreshold, settings.MaxSaturation);
    }

    public PopValues RecognizeCrop(Bitmap bmp, ResourceCropBox box, int brightnessThreshold = 175, int maxSaturation = 35)
    {
        if (_templates.Count == 0) return new PopValues();

        int cropX = Math.Clamp(box.X, 0, Math.Max(0, bmp.Width - 1));
        int cropY = Math.Clamp(box.Y, 0, Math.Max(0, bmp.Height - 1));
        int cropW = Math.Clamp(box.Width, 1, Math.Max(1, bmp.Width - cropX));
        int cropH = Math.Clamp(box.Height, 1, Math.Max(1, bmp.Height - cropY));

        if (cropW < 4 || cropH < 4) return new PopValues();

        // Nhị phân hóa với bộ lọc sắc độ / bão hòa màu chống nhiễu địa hình bản đồ (cỏ, nước, cát)
        bool[,] cropMatrix = BinarizeWithMapFilter(bmp, cropX, cropY, cropW, cropH, brightnessThreshold, maxSaturation);

        StringBuilder text = new();
        int curX = 0;

        while (curX <= cropW - 3)
        {
            // Kiểm tra cột curX có pixel trắng nào không
            bool colHasWhite = false;
            for (int y = 0; y < cropH; y++)
            {
                if (cropMatrix[curX, y])
                {
                    colHasWhite = true;
                    break;
                }
            }

            if (!colHasWhite)
            {
                curX++;
                continue;
            }

            // So khớp với tất cả 11 templates qua các độ lệch dọc dY
            GlyphTemplate? bestMatch = null;
            int bestScore = -999999;

            foreach (var t in _templates)
            {
                if (curX + t.Width > cropW) continue;

                int maxDy = Math.Max(0, cropH - t.Height);
                for (int dy = 0; dy <= maxDy; dy++)
                {
                    int matchWhite = 0;
                    int missingWhite = 0;
                    int extraWhite = 0;
                    int evalH = Math.Min(t.Height, cropH - dy);

                    for (int ty = 0; ty < evalH; ty++)
                    {
                        for (int tx = 0; tx < t.Width; tx++)
                        {
                            bool tVal = t.Matrix[tx, ty];
                            bool cVal = cropMatrix[curX + tx, dy + ty];

                            if (tVal)
                            {
                                if (cVal) matchWhite++;
                                else missingWhite++;
                            }
                            else
                            {
                                if (cVal) extraWhite++;
                            }
                        }
                    }

                    if (t.WhitePixelCount > 0)
                    {
                        double whiteRatio = (double)matchWhite / t.WhitePixelCount;
                        double extraRatio = (double)extraWhite / Math.Max(1, t.BlackPixelCount);

                        // Tiêu chuẩn khớp tin cậy cho POP:
                        // Trùng >= 78% pixel trắng của mẫu, và pixel lạ vào vùng đen <= 20%
                        if (whiteRatio >= 0.78 && extraRatio <= 0.20)
                        {
                            int score = matchWhite * 3 - missingWhite * 4 - extraWhite * 2;
                            if (score > bestScore)
                            {
                                bestScore = score;
                                bestMatch = t;
                            }
                        }
                    }
                }
            }

            if (bestMatch != null)
            {
                text.Append(bestMatch.Character);
                curX += bestMatch.Width; // Nhảy qua bề ngang ký tự vừa khớp
            }
            else
            {
                curX++;
            }
        }

        return ParsePopString(text.ToString());
    }

    public static bool[,] BinarizeWithMapFilter(Bitmap bmp, int cropX, int cropY, int cropW, int cropH, int brightnessThreshold = 175, int maxSaturation = 35)
    {
        bool[,] matrix = new bool[cropW, cropH];
        for (int y = 0; y < cropH; y++)
        {
            for (int x = 0; x < cropW; x++)
            {
                Color c = bmp.GetPixel(cropX + x, cropY + y);
                int brightness = (int)(c.R * 0.299 + c.G * 0.587 + c.B * 0.114);
                int maxChannel = Math.Max(c.R, Math.Max(c.G, c.B));
                int minChannel = Math.Min(c.R, Math.Min(c.G, c.B));
                int diff = maxChannel - minChannel;

                // Chỉ giữ pixel nếu vừa sáng vừa KHÔNG có màu mạnh (loại bỏ màu xanh cỏ, xanh nước, vàng đất)
                matrix[x, y] = (brightness >= brightnessThreshold) && (diff <= maxSaturation);
            }
        }
        return matrix;
    }

    public static Bitmap CreateFilteredPreviewBitmap(Bitmap bmp, int cropX, int cropY, int cropW, int cropH, int brightnessThreshold = 175, int maxSaturation = 35)
    {
        bool[,] matrix = BinarizeWithMapFilter(bmp, cropX, cropY, cropW, cropH, brightnessThreshold, maxSaturation);
        Bitmap dest = new(cropW, cropH);
        for (int y = 0; y < cropH; y++)
        {
            for (int x = 0; x < cropW; x++)
            {
                dest.SetPixel(x, y, matrix[x, y] ? Color.White : Color.Black);
            }
        }
        return dest;
    }

    public static PopValues ParsePopString(string raw)
    {
        var res = new PopValues();
        if (string.IsNullOrWhiteSpace(raw)) return res;

        int slashIdx = raw.IndexOf('/');
        if (slashIdx >= 0)
        {
            string part1 = raw.Substring(0, slashIdx).Trim();
            string part2 = raw.Substring(slashIdx + 1).Trim();

            if (int.TryParse(part1, out int c)) res.CurrentPop = c;
            if (int.TryParse(part2, out int m)) res.MaxPop = m;
        }
        else
        {
            if (int.TryParse(raw.Trim(), out int c)) res.CurrentPop = c;
        }

        return res;
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

    public void Dispose()
    {
        Stop();
        _scanTimer.Dispose();
        GC.SuppressFinalize(this);
    }
}
