
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
- Bỏ chức năng (F1): Bật app lên là đã tự động hoạt động (Enabled), không cần nhấn phím kích hoạt.
- Nhận diện trạng thái InGame bằng thanh tài nguyên (thay vì tên cửa sổ):
  + Khi quét và đọc được thanh tài nguyên (Wood/Food/Gold/Stone): Tự động chuyển sang trạng thái Hoạt động [Active/Enabled].
  + Nếu thanh tài nguyên không đọc được quá 1s (chuyển sang ứng dụng khác, ra ngoài Menu, kết thúc trận đấu): Tự động chuyển sang Tạm dừng [Suspended].
  + Khi quay lại trận đấu và đọc lại được thanh tài nguyên: Tự động Bật lại [Active/Enabled].
- Cơ chế lưu giá trị cuối cùng và làm mờ (Dimmed State):
  + Thay vì hiện `--` khi không đọc được thông số, hệ thống luôn lưu giữ giá trị hợp lệ cuối cùng đã đọc được và tiếp tục hiển thị trên cả Form chính và Mini HUD.
  + Sau 1 giây không đọc được thanh tài nguyên, toàn bộ các giá trị con số: Tài nguyên (Gỗ, Thịt, Vàng, Đá), Đồng hồ (Timer) và Dân số (POP) sẽ đồng thời chuyển sang trạng thái làm mờ (Dimmed).
  + **Quy tắc làm mờ**: Không làm mờ đại lượng (các nhãn icon/tên như "🪵 Gỗ: ", "🥩 Thịt: ", "🪙 Vàng: ", "🪨 Đá: ", "👥 POP: ", "⏱️ Giờ: " luôn giữ nguyên màu sắc đặc trưng, rõ nét), **chỉ làm mờ duy nhất giá trị con số** của chúng.
  + Khi ở trạng thái làm mờ: Tuyệt đối không phát bất kỳ cảnh báo nào (tắt nhấp nháy viền đỏ Mini HUD, tắt nhấp nháy cảnh báo đè dân POP, tắt tô đậm màu sắc tài nguyên L1-L3, và tắt còi chuông âm thanh cảnh báo).
  + Khi nhận diện lại được tài nguyên in-game: Tất cả giá trị con số hiển thị trở lại màu sắc rõ nét bình thường và mở lại các cảnh báo theo ngưỡng.
- Cơ chế Cảnh báo Dân số (POP) và Tự động ngăn cảnh báo khi xây nhà BE:
  + Cảnh báo đè dân (chậm dân/cần xây nhà BE) xuất hiện trên Mini HUD bằng cách **nhấp nháy viền đỏ dày 8px** bao quanh HUD và nhấp nháy dòng POP đỏ rực theo phân tầng:
    * POP < 26: Cảnh báo khi thiếu 2 dân (`MaxPOP - CurrentPOP <= 2`).
    * POP < 50: Cảnh báo khi thiếu 4 dân (`MaxPOP - CurrentPOP <= 4`).
    * POP < 100: Cảnh báo khi thiếu 8 dân (`MaxPOP - CurrentPOP <= 8`).
    * 100 <= POP < 200: Cảnh báo khi thiếu 16 dân (`MaxPOP - CurrentPOP <= 16`).
    * POP >= 200: Đổi màu nền tím tĩnh báo kịch trần dân số, không nhấp nháy cảnh báo.
  + **Tự động ngăn cảnh báo 20 giây sau khi bấm xây nhà BE**:
    * Khi người dùng nhấn phím xây nhà BE (`E`) hoặc click đặt móng BE, hệ thống ghi nhận người chơi đã tiếp nhận cảnh báo và đang cho dân xây nhà BE.
    * Lập tức toàn bộ cảnh báo nhấp nháy viền đỏ và nhấp nháy ô POP trên Mini HUD sẽ bị **ngăn chặn / tạm ngắt trong đúng 20 giây** (trở lại màu sắc bình thường, không làm rối mắt người chơi). Con số POP vẫn tiếp tục hiển thị và cập nhật theo OCR.
    * Sau 20 giây, nếu dân số vẫn chạm ngưỡng đè dân (do chưa xây xong móng hoặc tiếp tục sinh thêm dân chạm trần mới), cảnh báo nhấp nháy sẽ **tự động kích hoạt trở lại**. Nếu trong 20 giây người chơi bấm xây thêm nhà BE mới, bộ đếm 20 giây sẽ được gia hạn lại tính từ thời điểm bấm mới nhất.
- Tất cả các map phím sẽ hoạt động khi ở trạng thái InGame [Active]. Khi ở ngoài InGame [Suspended], phím hoàn toàn không bị chặn, hoạt động nguyên bản của hệ thống.
- Khi đang InGame [Active], nếu người dùng nhấn Enter để chat, thì Macro sẽ tự động Tạm dừng (SuspendedChat) để gõ chữ tự do. Khi người dùng nhấn Enter (gửi chat) hoặc Esc (hủy chat) để thoát khung chat, Macro sẽ tự động Bật lại [Active/Enabled].
- Khi đang [Active], vô hiệu hóa phím Windows để tránh bấm nhầm văng game, đồng thời vô hiệu hóa công cụ gõ tiếng Việt.

**Chức năng: Khởi đầu nhanh**
- **Thủ công (F2-F2)**: Liên tục [H -> C] cho tới khi nhả F2, sau khi nhả F2 thì [F4-F11].
- **Tự động nhận diện (Auto Fast Start)**:
  - **Điều kiện kích hoạt**: Khi game đang trong trận (`IsInGame` - đọc được thanh tài nguyên), và OCR đọc được chính xác giá trị tài nguyên khởi đầu trận đấu là `Gỗ: 200, Thịt: 200` (200 Wood, 200 Food).
  - *Cơ chế chạy tự động*: Vì Macro đã bật sẵn khi khởi động app, dịch vụ OCR quét liên tục và tự động kích hoạt ngay khi vào trận mà người chơi không cần bấm F1 hay thao tác gì thêm.
  - **Thao tác thực thi**:
    1. Ấn phím: [F4 > F11].
    2. Sau đó thực hiện [H > C] nhanh 8 lần liên tiếp.
    3. Trong suốt thời gian thực thi chuỗi này: Vô hiệu hóa toàn bộ click chuột trái và chuột phải vật lý của người dùng để tránh thao tác nhầm / lệch focus.
    4. Ngay khi thực hiện xong 8 lần [H > C]: Tự động mở lại click chuột trái, phải và reset toàn bộ các bộ đếm / chuỗi phím về trạng thái sơ khai ban đầu.
  - **Cơ chế kiểm soát an toàn & chống spam**:
    - **Kiểm soát Timer**: Chỉ vô hiệu hóa khi đồng hồ game thực tế đang hiển thị trên màn hình và đã qua giai đoạn đầu (> 5 giây). Nếu chưa có timer trên màn hình (chưa bấm F11) hoặc timer đang ở thời điểm đầu trận (00:00 - 00:05), chức năng vẫn kích hoạt bình thường. Timer lưu trữ từ ván trước tuyệt đối không ảnh hưởng đến ván đấu mới.
    - **Kiểm soát trigger đè**: Không cho phép kích hoạt lặp nếu một chuỗi khởi đầu nhanh đang trong quá trình thực thi.
    - **Kiểm soát trigger lặp lại**: Chỉ cho phép chạy đúng 1 lần trong mỗi ván đấu. Chỉ kích hoạt lại nếu đã từng quan sát thấy tài nguyên thay đổi khác 200 Gỗ / 200 Thực (restart ván mới) hoặc thoát game vào lại.
    - **Giới hạn thời gian tối thiểu**: Khoảng cách tối thiểu giữa 2 lần kích hoạt là 5 giây.

**Chức năng: Mở bảng ngoại giao & Timeline**
(F3): Click nút Diplomacy (bảng ngoại giao) - Khóa chuột và ghim tại nút trong thời gian xử lý, sau khi xong trả chuột và bù vào chuyển động đã mất của người dùng.
(F4): Mở timeline [F10 -> Mũi tên xuống * 2 -> Enter] - Khóa chuột tại chỗ trong thời gian mở menu để tránh sai sót, sau khi xong trả chuột và bù vào chuyển động đã mất của người dùng. 

**Chức năng: Làm mới toàn bộ trạng thái (Refresh - Phím F5)**
- Khi ấn phím (F5), phần mềm sẽ đưa tất cả về trạng thái ban đầu:
  + **Bộ đếm ruộng**: Dừng và reset cả 2 đạo ruộng 1 và 2 về trạng thái ban đầu (`-1, -1`), tắt toàn bộ chuông còi cảnh báo bíp / MIDI alarm.
  + **Thông số tài nguyên, POP, Timer**: Xóa sạch toàn bộ thông số về dạng `--` (Gỗ: `--`, Thịt: `--`, Vàng: `--`, Đá: `--`, POP: `--/--`, Giờ: `--:--`), chuyển trạng thái giá trị về làm mờ (dimmed) trên cả Form chính và Mini HUD.
  + **Bộ nhớ đệm OCR**: Xóa toàn bộ cache nhận diện của các dịch vụ OCR (Resource, POP, Timer) để không bị kẹt số liệu từ ván trước.
  + **Trạng thái Macro**: Reset sạch sẽ chuỗi kích đời 3 (ALT), chuỗi Vẩy E, móng xây nhà nhanh, chế độ đặt cờ, cờ ngăn cảnh báo POP 20 giây và nhả sạch các phím phần cứng ảo nếu đang bị kẹt.

**Chức năng: Chế độ Test (Test Mode - Phím F6 Toggle)**
- Phím (F6) hoạt động như một công tắc Bật/Tắt (Toggle) chế độ thử nghiệm:
  + **Khi BẬT (F6)**: Toàn bộ key map (xây nhà nhanh, xin dân, vẩy E, làm mới ruộng, kích đời 3, đặt cờ, click chuột đặt móng, xóa đơn vị, v.v.) sẽ **HOẠT ĐỘNG NGAY CẢ KHI KHÔNG Ở IN-GAME** (có thể test tự do ngoài Desktop, Notepad, v.v.).
  + Giao diện phần mềm hiển thị trạng thái `🧪 TEST MODE (Bật ngoài game)` với màu tím hồng nổi bật để người dùng dễ nhận biết.
  + **Khi TẮT (Ấn lại F6)**: Phần mềm trở về chế độ thông thường, chỉ kích hoạt macro khi nhận diện được thanh tài nguyên in-game thực tế, khi ở ngoài game phím hoạt động nguyên bản của hệ thống. 

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
- (E): [B -> E] (Nhà Dân BE) *Lưu ý: Tự động ngăn cảnh báo POP trong 20s ngay sau khi bấm*
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