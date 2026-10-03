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
/// Mẫu chữ số xin quân lưu cả điểm ảnh trắng (nét chữ) và điểm ảnh đen (nền).
/// </summary>
public class UnitQueueGlyphTemplate
{
    public char Character { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    public List<Point> WhitePixels { get; set; } = new();
    public List<Point> BlackPixels { get; set; } = new();
    public int TotalWhite => WhitePixels.Count;
    public int TotalBlack => BlackPixels.Count;
    public int TotalPixels => Width * Height;
}

public class UnitQueueCandidateMatch
{
    public int X { get; set; }
    public int Y { get; set; }
    public char Character { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    public int MatchedWhite { get; set; }
    public int TotalWhite { get; set; }
    public int MatchedBlack { get; set; }
    public int TotalBlack { get; set; }
    public int ExtraWhite { get; set; }
    public double Score { get; set; }
}

/// <summary>
/// Dịch vụ OCR nhận diện số lượng quân đang xin / xóc quân trong game AOE dựa trên template điểm trắng trong:
/// Templates/AOE xoc quan (crops: 0..9, data.json: Area 1).
/// Vùng quét màn hình rộng (265px), số lượng cần nhận diện là các nhóm 1-2 chữ số.
/// </summary>
public class UnitQueueOcrService : IDisposable
{
    private readonly List<UnitQueueGlyphTemplate> _templates = new();
    private CancellationTokenSource? _scanCts;
    private Task? _scanTask;
    private int _intervalMs = 150; // Quét mỗi 150ms
    private UnitQueueCropSettings _cropSettings;
    private UnitQueueValues? _lastRecognizedValues;
    private DateTime _lastSuccessfulScanTime = DateTime.MinValue;
    private bool _isScanningActive = false;
    private volatile bool _isRunning = false;
    private volatile bool _isEnabled = true;
    private readonly object _lock = new();

    /// <summary>
    /// Bắn ra khi số lượng xin quân thay đổi hoặc được nhận diện mới.
    /// </summary>
    public event Action<UnitQueueValues>? UnitQueueUpdated;

    public bool IsRunning => _isRunning;
    public bool IsEnabled => _isEnabled;
    public bool IsQueueActive => _isScanningActive && (DateTime.UtcNow - _lastSuccessfulScanTime).TotalMilliseconds <= 1500;
    public UnitQueueValues? CurrentQueue => _lastRecognizedValues;
    public int? CurrentPrimaryCount => _lastRecognizedValues?.PrimaryCount;
    public UnitQueueCropSettings CropSettings => _cropSettings;
    public int LoadedTemplateCount => _templates.Count;

    public UnitQueueOcrService(UnitQueueCropSettings? settings = null)
    {
        _cropSettings = settings ?? ConfigService.LoadSettings().UnitQueueCrop ?? new UnitQueueCropSettings();
        LoadTemplates();
    }

    public void UpdateSettings(UnitQueueCropSettings settings)
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
    /// Nạp các mẫu số 0..9 từ thư mục Templates/AOE xoc quan và đọc tọa độ từ data.json.
    /// </summary>
    public void LoadTemplates()
    {
        lock (_lock)
        {
            _templates.Clear();

            string[] searchDirs = new[]
            {
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Templates", "AOE xoc quan", "crops"),
                Path.Combine(Directory.GetCurrentDirectory(), "Templates", "AOE xoc quan", "crops"),
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Templates", "AOE xoc quan"),
                Path.Combine(Directory.GetCurrentDirectory(), "Templates", "AOE xoc quan")
            };

            string? validDir = searchDirs.FirstOrDefault(Directory.Exists);
            if (validDir == null) return;

            string? dataJsonPath = new[]
            {
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Templates", "AOE xoc quan", "data.json"),
                Path.Combine(Directory.GetCurrentDirectory(), "Templates", "AOE xoc quan", "data.json"),
                Path.Combine(Path.GetDirectoryName(validDir) ?? "", "data.json"),
                Path.Combine(validDir, "data.json")
            }.FirstOrDefault(File.Exists);

            // 1. Đọc data.json nếu có
            if (!string.IsNullOrEmpty(dataJsonPath) && File.Exists(dataJsonPath))
            {
                try
                {
                    string jsonText = File.ReadAllText(dataJsonPath);
                    using var doc = JsonDocument.Parse(jsonText);

                    // Đọc danh sách các tọa độ ô kiểm tra (Area 1..5) từ data.json
                    if (doc.RootElement.TryGetProperty("areas", out var areasElem) && areasElem.GetArrayLength() > 0)
                    {
                        List<ResourceCropBox> loadedBoxes = new();
                        foreach (var areaItem in areasElem.EnumerateArray())
                        {
                            if (areaItem.TryGetProperty("x", out var xProp) &&
                                areaItem.TryGetProperty("y", out var yProp) &&
                                areaItem.TryGetProperty("width", out var wProp) &&
                                areaItem.TryGetProperty("height", out var hProp))
                            {
                                loadedBoxes.Add(new ResourceCropBox(xProp.GetInt32(), yProp.GetInt32(), wProp.GetInt32(), hProp.GetInt32()));
                            }
                        }

                        if (loadedBoxes.Count > 0)
                        {
                            _cropSettings.QueueBoxes = loadedBoxes;
                            _cropSettings.QueueBox = loadedBoxes[0];
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

            // 2. Quét thêm toàn bộ file PNG trong thư mục crops
            var files = Directory.GetFiles(validDir, "*.png");
            foreach (var file in files)
            {
                string fname = Path.GetFileName(file);
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
            List<Point> blackPts = new();

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    Color c = bmp.GetPixel(x, y);
                    // Điểm trắng: Threshold = 255 (R==255 && G==255 && B==255)
                    if (c.R == 255 && c.G == 255 && c.B == 255)
                    {
                        whitePts.Add(new Point(x, y));
                    }
                    else
                    {
                        blackPts.Add(new Point(x, y));
                    }
                }
            }

            if (whitePts.Count == 0) return;

            _templates.RemoveAll(t => t.Character == character);
            _templates.Add(new UnitQueueGlyphTemplate
            {
                Character = character,
                Width = w,
                Height = h,
                WhitePixels = whitePts,
                BlackPixels = blackPts
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
                            UnitQueueUpdated?.Invoke(res);
                        }
                    }
                    else
                    {
                        if (_isScanningActive && (DateTime.UtcNow - _lastSuccessfulScanTime).TotalMilliseconds >= 1500)
                        {
                            _isScanningActive = false;
                            // Khi không còn số xin quân (đã xong quân hoặc chuyển chọn), reset về null và thông báo
                            if (_lastRecognizedValues != null)
                            {
                                _lastRecognizedValues = null;
                                UnitQueueUpdated?.Invoke(new UnitQueueValues());
                            }
                        }
                    }
                }
                catch { }
            }
        }
        catch (OperationCanceledException) { }
    }

    /// <summary>
    /// Chụp màn hình 5 ô xin quân trong AOE và nhận diện số lượng.
    /// "trong 5 nơi này tại 1 thời điểm chỉ có 1 nơi có giá trị, còn lại là không có gì"
    /// </summary>
    public UnitQueueValues? CaptureAndRecognize()
    {
        IntPtr hwnd = FindAoeWindow();
        if (hwnd == IntPtr.Zero) return null;

        NativeMethods.POINT pt = new() { X = 0, Y = 0 };
        NativeMethods.ClientToScreen(hwnd, ref pt);
        int originX = pt.X;
        int originY = pt.Y;

        var boxes = (_cropSettings.QueueBoxes != null && _cropSettings.QueueBoxes.Count > 0)
            ? _cropSettings.QueueBoxes
            : new List<ResourceCropBox> { _cropSettings.QueueBox };

        // 1. Tính toán vùng bao trùm (Bounding Rect) chứa toàn bộ các ô để chụp màn hình 1 lần duy nhất
        int minX = boxes.Min(b => b.X);
        int minY = boxes.Min(b => b.Y);
        int maxX = boxes.Max(b => b.X + b.Width);
        int maxY = boxes.Max(b => b.Y + b.Height);

        int totalW = Math.Max(10, maxX - minX);
        int totalH = Math.Max(10, maxY - minY);

        int screenCropX = originX + minX;
        int screenCropY = originY + minY;

        using Bitmap capture = new(totalW, totalH, PixelFormat.Format32bppArgb);
        using (Graphics g = Graphics.FromImage(capture))
        {
            g.CopyFromScreen(screenCropX, screenCropY, 0, 0, new Size(totalW, totalH));
        }

        // 2. Quét lần lượt qua 5 ô: tìm thấy ô có giá trị (> 0) thì dừng ngay lập tức
        UnitQueueValues? foundResult = null;
        int currentSlot = 1;

        foreach (var box in boxes)
        {
            int localX = Math.Clamp(box.X - minX, 0, totalW - 1);
            int localY = Math.Clamp(box.Y - minY, 0, totalH - 1);
            int boxW = Math.Min(box.Width, totalW - localX);
            int boxH = Math.Min(box.Height, totalH - localY);

            var res = RecognizeFromBitmap(capture, localX, localY, boxW, boxH);
            if (res != null && res.IsValid && res.PrimaryCount.HasValue && res.PrimaryCount.Value > 0)
            {
                res.SlotIndex = currentSlot;
                foundResult = res;
                break; // Tìm thấy ô có giá trị, dừng ngay lập tức
            }
            currentSlot++;
        }

        foundResult ??= new UnitQueueValues();
        _lastSuccessfulScanTime = DateTime.UtcNow;
        _isScanningActive = foundResult.IsValid;
        _lastRecognizedValues = foundResult;
        UnitQueueUpdated?.Invoke(foundResult);
        return foundResult;
    }

    /// <summary>
    /// Nhận diện số lượng xin quân từ Bitmap nguồn.
    /// Tọa độ quét rộng (width ~265px), số lượng xuất hiện dưới dạng các nhóm 1-2 chữ số.
    /// </summary>
    public UnitQueueValues RecognizeFromBitmap(Bitmap bmp, int cropX = 0, int cropY = 0, int cropW = -1, int cropH = -1)
    {
        if (_templates.Count == 0) return new UnitQueueValues();

        if (cropW <= 0) cropW = bmp.Width;
        if (cropH <= 0) cropH = bmp.Height;

        cropX = Math.Clamp(cropX, 0, Math.Max(0, bmp.Width - 1));
        cropY = Math.Clamp(cropY, 0, Math.Max(0, bmp.Height - 1));
        cropW = Math.Clamp(cropW, 1, Math.Max(1, bmp.Width - cropX));
        cropH = Math.Clamp(cropH, 1, Math.Max(1, bmp.Height - cropY));

        if (cropW < 3 || cropH < 5) return new UnitQueueValues();

        // 1. Nhị phân hóa với Threshold cấu hình (mặc định 190: R >= th && G >= th && B >= th)
        int th = _cropSettings.Threshold > 0 ? _cropSettings.Threshold : 190;
        bool[,] bin = new bool[cropW, cropH];
        for (int y = 0; y < cropH; y++)
        {
            for (int x = 0; x < cropW; x++)
            {
                Color px = bmp.GetPixel(cropX + x, cropY + y);
                // Điểm sáng: Tất cả RGB >= th hoặc độ sáng trung bình >= th và ít lệch màu (sắc độ xám/trắng)
                bin[x, y] = (px.R >= th && px.G >= th && px.B >= th) ||
                            ((px.R + px.G + px.B) / 3 >= th && Math.Abs(px.R - px.G) <= 25 && Math.Abs(px.G - px.B) <= 25);
            }
        }

        // 2. Tìm tất cả các vị trí ứng viên có điểm ảnh trắng khớp với template
        List<UnitQueueCandidateMatch> candidates = new();

        for (int x = 0; x < cropW; x++)
        {
            for (int y = 0; y < cropH; y++)
            {
                foreach (var t in _templates)
                {
                    if (x + t.Width > cropW || y + t.Height > cropH) continue;

                    // 2.1. So khớp điểm trắng (Nét chữ số)
                    int matchedWhite = 0;
                    foreach (var pt in t.WhitePixels)
                    {
                        if (bin[x + pt.X, y + pt.Y]) matchedWhite++;
                    }

                    // Điểm trắng phải khớp từ 85% trở lên (dung sai tối đa 1-2 điểm)
                    int minWhite = Math.Max(t.TotalWhite - 2, (int)Math.Floor(t.TotalWhite * 0.85));
                    if (matchedWhite < minWhite) continue;

                    // 2.2. So khớp điểm đen (Nền xung quanh và các khe rỗng của chữ số)
                    int matchedBlack = 0;
                    foreach (var pt in t.BlackPixels)
                    {
                        if (!bin[x + pt.X, y + pt.Y]) matchedBlack++;
                    }

                    // Điểm đen phải khớp từ 80% trở lên (so khớp cả nền đen của template để tránh nhận nhầm số)
                    int minBlack = (int)Math.Floor(t.TotalBlack * 0.80);
                    if (matchedBlack < minBlack) continue;

                    int extraWhite = t.TotalBlack - matchedBlack;
                    int missedWhite = t.TotalWhite - matchedWhite;

                    // Điểm số: Thưởng điểm khớp trắng và khớp đen, phạt nặng các điểm pixel sai lệch
                    double score = (matchedWhite * 10) + (matchedBlack * 3) - (extraWhite * 12) - (missedWhite * 10);

                    if (score > 0)
                    {
                        candidates.Add(new UnitQueueCandidateMatch
                        {
                            X = x,
                            Y = y,
                            Character = t.Character,
                            Width = t.Width,
                            Height = t.Height,
                            MatchedWhite = matchedWhite,
                            TotalWhite = t.TotalWhite,
                            MatchedBlack = matchedBlack,
                            TotalBlack = t.TotalBlack,
                            ExtraWhite = extraWhite,
                            Score = score
                        });
                    }
                }
            }
        }

        if (candidates.Count == 0) return new UnitQueueValues();

        // 3. Chọn tham lam các ký tự không đè nhau theo thứ tự điểm số cao nhất
        candidates.Sort((a, b) => b.Score.CompareTo(a.Score));
        List<UnitQueueCandidateMatch> chosen = new();

        foreach (var c in candidates)
        {
            bool overlap = false;
            foreach (var existing in chosen)
            {
                // Kiểm tra chồng lấn ngang
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

        // 5. Gom các chữ số liền kề thành từng nhóm số 1-2 chữ số (khoảng cách giữa 2 chữ số cùng số <= 6px)
        List<string> numberGroups = new();
        StringBuilder currentGroup = new();
        int lastEndX = -999;

        foreach (var item in chosen)
        {
            if (lastEndX >= 0 && (item.X - lastEndX) > 6)
            {
                // Khoảng cách quá xa -> thuộc nhóm/ô xin quân khác
                if (currentGroup.Length > 0)
                {
                    numberGroups.Add(currentGroup.ToString());
                    currentGroup.Clear();
                }
            }

            currentGroup.Append(item.Character);
            lastEndX = item.X + item.Width;
        }

        if (currentGroup.Length > 0)
        {
            numberGroups.Add(currentGroup.ToString());
        }

        var result = new UnitQueueValues();
        List<int> validCounts = new();

        foreach (var grp in numberGroups)
        {
            if (int.TryParse(grp, out int count))
            {
                validCounts.Add(count);
            }
        }

        if (validCounts.Count > 0)
        {
            result.SlotCounts = validCounts;
            result.PrimaryCount = validCounts[0];
            result.TotalCount = validCounts.Sum();
            result.RawText = string.Join(", ", validCounts);
        }

        return result;
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
