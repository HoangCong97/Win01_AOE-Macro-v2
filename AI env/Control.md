
## 1. Tổng quan dự án (Project Overview)
* **Tên dự án**: AOE Keyboard Macro Pro
* **Công nghệ**: C# (.NET 9.0) Windows Forms.
* **Mục tiêu**: Xây dựng công cụ macro phím/chuột cấp thấp phục vụ chơi game Đế Chế (Age of Empires) chống bị chặn/detect, tối ưu hóa các lệnh xây nhà nhanh, xin quân nhanh, quét đạo ruộng nhanh và có còi báo hiệu làm mới ruộng định kỳ.
* **File chạy**: Macro.exe` (Standalone lightweight win-x64 binary).
Đây là phần mềm macro, khi khởi động lên sẽ chiếm quyền điều khiển bàn phím ở mức cao nhất

## 2. Quy ước
**Ghi chú**
- User input: sử dụng dấu (...) để biểu thị input của user
- Macro output: sử dụng dấu [..]để biểu thị output của macro
- Ghi chú: sử dụng dấu *...* để thể hiện ghi chú
- Phím ấn đồng thời: sử dụng + để ấn đồng thời
- Phím tuần tự: sử dụng > hoặc -> để thể hiện ấn tuần tự
- Double tap: sử dụng >> để biểu thị ấn Double tap (<200ms)
- Ấn giữ: sử dụng - để biểu bị ấn giữ (ví dụ A-A)

## 3. Controll
** Chức năng: điều khiển**
- Khởi động Macro bằng (F1): [Enabled]
- Tạm dừng Macro bằng (F1): [Suspended]
- Khi đang [Enabled], nếu người dùng chuyển sang ứng dụng khác, thì Macro sẽ tự động Tạm dừng (Suspended), và tự động Bật lại (Enabled) khi người dùng quay lại trò chơi
- Khi đang [Enabled], nếu người dùng nhấn Enter để chat, thì Macro sẽ tự động Tạm dừng (Suspended). Khi người dùng nhấn Enter hoặc Esc để thoát khung chat, Macro sẽ tự động Bật lại (Enabled).
- Khi đang [Enabled], vô hiệu hóa công cụ gõ tiếng việt

**Chức năng: Khởi đầu nhanh**
(F2-F2): Liên tục [H -> C] cho tới khi nhả F2, sau khi nhả F2 thì [F4-F11]
*Nếu macro chưa bật, sẽ bật macro lên*

**Chức năng: Mở bảng ngoại giao**
(F3): Click nút Diplomacy (bảng ngoại giao) - Khóa chuột và ghim tại nút trong thời gian xử lý, sau khi xong trả chuột và bù vào chuyển động đã mất của người dùng.
(F4): Mở timeline [F10 -> Mũi tên xuống * 2 -> Enter] - Khóa chuột tại chỗ trong thời gian mở menu để tránh sai sót, sau khi xong trả chuột và bù vào chuyển động đã mất của người dùng. 

**Chức năng: Click 5 ô biểu tượng / Xin quân lẻ**
- (Numpad 1..5): Click vào ô biểu tượng lệnh số 1 đến 5 ở góc dưới giao diện game. Tự động khóa chuột và ghim tại ô biểu tượng trong thời gian xử lý, sau khi click xong trả chuột và bù vào chuyển động đã mất của người dùng. 

**Chức năng: Remap**
- (F12): [F3]
- (`): [S]
- (Q): [C]
- (W): [H]
- 
**Chức năng: Xây các loại nhà nhanh**
*Mô tả cơ chế hoạt động:*
- Nhấn phím đơn (Tap): Thực thi phím xây dựng tương ứng (Ví dụ E: [B -> E]). Trỏ chuột hiển thị móng nhà để người chơi click chuột trái đặt móng. Khi click đặt móng xong, kết thúc trạng thái đặt nhà.
- Nhấn giữ phím (Hold): Bắt đầu gửi [B -> Key] (Ví dụ E: [B -> E]), sau đó mỗi lần người dùng click chuột trái đặt móng, hệ thống tự động gửi tiếp [B -> Key] để lấy móng mới. Vòng lặp chờ click chuột -> gọi móng mới này duy trì liên tục cho tới khi người dùng nhả phím.
- Chuyển đổi loại nhà: Khi đang trong trạng thái xây nhà nhanh mà đổi sang bấm phím xây nhà khác, hệ thống tự động gửi [ESC] trước để hủy móng cũ rồi mới gửi lệnh xây nhà mới.
- Hủy móng: Khi người dùng click chuột phải, nhấn ESC hoặc chuyển cửa sổ, trạng thái xây nhà sẽ được hủy và reset về bình thường.

Chi tiết các phím remapping (14 phím):
- (E): [B -> E] (Nhà Dân BE)
- (R): [B -> S] (Nhà Kho BS)
- (T): [B -> G] (Nhà Chứa Ruộng BG)
- (V): [B -> M] (Nhà Chợ BM)
- (F): [B -> F] (Ruộng BF) *Lưu ý: Shift + F để refresh đạo ruộng 1*
- (G): [B -> F] (Ruộng BF) *Lưu ý: Shift + G để refresh đạo ruộng 2*
- (B): [B -> C] (Nhà Chính BC)
- (N): [B -> N] (Nhà Chòi BN)

- (A): [B -> A] (Nhà Bắn Cung BA)
- (S): [B -> L] (Nhà Ngựa Chém BL)
- (Z): [B -> K] (Nhà Chế Pháo BK)
- (X): [B -> Y] (Nhà Xọc Xiên BY)
- (D): [B -> B] (Nhà Lính Chùy BB)
- (C): [B -> P] (Nhà Phù Thủy BP)

**Chức năng: Duyệt nhà binh**
*Mô tả cơ chế hoạt động (Mẫu 2):*
- Cho phép người dùng duyệt qua (xoay vòng chọn) các nhà binh đã xây bằng tổ hợp phím CTRL + Phím.
- *Lưu ý:* Nếu người dùng giữ phím CTRL vật lý liên tục và nhấn phím chữ (ví dụ giữ CTRL và nhấn liên tục A, A, A...), hệ thống vẫn nhận diện chính xác và gửi từng lệnh duyệt nhà tương ứng.

Chi tiết các phím remapping:
- (CTRL + A): [CTRL + A] Duyệt nhà bắn cung
- (CTRL + S): [CTRL + L] Duyệt nhà ngựa chém 
- (CTRL + Z): [CTRL + K] Duyệt nhà chế pháo 
- (CTRL + X): [CTRL + Y] Duyệt nhà xọc xiên 
- (CTRL + D): [CTRL + B] Duyệt nhà lính chùy 
- (CTRL + C): [CTRL + P] Duyệt nhà phù thủy 

**Chức năng: Xin quân nhanh**
*Mô tả cơ chế hoạt động:*
- Cho phép người dùng duyệt qua (xoay vòng chọn) các nhà binh đã xây bằng tổ hợp phím SHIFT + Phím.
- *Lưu ý:* Nếu người dùng giữ phím SHIFT vật lý liên tục và nhấn phím chữ (ví dụ giữ SHIFT và nhấn liên tục A, A, A...), hệ thống vẫn nhận diện chính xác và gửi từng lệnh duyệt nhà binh tương ứng.

Chi tiết các phím remapping:
- (SHIFT + A): [CTRL + A] | Từ lần 2 trở đi, sẽ là [CTRL + A -> Click chuột]
- (SHIFT + S): [CTRL + L] | Từ lần 2 trở đi, sẽ là [CTRL + L -> Click chuột] 
- (SHIFT + Z): [CTRL + K] | Từ lần 2 trở đi, sẽ là [CTRL + K -> Click chuột]
- (SHIFT + X): [CTRL + Y] | Từ lần 2 trở đi, sẽ là [CTRL + Y -> Click chuột]
- (SHIFT + D): [CTRL + B] | Từ lần 2 trở đi, sẽ là [CTRL + B -> Click chuột] 
- (SHIFT + C): [CTRL + P] | Từ lần 2 trở đi, sẽ là [CTRL + P -> Click chuột]

**Chức năng: Tab công nghệ**
(CTRL+TAB): [SHIFT+9 -> CTRL+9]
(TAB): 9 | Nhấn từ lần 2 đến n: [TAB] 
# - Hủy chuỗi liên tục (quay về trạng thái lần đầu): 
  + Nhấn bất kỳ phím nào khác trên bàn phím.
  + Click bất kỳ nút nào trong 3 nút chuột (Trái, Phải, Giữa) của người dùng.
  + Quá 20 giây kể từ lần nhấn cuối cùng.
- Lưu ý: không chắn tính năng ALT+TAB



**Chức năng: Đạo ruộng nhanh**
*Mô tả cơ chế hoạt động:*
Cũ: 
- Hỗ trợ quản lý song song 2 đạo ruộng độc lập (Đạo 1 mặc định là đạo 7, Đạo 2 mặc định là đạo 8) giúp tối ưu hóa việc quản lý ruộng trong game.
- **Đạo ruộng 1 (phím F)**:
  + Nhấn phím (CTRL + F): Gán và chọn đạo ruộng 1 (mặc định là đạo 7) thông qua chuỗi phím [SHIFT + n -> CTRL + n]. Khởi động bộ đếm thời gian ruộng 1 (không reset nếu đã chạy).
  + Nhấn tổ hợp phím [ SHIFT + F ]: Tắt âm thanh cảnh báo của ruộng 1, đồng thời tự động chạy chuỗi phím làm mới ruộng [n -> S -> SPACE]. Khởi động lại bộ đếm ruộng 1.
- **Đạo ruộng 2 (phím G)**:
  + Nhấn phím (CTRL + G): Gán và chọn đạo ruộng 2 (mặc định là đạo 8) thông qua chuỗi phím [SHIFT + m -> CTRL + m]. Khởi động bộ đếm thời gian ruộng 2 (không reset nếu đã chạy).
  + Nhấn tổ hợp phím [ SHIFT + G ]: Tắt âm thanh cảnh báo của ruộng 2, đồng thời tự động chạy chuỗi phím làm mới ruộng [m -> S -> SPACE]. Khởi động lại bộ đếm ruộng 2.
- *Nhắc nhở ruộng hết hạn:* Khi bất kỳ đạo ruộng nào đếm ngược về 0, hệ thống phát cảnh báo âm thanh bíp kép liên tục, nhưng mỗi lần trễ 10 giây và hiển thị thông báo trạng thái đạo ruộng tương ứng trên giao diện.

Chi tiết phím remapping:
- (CTRL + F): [SHIFT + n -> CTRL + n] Gán và chọn đạo ruộng 1 (mặc định n = 7)
- (SHIFT + F): Tắt còi báo và tự động thực thi [ESC -> n -> S -> SPACE] để làm mới đạo ruộng 1
- (CTRL + G): [SHIFT + m -> CTRL + m] Gán và chọn đạo ruộng 2 (mặc định m = 8)7s 
- (SHIFT + G): Tắt còi báo và tự động thực thi [ESC -> m -> S -> SPACE] để làm mới đạo ruộng 2

Thời gian của bộ đếm: mặc định 200s, nếu set khác thì sẽ lưu theo giá trị mới dù có tắt app

Mới:
- **Đạo ruộng 1 (phím F)**:
  + (Nhấn phím CTRL + F): [7 -> CTRL down]
  + (Vẫn giữ CTRL, ấn F): [7]
  + (Nhả CTRL): [CTRL up]
  Khởi động bộ đếm thời gian ruộng 1 (không reset nếu đã chạy).
  *Lưu ý, người dùng sẽ không nhả phím CTRL, chỉ hủy liên tục khi người dùng nhả CTRL*
  + (SHIFT + F): Tắt còi báo và tự động thực thi [ESC -> 7 -> S -> SPACE] để làm mới đạo ruộng 1
  
- **Đạo ruộng 2 (phím G)**:
  + (Nhấn phím CTRL + G): [8 -> CTRL down]
  + (Vẫn giữ CTRL, ấn G): [8]
  + (Nhả CTRL): [CTRL up]
  Khởi động bộ đếm thời gian ruộng 2 (không reset nếu đã chạy).
  *Lưu ý, người dùng sẽ không nhả phím CTRL, chỉ hủy liên tục khi người dùng nhả CTRL*
  + (SHIFT + G): Tắt còi báo và tự động thực thi [ESC -> 8 -> S -> SPACE] để làm mới đạo ruộng 2

*Lưu ý khi làm mới ruộng (SHIFT + F / SHIFT + G):*
- Người dùng có thể giữ phím SHIFT liên tục: bấm SHIFT + F rồi bấm tiếp G (hoặc SHIFT + G rồi bấm tiếp F) mà không cần nhả phím SHIFT. Hệ thống tự nhận diện và thực thi làm mới liên tục cho từng đạo ruộng mà không bị mất phím hay xung đột.
- **Tự động nhả SHIFT khi click chuột phải thông minh (Smart Context)**: Cơ chế tự động nhả SHIFT khi click chuột phải chỉ kích hoạt khi người dùng vừa bấm `SHIFT + F` hoặc `SHIFT + G` để làm mới ruộng và đang giữ phím SHIFT (hiệu lực kéo dài đến 6 giây cho mỗi lần bấm làm mới). Khi người dùng nhả phím SHIFT hoặc trong mọi tình huống bình thường khác (không bấm làm mới ruộng), phím `SHIFT` vật lý giữ nguyên 100% chức năng gốc của game AOE (giữ SHIFT + click chuột phải để cắm cờ waypoint di chuyển/dò đường thoải mái mà không bị mất). Chuột trái vẫn giữ nguyên hành vi gốc để chọn cộng dồn quân/dân.

**Chức năng: Đạo quân nhanh**
(SHIFT + 1): [SHIFT + 1 -> Ctrl + 1 -> SPACE] (Số 1 có thể thay đổi tùy theo số đạo quân 1 -> 6)
(CTRL + `): [CTRL + 0]

**Chức năng: Chuẩn bị cho kích đời 3**
*Mô tả cơ chế hoạt động:*
- Phím (ALT) kích hoạt chuỗi macro 3 bước để chuẩn bị lên đời 3 nhanh:
  + Lần 1: Thực thi [H -> C -> 2 -> SPACE -> B -> M] để chọn nhà chính (H), xin dân/kích đời (C -> 2), giãn góc nhìn (SPACE) và đặt móng Chợ (B -> M).
  + Lần 2 (trong vòng tối đa 30 giây từ lần 1): Thực thi [3 -> SPACE -> B -> A] để chọn đạo 3, giãn góc nhìn và đặt móng nhà BA.
  + Lần 3 (trong vòng tối đa 30 giây từ lần 2): Thực thi [ESC -> 3 -> SPACE -> B -> L] để hủy móng đang chọn (ESC), chọn đạo 3, giãn góc nhìn và đặt móng nhà BL.
  + Quá 30 giây không nhấn phím ALT kế tiếp hoặc sau khi hoàn thành xong bước 3, máy trạng thái sẽ tự động reset về trạng thái ban đầu (Lần 1).
  *Cơ chế chống Menu Mode & tương thích tổ hợp:*
  - Nhấn thả (Tap) phím ALT đơn lẻ được chặn hoàn toàn trước khi tới Windows để loại bỏ triệt để hiện tượng treo game/vào Menu Mode và tránh lỗi tổ hợp ngoài ý muốn ALT + SPACE.
  - Các tổ hợp phím hệ thống như ALT + TAB, ALT + F4 vẫn được nhận diện và hoạt động trơn tru bình thường.

Chi tiết phím remapping:
- (ALT):
  + Nhấn lần 1: [H -> C -> 2 -> SPACE -> B -> M]
  + Nhấn lần 2 (<= 30s): [3 -> SPACE -> B -> A]
  + Nhấn lần 3 (<= 30s): [ESC -> 3 -> SPACE -> B -> L]

**Chức năng: Đặt cờ (Flag / Waypoint Mode)**
*Mô tả cơ chế hoạt động:*
- Nút (CAPS LOCK) đóng vai trò như một công tắc Bật/Tắt (toggle) chế độ Đặt cờ:
  + Khi BẬT CAPS LOCK: Hệ thống tự động kích hoạt giữ phím SHIFT ảo (`ShiftDown`), đồng thời chuyển 4 phím `A, W, S, D` thành 4 phím mũi tên (`Left, Up, Down, Right`) để người dùng dễ dàng lướt/cuộn góc nhìn bản đồ bằng tay trái.
  + Trong lúc chế độ đặt cờ đang bật, người dùng thoải mái click chuột phải trên bản đồ để cắm các mốc cờ tuần tra/dò đường (waypoint).
  + Khi kết thúc chuỗi đặt cờ:
    * Click Chuột Trái: Hệ thống tự động `ShiftUp` trước, sau đó chuyển cú click chuột trái này thành một cú nhấp Chuột Phải (để chỉ định điểm đến kết thúc chuỗi) và tự động TẮT chế độ CAPS LOCK.
    * Hoặc Bấm lại CAPS LOCK: Hệ thống tự động `ShiftUp`, nhả các phím mũi tên và đưa 4 phím `A, W, S, D` trở lại chức năng thông thường.

Chi tiết phím remapping:
- (CAPS LOCK): Toggle Bật/Tắt chế độ Đặt cờ (Giữ SHIFT down + AWSD -> 4 phím mũi tên).
- (Chuột Trái khi đang bật CAPS LOCK): ShiftUp -> Chuyển thành Chuột Phải -> Tắt CAPS LOCK.


**Chức năng: thiết lập thay đổi phím dynamic**
Người dùng muốn tùy biến các phím nên có thể thay đổi các phím so với hardcode ở bên trên
Tên chức năng: Phím mặc định
Xin dân C: J
Xin dân nhanh: `
Vẫy E: Q
Dừng quân: W
Xây nhà E: E
Xây nhà S: R
Xây nhà G: T
Xây nhà M: V
Xây nhà C: B
Xây nhà N: N
Nhà quân B: D
Nhà quân A: A
Nhà quân L: S
Nhà quân K: Z
Nhà quân Y: X
Nhà quân P: C
Ruộng F1: F
Ruộng F2: G

**Chức năng: Vẩy E**
*Khi (ấn giữ CTRL + E nhả E nhưng vẫn giữ CTRL): [7 -> B -> E], mỗi lần click sẽ là [Click -> S -> B -> E] cho tới khi thả CTRL, sẽ chọn lại phím đang có đạo quân trước đó*
*Lưu ý tổ chức code cho tốt*

**Chức năng: Delete**
(Click chuột middle): [CTRL + 6 -> Click chuột trái -> Delete -> 6]
(Click giữ chuột middle): [CTRL + 6 -> Click chuột trái -> Delete -> 6] (tốc độ cao)

**Chức năng: Xin quân lẻ**
*Đây là một chức năng cực kỳ phức tạp*

**Chức năng: Tự nhả chuột phải**
Khi người dùng click chuột phải, macro phải can thiệt tự động nhả chuột phải ngay lập tức 1-2ms.
```