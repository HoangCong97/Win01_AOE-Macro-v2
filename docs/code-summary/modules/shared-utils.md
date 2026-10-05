# Module: shared-utils
> `Models/*.cs`, `Services/ConfigService.cs`, `Services/CrashLogger.cs`, `Services/MidiPlayer.cs` · ~520 dòng · cập nhật 2026-10-06 · [tổng quan](../PROJECT.md)

**Trách nhiệm:** Lưu trữ mô hình dữ liệu (DTO), nạp/ghi cấu hình JSON `config.json`, ghi nhật ký lỗi ứng dụng `crash.log`, và phát âm thanh thông báo MIDI tổng hợp.
**Không làm:** Không chứa logic nghiệp vụ macro hay tương tác đồ hoạ game.

## Điểm vào / giao diện & Đánh giá dịch vụ
- `Models/AppSettings.cs:AppSettings` - Mô hình cấu hình HUD, phím tắt, thời gian giãn cách đếm ruộng.
- `Models/MacroState.cs:MacroState` - Enum các trạng thái vĩ mô: Disabled, Active, InGamePaused.
- `Services/ConfigService.cs:ConfigService` - Lưu/tải cấu hình JSON từ file `config.json`.
  *(1)* **Không** phụ thuộc NativeMethods/InputSimulator/Form. *(2)* **Có**, unit test đọc/ghi JSON thuần túy. *(3)* Lock static `_fileLock` (L10,68), đường dẫn `ConfigPath`.
- `Services/CrashLogger.cs:CrashLogger` - Bắt lỗi ngoại lệ chưa xử lý (`UnhandledException`).
  *(1)* **Không** phụ thuộc NativeMethods/InputSimulator/Form. *(2)* **Có**. *(3)* Lock static `_logLock` (L8,44) bảo vệ ghi file `crash.log`.
- `Services/MidiPlayer.cs:MidiPlayer` - Phát âm thanh tổng hợp (chuông báo ruộng, toggle đặt cờ) qua WinMM MIDI.
  *(1)* Dùng P/Invoke `winmm.dll` (L8-14), **Không** phụ thuộc NativeMethods/InputSimulator/Form. *(2)* **Có** (nhưng sẽ phát âm thanh ra loa hệ thống). *(3)* Lock static `_midiLock` (L21) và `_alarmLock` (L59).

## Luồng dữ liệu
- Nạp cấu hình: `MainForm.cs:MainForm` → `ConfigService.LoadSettings` → Trả về `AppSettings` → Cập nhật UI & Engine.
- Ghi lỗi: AppDomain UnhandledException → `CrashLogger.cs:CrashLogger` → Ghi log có lock vào `crash.log`.
- Báo động: Hết giờ ruộng / bật cờ → `MidiPlayer.Play*` → Gửi mã MIDI qua `midiOutShortMsg`.

## Phụ thuộc
- Dùng: `System.Text.Json`, WinMM (`winmm.dll`).
- Được dùng bởi: Toàn bộ hệ thống (`ui`, `core-engine`, `macro-features`).

## Bẫy / lưu ý
- Nếu `config.json` bị lỗi cú pháp, `ConfigService` tự tạo lại cấu hình mặc định (fallback), tránh làm ứng dụng crash.
- Phát âm thanh cảnh báo ruộng chạy trên luồng nền (`Thread`), cần cờ dừng để không bị phát lặp sau khi reset.
