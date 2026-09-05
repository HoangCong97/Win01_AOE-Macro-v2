# Danh sách công việc nâng cấp AOE Keyboard Macro Pro (Đã hoàn thành)

- [x] **Triển khai Bộ lọc cửa sổ (Active Window Filtering)**
  - [x] Import `GetForegroundWindow` và `GetWindowText` vào [KeyboardHook.cs](file:///d:/WorkSpace/Macro/KeyboardHook.cs)
  - [x] Viết hàm kiểm tra `IsAoeWindowActive()` lọc theo các từ khóa `"Age of Empires"`, `"Empiresx"`, `"AoEDE"`
  - [x] Cập nhật `HookCallback` và `MouseHookCallback` để bypass phím khi game không active
  - [x] Báo trạng thái lên UI và ghi Log khi Alt-Tab ra ngoài / vào lại game (Trạng thái `Unfocused`)

- [x] **Triển khai Cấu hình đạo ruộng**
  - [x] Thêm ComboBox chọn Đạo ruộng (1-9) vào [MainForm.cs](file:///d:/WorkSpace/Macro/MainForm.cs)
  - [x] Cập nhật mô tả phím tắt động trên GUI hiển thị theo Đạo ruộng được chọn
  - [x] Thêm biến `FarmGroup` và logic ánh xạ từ số đạo ruộng (1-9) sang phím số tương ứng (ví dụ: đạo 7 -> scan code 0x08)
  - [x] Thay thế các vị trí hardcode phím `7` (scan code `0x08`) bằng biến động trong [KeyboardHook.cs](file:///d:/WorkSpace/Macro/KeyboardHook.cs)
  - [x] Đảm bảo đồng bộ hóa cài đặt Đạo ruộng từ GUI xuống class `KeyboardHook`

- [x] **Đồng bộ hóa phím tắt và tính năng theo Control.md**
  - [x] Đổi phím mặc định `Xin dân nhanh` sang `` ` `` (Oemtilde) và `Vẫy E` sang `Q`.
  - [x] Cập nhật logic phím **Vẫy E**: Lần đầu ấn sẽ chọn đạo ruộng rồi xây BE, các lần nhấn liên tiếp sau chỉ thực hiện click chuột trái rồi xây BE trực tiếp để tối ưu thao tác.
  - [x] Mở rộng lưới phím tắt trên GUI từ 17 lên 18 phím để chứa phím mới.
  - [x] Căn chỉnh lại vị trí nút Reset mặc định và hướng dẫn trên UI.
  - [x] Thêm bộ định dạng phím thân thiện để hiển thị các phím đặc biệt (Oemtilde hiển thị thành `` ` ``).

- [x] **Chuẩn hóa môi trường AI (AI Environment Standardization)**
  - [x] Xóa file rỗng [Requiry.md](file:///d:/WorkSpace/Macro/AI%20env/Requiry.md) để tránh tốn token quét file.
  - [x] Cập nhật toàn bộ [README.md](file:///d:/WorkSpace/Macro/AI%20env/README.md) khớp chính xác với mã nguồn và logic remapping mới nhất.
  - [x] Sửa đổi [nguy_co.md](file:///d:/WorkSpace/Macro/AI%20env/nguy_co.md) để cập nhật các cảnh báo phím nóng bị lỗi thời.

- [x] **Triển khai Trễ ngẫu nhiên chống Anti-Cheat (Jitter Delay)**
  - [x] Viết hàm `RandomSleep(int milliseconds)` sinh độ trễ ngẫu nhiên `+-5 ms` (đảm bảo tối thiểu 1 ms).
  - [x] Thay thế toàn bộ `Thread.Sleep` mô phỏng phím/chuột bằng `RandomSleep` trong [KeyboardHook.cs](file:///d:/WorkSpace/Macro/KeyboardHook.cs).
  - [x] Cập nhật [README.md](file:///d:/WorkSpace/Macro/AI%20env/README.md) và [project_proposals.md](file:///C:/Users/congq/.gemini/antigravity-ide/brain/fd2070c6-2b3b-4331-a0ed-b76d313b6dc8/project_proposals.md).

- [x] **Lược bỏ tính năng Tự động gán nhà công nghệ vào đạo 9**
  - [x] Xóa hàm `AddToGroup9` và cờ `_pendingTechBuildingPlacement` trong [KeyboardHook.cs](file:///d:/WorkSpace/Macro/KeyboardHook.cs).
  - [x] Loại bỏ các logic check tự động kích hoạt gán đạo 9 khi đặt móng và click chuột trái.
  - [x] Cập nhật tài liệu [Control.md](file:///d:/WorkSpace/Macro/AI%20env/Control.md) và [README.md](file:///d:/WorkSpace/Macro/AI%20env/README.md) đồng bộ.


