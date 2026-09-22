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
Dự án được viết theo cấu trúc tối giản và module hóa cao:
1. **[Program.cs](file:///d:/WorkSpace/Win01_AOE-Macro-v2/Program.cs)**: Khởi tạo cấu hình và chạy Form chính `MainForm`.
2. **[MainForm.cs](file:///d:/WorkSpace/Win01_AOE-Macro-v2/MainForm.cs)**: Giao diện Dark Theme hiện đại, hiển thị bảng trạng thái, còi đếm hạn ruộng, nhật ký hoạt động.
3. **[Services/ControlEngine.cs](file:///d:/WorkSpace/Win01_AOE-Macro-v2/Services/ControlEngine.cs)**: Trung tâm điều phối toàn bộ luồng sự kiện bàn phím/chuột cấp thấp, quản lý trạng thái kích hoạt, lọc cửa sổ game và chat.
4. **[Services/FastBuildManager.cs](file:///d:/WorkSpace/Win01_AOE-Macro-v2/Services/FastBuildManager.cs)**: Quản lý tính năng Xây 14 loại nhà nhanh (Tap gọi móng, Hold lặp theo click chuột trái, chuyển nhà tự chèn ESC).
5. **[Services/VayEManager.cs](file:///d:/WorkSpace/Win01_AOE-Macro-v2/Services/VayEManager.cs)**: Quản lý tính năng Vẩy E (`CTRL + E`).
6. **[Services/DeleteManager.cs](file:///d:/WorkSpace/Win01_AOE-Macro-v2/Services/DeleteManager.cs)**: Quản lý tính năng Xóa nhanh đối tượng liên tục tốc độ cao bằng chuột giữa.
7. **[Services/MouseLockManager.cs](file:///d:/WorkSpace/Win01_AOE-Macro-v2/Services/MouseLockManager.cs)**: Cơ chế khóa chuột bằng `ClipCursor` và bù trừ chuyển động bằng Raw Input (`WM_INPUT`).
8. **[Services/InputSimulator.cs](file:///d:/WorkSpace/Win01_AOE-Macro-v2/Services/InputSimulator.cs)**: Gói API Windows `SendInput` ở cấp độ phần cứng (dùng Scan Codes) để mô phỏng phím và chuột chống bị DirectX chặn.
9. **[Services/FarmTimerManager.cs](file:///d:/WorkSpace/Win01_AOE-Macro-v2/Services/FarmTimerManager.cs)** & **[Services/MidiPlayer.cs](file:///d:/WorkSpace/Win01_AOE-Macro-v2/Services/MidiPlayer.cs)**: Quản lý bộ đếm hạn ruộng song song và âm thanh cảnh báo / âm hiệu chiến thuật.

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

### 3.3. Chức năng: Xây các loại nhà nhanh (Fast Building)
* **Cơ chế hoạt động**:
  * **Nhấn phím đơn (Tap)**: Thực thi phím xây dựng tương ứng `[B -> Key]`. Trỏ chuột hiển thị móng nhà để người chơi click chuột trái đặt móng. Khi click đặt móng xong, kết thúc trạng thái đặt nhà.
  * **Nhấn giữ phím (Hold)**: Bắt đầu gửi `[B -> Key]`, sau đó mỗi lần người dùng click chuột trái (đặt móng), hệ thống tự động nhả chuột hộ người chơi và gửi tiếp `[B -> Key]` để lấy móng mới. Vòng lặp chờ click chuột -> gọi móng mới này duy trì liên tục cho tới khi người dùng nhả phím.
  * **Sau khi nhả giữ phím**: Tự động gửi phím `[ESC]` để hủy móng thừa đang lơ lửng trên con trỏ chuột.
  * **Hủy chức năng xây nhanh bằng chuột phải**: Nếu đang trong chế độ giữ phím xây nhanh (hoặc đang có móng), khi người dùng click chuột phải, macro tự động ấn `[ESC]` để hủy móng và thực thi `[Click Chuột Phải]` sạch sẽ cho người chơi (di chuyển/tấn công).
  * **Chuyển đổi loại nhà**: Khi đang trong trạng thái xây một loại nhà mà đổi sang bấm phím xây nhà khác, hệ thống tự động gửi `[ESC]` trước để hủy móng cũ rồi mới gửi lệnh xây nhà mới.
  * **Hủy móng**: Click chuột phải, nhấn ESC hoặc chuyển cửa sổ sẽ hủy móng và reset trạng thái về bình thường.
* **Chi tiết 14 phím xây nhà**:
  * **Nhà dân sự**:
    * `E` ➡️ `B -> E` (Nhà Dân BE)
    * `R` ➡️ `B -> S` (Nhà Kho BS)
    * `T` ➡️ `B -> G` (Nhà Chứa Ruộng BG)
    * `V` ➡️ `B -> M` (Nhà Chợ BM)
    * `F` ➡️ `B -> F` (Ruộng BF - Đạo ruộng 1)
    * `G` ➡️ `B -> F` (Ruộng BF - Đạo ruộng 2)
    * `B` ➡️ `B -> C` (Nhà Chính BC)
    * `N` ➡️ `B -> N` (Nhà Chòi BN)
  * **Nhà quân sự**:
    * `A` ➡️ `B -> A` (BA - Nhà Bắn Cung)
    * `S` ➡️ `B -> L` (BL - Nhà Ngựa Chém)
    * `Z` ➡️ `B -> K` (BK - Nhà Chế Pháo)
    * `X` ➡️ `B -> Y` (BY - Nhà Xọc Xiên)
    * `D` ➡️ `B -> B` (BB - Nhà Lính Chùy)
    * `C` ➡️ `B -> P` (BP - Nhà Phù Thủy)

### 3.4. Remap Phím Đơn Tiện Ích & Vẩy E
* `` ` `` (Backtick / Oemtilde) ➡️ `S` (Dừng quân / Stop)
* `Q` ➡️ `C` (Xin dân C tại nhà chính)
* `W` ➡️ `H` (Chọn nhà chính H)
* **Vẩy E (`CTRL + E`)**:
  * Khi ấn giữ `CTRL + E` (nhả `E` nhưng vẫn giữ `CTRL`): `[7 -> B -> E]` (Chọn dân đạo 7 và gọi móng nhà dân BE).
  * Mỗi lần click chuột trái (hoặc bấm tiếp `E` khi vẫn giữ `CTRL`): `[Click -> S -> B -> E]` (Tự động click chuột trái đặt móng trước, phím `S` dừng dân không cho chạy ra xây móng, và phím `B -> E` lấy móng BE mới).
  * Cho tới khi thả `CTRL`: `[ESC -> Chọn lại đạo quân trước đó]` (Hủy móng BE đang treo và chọn lại đạo quân 1..6 đang điều khiển trước đó).
  * Hủy chuỗi khi bấm phím khác hoặc chuyển cửa sổ.

### 3.5. Quản lý Đạo ruộng (Farm Timer)
* **Đạo ruộng 1 (F)**:
  * Nhấn `Ctrl + F`: Gán và chọn đạo ruộng 1 (`Shift + n` rồi `Ctrl + n`, mặc định n = 7). Khởi động bộ đếm thời gian ruộng 1.
  * Nhấn `Shift + F`: Tắt còi báo ruộng 1, gửi chuỗi phím làm mới ruộng `[ESC -> n -> S -> SPACE]`, khởi động lại bộ đếm ruộng 1.
* **Đạo ruộng 2 (G)**:
  * Nhấn `Ctrl + G`: Gán và chọn đạo ruộng 2 (`Shift + m` rồi `Ctrl + m`, mặc định m = 8). Khởi động bộ đếm thời gian ruộng 2.
  * Nhấn `Shift + G`: Tắt còi báo ruộng 2, gửi chuỗi phím làm mới ruộng `[ESC -> m -> S -> SPACE]`, khởi động lại bộ đếm ruộng 2.
* Khi bộ đếm về 0: Hệ thống phát cảnh báo âm thanh bíp kép liên tục (trễ 10 giây mỗi lần) và hiển thị thông báo trạng thái đạo ruộng tương ứng trên giao diện.

### 3.6. Duyệt nhà & Xin quân nhanh
* **Duyệt nhà (Ctrl + Phím)**: Giữ `Ctrl` vật lý liên tục và gõ phím nóng nhà quân (`A`/`S`/`Z`/`X`/`D`/`C`) để duyệt qua các nhà binh tương ứng (BA/BL/BK/BY/BB/BP).
* **Xin quân nhanh (Alt + Phím)**: Nhấn `Alt + Phím nhà quân` để tự động xin quân theo số lượng đã cấu hình trên GUI (AA, SS, DD, ZZ, XX, CC).
* **Đạo quân nhanh (Shift + 1..6)**: Gửi `Shift + n -> Ctrl + n -> SPACE` để gom và đưa camera đến đạo quân nhanh.

### 3.7. Chuẩn bị kích đời 3 nhanh (Windows Key)
Phím `Windows` kích hoạt chuỗi macro 3 bước để chuẩn bị lên đời 3 nhanh:
* Nhấn lần 1: `H -> C -> 2 -> SPACE -> B -> M` (Nhà chính -> xin dân -> chọn đạo 2 -> Space -> đặt móng Chợ BM).
* Nhấn lần 2 (<=30s): `3 -> SPACE -> B -> A` (Chọn đạo 3 -> Space -> đặt móng nhà BA).
* Nhấn lần 3 (<=30s): `ESC -> 3 -> SPACE -> B -> L` (Hủy móng -> chọn đạo 3 -> Space -> đặt móng nhà BL).

### 3.8. Tab công nghệ (CTRL+TAB & TAB)
* **`CTRL + TAB`**: Gửi tổ hợp `CTRL + 9` để gán hoặc chọn đạo công nghệ 9.
* **`TAB`**:
  * Nhấn lần đầu: Gửi `9` để chọn đạo công nghệ 9.
  * Nhấn liên tiếp (<=20s): Gửi `TAB` để duyệt (xoay vòng chọn) các nhà công nghệ trong đạo 9.
  * Hủy chuỗi liên tiếp (quay lại lần đầu) khi nhấn phím khác bất kỳ, click chuột (trái, phải, giữa) hoặc quá 20s.
  * **Lưu ý**: Không chặn tính năng chuyển cửa sổ `ALT + TAB` của hệ điều hành.

### 3.9. Xin quân lẻ / Click biểu tượng lệnh (Numpad 1..5)
* **Phím `Numpad 1..5`**: Tự động click vào 5 ô biểu tượng hành động/xin quân lẻ ở thanh điều khiển phía dưới màn hình (`X = Offset + (slot - 1) * Width, Y = H - Offset`).
* **Cơ chế khóa chuột & bù di chuyển**: Áp dụng `MouseLockManager.ExecuteLockedAction`, ghim chuột vào đúng ô biểu tượng và chặn click người dùng trong 30ms thực thi, sau đó lập tức trả chuột về vị trí cũ kèm bù đắp toàn bộ chuyển động người dùng đã lia chuột.

### 3.10. Chức năng: Delete (Chuột giữa / Middle Mouse)
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