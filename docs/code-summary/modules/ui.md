# Module: ui
> `MainForm.cs`, `MainForm.Designer.cs`, `MiniHudForm.cs`, `Program.cs` · ~2.6k dòng · cập nhật 2026-10-06 · [tổng quan](../PROJECT.md)

**Trách nhiệm:** Giao diện điều khiển chính MainForm, lớp phủ MiniHudForm hiển thị trên game, và điểm khởi động Program.
**Không làm:** Không trực tiếp cài đặt hook bàn phím/chuột hay thực thi lệnh SendInput (ủy quyền cho Services).

## Điểm vào / giao diện công khai
- `Program.cs:Main` - Điểm khởi động ứng dụng, bật timer 1ms, xử lý cờ CLI `--test-loading`, `--test-queue`.
- `MainForm.cs:MainForm` - Cửa sổ điều khiển trung tâm, cấu hình phím tắt, hiển thị log và thông số OCR.
- `MiniHudForm.cs:MiniHudForm` - Form trong suốt không viền (TopMost/Layered) phủ thông số tài nguyên, pop, timer lên game.

## Logic nghiệp vụ nằm trong MainForm.cs
- `MainForm.cs:326` - Tính thời gian hoãn cảnh báo pop còn lại (`TotalSeconds`) khi toggle HUD.
- `MainForm.cs:371` - Giới hạn (`Math.Clamp`) và lưu khoảng cách giãn cách đếm ruộng vào `ConfigService`.
- `MainForm.cs:498` - Chuyển đổi trạng thái InGame, điều khiển làm mờ dữ liệu (`_isDataDimmed`) và reset HUD.
- `MainForm.cs:652` - Hợp nhất và lưu giá trị tài nguyên mới nhất (`_lastKnownResources`).
- `MainForm.cs:666` - Xử lý sự kiện xây nhà BE, hoãn cảnh báo pop trong 20 giây (`_popSuppressedUntil`).
- `MainForm.cs:704` - Reset toàn bộ dữ liệu, bộ đếm ruộng và gọi `Reset()` trên cả 6 dịch vụ OCR khi bấm F5.
- `MainForm.cs:965` - Chuyển tiếp tiến độ loading (`NotifyLoadingProgress`) từ OCR sang engine.
- `MainForm.cs:1000` - Chuyển tiếp sự kiện tải trận xong (`NotifyLoadingCompleted`) sang engine.
- `MainForm.cs:1047` - Nhận diện số lượng xin quân và chuyển sang engine (`NotifyUnitQueue`).

## Luồng dữ liệu
- Khởi động: `Program.cs:Main` → `CrashLogger.Initialize` → `NativeMethods.TimeBeginPeriod` → `MainForm.cs:MainForm`.
- Cập nhật OCR: OCR Service Event → `MainForm.cs:MainForm` (UI thread invoke) → `MiniHudForm.cs:MiniHudForm`.
- Phím tắt & Trạng thái: `MainForm.cs:MainForm` ↔ `Services/ControlEngine.cs:ControlEngine` điều khiển Start/Stop/Toggle.

## Phụ thuộc
- Dùng: `core-engine` (điều khiển macro), `ocr-vision` (nhận diện), `shared-utils` (lưu cài đặt).
- Được dùng bởi: Điểm khởi đầu của ứng dụng (OS gọi `Program.cs:Main`).

## Bẫy / lưu ý
- Mọi cập nhật UI từ hook hoặc OCR chạy trên luồng nền bắt buộc qua `InvokeRequired` / `BeginInvoke`.
- Thuộc tính TopMost của `MiniHudForm` cần ghim định kỳ để không bị game AOE đè mất khi chuyển cửa sổ.
