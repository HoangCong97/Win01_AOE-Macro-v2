# Module: ocr-vision
> `Services/*OcrService.cs`, `Services/ChatDetectionService.cs`, `Models/*CropSettings.cs` · ~3.2k dòng · cập nhật 2026-10-06 · [tổng quan](../PROJECT.md)

**Trách nhiệm:** Chụp ảnh màn hình theo toạ độ vùng crop, nhị phân hoá và so khớp mẫu glyph (template matching) nhận diện tài nguyên, pop, timer, loading, hàng đợi quân, và chat.
**Không làm:** Không trực tiếp điều khiển gửi phím bấm hay can thiệp logic macro.

## Thành phần chính & 3 câu hỏi kiểm định
- `Services/ResourceOcrService.cs:ResourceOcrService` - Nhận diện 4 loại tài nguyên (Thịt, Gỗ, Vàng, Đá).
  *(1)* Dùng `Services/NativeMethods.cs` (L274,275: ClientToScreen). *(2)* **Có**, gọi hàm `RecognizeFromBitmap(bmp)`. *(3)* Mutable `_lastValues`, không lock.
- `Services/PopOcrService.cs:PopOcrService` - Nhận diện dân số hiện tại và giới hạn nhà (vd: 24/28).
  *(1)* Dùng `Services/NativeMethods.cs` (L281,282). *(2)* **Có**, `RecognizeFromBitmap` hoặc `ParsePopString`. *(3)* Static parsing, không lock.
- `Services/TimerOcrService.cs:TimerOcrService` - Nhận diện đồng hồ phút:giây trong trận đấu.
  *(1)* Dùng `Services/NativeMethods.cs` (L282,283). *(2)* **Có**, `RecognizeFromBitmap` hoặc `ParseTimerString`. *(3)* Mutable `_lastValues`, không lock.
- `Services/LoadingOcrService.cs:LoadingOcrService` - Nhận diện tỉ lệ tải trận (%) tại góc dưới.
  *(1)* Dùng `Services/NativeMethods.cs` (L332,333). *(2)* **Có** (`Program.cs:Main` có cờ `--test-loading`). *(3)* Lock `_lock` (L106) bảo vệ danh sách templates.
- `Services/UnitQueueOcrService.cs:UnitQueueOcrService` - Nhận diện số lượng quân đang xếp hàng ở 5 ô.
  *(1)* Dùng `Services/NativeMethods.cs` (L345,346). *(2)* **Có** (`Program.cs:Main` có cờ `--test-queue`). *(3)* Lock `_lock` (L107) bảo vệ mẫu số.
- `Services/ChatDetectionService.cs:ChatDetectionService` - Quét pixel nhận diện khung chat ingame mở hay đóng.
  *(1)* Dùng `Services/NativeMethods.cs` (L280,281,285). *(2)* **Có** khi truyền Bitmap hoặc giả lập toạ độ. *(3)* Lock `_scanLock` (L242), cờ mutable `_isInChat`.

## Luồng dữ liệu
- Chụp ảnh màn hình: Timer định kỳ hoặc On-Demand → Lấy toạ độ client qua `Services/NativeMethods.cs` → BitBlt Bitmap → Binarize/Match templates → Bắn Event `On*Updated` về `MainForm.cs:MainForm` và `MiniHudForm.cs:MiniHudForm`.

## Phụ thuộc
- Dùng: `input-native` (`Services/NativeMethods.cs`, `Services/AoeWindowHelper.cs`).
- Được dùng bởi: `ui` (hiển thị), `core-engine` (ra quyết định click xin quân ở `OnKeyAction`).

## Bẫy / lưu ý
- Độ phân giải hoặc tỷ lệ scale Windows (DPI) khác 100% làm lệch toạ độ vùng crop; cần bật DPI Unaware hoặc scale toạ độ.
- Nhận diện ký tự nhạy cảm với ngưỡng sáng (Threshold); ảnh mẫu lưu trong `Templates/` phải khớp đúng font game.
