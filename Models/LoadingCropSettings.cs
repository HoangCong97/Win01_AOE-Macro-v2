namespace AOEKeyboardMacroPro.Models;

/// <summary>
/// Cấu hình tọa độ và thông số quét tỉ lệ Loading của game AOE.
/// Tọa độ mặc định lấy theo Area 1 trong data.json của Templates/AOE loading.
/// </summary>
public class LoadingCropSettings
{
    /// <summary>
    /// Vùng chữ nhật chứa con số tỉ lệ loading (mặc định x=92, y=683, w=28, h=16 từ data.json).
    /// </summary>
    public ResourceCropBox LoadingBox { get; set; } = new(92, 683, 28, 16);

    /// <summary>
    /// Vùng phụ dự phòng (nếu game hiển thị tỉ lệ ở hàng dưới y=744).
    /// </summary>
    public ResourceCropBox SecondaryBox { get; set; } = new(8, 744, 42, 12);

    /// <summary>
    /// Ngưỡng màu điểm ảnh trắng (mặc định 190: R >= 190 && G >= 190 && B >= 190).
    /// </summary>
    public int Threshold { get; set; } = 190;

    public int SourceWidth { get; set; } = 1366;
    public int SourceHeight { get; set; } = 768;

    /// <summary>
    /// Tự động quét vùng phụ nếu vùng chính không tìm thấy số loading.
    /// </summary>
    public bool AutoCheckSecondaryBox { get; set; } = true;
}

/// <summary>
/// Đại diện cho kết quả đọc tỉ lệ loading (0 - 100%).
/// </summary>
public class LoadingValues
{
    /// <summary>
    /// Tỉ lệ phần trăm loading nhận diện được (0..100).
    /// </summary>
    public int? Percentage { get; set; }

    /// <summary>
    /// Chuỗi văn bản thô đọc được từ ảnh (ví dụ: "0", "45", "100").
    /// </summary>
    public string? RawText { get; set; }

    /// <summary>
    /// Thời điểm nhận diện thành công.
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Kiểm tra giá trị có hợp lệ hay không (0..100%).
    /// </summary>
    public bool IsValid => Percentage.HasValue && Percentage.Value >= 0 && Percentage.Value <= 100;

    /// <summary>
    /// Đã hoàn tất loading (đạt 100%).
    /// </summary>
    public bool IsComplete => Percentage.HasValue && Percentage.Value >= 100;

    public bool EqualsValues(LoadingValues? other)
    {
        if (other is null) return false;
        return Percentage == other.Percentage && RawText == other.RawText;
    }

    public override string ToString()
    {
        return IsValid ? $"{Percentage}%" : "--%";
    }
}
