using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using AOEKeyboardMacroPro.Models;

namespace AOEKeyboardMacroPro.Services;

/// <summary>
/// Mẫu chữ số loading lưu các điểm ảnh trắng (Threshold = 255).
/// </summary>
public class LoadingGlyphTemplate
{
    public char Character { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    public List<Point> WhitePixels { get; set; } = new();
    public int TotalWhite => WhitePixels.Count;
}

public class LoadingCandidateMatch
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

/// <summary>
/// Dịch vụ OCR nhận diện tỉ lệ Loading (0 - 100%) của game AOE dựa trên template điểm trắng trong:
/// Templates/AOE loading (crops: 0..9, data.json: Area 1).
/// </summary>
public class LoadingOcrService : IDisposable
{
    private readonly List<LoadingGlyphTemplate> _templates = new();
    private CancellationTokenSource? _scanCts;
    private Task? _scanTask;
    private int _intervalMs = 120; // Quét mỗi 120ms
    private LoadingCropSettings _cropSettings;
    private LoadingValues? _lastRecognizedValues;
    private DateTime _lastSuccessfulScanTime = DateTime.MinValue;
    private bool _isScanningActive = false;
    private volatile bool _isRunning = false;
    private volatile bool _isEnabled = true;
    private readonly object _lock = new();

    /// <summary>
    /// Bắn ra khi tỉ lệ Loading thay đổi hoặc được nhận diện mới.
    /// </summary>
    public event Action<LoadingValues>? LoadingProgressUpdated;

    /// <summary>
    /// Bắn ra khi tiến trình Loading chạm mốc 100% (hoàn thành).
    /// </summary>
    public event Action<LoadingValues>? LoadingCompleted;

    public bool IsRunning => _isRunning;
    public bool IsEnabled => _isEnabled;
    public bool IsLoadingActive => _isScanningActive && (DateTime.UtcNow - _lastSuccessfulScanTime).TotalMilliseconds <= 1500;
    public LoadingValues? CurrentLoading => _lastRecognizedValues;
    public int? CurrentPercentage => _lastRecognizedValues?.Percentage;
    public LoadingCropSettings CropSettings => _cropSettings;
    public int LoadedTemplateCount => _templates.Count;

    public LoadingOcrService(LoadingCropSettings? settings = null)
    {
        _cropSettings = settings ?? ConfigService.LoadSettings().LoadingCrop ?? new LoadingCropSettings();
        LoadTemplates();
    }

    public void UpdateSettings(LoadingCropSettings settings)
    {
        _cropSettings = settings;
    }

    public void SetScanInterval(int intervalMs)
    {
        _intervalMs = Math.Clamp(intervalMs, 20, 5000);
    }

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
        lock (_lock)
        {
            _lastRecognizedValues = null;
            _lastSuccessfulScanTime = DateTime.MinValue;
            _isScanningActive = false;
        }
    }

    /// <summary>
    /// Nạp các mẫu số 0..9 từ thư mục Templates/AOE loading và đọc tọa độ từ data.json.
    /// </summary>
    public void LoadTemplates()
    {
        lock (_lock)
        {
            _templates.Clear();

            string[] searchDirs = new[]
            {
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Templates", "AOE loading", "crops"),
                Path.Combine(Directory.GetCurrentDirectory(), "Templates", "AOE loading", "crops"),
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Templates", "AOE loading"),
                Path.Combine(Directory.GetCurrentDirectory(), "Templates", "AOE loading")
            };

            string? validDir = searchDirs.FirstOrDefault(Directory.Exists);
            if (validDir == null) return;

            string dataJsonPath = Path.Combine(Path.GetDirectoryName(validDir) ?? "", "data.json");
            if (!File.Exists(dataJsonPath))
            {
                dataJsonPath = Path.Combine(validDir, "data.json");
            }

            // 1. Đọc data.json nếu có
            if (File.Exists(dataJsonPath))
            {
                try
                {
                    string jsonText = File.ReadAllText(dataJsonPath);
                    using var doc = JsonDocument.Parse(jsonText);

                    // Đọc tọa độ Area 1
                    if (doc.RootElement.TryGetProperty("areas", out var areasElem) && areasElem.GetArrayLength() > 0)
                    {
                        var firstArea = areasElem[0];
                        if (firstArea.TryGetProperty("x", out var xProp) &&
                            firstArea.TryGetProperty("y", out var yProp) &&
                            firstArea.TryGetProperty("width", out var wProp) &&
                            firstArea.TryGetProperty("height", out var hProp))
                        {
                            // Cập nhật tọa độ mặc định nếu chưa chỉnh sửa thủ công
                            if (_cropSettings.LoadingBox.X == 92 && _cropSettings.LoadingBox.Y == 685)
                            {
                                _cropSettings.LoadingBox = new ResourceCropBox(xProp.GetInt32(), yProp.GetInt32(), wProp.GetInt32(), hProp.GetInt32());
                            }
                        }
                    }

                    // Đọc danh sách crops từ data.json
                    if (doc.RootElement.TryGetProperty("crops", out var cropsElem))
                    {
                        string baseFolder = Path.GetDirectoryName(dataJsonPath) ?? "";
                        foreach (var cropItem in cropsElem.EnumerateArray())
                        {
                            string? relPath = cropItem.TryGetProperty("relative_path", out var rProp) ? rProp.GetString() : null;
                            string? fileName = cropItem.TryGetProperty("file_name", out var fProp) ? fProp.GetString() : null;
                            string? name = cropItem.TryGetProperty("name", out var nProp) ? nProp.GetString() : null;

                            char c = ' ';
                            if (!string.IsNullOrEmpty(name))
                            {
                                var m = Regex.Match(name, @"\d");
                                if (m.Success) c = m.Value[0];
                            }

                            string targetFile = "";
                            if (!string.IsNullOrEmpty(relPath))
                            {
                                targetFile = Path.Combine(baseFolder, relPath);
                            }
                            if (!File.Exists(targetFile) && !string.IsNullOrEmpty(fileName))
                            {
                                targetFile = Path.Combine(validDir, fileName);
                            }

                            if (File.Exists(targetFile) && c != ' ')
                            {
                                LoadSingleTemplate(targetFile, c);
                            }
                        }
                    }
                }
                catch { }
            }

            // 2. Quét thêm toàn bộ file PNG trong thư mục crops để tránh thiếu sót
            var files = Directory.GetFiles(validDir, "*.png");
            foreach (var file in files)
            {
                string fname = Path.GetFileName(file);
                // Tìm chữ số cuối cùng trước .png (ví dụ crop_001_Crop 0.png -> 0)
                var m = Regex.Match(fname, @"(\d)\.png$", RegexOptions.IgnoreCase);
                if (m.Success && char.IsDigit(m.Groups[1].Value[0]))
                {
                    char c = m.Groups[1].Value[0];
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
                    // Mẫu thuần nhị phân điểm trắng với ngưỡng Threshold = 255
                    if (c.R == 255 && c.G == 255 && c.B == 255)
                    {
                        whitePts.Add(new Point(x, y));
                    }
                }
            }

            if (whitePts.Count == 0) return;

            // Xóa mẫu cũ nếu đã tồn tại để cập nhật mẫu mới nhất
            _templates.RemoveAll(t => t.Character == character);
            _templates.Add(new LoadingGlyphTemplate
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
        try { _scanTask?.Wait(250); } catch { }
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
                if (!_isRunning || !_isEnabled || _templates.Count == 0 || !AoeWindowHelper.IsAoeForeground())
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

                        bool changed = _lastRecognizedValues == null || !res.EqualsValues(_lastRecognizedValues);
                        if (changed)
                        {
                            _lastRecognizedValues = res;
                            LoadingProgressUpdated?.Invoke(res);

                            if (res.IsComplete)
                            {
                                LoadingCompleted?.Invoke(res);
                            }
                        }
                    }
                    else
                    {
                        if (_isScanningActive && (DateTime.UtcNow - _lastSuccessfulScanTime).TotalMilliseconds >= 1500)
                        {
                            _isScanningActive = false;
                            // Không xóa _lastRecognizedValues để giữ giá trị quan sát cuối cùng cho Dev
                        }
                    }
                }
                catch { }
            }
        }
        catch (OperationCanceledException) { }
    }

    /// <summary>
    /// Chụp màn hình vùng game AOE và nhận diện tỉ lệ loading.
    /// </summary>
    public LoadingValues? CaptureAndRecognize()
    {
        IntPtr hwnd = FindAoeWindow();
        if (hwnd == IntPtr.Zero) return null;

        NativeMethods.POINT pt = new() { X = 0, Y = 0 };
        NativeMethods.ClientToScreen(hwnd, ref pt);
        int originX = pt.X;
        int originY = pt.Y;

        // 1. Thử quét ở vị trí chính LoadingBox
        var res = CaptureRegion(originX, originY, _cropSettings.LoadingBox);

        // 2. Nếu không ra và có bật AutoCheckSecondaryBox, thử quét vị trí phụ (SecondaryBox)
        if ((res == null || !res.IsValid) && _cropSettings.AutoCheckSecondaryBox && _cropSettings.SecondaryBox != null)
        {
            var secondaryRes = CaptureRegion(originX, originY, _cropSettings.SecondaryBox);
            if (secondaryRes != null && secondaryRes.IsValid)
            {
                res = secondaryRes;
            }
        }

        res ??= new LoadingValues();
        _lastSuccessfulScanTime = DateTime.UtcNow;
        _isScanningActive = res.IsValid;
        _lastRecognizedValues = res;
        LoadingProgressUpdated?.Invoke(res);
        if (res.IsComplete) LoadingCompleted?.Invoke(res);
        return res;
    }

    private LoadingValues? CaptureRegion(int originX, int originY, ResourceCropBox box)
    {
        int screenCropX = originX + box.X;
        int screenCropY = originY + box.Y;
        int boxW = Math.Max(4, box.Width);
        int boxH = Math.Max(4, box.Height);

        using Bitmap capture = new(boxW, boxH, PixelFormat.Format32bppArgb);
        using (Graphics g = Graphics.FromImage(capture))
        {
            g.CopyFromScreen(screenCropX, screenCropY, 0, 0, new Size(boxW, boxH));
        }

        return RecognizeFromBitmap(capture, 0, 0, boxW, boxH);
    }

    /// <summary>
    /// Nhận diện tỉ lệ Loading từ ảnh Bitmap nguồn.
    /// Binarize bằng ngưỡng Threshold = 255 (chỉ nhận điểm trắng tuyệt đối).
    /// </summary>
    public LoadingValues RecognizeFromBitmap(Bitmap bmp, int cropX = 0, int cropY = 0, int cropW = -1, int cropH = -1)
    {
        if (_templates.Count == 0) return new LoadingValues();

        if (cropW <= 0) cropW = bmp.Width;
        if (cropH <= 0) cropH = bmp.Height;

        cropX = Math.Clamp(cropX, 0, Math.Max(0, bmp.Width - 1));
        cropY = Math.Clamp(cropY, 0, Math.Max(0, bmp.Height - 1));
        cropW = Math.Clamp(cropW, 1, Math.Max(1, bmp.Width - cropX));
        cropH = Math.Clamp(cropH, 1, Math.Max(1, bmp.Height - cropY));

        if (cropW < 3 || cropH < 5) return new LoadingValues();

        // 1. Nhị phân hóa ảnh với Threshold (mặc định 190: R >= th && G >= th && B >= th)
        int th = _cropSettings.Threshold > 0 ? _cropSettings.Threshold : 190;
        bool[,] bin = new bool[cropW, cropH];
        for (int y = 0; y < cropH; y++)
        {
            for (int x = 0; x < cropW; x++)
            {
                Color px = bmp.GetPixel(cropX + x, cropY + y);
                bin[x, y] = (px.R >= th && px.G >= th && px.B >= th) ||
                            ((px.R + px.G + px.B) / 3 >= th && Math.Abs(px.R - px.G) <= 25 && Math.Abs(px.G - px.B) <= 25);
            }
        }

        // 2. Tìm tất cả các vị trí ứng viên có điểm ảnh trắng khớp với template số
        List<LoadingCandidateMatch> candidates = new();

        for (int x = 0; x <= cropW - 3; x++)
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

                    // Điểm trắng phải khớp từ 85% trở lên (cho phép dung sai nhẹ)
                    int minRequired = Math.Max(t.TotalWhite - 3, (int)Math.Floor(t.TotalWhite * 0.85));
                    if (matchedWhite >= minRequired)
                    {
                        // Đếm số điểm trắng thực tế trong bounding box của ký tự
                        int imgWhite = 0;
                        for (int ty = 0; ty < t.Height; ty++)
                        {
                            for (int tx = 0; tx < t.Width; tx++)
                            {
                                if (bin[x + tx, y + ty]) imgWhite++;
                            }
                        }

                        int extraWhite = imgWhite - matchedWhite;
                        // Điểm số: thưởng điểm khớp trắng, phạt điểm thừa trong ô
                        double score = matchedWhite * 10 - extraWhite * 12;

                        if (score > 0)
                        {
                            candidates.Add(new LoadingCandidateMatch
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
        }

        if (candidates.Count == 0) return new LoadingValues();

        // 3. Chọn tham lam các ký tự không đè nhau (Horizontal non-overlapping) theo thứ tự điểm cao nhất
        candidates.Sort((a, b) => b.Score.CompareTo(a.Score));
        List<LoadingCandidateMatch> chosen = new();

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

        // 4. Sắp xếp các ký tự từ trái qua phải
        chosen.Sort((a, b) => a.X.CompareTo(b.X));

        StringBuilder sb = new();
        foreach (var item in chosen)
        {
            sb.Append(item.Character);
        }

        string raw = sb.ToString();
        var res = new LoadingValues { RawText = raw };

        // Parse số phần trăm (0..100)
        string digitsOnly = new(raw.Where(char.IsDigit).ToArray());
        if (int.TryParse(digitsOnly, out int val))
        {
            // Tỉ lệ loading chuẩn trong game nằm trong khoảng 0 đến 100%
            if (val >= 0 && val <= 100)
            {
                res.Percentage = val;
            }
        }

        return res;
    }

    private static IntPtr FindAoeWindow()
    {
        return AoeWindowHelper.FindAnyAoeWindow();
    }

    public void Dispose()
    {
        Stop();
        GC.SuppressFinalize(this);
    }
}
