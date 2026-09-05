# Đề xuất cải tiến dự án AOE Keyboard Macro Pro

Dưới đây là 5 đề xuất cải tiến kỹ thuật và tính năng nhằm tối ưu hóa hiệu năng chơi game, giải quyết các rủi ro hiện tại và nâng cao khả năng chống quét cản (anti-detection) của Macro.

---

## 1. Tùy chọn Di chuyển Camera khi Làm mới Ruộng (Optional Camera Jump)

### Vấn đề hiện tại
* Trong [nguy_co.md](file:///d:/WorkSpace/Macro/AI%20env/nguy_co.md#L29-L34), việc nhấn làm mới ruộng (`Shift + F` hoặc `Shift + G`) sẽ gửi phím `SPACE` ở cuối để đưa camera về vị trí đạo ruộng đó. Điều này gây gián đoạn lớn khi người chơi đang điều quân chiến đấu ở xa (micro quân).

### Giải pháp đề xuất
* **Giao diện**: Thêm checkbox `"Lướt camera về ruộng khi làm mới"` trên GUI (mặc định bật).
* **Mã nguồn**: 
  - Thêm thuộc tính cấu hình `EnableCameraJump` vào `AppSettings`.
  - Trong [KeyboardHook.cs](file:///d:/WorkSpace/Macro/KeyboardHook.cs) tại hàm `HandleFarmRefresh` và `HandleFarmRefresh2`, chỉ gửi phím `SPACE` (scan code `0x39`) nếu cấu hình này được bật. Nếu tắt, chuỗi lệnh chỉ gửi `ESC -> n -> S` để làm mới ruộng trong âm thầm mà không giật camera.

---

## 2. Trễ ngẫu nhiên chống Anti-Cheat (Anti-Detection Jitter Delay)

### Vấn đề hiện tại
* Các giải đấu hoặc nền tảng chơi game trực tuyến (như EGoplay, GTV) sử dụng hệ thống chống phần mềm thứ ba. Nếu khoảng cách giữa phím nhấn xuống (KeyDown) và phím nhả ra (KeyUp) luôn cố định là `5ms` hoặc `10ms` (do `Thread.Sleep(5)` cố định), hệ thống chống cheat có thể phát hiện mẫu macro tự động.

### Giải pháp đề xuất
* **Mã nguồn**: 
  - Triển khai một cơ chế trễ ngẫu nhiên siêu nhỏ (Jitter Delay) trong hàm mô phỏng phím bấm và mouse click.
  - Thay vì `Thread.Sleep(5)` cố định, hãy dùng `Thread.Sleep(5 + Random.Shared.Next(0, 8))` hoặc tương tự.
  - Điều này giúp mô phỏng thao tác bấm của con người chân thật hơn rất nhiều mà không làm giảm đáng kể tốc độ của macro.

---

## 3. Cảnh báo sớm Ruộng sắp hết hạn (Early Warning Alarms)

### Vấn đề hiện tại
* Bộ đếm ngược ruộng hiện nay chỉ phát còi báo khi đã chạm về `0` (ruộng đã hết hạn). Lúc này nông dân đã dừng làm việc và bắt đầu đứng chơi (idle), làm giảm tốc độ phát triển kinh tế.

### Giải pháp đề xuất
* **Tính năng**: Thêm chức năng cảnh báo sớm ruộng hết hạn.
* **Triển khai**: 
  - Thêm thông số `"Cảnh báo sớm"` trên GUI (ví dụ: trước 10 giây hoặc 15 giây).
  - Khi bộ đếm ngược đạt đến mốc cảnh báo sớm, hệ thống sẽ phát một tiếng bíp đơn nhẹ (ví dụ: `Console.Beep(600, 100)`) để người chơi biết và chủ động bấm `Shift + F/G` làm mới ruộng sớm vào những lúc đang rảnh tay, thay vì đợi nông dân đứng chơi mới xử lý.

---

## 4. Tự động tương thích Độ phân giải & Phiên bản Game (Game Profiles & Aspect Ratio)

### Vấn đề hiện tại
* Các hàm click mở bảng ngoại giao, kinh tế, quân sự (`ClickDiplomacy`, `ClickEconomy`, v.v.) đang dùng tỉ lệ tọa độ phần trăm cố định của cửa sổ game.
* Giao diện của **Age of Empires 1 Classic** (bản gốc trên EGoplay/GTV) khác biệt hoàn toàn so với **Age of Empires: Definitive Edition** (bản Steam/Microsoft Store). Ngoài ra tỉ lệ màn hình Ultrawide 21:9 hay màn hình dọc cũng làm sai lệch tọa độ click chuột.

### Giải pháp đề xuất
* **Mã nguồn**: 
  - Thêm tùy chọn Dropdown `"Phiên bản Game"` trên GUI: `AoE 1 Classic` và `AoE: DE`.
  - Thay đổi hệ số tọa độ click tương ứng với từng phiên bản game.
  - Cho phép tinh chỉnh độ lệch tọa độ click (`Offset X`, `Offset Y`) trong file `config.json` để người dùng màn hình đặc biệt có thể tự sửa đổi mà không cần build lại code.

---

## 5. Quản lý nhiều cấu hình phím tắt (Multi-Profile Management)

### Vấn đề hiện tại
* Tùy thuộc vào bản đồ, thể loại chơi (đánh team, solo) hoặc loại quân đang chơi (ví dụ: quân Shang dân rẻ cần xin liên tục từ đầu, quân Assyrian đi nhanh, v.v.), người chơi sẽ muốn các thiết lập thời gian nhắc ruộng, phím tắt hoặc số lượng xin quân nhanh khác nhau.

### Giải pháp đề xuất
* **Giao diện**: Thêm một ComboBox chọn Profile ở góc Dashboard (ví dụ: `Default`, `Shang Fast`, `Assyrian Micro`, `Team Game`).
* **Mã nguồn**:
  - Lưu trữ các profile cấu hình trong file `config.json` dưới dạng một mảng các Profile.
  - Khi người chơi chọn đổi profile trên UI, toàn bộ cấu hình hotkeys, thời gian ruộng và xin quân sẽ được cập nhật và áp dụng ngay lập tức mà không cần khởi động lại ứng dụng.
