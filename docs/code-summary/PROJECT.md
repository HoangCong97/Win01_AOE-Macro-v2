# AOE Keyboard Macro Pro
> Tóm tắt cho AI/dev mới - đọc file này trước, chỉ mở modules/*.md khi cần. Cập nhật 2026-10-06 · commit 5f674d7 · project-summarizer

**Mục đích:** Ứng dụng macro bàn phím/chuột và trợ lý hiển thị HUD thời gian thực cho tựa game Age of Empires (AOE 1 RoR), tự động hoá các thao tác vi mô và giám sát thông số trận đấu qua OCR.
**Loại:** App Desktop Windows Forms (C# .NET 9.0).
**Stack (đã kiểm chứng):** C# 13 / .NET 9.0 Windows (`net9.0-windows`), WinForms, Win32 P/Invoke (`user32.dll`, `kernel32.dll`, `winmm.dll`), 0 thư viện NuGet bên ngoài.

## Kiến trúc
- **UI & HUD** (modules/ui.md): `MainForm.cs:MainForm` điều khiển trung tâm; `MiniHudForm.cs:MiniHudForm` overlay trong suốt hiển thị trên màn hình game.
- **Core Engine** (modules/core-engine.md): `Services/ControlEngine.cs:ControlEngine` đóng vai trò nhạc trưởng, lắng nghe hook, quản lý trạng thái `MacroState`, điều phối chu kỳ phím và timer.
- **Macro Features** (modules/macro-features.md): Các nghiệp vụ macro game: Xây nhà nhanh (FastBuild), đếm ruộng (FarmTimer), duyệt nhà quân (MilitaryCycle), vẩy E (VayE), xoá nhanh (Delete).
- **OCR Vision** (modules/ocr-vision.md): Chụp ảnh màn hình, nhị phân hoá và template matching nhận diện tài nguyên, pop, timer, loading, hàng đợi quân, khung chat.
- **Input & Native Interop** (modules/input-native.md): Hook toàn cục WH_KEYBOARD_LL / WH_MOUSE_LL, phát xung SendInput, khoá chuột RawInput.
- **Shared Utils** (modules/shared-utils.md): Cấu hình JSON, ghi log sự cố và chuông MIDI.

## Luồng chính
- Đánh chặn & thực thi phím: `Services/KeyboardHookManager.cs:KeyboardHookManager` → `Services/ControlEngine.cs:ControlEngine` → `Services/InputSimulator.cs:InputSimulator`.
- Quét thông số màn hình: Timer → `Services/ResourceOcrService.cs:ResourceOcrService` → `MainForm.cs:MainForm` & `MiniHudForm.cs:MiniHudForm`.
- Xin quân thông minh: Shift + Key → `Services/ControlEngine.cs:ControlEngine` → Quét `Services/UnitQueueOcrService.cs:UnitQueueOcrService` & `Services/LoadingOcrService.cs:LoadingOcrService` → `Services/InputSimulator.cs:InputSimulator`.

## Bản đồ module
| Module | Đường dẫn | Trách nhiệm | Chi tiết |
|---|---|---|---|
| ui | `MainForm.cs`, `MiniHudForm.cs`, `Program.cs` | Giao diện điều khiển chính, Mini HUD overlay, điểm vào ứng dụng | [modules/ui.md](modules/ui.md) |
| core-engine | `Services/ControlEngine.cs` | Trái tim điều phối macro, máy trạng thái, xử lý phím OnKeyAction | [modules/core-engine.md](modules/core-engine.md) |
| macro-features | `Services/FastBuildManager.cs`, `Services/FarmTimerManager.cs`, ... | Xây nhà nhanh, đếm ruộng, duyệt nhà binh, vẩy E, xoá dân/nhà | [modules/macro-features.md](modules/macro-features.md) |
| ocr-vision | `Services/ResourceOcrService.cs`, `Models/ResourceCropSettings.cs`, ... | Nhận diện tài nguyên, pop, timer, loading, hàng đợi quân, chat | [modules/ocr-vision.md](modules/ocr-vision.md) |
| input-native | `Services/NativeMethods.cs`, `Services/InputSimulator.cs`, ... | Hook bàn phím/chuột OS, SendInput, khoá chuột RawInput | [modules/input-native.md](modules/input-native.md) |
| shared-utils | `Models/AppSettings.cs`, `Services/ConfigService.cs`, ... | Mô hình dữ liệu DTO, lưu JSON config.json, crash logger, MIDI | [modules/shared-utils.md](modules/shared-utils.md) |

Phụ thuộc: `ui` → `core-engine` → `macro-features` & `ocr-vision` → `input-native`; `shared-utils` dùng chung toàn project.

## Quy ước toàn project
- Không phụ thuộc NuGet bên ngoài; mọi tương tác phần cứng và cửa sổ thông qua P/Invoke Win32 thủ công.
- Tác vụ tương tác phần cứng mô phỏng phải mang cờ `MACRO_EXTRA_INFO` (0xA0E9999) để bộ hook không tự đánh chặn chính nó.
- Cập nhật dữ liệu từ luồng OCR/Hook lên Form bắt buộc kiểm tra `InvokeRequired` và gọi `BeginInvoke`.

## Bẫy / lưu ý
- `Services/ControlEngine.cs` là god-class (1851 dòng) chứa phương thức `OnKeyAction` (765 dòng) mang nhiều trạng thái phụ thuộc thứ tự; rất dễ gây xung đột nuốt phím khi bổ sung tính năng mới.
- 0 unit test tự động trong toàn bộ dự án; kiểm thử phụ thuộc vào game AOE thật hoặc 2 cờ CLI test ảnh mẫu trong `Program.cs:Main`.
- Tốc độ chuột và phím phụ thuộc các hàm spin-wait / sleep (1-25ms); chạy trên máy có cấu hình hoặc DPI khác có thể gây hụt phím hoặc lệch toạ độ crop.

## Lệnh (đã kiểm chứng)
- Dev / Build: `dotnet build` (.NET SDK 9.0, 0 lỗi, 0 cảnh báo).
- Chạy ứng dụng: `dotnet run` hoặc mở file `.exe` đã build.
- Chạy test OCR mẫu tích hợp: `dotnet run -- --test-loading` và `dotnet run -- --test-queue`.

## ⚠️ Tài liệu lệch thực tế
- Chưa phát hiện mâu thuẫn lớn; manifest `.csproj` chỉ rõ target `net9.0-windows` và không có package NuGet ngoài.

## Cần bổ sung (người hiểu project điền)
- Bộ dữ liệu mẫu ảnh crop chuẩn cho các độ phân giải màn hình khác nhau (hiện phụ thuộc thư mục `Templates/`).
- Kế hoạch tái cấu trúc tách `OnKeyAction` và `MainForm` theo kiến trúc MVP/Clean Architecture.
