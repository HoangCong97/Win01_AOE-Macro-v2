using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Text;
using System.Text.Json;
using AOEKeyboardMacroPro.Models;

namespace AOEKeyboardMacroPro.Services;

public class GlyphTemplate
{
    public char Character { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    public List<Point> WhitePixels { get; set; } = new();
    public int TotalWhite => WhitePixels.Count;
}

public class PopCandidateMatch
{
    public int X { get; set; }
    public int Y { get; set; }
    public char Character { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    public int MatchedWhite { get; set; }
    public int TotalWhite { get; set; }
    public int ExtraWhite { get; set; }
    public double Score { get; set; }
}

public class PopOcrService : IDisposable
{
    private readonly List<GlyphTemplate> _templates = new();
    private CancellationTokenSource? _scanCts;
    private Task? _scanTask;
    private int _intervalMs = 120;
    private PopCropSettings _cropSettings;
    private PopValues? _lastRecognizedValues;
    private DateTime _lastSuccessfulScanTime = DateTime.MinValue;
    private bool _isScanningActive = false;
    private volatile bool _isRunning = false;
    private volatile bool _isEnabled = true;

    public event Action<PopValues>? PopUpdated;

    public bool IsRunning => _isRunning;
    public bool IsEnabled => _isEnabled;

    public void SetEnabled(bool enabled)
    {
        _isEnabled = enabled;
        if (!enabled)
        {
            _isScanningActive = false;
            _lastRecognizedValues = null;
        }
    }

    public void Reset()
    {
        _lastRecognizedValues = null;
        _lastSuccessfulScanTime = DateTime.MinValue;
        _isScanningActive = false;
    }

    public PopOcrService(PopCropSettings? settings = null)
    {
        _cropSettings = settings ?? ConfigService.LoadSettings().PopCrop ?? new PopCropSettings();
        LoadTemplates();
    }

    public void UpdateSettings(PopCropSettings settings)
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
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Templates", "PopAoe", "crops"),
            Path.Combine(Directory.GetCurrentDirectory(), "Templates", "PopAoe", "crops"),
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Templates", "PopAoe"),
            Path.Combine(Directory.GetCurrentDirectory(), "Templates", "PopAoe"),
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Templates", "Pop"),
            Path.Combine(Directory.GetCurrentDirectory(), "Templates", "Pop")
        };

        string? validDir = searchDirs.FirstOrDefault(Directory.Exists);
        if (validDir == null) return;

        // Cố gắng đọc tọa độ mặc định từ data.json nếu có
        string dataJsonPath = Path.Combine(Path.GetDirectoryName(validDir) ?? "", "data.json");
        if (!File.Exists(dataJsonPath))
        {
            dataJsonPath = Path.Combine(validDir, "data.json");
        }
        if (File.Exists(dataJsonPath))
        {
            try
            {
                string jsonText = File.ReadAllText(dataJsonPath);
                using var doc = JsonDocument.Parse(jsonText);
                if (doc.RootElement.TryGetProperty("areas", out var areasElem) && areasElem.GetArrayLength() > 0)
                {
                    var firstArea = areasElem[0];
                    if (firstArea.TryGetProperty("x", out var xProp) &&
                        firstArea.TryGetProperty("y", out var yProp) &&
                        firstArea.TryGetProperty("width", out var wProp) &&
                        firstArea.TryGetProperty("height", out var hProp))
                    {
                        // Chỉ cập nhật nếu cài đặt hiện tại chưa tùy biến
                        if (_cropSettings.PopBox.X == 650 && _cropSettings.PopBox.Y == 27)
                        {
                            _cropSettings.PopBox = new ResourceCropBox(xProp.GetInt32(), yProp.GetInt32(), wProp.GetInt32(), hProp.GetInt32());
                        }
                    }
                }
            }
            catch { }
        }

        // Tải các file template PNG trong thư mục
        var files = Directory.GetFiles(validDir, "*.png");
        foreach (var file in files)
        {
            string fname = Path.GetFileName(file);
            char c = ' ';

            if (fname.Contains("Slash", StringComparison.OrdinalIgnoreCase) || fname.Contains("div", StringComparison.OrdinalIgnoreCase))
            {
                c = '/';
            }
            else
            {
                var parts = fname.Split('_');
                if (parts.Length >= 3)
                {
                    string charStr = Path.GetFileNameWithoutExtension(parts[2]);
                    if (charStr.Length > 0) c = charStr[0];
                }
                else
                {
                    string withoutExt = Path.GetFileNameWithoutExtension(fname);
                    if (withoutExt.Length == 1 && char.IsDigit(withoutExt[0]))
                    {
                        c = withoutExt[0];
                    }
                }
            }

            if (c != ' ')
            {
                LoadSingleTemplate(file, c);
            }
        }
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
            List<Point> whitePts = new();

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    Color c = bmp.GetPixel(x, y);
                    // Mẫu thuần nhị phân: điểm trắng (255, 255, 255)
                    if (c.R == 255 && c.G == 255 && c.B == 255)
                    {
                        whitePts.Add(new Point(x, y));
                    }
                }
            }

            // Tránh nạp trùng nếu đã nạp ký tự này
            _templates.RemoveAll(t => t.Character == character);
            _templates.Add(new GlyphTemplate
            {
                Character = character,
                Width = w,
                Height = h,
                WhitePixels = whitePts
            });
        }
        catch { }
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
                if (!_isRunning || !_isEnabled || _templates.Count == 0)
                {
                    await Task.Delay(250, ct);
                    continue;
                }

                try
                {
                    var res = CaptureAndRecognize();
                    if (res != null && res.IsValid)
                    {
                        _lastSuccessfulScanTime = DateTime.UtcNow;
                        _isScanningActive = true;
                        if (_lastRecognizedValues == null || !res.EqualsValues(_lastRecognizedValues))
                        {
                            _lastRecognizedValues = res;
                            PopUpdated?.Invoke(res);
                        }
                    }
                    else
                    {
                        // Không quét được (thoát game, thay tab ra ngoài, hoặc trong menu)
                        if (_isScanningActive && (DateTime.UtcNow - _lastSuccessfulScanTime).TotalMilliseconds >= 1000)
                        {
                            // Trong 1s nếu không quét được -> ngừng thu thập các thông số
                            _isScanningActive = false;
                            _lastRecognizedValues = null;
                            // Không phát new PopValues() rỗng để giữ lại giá trị cuối cùng
                        }

                        if (!_isScanningActive)
                        {
                            // Khi đang tạm dừng thu thập, ngủ thêm để tiết kiệm CPU
                            await Task.Delay(200, ct);
                        }
                    }
                }
                catch { }
            }
        }
        catch (OperationCanceledException) { }
    }

    public PopValues? CaptureAndRecognize()
    {
        IntPtr hwnd = FindAoeWindow();
        if (hwnd == IntPtr.Zero)
        {
            return null;
        }

        NativeMethods.POINT pt = new() { X = 0, Y = 0 };
        NativeMethods.ClientToScreen(hwnd, ref pt);
        int originX = pt.X;
        int originY = pt.Y;

        int boxX = _cropSettings.PopBox.X;
        int boxY = _cropSettings.PopBox.Y;
        int boxW = _cropSettings.PopBox.Width;
        int boxH = _cropSettings.PopBox.Height;

        int screenCropX = originX + boxX;
        int screenCropY = originY + boxY;

        using Bitmap capture = new(boxW, boxH, PixelFormat.Format32bppArgb);
        using (Graphics g = Graphics.FromImage(capture))
        {
            g.CopyFromScreen(screenCropX, screenCropY, 0, 0, new Size(boxW, boxH));
        }

        return RecognizeFromBitmap(capture, 0, 0, boxW, boxH);
    }

    /// <summary>
    /// Nhận diện POP từ Bitmap nguồn với nguyên tắc:
    /// Đưa hình ảnh về dạng đen trắng (Threshold = 255), sau đó chỉ so khớp điểm ảnh trắng.
    /// </summary>
    public PopValues RecognizeFromBitmap(Bitmap bmp, int cropX, int cropY, int cropW, int cropH)
    {
        if (_templates.Count == 0) return new PopValues();

        cropX = Math.Clamp(cropX, 0, Math.Max(0, bmp.Width - 1));
        cropY = Math.Clamp(cropY, 0, Math.Max(0, bmp.Height - 1));
        cropW = Math.Clamp(cropW, 1, Math.Max(1, bmp.Width - cropX));
        cropH = Math.Clamp(cropH, 1, Math.Max(1, bmp.Height - cropY));

        if (cropW < 4 || cropH < 4) return new PopValues();

        // 1. Nhị phân hóa với ngưỡng threshold = 255 (chỉ màu trắng tinh RGB 255,255,255)
        bool[,] bin = BinarizeThreshold255(bmp, cropX, cropY, cropW, cropH);

        // 2. Tìm tất cả các vị trí ứng viên có 100% điểm ảnh trắng khớp với template
        List<PopCandidateMatch> candidates = new();

        for (int x = 0; x <= cropW - 4; x++)
        {
            for (int y = 0; y <= cropH - 10; y++)
            {
                foreach (var t in _templates)
                {
                    if (x + t.Width > cropW || y + t.Height > cropH) continue;

                    int matchedWhite = 0;
                    foreach (var pt in t.WhitePixels)
                    {
                        if (bin[x + pt.X, y + pt.Y]) matchedWhite++;
                    }

                    // Chỉ so khớp điểm ảnh trắng (100% điểm trắng của mẫu phải có trên ảnh)
                    if (matchedWhite == t.TotalWhite)
                    {
                        // Đếm số điểm ảnh trắng thực tế trong vùng khung chữ nhật
                        int imgWhite = 0;
                        for (int ty = 0; ty < t.Height; ty++)
                        {
                            for (int tx = 0; tx < t.Width; tx++)
                            {
                                if (bin[x + tx, y + ty]) imgWhite++;
                            }
                        }

                        int extraWhite = imgWhite - matchedWhite;
                        // Điểm số: thưởng điểm khớp trắng, trừ điểm pixel trắng thừa trong bounding box
                        double score = matchedWhite * 10 - extraWhite * 15;

                        candidates.Add(new PopCandidateMatch
                        {
                            X = x,
                            Y = y,
                            Character = t.Character,
                            Width = t.Width,
                            Height = t.Height,
                            MatchedWhite = matchedWhite,
                            TotalWhite = t.TotalWhite,
                            ExtraWhite = extraWhite,
                            Score = score
                        });
                    }
                }
            }
        }

        if (candidates.Count == 0) return new PopValues();

        // 3. Chọn tham lam các ký tự không đè nhau theo thứ tự điểm số cao nhất
        candidates.Sort((a, b) => b.Score.CompareTo(a.Score));
        List<PopCandidateMatch> chosen = new();

        foreach (var c in candidates)
        {
            bool overlap = false;
            foreach (var existing in chosen)
            {
                // Kiểm tra xung đột ngang (Horizontal overlap)
                if (!(c.X + c.Width <= existing.X || c.X >= existing.X + existing.Width))
                {
                    overlap = true;
                    break;
                }
            }

            if (!overlap)
            {
                chosen.Add(c);
            }
        }

        // 4. Sắp xếp các ký tự đã chọn từ trái sang phải
        chosen.Sort((a, b) => a.X.CompareTo(b.X));

        StringBuilder sb = new();
        foreach (var item in chosen)
        {
            sb.Append(item.Character);
        }

        return ParsePopString(sb.ToString());
    }

    public static bool[,] BinarizeThreshold255(Bitmap bmp, int cropX, int cropY, int cropW, int cropH)
    {
        bool[,] bin = new bool[cropW, cropH];
        for (int y = 0; y < cropH; y++)
        {
            for (int x = 0; x < cropW; x++)
            {
                Color px = bmp.GetPixel(cropX + x, cropY + y);
                bin[x, y] = (px.R == 255 && px.G == 255 && px.B == 255);
            }
        }
        return bin;
    }

    public static Bitmap CreateBinarizedBitmap(Bitmap bmp, int cropX, int cropY, int cropW, int cropH)
    {
        bool[,] bin = BinarizeThreshold255(bmp, cropX, cropY, cropW, cropH);
        Bitmap dest = new(cropW, cropH);
        for (int y = 0; y < cropH; y++)
        {
            for (int x = 0; x < cropW; x++)
            {
                dest.SetPixel(x, y, bin[x, y] ? Color.White : Color.Black);
            }
        }
        return dest;
    }

    public static PopValues ParsePopString(string raw)
    {
        var res = new PopValues();
        if (string.IsNullOrWhiteSpace(raw)) return res;

        int slashIdx = raw.IndexOf('/');
        if (slashIdx > 0)
        {
            string curStr = raw[..slashIdx].Trim();
            string maxStr = raw[(slashIdx + 1)..].Trim();

            if (int.TryParse(curStr, out int curVal))
            {
                res.CurrentPop = curVal;
            }
            if (int.TryParse(maxStr, out int maxVal))
            {
                res.MaxPop = maxVal;
            }
        }
        else
        {
            if (int.TryParse(raw.Trim(), out int curVal))
            {
                res.CurrentPop = curVal;
            }
        }

        return res;
    }

    private static IntPtr FindAoeWindow()
    {
        IntPtr fgHwnd = NativeMethods.GetForegroundWindow();
        if (fgHwnd == IntPtr.Zero || NativeMethods.IsIconic(fgHwnd))
        {
            return IntPtr.Zero;
        }

        try
        {
            NativeMethods.GetWindowThreadProcessId(fgHwnd, out uint pid);
            if (pid == Environment.ProcessId)
            {
                return IntPtr.Zero;
            }
        }
        catch { }

        return fgHwnd;
    }

    public void Dispose()
    {
        Stop();
        GC.SuppressFinalize(this);
    }
}
