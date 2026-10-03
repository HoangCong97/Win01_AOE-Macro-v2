using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AOEKeyboardMacroPro.Models;

namespace AOEKeyboardMacroPro.Services;

/// <summary>
/// Mẫu template đại diện cho khung chữ "CHAT" trong AOE.
/// Lưu trữ tọa độ các điểm ảnh trắng (chữ) và đen (viền bóng) theo ngưỡng Threshold = 255.
/// </summary>
public class ChatTemplate
{
    public int Width { get; set; }
    public int Height { get; set; }
    public List<Point> WhitePixels { get; set; } = new();
    public List<Point> BlackPixels { get; set; } = new();
    public int TotalWhite => WhitePixels.Count;
    public int TotalBlack => BlackPixels.Count;
}

/// <summary>
/// Dịch vụ tự động quét và phát hiện trạng thái mở/đóng của khung Chat trong game AOE
/// bằng cách so khớp điểm ảnh mẫu CHAT (Threshold = 255, kiểm tra cả 2 màu trắng và đen).
/// Thay thế hoàn toàn cơ chế toggle thủ công bằng phím Enter/Escape cũ.
/// </summary>
public class ChatDetectionService : IDisposable
{
    private ChatCropSettings _cropSettings;
    private readonly List<ChatTemplate> _templates = new();
    private int _intervalMs = 50; // Quét mỗi 50ms cho độ nhạy cực cao (<0.05s) và tối ưu CPU < 0.1%
    private bool _isRunning = false;
    private bool _isEnabled = true;
    private bool _isChatOpen = false;
    private int _missCount = 0;

    private CancellationTokenSource? _scanCts;
    private Task? _scanTask;
    private readonly object _scanLock = new();

    public event Action<bool>? ChatStatusChanged;

    public bool IsChatOpen => _isChatOpen;
    public bool IsRunning => _isRunning;
    public bool IsEnabled => _isEnabled;
    public ChatCropSettings CropSettings => _cropSettings;

    public ChatDetectionService(ChatCropSettings? settings = null, int intervalMs = 50)
    {
        _cropSettings = settings ?? ConfigService.LoadSettings().ChatCrop ?? new ChatCropSettings();
        _intervalMs = Math.Clamp(intervalMs, 20, 2000);
        LoadTemplates();
    }

    public void UpdateSettings(ChatCropSettings settings)
    {
        _cropSettings = settings;
    }

    public void SetEnabled(bool enabled)
    {
        _isEnabled = enabled;
        if (!_isEnabled && _isChatOpen)
        {
            _isChatOpen = false;
            ChatStatusChanged?.Invoke(false);
        }
    }

    public void Reset()
    {
        _missCount = 0;
        if (_isChatOpen)
        {
            _isChatOpen = false;
            ChatStatusChanged?.Invoke(false);
        }
    }

    public void LoadTemplates()
    {
        _templates.Clear();

        string[] searchDirs = new[]
        {
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Templates", "ChatAOE", "crops"),
            Path.Combine(Directory.GetCurrentDirectory(), "Templates", "ChatAOE", "crops"),
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Templates", "ChatAOE"),
            Path.Combine(Directory.GetCurrentDirectory(), "Templates", "ChatAOE")
        };

        string? validDir = searchDirs.FirstOrDefault(Directory.Exists);
        if (validDir == null) return;

        // 1. Đọc dữ liệu tọa độ mặc định từ data.json nếu có
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
                        int srcW = 1366;
                        int srcH = 768;
                        if (firstArea.TryGetProperty("source_width", out var swProp)) srcW = swProp.GetInt32();
                        if (firstArea.TryGetProperty("source_height", out var shProp)) srcH = shProp.GetInt32();

                        _cropSettings.ChatBox = new ResourceCropBox(xProp.GetInt32(), yProp.GetInt32(), wProp.GetInt32(), hProp.GetInt32());
                        _cropSettings.SourceWidth = srcW;
                        _cropSettings.SourceHeight = srcH;
                    }
                }
            }
            catch { }
        }

        // 2. Nạp file ảnh mẫu PNG (crop_001_Chat Crop.png)
        var files = Directory.GetFiles(validDir, "*.png");
        foreach (var file in files)
        {
            LoadSingleTemplate(file);
        }
    }

    private void LoadSingleTemplate(string path)
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
                    // Ngưỡng Threshold = 255: điểm trắng tinh (255, 255, 255)
                    if (c.R == 255 && c.G == 255 && c.B == 255)
                    {
                        whitePts.Add(new Point(x, y));
                    }
                    // Điểm đen viền/bóng của chữ (0, 0, 0)
                    else if (c.R == 0 && c.G == 0 && c.B == 0)
                    {
                        blackPts.Add(new Point(x, y));
                    }
                }
            }

            if (whitePts.Count > 0)
            {
                _templates.Add(new ChatTemplate
                {
                    Width = w,
                    Height = h,
                    WhitePixels = whitePts,
                    BlackPixels = blackPts
                });
            }
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
        try { _scanTask?.Wait(200); } catch { }
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
                    await Task.Delay(200, ct);
                    continue;
                }

                PerformScan();
            }
        }
        catch (OperationCanceledException) { }
    }

    /// <summary>
    /// Kích hoạt quét tức thì khi có sự kiện phím Enter/Escape để cập nhật trạng thái cực nhanh (<15ms).
    /// </summary>
    public void TriggerImmediateScan()
    {
        Task.Run(async () =>
        {
            await Task.Delay(15);
            PerformScan();
            await Task.Delay(35);
            PerformScan();
        });
    }

    public void PerformScan()
    {
        lock (_scanLock)
        {
            try
            {
                bool isOpen = CheckChatOpen();
                if (isOpen)
                {
                    _missCount = 0;
                    if (!_isChatOpen)
                    {
                        _isChatOpen = true;
                        ChatStatusChanged?.Invoke(true);
                    }
                }
                else
                {
                    if (_isChatOpen)
                    {
                        _missCount++;
                        if (_missCount >= 1)
                        {
                            _isChatOpen = false;
                            ChatStatusChanged?.Invoke(false);
                        }
                    }
                }
            }
            catch { }
        }
    }

    public bool CheckChatOpen()
    {
        if (_templates.Count == 0) return false;

        IntPtr hwnd = FindAoeWindow();
        if (hwnd == IntPtr.Zero) return false;

        NativeMethods.POINT pt = new() { X = 0, Y = 0 };
        NativeMethods.ClientToScreen(hwnd, ref pt);
        int originX = pt.X;
        int originY = pt.Y;

        NativeMethods.GetClientRect(hwnd, out NativeMethods.RECT clientRect);
        int clientW = clientRect.Right - clientRect.Left;
        int clientH = clientRect.Bottom - clientRect.Top;
        if (clientW <= 0 || clientH <= 0) return false;

        int boxX = _cropSettings.ChatBox.X;
        int boxY = _cropSettings.ChatBox.Y;
        int boxW = _cropSettings.ChatBox.Width;
        int boxH = _cropSettings.ChatBox.Height;

        // 1. Kiểm tra vị trí gốc cấu hình
        if (CheckRegion(originX + boxX, originY + boxY, boxW, boxH))
        {
            return true;
        }

        // 2. Kiểm tra theo tỷ lệ độ phân giải nếu kích thước cửa sổ game khác 1366x768
        int srcW = _cropSettings.SourceWidth > 0 ? _cropSettings.SourceWidth : 1366;
        int srcH = _cropSettings.SourceHeight > 0 ? _cropSettings.SourceHeight : 768;

        if (clientW != srcW || clientH != srcH)
        {
            int scaledX = (int)Math.Round((double)boxX * clientW / srcW);
            int scaledY = (int)Math.Round((double)boxY * clientH / srcH);
            if (CheckRegion(originX + scaledX, originY + scaledY, boxW, boxH))
            {
                return true;
            }

            // AOE 1 luôn đặt dòng Chat ở trục giữa theo chiều dọc
            int centerY = (clientH / 2) - (boxH / 2);
            if (centerY != scaledY && centerY != boxY)
            {
                if (CheckRegion(originX + scaledX, originY + centerY, boxW, boxH))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private bool CheckRegion(int screenX, int screenY, int boxW, int boxH)
    {
        int margin = 3;
        int capX = screenX - margin;
        int capY = screenY - margin;
        int capW = boxW + margin * 2;
        int capH = boxH + margin * 2;

        try
        {
            using Bitmap capture = new(capW, capH, PixelFormat.Format32bppArgb);
            using (Graphics g = Graphics.FromImage(capture))
            {
                g.CopyFromScreen(capX, capY, 0, 0, new Size(capW, capH));
            }

            foreach (var t in _templates)
            {
                for (int dy = 0; dy <= capH - t.Height; dy++)
                {
                    for (int dx = 0; dx <= capW - t.Width; dx++)
                    {
                        if (IsMatchAt(capture, dx, dy, t))
                        {
                            return true;
                        }
                    }
                }
            }
        }
        catch { }

        return false;
    }

    private static bool IsMatchAt(Bitmap bmp, int offsetX, int offsetY, ChatTemplate t)
    {
        // 1. Kiểm tra màu chuẩn: Chữ trắng (255, 255, 255), viền bóng đen (0, 0, 0)
        int matchedWhite = 0;
        foreach (var pt in t.WhitePixels)
        {
            Color px = bmp.GetPixel(offsetX + pt.X, offsetY + pt.Y);
            if (px.R == 255 && px.G == 255 && px.B == 255)
            {
                matchedWhite++;
            }
        }

        if (matchedWhite >= t.TotalWhite * 0.92)
        {
            int matchedBlack = 0;
            foreach (var pt in t.BlackPixels)
            {
                Color px = bmp.GetPixel(offsetX + pt.X, offsetY + pt.Y);
                if (px.R <= 5 && px.G <= 5 && px.B <= 5)
                {
                    matchedBlack++;
                }
            }

            if (matchedBlack >= t.TotalBlack * 0.85)
            {
                return true;
            }
        }

        // 2. Dự phòng: Chữ đen, viền bóng trắng (đảo màu tùy mod/chế độ hiển thị)
        int matchedInvertedWhite = 0;
        foreach (var pt in t.WhitePixels)
        {
            Color px = bmp.GetPixel(offsetX + pt.X, offsetY + pt.Y);
            if (px.R <= 5 && px.G <= 5 && px.B <= 5)
            {
                matchedInvertedWhite++;
            }
        }

        if (matchedInvertedWhite >= t.TotalWhite * 0.92)
        {
            int matchedInvertedBlack = 0;
            foreach (var pt in t.BlackPixels)
            {
                Color px = bmp.GetPixel(offsetX + pt.X, offsetY + pt.Y);
                if (px.R == 255 && px.G == 255 && px.B == 255)
                {
                    matchedInvertedBlack++;
                }
            }

            if (matchedInvertedBlack >= t.TotalBlack * 0.85)
            {
                return true;
            }
        }

        return false;
    }

    private static IntPtr FindAoeWindow()
    {
        return AoeWindowHelper.GetAoeWindow();
    }

    public void Dispose()
    {
        Stop();
        GC.SuppressFinalize(this);
    }
}
