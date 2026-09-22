# AOE Keyboard Macro Pro - AI Environment & Project Summary

Tài liệu này tóm tắt toàn bộ dự án, cấu trúc mã nguồn và logic hoạt động để các AI thế hệ tiếp theo nắm bắt thông tin dự án tức thời khi làm việc trên codebase này.

---

## 1. Tổng quan dự án (Project Overview)
* **Tên dự án**: AOE Keyboard Macro Pro
* **Công nghệ**: C# (.NET 9.0) Windows Forms.
* **Mục tiêu**: Xây dựng công cụ macro phím/chuột cấp thấp phục vụ chơi game Đế Chế (Age of Empires) chống bị chặn/detect, tối ưu hóa các lệnh xây nhà nhanh, xin quân nhanh, quét đạo ruộng nhanh và có còi báo hiệu làm mới ruộng định kỳ.
* **File chạy**: Macro.exe` (Standalone lightweight win-x64 binary).

---

## 2. Cấu trúc mã nguồn (Codebase Structure)
Dự án được viết theo cấu trúc tối giản không dùng file Designer riêng biệt để tối ưu tốc độ đọc/ghi code:
1. **[Program.cs](file:///d:/WorkSpace/Macro/Program.cs)**: Khởi tạo cấu hình và chạy Form chính `MainForm`.
2. **[MainForm.cs](file:///d:/WorkSpace/Macro/MainForm.cs)**: Giao diện Dark Theme hiện đại. Chứa panel trạng thái, nhãn đếm ngược ruộng thời gian thực, khung hướng dẫn phím nóng, bảng cấu hình thời gian nhắc ruộng, và khung hiển thị Logs hoạt động.
3. **[KeyboardHook.cs](file:///d:/WorkSpace/Macro/KeyboardHook.cs)**:
   * **`KeyboardHook`**: Triển khai low-level hooks (`WH_KEYBOARD_LL` = 13, `WH_MOUSE_LL` = 14) qua API Windows. Quản lý hệ thống đếm ngược ruộng, beep âm thanh cảnh báo, và chứa toàn bộ logic remapping.
   * **`SendInputHelper`**: Gói API Windows `SendInput` ở cấp độ phần cứng để mô phỏng phím (dùng Scan Codes thay vì Virtual Keys nhằm tránh bị game DirectX bỏ qua) và click chuột.
   * **`HotkeySettings`**: Lưu trữ cấu hình 18 phím tắt động có thể tùy biến trên GUI và lưu vào `config.json`.

---

## 3. Chi tiết logic phím tắt & tính năng (Hotkey & Macro Specs)

### 3.1. Kích hoạt & Trạng thái (F1 Toggle & Window Filter & Utility keys)
* Phím **`F1`** bật/tắt toàn bộ macro. 
* Phím **`F12`**: Tạm dừng game (gửi phím pause F3 gốc).
* Phím **`F3`**: Click nút Diplomacy (bảng ngoại giao). Tự động khóa chuột (ghim tại nút ngoại giao và chặn bấm chuột nhầm) trong quá trình hover/click, sau khi hoàn thành sẽ trả lại chuột và bù trừ chính xác quãng đường di chuyển vật lý của người dùng.
* Phím **`F4`**: Mở timeline [`F10 -> Mũi tên xuống * 2 -> Enter`]. Tự động ghim chuột tại chỗ và chặn click trong thời gian gửi phím để tránh trôi trúng menu hoặc hủy menu game, sau đó trả chuột về vị trí đã bù trừ quãng đường di chuyển của người dùng.
* Khi nhấn **`Enter`** (để chat trong game): macro tự động chuyển sang **Tạm dừng (Suspended)** để gõ chữ bình thường. Khi nhấn `Enter` hoặc `Esc` để thoát chat, macro tự động bật lại.
* Khi Alt-Tab ra ngoài game (cửa sổ Age of Empires không active): macro chuyển sang **Tạm dừng (Unfocused)** và bypass toàn bộ phím tắt.

### 3.2. Khởi đầu nhanh (F2 giữ)
* Khi macro đang bật, nhấn giữ phím **`F2`** để liên tục gửi lệnh xin dân `H -> C` (H: Chọn nhà chính, C: Xin dân).
* Khi nhả phím **`F2`**, dừng xin dân và tự động gửi phím **`F4`** (Điểm số) và **`F11`** (Thời gian game) để hiển thị thông tin.

### 3.3. Remap Phím Đơn (Single Key building orders)
* `A` ➡️ `B -> A` (BA - Nhà bắn cung)
* `S` ➡️ `B -> L` (BL - Nhà ngựa chém)
* `Z` ➡️ `B -> K` (BK - Nhà chế pháo)
* `X` ➡️ `B -> Y` (BY - Nhà xọc xiên)
* `D` ➡️ `B -> B` (BB - Nhà lính chùy)
* `C` ➡️ `B -> P` (BP - Nhà phù thủy)
* `` ` `` (Backtick / Oemtilde) ➡️ `H -> C` (Xin dân nhanh từ nhà chính)
* `J` ➡️ `C` (Xin dân C trực tiếp tại nhà chính đang chọn)
* `W` ➡️ `S` (Dừng quân / Stop)
* **Vẩy E (`CTRL + E`)**:
  * Khi ấn giữ `CTRL + E` (nhả `E` nhưng vẫn giữ `CTRL`): `[7 -> B -> E]` (Chọn dân đạo 7 và gọi móng nhà dân BE).
  * Mỗi lần click chuột trái (hoặc bấm tiếp `E` khi vẫn giữ `CTRL`): `[Click -> S -> S -> B -> E]` (Tự động click chuột trái đặt móng trước, 2 lần phím `S` dừng dân chắc chắn không cho chạy ra xây móng, và phím `B -> E` lấy móng BE mới).
  * Cho tới khi thả `CTRL`: `[ESC -> Chọn lại đạo quân trước đó]` (Hủy móng BE đang treo và chọn lại đạo quân 1..6 đang điều khiển trước đó).
  * Hủy chuỗi khi bấm phím khác hoặc chuyển cửa sổ.
* Phím Mẫu 1 (`E`, `R`, `T`, `V`, `B`, `N`, `F`, `G`):
  * Lần đầu bấm: Xây `BE`, `BS`, `BG`, `BM`, `BC`, `BN`, `BF` (đạo 1), `BF` (đạo 2).
  * Nhấn liên tiếp: Tự động click chuột trái trước rồi gửi chuỗi phím xây dựng tương ứng.

### 3.4. Quản lý Đạo ruộng (Farm Timer)
* **Đạo ruộng 1 (F)**:
  * Nhấn `Ctrl + F`: Gán và chọn đạo ruộng 1 (`Shift + n` rồi `Ctrl + n`, mặc định n = 7). Khởi động bộ đếm thời gian ruộng 1.
  * Nhấn `Shift + F`: Tắt còi báo ruộng 1, gửi chuỗi phím làm mới ruộng `[ESC -> n -> S -> SPACE]`, khởi động lại bộ đếm ruộng 1.
* **Đạo ruộng 2 (G)**:
  * Nhấn `Ctrl + G`: Gán và chọn đạo ruộng 2 (`Shift + m` rồi `Ctrl + m`, mặc định m = 8). Khởi động bộ đếm thời gian ruộng 2.
  * Nhấn `Shift + G`: Tắt còi báo ruộng 2, gửi chuỗi phím làm mới ruộng `[ESC -> m -> S -> SPACE]`, khởi động lại bộ đếm ruộng 2.
* Khi bộ đếm về 0: Hệ thống phát cảnh báo âm thanh bíp kép liên tục (trễ 10 giây mỗi lần) và hiển thị thông báo trạng thái đạo ruộng tương ứng trên giao diện.

### 3.5. Duyệt nhà & Xin quân nhanh
* **Duyệt nhà (Ctrl + Phím)**: Giữ `Ctrl` vật lý liên tục và gõ phím nóng nhà quân (`A`/`S`/`Z`/`X`/`D`/`C`) để duyệt qua các nhà binh tương ứng (BA/BL/BK/BY/BB/BP).
* **Xin quân nhanh (Alt + Phím)**: Nhấn `Alt + Phím nhà quân` để tự động xin quân theo số lượng đã cấu hình trên GUI (AA, SS, DD, ZZ, XX, CC).
* **Đạo quân nhanh (Shift + 1..6)**: Gửi `Shift + n -> Ctrl + n -> SPACE` để gom và đưa camera đến đạo quân nhanh.

### 3.6. Chuẩn bị kích đời 3 nhanh (Windows Key)
Phím `Windows` kích hoạt chuỗi macro 3 bước để chuẩn bị lên đời 3 nhanh:
* Nhấn lần 1: `H -> C -> 2 -> SPACE -> B -> M` (Nhà chính -> xin dân -> chọn đạo 2 -> Space -> đặt móng Chợ BM).
* Nhấn lần 2 (<=30s): `3 -> SPACE -> B -> A` (Chọn đạo 3 -> Space -> đặt móng nhà BA).
* Nhấn lần 3 (<=30s): `ESC -> 3 -> SPACE -> B -> L` (Hủy móng -> chọn đạo 3 -> Space -> đặt móng nhà BL).

### 3.7. Tab công nghệ (CTRL+TAB & TAB)
* **`CTRL + TAB`**: Gửi tổ hợp `CTRL + 9` để gán hoặc chọn đạo công nghệ 9.
* **`TAB`**:
  * Nhấn lần đầu: Gửi `9` để chọn đạo công nghệ 9.
  * Nhấn liên tiếp (<=20s): Gửi `TAB` để duyệt (xoay vòng chọn) các nhà công nghệ trong đạo 9.
  * Hủy chuỗi liên tiếp (quay lại lần đầu) khi nhấn phím khác bất kỳ, click chuột (trái, phải, giữa) hoặc quá 20s.
  * **Lưu ý**: Không chặn tính năng chuyển cửa sổ `ALT + TAB` của hệ điều hành.

### 3.8. Xin quân lẻ / Click biểu tượng lệnh (Numpad 1..5)
* **Phím `Numpad 1..5`**: Tự động click vào 5 ô biểu tượng hành động/xin quân lẻ ở thanh điều khiển phía dưới màn hình (`X = Offset + (slot - 1) * Width, Y = H - Offset`).
* **Cơ chế khóa chuột & bù di chuyển**: Áp dụng `MouseLockManager.ExecuteLockedAction`, ghim chuột vào đúng ô biểu tượng và chặn click người dùng trong 30ms thực thi, sau đó lập tức trả chuột về vị trí cũ kèm bù đắp toàn bộ chuyển động người dùng đã lia chuột.

### 3.9. Chức năng: Delete (Chuột giữa / Middle Mouse)
* **Click chuột giữa**: Thực hiện 1 chu kỳ `[CTRL + 6 -> Click chuột trái -> Delete -> 6]`.
  * `CTRL + 6`: Lưu đơn vị/đạo quân đang chọn vào đạo 6.
  * `Click chuột trái`: Chọn đối tượng/móng nhà/ruộng dưới con trỏ chuột.
  * `Delete`: Xóa đối tượng vừa click.
  * `6`: Chọn lại đạo quân 6 ban đầu.
* **Click giữ chuột giữa**: Lặp lại chuỗi `[CTRL + 6 -> Click chuột trái -> Delete -> 6]` liên tục ở tốc độ cao (~10 lần/giây) cho phép người chơi vừa giữ vừa lia chuột để xóa liên hoàn nhiều móng/ruộng/tường thành cho tới khi nhả nút chuột giữa.

---

## 4. Các lưu ý kỹ thuật cho AI thế hệ sau (Technical Tips)
* **Tránh lặp vô hạn**: Tại hàm hook bàn phím, luôn kiểm tra flag `LLKHF_INJECTED` (0x10) từ `KBDLLHOOKSTRUCT.flags`. Nếu phím do macro tự phát ra thì lập tức chuyển tiếp bằng `CallNextHookEx` chứ không intercept/remap.
* **Quản lý phím Ctrl vật lý**: Trong các lệnh `Ctrl + Key` và `Ctrl + F/G`, việc mô phỏng phím bấm cần kiểm tra trạng thái phím Ctrl vật lý qua `GetKeyState(0x11)`. Nếu người chơi đang giữ phím Ctrl thì không được gửi lệnh nhả Ctrl giả lập bừa bãi tránh xung đột thao tác của người chơi.
* **Scan Codes vs Virtual Keys**: Khi gửi phím vào AOE, phải sử dụng Scan Codes (VD: `wScan` và cờ `KEYEVENTF_SCANCODE` = 0x0008). AOE sử dụng DirectInput nên sẽ bỏ qua các mô phỏng bằng Virtual Key đơn thuần.
* **Thời gian trễ ngẫu nhiên (Anti-Cheat Jitter)**: Để tránh bị phát hiện bởi các phần mềm chống gian lận (anti-cheat) quét dấu hiệu thời gian đều đặn cố định (ví dụ luôn là `5ms`), toàn bộ các lệnh trễ mô phỏng phím/chuột đã được thay thế bằng hàm `RandomSleep(int milliseconds)`, tự động tạo biến thiên ngẫu nhiên `+-5ms` (tối thiểu 1ms).
* **Khóa chuột & Bù trừ chuyển động bằng Raw Input (`MouseLockManager`)**:
  * Khi thực hiện các macro nhạy cảm với chuột như `F3` (click nút ngoại giao), `F4` (điều hướng menu Timeline F10), và `Numpad 1..5` (click ô biểu tượng xin quân lẻ): Macro sử dụng Win32 `ClipCursor` để ghim chuột vào tọa độ 1x1 pixel và chặn toàn bộ các click chuột vật lý trong `WH_MOUSE_LL` hook để tránh bấm nhầm làm hỏng thao tác.
  * Đồng thời, một cửa sổ ẩn toàn cục lắng nghe `WM_INPUT` (`RegisterRawInputDevices` với cờ `RIDEV_INPUTSINK`) vẫn liên tục nhận các xung dịch chuyển tương đối `(lLastX, lLastY)` từ cảm biến chuột vật lý bất chấp việc con trỏ màn hình đang bị `ClipCursor` ghim lại.
  * Khi macro kết thúc, hệ thống nhả `ClipCursor(IntPtr.Zero)` và tự động di chuyển con trỏ tới `(initialX + deltaX, initialY + deltaY)`, mang lại trải nghiệm lia chuột mượt mà và không bao giờ bị giật lùi vị trí của game thủ.

## 5. Chỉ cần build thành công, không cần test, người dùng sẽ test