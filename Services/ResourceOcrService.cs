using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Text;
using AOEKeyboardMacroPro.Models;

namespace AOEKeyboardMacroPro.Services;

public class ResourceValues
{
    public int? Wood { get; set; }
    public int? Food { get; set; }
    public int? Gold { get; set; }
    public int? Stone { get; set; }

    public bool IsEmpty => !Wood.HasValue && !Food.HasValue && !Gold.HasValue && !Stone.HasValue;

    public override string ToString()
    {
        return $"Gỗ: {Format(Wood)} | Thịt: {Format(Food)} | Vàng: {Format(Gold)} | Đá: {Format(Stone)}";
    }

    public static string Format(int? val) => val.HasValue ? val.Value.ToString("N0") : "--";
}

public class ResourceOcrService : IDisposable
{
    public static string TestSampleRecognition()
    {
        using var ocr = new ResourceOcrService();
        ocr.LoadTemplates();
        string samplePath = "sample_aoe.jpg";
        if (!File.Exists(samplePath))
        {
            samplePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "sample_aoe.jpg");
        }
        if (File.Exists(samplePath))
        {
            using var bmp = new Bitmap(samplePath);
            var settings = ConfigService.LoadSettings().ResourceCrop ?? new ResourceCropSettings();
            var res = ocr.RecognizeFromBitmap(bmp, settings);
            return res.ToString();
        }
        return "sample_aoe.jpg not found";
    }
    private class DigitTemplate
    {
        public int Digit { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public bool[,] Matrix { get; set; } = null!;
        public int WhitePixelCount { get; set; }
        public int BlackPixelCount { get; set; }
    }

    private readonly List<DigitTemplate> _templates = new();
    private readonly System.Windows.Forms.Timer _scanTimer = new();
    private ResourceCropSettings _cropSettings;
    private bool _isRunning = false;
    private bool _isScanning = false;

    public event Action<ResourceValues>? ResourcesUpdated;

    public bool IsRunning => _isRunning;

    public ResourceOcrService(ResourceCropSettings? settings = null)
    {
        _cropSettings = settings ?? ConfigService.LoadSettings().ResourceCrop ?? new ResourceCropSettings();
        LoadTemplates();

        _scanTimer.Interval = 100; // 100ms (0.1 giây)
        _scanTimer.Tick += ScanTimer_Tick;
    }

    public void UpdateSettings(ResourceCropSettings settings)
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
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Templates", "Digits"),
            Path.Combine(Directory.GetCurrentDirectory(), "Templates", "Digits")
        };

        string? validDir = searchDirs.FirstOrDefault(Directory.Exists);
        if (validDir == null) return;

        for (int d = 0; d <= 9; d++)
        {
            string path = Path.Combine(validDir, $"{d}.png");
            if (File.Exists(path))
            {
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

                    _templates.Add(new DigitTemplate
                    {
                        Digit = d,
                        Width = w,
                        Height = h,
                        Matrix = matrix,
                        WhitePixelCount = whiteCount,
                        BlackPixelCount = w * h - whiteCount
                    });
                }
                catch { }
            }
        }
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
                ResourcesUpdated?.Invoke(res);
            }
        }
        catch { }
        finally
        {
            _isScanning = false;
        }
    }

    public ResourceValues? CaptureAndRecognize()
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

        // Tính toán kích thước vùng chụp cần thiết
        int maxX = Math.Max(300, Math.Max(_cropSettings.Wood.X + _cropSettings.Wood.Width,
            Math.Max(_cropSettings.Food.X + _cropSettings.Food.Width,
            Math.Max(_cropSettings.Gold.X + _cropSettings.Gold.Width, _cropSettings.Stone.X + _cropSettings.Stone.Width)))) + 10;

        int maxY = Math.Max(25, Math.Max(_cropSettings.Wood.Y + _cropSettings.Wood.Height,
            Math.Max(_cropSettings.Food.Y + _cropSettings.Food.Height,
            Math.Max(_cropSettings.Gold.Y + _cropSettings.Gold.Height, _cropSettings.Stone.Y + _cropSettings.Stone.Height)))) + 5;

        using Bitmap capture = new(maxX, maxY, PixelFormat.Format32bppArgb);
        using (Graphics g = Graphics.FromImage(capture))
        {
            g.CopyFromScreen(originX, originY, 0, 0, new Size(maxX, maxY));
        }

        return RecognizeFromBitmap(capture, _cropSettings);
    }

    public ResourceValues RecognizeFromBitmap(Bitmap bmp, ResourceCropSettings settings)
    {
        return new ResourceValues
        {
            Wood = RecognizeCrop(bmp, settings.Wood),
            Food = RecognizeCrop(bmp, settings.Food),
            Gold = RecognizeCrop(bmp, settings.Gold),
            Stone = RecognizeCrop(bmp, settings.Stone)
        };
    }

    public int? RecognizeCrop(Bitmap bmp, ResourceCropBox box)
    {
        if (_templates.Count == 0) return null;

        int cropX = Math.Clamp(box.X, 0, Math.Max(0, bmp.Width - 1));
        int cropY = Math.Clamp(box.Y, 0, Math.Max(0, bmp.Height - 1));
        int cropW = Math.Clamp(box.Width, 1, Math.Max(1, bmp.Width - cropX));
        int cropH = Math.Clamp(box.Height, 1, Math.Max(1, bmp.Height - cropY));

        if (cropW < 4 || cropH < 4) return null;

        // Binarize crop into bool array using consistent threshold 175
        bool[,] cropMatrix = new bool[cropW, cropH];
        for (int y = 0; y < cropH; y++)
        {
            for (int x = 0; x < cropW; x++)
            {
                Color c = bmp.GetPixel(cropX + x, cropY + y);
                int brightness = (int)(c.R * 0.299 + c.G * 0.587 + c.B * 0.114);
                cropMatrix[x, y] = (brightness >= 175) || (c.R > 175 && c.G > 175 && c.B > 175);
            }
        }

        StringBuilder digits = new();
        int curX = 0;

        while (curX <= cropW - 4)
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

            // So khớp với tất cả templates across all possible vertical shifts dY
            DigitTemplate? bestMatch = null;
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

                        // Tiêu chuẩn so khớp tin cậy cao:
                        // 1. Khớp ít nhất 80% số pixel trắng của mẫu chữ số
                        // 2. Tỷ lệ pixel trắng lạc vào vùng đen của mẫu không quá 18%
                        if (whiteRatio >= 0.80 && extraRatio <= 0.18)
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
                digits.Append(bestMatch.Digit);
                curX += bestMatch.Width; // Nhảy qua bề ngang chữ số vừa nhận diện thành công
            }
            else
            {
                curX++;
            }
        }

        if (digits.Length == 0) return null;
        if (int.TryParse(digits.ToString(), out int val))
        {
            return val;
        }
        return null;
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
