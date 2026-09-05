# Danh sách nguy cơ xung đột (Overlap) phím nóng - AOE Keyboard Macro Pro

Tài liệu này lưu trữ các nguy cơ trùng lặp phím hoặc phản ứng không mong muốn của Macro trong quá trình chơi game AoE để người sử dụng nắm bắt và kiểm soát.

---

## 1. Nguy cơ xung đột giữa cấu hình quân nhanh và gán đạo (Control Groups)
* **Mô tả**: Khi người chơi nhấn một phím xây dựng đơn (`A`, `S`, `D`, `Z`, `X`, `C`) để mở móng nhà, macro sẽ chuyển `_configState = 1` để chờ cấu hình.
* **Nguy cơ**: Nếu người chơi thực hiện gán đạo hoặc chọn đạo bằng phím số (ví dụ: gán đạo bằng `Ctrl + 1..9` hoặc chọn đạo bằng `1..9`) trong vòng 3 giây ngay sau đó mà không click chuột trước, phím số đầu tiên sẽ bị macro nuốt để chuyển sang `_configState = 2`, phím số thứ hai sẽ bị nuốt để xác nhận số lượng quân.
* **Cách kiểm soát**: 
  - Đảm bảo thực hiện click chuột để đặt móng nhà (thao tác click chuột sẽ tự động reset trạng thái cấu hình về `0`) trước khi sử dụng các phím số.
  - Nếu nhấn phím xây dựng nhưng đổi ý không xây nữa, hãy bấm click chuột bất kỳ (trái/phải/giữa) ra khoảng trống để reset trạng thái cấu hình, tránh phím số bị chặn.

---

## 2. Nguy cơ xung đột các phím lệnh mặc định của quân lính
* **Tấn công (Attack-Move - Phím `A`)**:
  - `A` bị remap thành xây nhà bắn cung BA (`B -> A`).
  - **Cách kiểm soát**: Dùng chuột để click icon tấn công trên màn hình hoặc chấp nhận không sử dụng phím nóng `A` để điều quân công.
* **Dừng quân (Stop - Phím `S`)**:
  - `S` bị remap thành xây nhà ngựa BL (`B -> L`).
  - **Cách kiểm soát**: Dùng chuột click nút dừng quân trên giao diện hoặc di chuyển quân liên tục thay cho phím dừng.
* **Xin dân (Train Villager - Phím `C`)**:
  - Phím `C` đã bị remap thành xây nhà phù thủy (`B -> P`).
  - **Cách kiểm soát**: Sử dụng phím **`` ` ``** (gửi `H -> C`: chọn nhà chính rồi xin dân) hoặc phím **`J`** (gửi `C` trực tiếp) thay thế cho phím `C` khi cần xin dân ở nhà chính Town Center. Lưu ý phím **`Q`** hiện tại đã được chuyển sang chức năng **Vẫy E**.

---

## 3. Nguy cơ dịch chuyển Camera đột ngột khi làm mới ruộng
* **Mô tả**: Tổ hợp phím làm mới ruộng `Shift + F` / `Shift + G` sẽ gửi phím `SPACE` ở cuối chuỗi lệnh để nhảy camera về ruộng vừa làm mới.
* **Nguy cơ**: Khi đang điều quân chiến đấu ở xa, việc nhấn làm mới ruộng sẽ kéo camera giật về ruộng nhà chính, gây mất tập trung hoặc mất dấu trận đánh.
* **Cách kiểm soát**: Người chơi chủ động làm mới ruộng vào các thời điểm không cần micro quân căng thẳng, hoặc tắt còi báo bằng cách bấm phím trước khi ruộng hết hạn.

---

## 4. Chiếm dụng phím Windows hệ thống
* **Mô tả**: Phím Windows bị chặn hoàn toàn để gán cho chuỗi macro kích đời 3 nhanh (Lần 1: `H -> C -> 2 -> SPACE -> B -> M`, Lần 2: `3 -> SPACE -> B -> A`, Lần 3: `ESC -> 3 -> SPACE -> B -> L`).
* **Nguy cơ**: 
  - Không thể sử dụng các phím tắt hệ thống của Windows (`Win + D`, `Win + L`, v.v.) khi game đang active và macro đang bật.
  - Nếu bấm nhầm phím Windows khi định bấm `Ctrl` hoặc `Alt`, macro sẽ chạy chuỗi phím kích đời ngoài ý muốn.
* **Cách kiểm soát**: Hạn chế bấm nhầm vào khu vực phím Windows, tắt macro bằng `F1` trước khi cần chuyển ứng dụng thủ công bằng phím Windows.
