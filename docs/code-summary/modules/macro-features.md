# Module: macro-features
> `Services/FastBuildManager.cs`, `Services/FarmTimerManager.cs`, `Services/MilitaryCycleManager.cs`, `Services/VayEManager.cs`, `Services/DeleteManager.cs`, `Services/GameStateWatcher.cs` · ~1.0k dòng · cập nhật 2026-10-06 · [tổng quan](../PROJECT.md)

**Trách nhiệm:** Thực thi các tính năng macro trong game: xây nhà nhanh 2 phím, hẹn giờ làm mới ruộng, duyệt nhà quân vòng lặp, vẩy E, xoá dân/nhà nhanh, và theo dõi trạng thái trận đấu.

## Thành phần chính & 3 câu hỏi kiểm định
- `Services/FastBuildManager.cs:FastBuildManager` - Xây nhà nhanh (E, R, T, V, F, G, B...).
  *(1)* Dùng `Services/InputSimulator.cs` (L150,153,199,221). *(2)* **Có**, mock được delegate `RunActionSync`. *(3)* `_stateLock` bảo vệ trạng thái phím chờ, đọc/ghi từ hook thread.
- `Services/FarmTimerManager.cs:FarmTimerManager` - Đếm ngược 2 đạo ruộng 7 và 8.
  *(1)* **Không** phụ thuộc NativeMethods/InputSimulator/Form. *(2)* **Có**, dùng `System.Timers.Timer` và event độc lập. *(3)* Mutable fields `_farm1Remaining`, `_farm2Remaining`, không lock.
- `Services/MilitaryCycleManager.cs:MilitaryCycleManager` - Duyệt xoay vòng nhà quân (A, S, Z, X, D, C).
  *(1)* Dùng `Services/InputSimulator.cs` (L84,133,135). *(2)* **Có**, test đếm chu kỳ phím nếu mock `RunActionSync`. *(3)* Mutable dict `_currentCounts`, không lock.
- `Services/VayEManager.cs:VayEManager` - Vẩy móng nhà E bảo vệ nông dân / bắt quân.
  *(1)* Dùng `Services/InputSimulator.cs` (L45,47,74,98). *(2)* **Có** cho logic trạng thái (nhưng action gửi input thật). *(3)* Mutable fields `_isActive`, `_villagerGroup`, không lock.
- `Services/DeleteManager.cs:DeleteManager` - Xoá nhanh đơn vị/nhà (gán đạo 6 -> click -> Delete -> đạo 6).
  *(1)* Dùng `Services/InputSimulator.cs` (L104,108,112,116). *(2)* **Có**, khởi tạo độc lập. *(3)* Mutable `_cancellationTokenSource`, không lock.
- `Services/GameStateWatcher.cs:GameStateWatcher` - Giám sát tiến trình game AOE đang chạy.
  *(1)* **Không** trực tiếp phụ thuộc (gọi qua `Services/AoeWindowHelper.cs`). *(2)* **Có**, gọi hàm `CheckState()`. *(3)* Mutable field `_isInGame`, event `OnInGameChanged`.

## Luồng dữ liệu
- Phím kích hoạt → `Services/ControlEngine.cs:ControlEngine` chuyển tiếp vào Manager tương ứng → Manager kiểm tra chu kỳ / timer → gọi `RunActionSync` bắn phím qua `Services/InputSimulator.cs:InputSimulator`.

## Phụ thuộc
- Dùng: `input-native` (`Services/InputSimulator.cs`, `Services/AoeWindowHelper.cs`).
- Được dùng bởi: `core-engine` (gọi xử lý sự kiện phím).

## Bẫy / lưu ý
- `FastBuildManager` có timeout 300ms giữa 2 phím; quá thời gian sẽ tự hủy trạng thái chờ.
- Đạo ruộng 1 và 2 dùng chung kênh bàn phím với game nên việc chuyển đạo gửi phím Esc/Ctrl cần độ trễ sleep 10ms để game nhận kịp.
