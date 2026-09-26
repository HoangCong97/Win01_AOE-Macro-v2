using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Text;
using System.Text.Json;
using AOEKeyboardMacroPro.Models;

namespace AOEKeyboardMacroPro.Services;

public class TimerOcrService : IDisposable
{
    private readonly List<GlyphTemplate> _templates = new();
    private CancellationTokenSource? _scanCts;
    private Task? _scanTask;
    private int _intervalMs = 200; // Thời gian trong game nhảy mỗi 1s, quét 200ms là đủ nhanh và tối ưu CPU
    private TimerCropSettings _cropSettings;
    private TimerValues? _lastRecognizedValues;
    private DateTime _lastSuccessfulScanTime = DateTime.MinValue;
    private bool _isScanningActive = false;
    private volatile bool _isRunning = false;
    private volatile bool _isEnabled = false;

    public event Action<TimerValues>? TimerUpdated;

    public bool IsRunning => _isRunning;
    public bool IsEnabled => _isEnabled;

    public void SetEnabled(bool enabled)
    {
        _isEnabled = enabled;
        if (!enabled)
        {
            _isScanningActive = false;
            _lastRecognizedValues = null;
            try
            {
                TimerUpdated?.Invoke(new TimerValues());
            }
            catch { }
        }
    }

    public TimerOcrService(TimerCropSettings? settings = null)
    {
        _cropSettings = settings ?? ConfigService.LoadSettings().TimerCrop ?? new TimerCropSettings();
        LoadTemplates();
    }

    public void UpdateSettings(TimerCropSettings settings)
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

        // 1. Tải template dấu hai chấm ":" từ TimerAoe
        string[] timerDirs = new[]
        {
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Templates", "TimerAoe", "crops"),
            Path.Combine(Directory.GetCurrentDirectory(), "Templates", "TimerAoe", "crops"),
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Templates", "TimerAoe"),
            Path.Combine(Directory.GetCurrentDirectory(), "Templates", "TimerAoe")
        };

        string? validTimerDir = timerDirs.FirstOrDefault(Directory.Exists);
        if (validTimerDir != null)
        {
            // Cố gắng đọc tọa độ mặc định từ data.json nếu có
            string dataJsonPath = Path.Combine(Path.GetDirectoryName(validTimerDir) ?? "", "data.json");
            if (!File.Exists(dataJsonPath))
            {
                dataJsonPath = Path.Combine(validTimerDir, "data.json");
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
                            if (_cropSettings.TimerBox.X == 4 && _cropSettings.TimerBox.Y == 27)
                            {
                                _cropSettings.TimerBox = new ResourceCropBox(xProp.GetInt32(), yProp.GetInt32(), wProp.GetInt32(), hProp.GetInt32());
                            }
                        }
                    }
                }
                catch { }
            }

            var timerFiles = Directory.GetFiles(validTimerDir, "*.png");
            foreach (var file in timerFiles)
            {
                string fname = Path.GetFileName(file);
                if (fname.Contains("Phân tách", StringComparison.OrdinalIgnoreCase) ||
                    fname.Contains("colon", StringComparison.OrdinalIgnoreCase) ||
                    fname.Contains("sep", StringComparison.OrdinalIgnoreCase))
                {
                    LoadSingleTemplate(file, ':');
                }
                else
                {
                    var parts = fname.Split('_');
                    if (parts.Length >= 3)
                    {
                        string charStr = Path.GetFileNameWithoutExtension(parts[2]);
                        if (charStr.Length > 0 && char.IsDigit(charStr[0])) LoadSingleTemplate(file, charStr[0]);
                    }
                }
            }
        }

        // 2. Tải các chữ số 0..9 từ PopAoe (dùng chung font số trên thanh trạng thái game AOE)
        string[] popDirs = new[]
        {
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Templates", "PopAoe", "crops"),
            Path.Combine(Directory.GetCurrentDirectory(), "Templates", "PopAoe", "crops"),
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Templates", "PopAoe"),
            Path.Combine(Directory.GetCurrentDirectory(), "Templates", "PopAoe")
        };

        string? validPopDir = popDirs.FirstOrDefault(Directory.Exists);
        if (validPopDir != null)
        {
            var popFiles = Directory.GetFiles(validPopDir, "*.png");
            foreach (var file in popFiles)
            {
                string fname = Path.GetFileName(file);
                char c = ' ';
                var parts = fname.Split('_');
                if (parts.Length >= 3)
                {
                    string charStr = Path.GetFileNameWithoutExtension(parts[2]);
                    if (charStr.Length > 0 && char.IsDigit(charStr[0])) c = charStr[0];
                }
                else
                {
                    string withoutExt = Path.GetFileNameWithoutExtension(fname);
                    if (withoutExt.Length == 1 && char.IsDigit(withoutExt[0]))
                    {
                        c = withoutExt[0];
                    }
                }

                if (c != ' ')
                {
                    LoadSingleTemplate(file, c);
                }
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
                    if (c.R == 255 && c.G == 255 && c.B == 255)
                    {
                        whitePts.Add(new Point(x, y));
                    }
                }
            }

            if (whitePts.Count == 0) return;

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
                            TimerUpdated?.Invoke(res);
                        }
                    }
                    else
                    {
                        // Không quét được (thoát game, thay tab ra ngoài, hoặc trong menu)
                        if (_isScanningActive && (DateTime.UtcNow - _lastSuccessfulScanTime).TotalSeconds >= 3)
                        {
                            _isScanningActive = false;
                            _lastRecognizedValues = null;
                            TimerUpdated?.Invoke(new TimerValues());
                        }

                        if (!_isScanningActive)
                        {
                            await Task.Delay(400, ct);
                        }
                    }
                }
                catch { }
            }
        }
        catch (OperationCanceledException) { }
    }

    public TimerValues? CaptureAndRecognize()
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

        int boxX = _cropSettings.TimerBox.X;
        int boxY = _cropSettings.TimerBox.Y;
        int boxW = _cropSettings.TimerBox.Width;
        int boxH = _cropSettings.TimerBox.Height;

        int screenCropX = originX + boxX;
        int screenCropY = originY + boxY;

        using Bitmap capture = new(boxW, boxH, PixelFormat.Format32bppArgb);
        using (Graphics g = Graphics.FromImage(capture))
        {
            g.CopyFromScreen(screenCropX, screenCropY, 0, 0, new Size(boxW, boxH));
        }

        return RecognizeFromBitmap(capture, 0, 0, boxW, boxH);
    }

    public TimerValues RecognizeFromBitmap(Bitmap bmp, int cropX, int cropY, int cropW, int cropH)
    {
        if (_templates.Count == 0) return new TimerValues();

        cropX = Math.Clamp(cropX, 0, Math.Max(0, bmp.Width - 1));
        cropY = Math.Clamp(cropY, 0, Math.Max(0, bmp.Height - 1));
        cropW = Math.Clamp(cropW, 1, Math.Max(1, bmp.Width - cropX));
        cropH = Math.Clamp(cropH, 1, Math.Max(1, bmp.Height - cropY));

        if (cropW < 4 || cropH < 4) return new TimerValues();

        bool[,] bin = PopOcrService.BinarizeThreshold255(bmp, cropX, cropY, cropW, cropH);

        List<PopCandidateMatch> candidates = new();

        for (int x = 0; x <= cropW - 2; x++)
        {
            for (int y = 0; y <= cropH - 7; y++)
            {
                foreach (var t in _templates)
                {
                    if (x + t.Width > cropW || y + t.Height > cropH) continue;

                    int matchedWhite = 0;
                    foreach (var pt in t.WhitePixels)
                    {
                        if (bin[x + pt.X, y + pt.Y]) matchedWhite++;
                    }

                    if (matchedWhite == t.TotalWhite)
                    {
                        int imgWhite = 0;
                        for (int ty = 0; ty < t.Height; ty++)
                        {
                            for (int tx = 0; tx < t.Width; tx++)
                            {
                                if (bin[x + tx, y + ty]) imgWhite++;
                            }
                        }

                        int extraWhite = imgWhite - matchedWhite;
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

        if (candidates.Count == 0) return new TimerValues();

        candidates.Sort((a, b) => b.Score.CompareTo(a.Score));
        List<PopCandidateMatch> chosen = new();

        foreach (var c in candidates)
        {
            bool overlap = false;
            foreach (var existing in chosen)
            {
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

        chosen.Sort((a, b) => a.X.CompareTo(b.X));

        StringBuilder sb = new();
        foreach (var item in chosen)
        {
            sb.Append(item.Character);
        }

        return ParseTimerString(sb.ToString());
    }

    public static TimerValues ParseTimerString(string raw)
    {
        var res = new TimerValues();
        if (string.IsNullOrWhiteSpace(raw)) return res;

        raw = raw.Trim();
        if (raw.Contains(':') && raw.All(c => char.IsDigit(c) || c == ':'))
        {
            var parts = raw.Split(':');
            if (parts.Length >= 2 && parts.Length <= 3 && parts.All(p => p.Length >= 1 && p.Length <= 2))
            {
                res.RawText = raw;
            }
        }

        return res;
    }

    private static IntPtr FindAoeWindow()
    {
        IntPtr fgHwnd = NativeMethods.GetForegroundWindow();
        if (fgHwnd != IntPtr.Zero && !NativeMethods.IsIconic(fgHwnd) && IsAoeWindow(fgHwnd))
        {
            return fgHwnd;
        }

        return IntPtr.Zero;
    }

    private static bool IsAoeWindow(IntPtr hwnd)
    {
        StringBuilder sb = new(256);
        NativeMethods.GetWindowText(hwnd, sb, sb.Capacity);
        if (IsAoeTitle(sb.ToString())) return true;

        try
        {
            NativeMethods.GetWindowThreadProcessId(hwnd, out uint pid);
            if (pid != 0)
            {
                using var proc = Process.GetProcessById((int)pid);
                string name = proc.ProcessName.ToLowerInvariant();
                return name.Contains("empire") || name.Contains("aoe") || name.Contains("age");
            }
        }
        catch { }

        return false;
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
