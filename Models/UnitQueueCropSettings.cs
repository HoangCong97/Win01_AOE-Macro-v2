namespace AOEKeyboardMacroPro.Models;

/// <summary>
/// Cấu hình tọa độ và thông số quét số lượng xin quân / xóc quân trong game AOE.
/// Tọa độ mặc định lấy theo Area 1 trong data.json của Templates/AOE xoc quan.
/// </summary>
public class UnitQueueCropSettings
{
    /// <summary>
    /// Danh sách 5 vùng kiểm tra xin quân (Area 1..5 trong data.json của Templates/AOE xoc quan).
    /// Tại 1 thời điểm chỉ có 1 nơi có giá trị, các nơi còn lại trống.
    /// </summary>
    public List<ResourceCropBox> QueueBoxes { get; set; } = new()
    {
        new ResourceCropBox(139, 654, 24, 12),
        new ResourceCropBox(194, 654, 24, 12),
        new ResourceCropBox(248, 654, 24, 12),
        new ResourceCropBox(302, 654, 24, 12),
        new ResourceCropBox(357, 654, 24, 12)
    };

    /// <summary>
    /// Vùng mặc định (backward-compatibility), trỏ tới Area 1.
    /// </summary>
    public ResourceCropBox QueueBox
    {
        get => QueueBoxes.Count > 0 ? QueueBoxes[0] : new ResourceCropBox(139, 654, 24, 12);
        set
        {
            if (QueueBoxes.Count > 0) QueueBoxes[0] = value;
            else QueueBoxes.Add(value);
        }
    }

    /// <summary>
    /// Ngưỡng màu điểm ảnh sáng / chữ số hàng chờ (mặc định 190: R >= 190 && G >= 190 && B >= 190).
    /// </summary>
    public int Threshold { get; set; } = 190;

    public int SourceWidth { get; set; } = 1366;
    public int SourceHeight { get; set; } = 768;
}

/// <summary>
/// Kết quả nhận diện số lượng quân đang xin / xóc quân trong hàng đợi.
/// Thường là con số 1 hoặc 2 chữ số (ví dụ: 1..9, 12, 20).
/// </summary>
public class UnitQueueValues
{
    /// <summary>
    /// Số lượng quân xin chính (nhóm số đầu tiên hoặc đơn lẻ, ví dụ: 5, 12).
    /// </summary>
    public int? PrimaryCount { get; set; }

    /// <summary>
    /// Vị trí ô phát hiện số lượng quân (1..5 tương ứng Area 1..5).
    /// </summary>
    public int SlotIndex { get; set; } = 0;

    /// <summary>
    /// Tổng số lượng quân xin trên tất cả các ô trong thanh kiểm tra.
    /// </summary>
    public int TotalCount { get; set; } = 0;

    /// <summary>
    /// Danh sách tất cả các số lượng đọc được tại từng vị trí ô (nếu có nhiều ô đang xin).
    /// </summary>
    public List<int> SlotCounts { get; set; } = new();

    /// <summary>
    /// Chuỗi văn bản thô đọc được (ví dụ: "5", "12" hoặc "5, 3").
    /// </summary>
    public string? RawText { get; set; }

    /// <summary>
    /// Thời điểm nhận diện thành công.
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Kiểm tra kết quả có hợp lệ (có ít nhất 1 số đọc được).
    /// </summary>
    public bool IsValid => PrimaryCount.HasValue;

    public bool EqualsValues(UnitQueueValues? other)
    {
        if (other is null) return false;
        return PrimaryCount == other.PrimaryCount &&
               TotalCount == other.TotalCount &&
               RawText == other.RawText;
    }

    public override string ToString()
    {
        return IsValid ? (PrimaryCount.HasValue ? PrimaryCount.Value.ToString() : "--") : "--";
    }
}
