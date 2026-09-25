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

    public bool EqualsValues(ResourceValues? other)
    {
        if (other is null) return false;
        return Wood == other.Wood && Food == other.Food && Gold == other.Gold && Stone == other.Stone;
    }

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
    private CancellationTokenSource? _scanCts;
    private Task? _scanTask;
    private int _intervalMs = 120;
    private ResourceCropSettings _cropSettings;
    private ResourceValues? _lastRecognizedValues;
    private volatile bool _isRunning = false;

    public event Action<ResourceValues>? ResourcesUpdated;

    public bool IsRunning => _isRunning;

    public ResourceOcrService(ResourceCropSettings? settings = null)
    {
        _cropSettings = settings ?? ConfigService.LoadSettings().ResourceCrop ?? new ResourceCropSettings();
        LoadTemplates();
    }

    public void UpdateSettings(ResourceCropSettings settings)
    {
        _cropSettings = settings;
    }

    public void SetScanInterval(int intervalMs)
    {
        _intervalMs = Math.Clamp(intervalMs, 20, 5000);
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
        _scanCts = new CancellationTokenSource();
        _scanTask = Task.Run(() => ScanLoopAsync(_scanCts.Token));
    }

    public void Stop()
    {
        _isRunning = false;
        _scanCts?.Cancel();
        try { _scanTask?.Wait(300); } catch { }
        _scanCts?.Dispose();
        _scanCts = null;
        _scanTask = null;
    }

    private async Task ScanLoopAsync(CancellationToken ct)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(_intervalMs));
        try
        {
            while (!ct.IsCancellationRequested && await timer.WaitForNextTickAsync(ct))
            {
                if (!_isRunning || _templates.Count == 0) continue;

                try
                {
                    var res = CaptureAndRecognize();
                    if (res != null && !res.IsEmpty)
                    {
                        if (_lastRecognizedValues == null || !res.EqualsValues(_lastRecognizedValues))
                        {
                            _lastRecognizedValues = res;
                            ResourcesUpdated?.Invoke(res);
                        }
                    }
                }
                catch { }
            }
        }
        catch (OperationCanceledException) { }
    }

    public ResourceValues? CaptureAndRecognize()
    {
        IntPtr hwnd = FindAoeWindow();
        if (hwnd == IntPtr.Zero)
        {
            // Không quét màn hình Desktop khi game chưa mở, tránh nghẽn GDI / GPU
            return null;
        }

        NativeMethods.POINT pt = new() { X = 0, Y = 0 };
        NativeMethods.ClientToScreen(hwnd, ref pt);
        int originX = pt.X;
        int originY = pt.Y;

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

        Color[,] pixels = new Color[cropW, cropH];
        for (int y = 0; y < cropH; y++)
        {
            for (int x = 0; x < cropW; x++)
            {
                pixels[x, y] = bmp.GetPixel(cropX + x, cropY + y);
            }
        }

        // 1. So khớp với pixel game gốc (chữ sáng màu trên nền tối)
        int? result = MatchDigits(pixels, cropW, cropH, invert: false);
        if (result.HasValue) return result;

        // 2. Nếu như so khớp mà không ra kết quả, đảo ngược pixel game để so sánh (đối với trường hợp số tài nguyên hiển thị màu đen)
        return MatchDigits(pixels, cropW, cropH, invert: true);
    }

    private int? MatchDigits(Color[,] pixels, int cropW, int cropH, bool invert)
    {
        // Binarize crop into bool array using consistent threshold 175
        bool[,] cropMatrix = new bool[cropW, cropH];
        for (int y = 0; y < cropH; y++)
        {
            for (int x = 0; x < cropW; x++)
            {
                Color c = pixels[x, y];
                int r = invert ? (255 - c.R) : c.R;
                int g = invert ? (255 - c.G) : c.G;
                int b = invert ? (255 - c.B) : c.B;
                int brightness = (int)(r * 0.299 + g * 0.587 + b * 0.114);
                cropMatrix[x, y] = (brightness >= 175) || (r > 175 && g > 175 && b > 175);
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
        GC.SuppressFinalize(this);
    }
}
