# Module: core-engine
> `Services/ControlEngine.cs` · ~1.9k dòng · cập nhật 2026-10-06 · [tổng quan](../PROJECT.md)

**Trách nhiệm:** Trái tim điều phối macro: đánh chặn hook phím/chuột, máy trạng thái MacroState, timer chu kỳ và kích hoạt chuỗi macro.
**Không làm:** Không trực tiếp gọi API Win32 P/Invoke (ủy quyền qua NativeMethods và InputSimulator).

## Điểm vào / giao diện & Đánh giá dịch vụ
- `Services/ControlEngine.cs:ControlEngine` - Điểm điều phối trung tâm.
- *(1) Phụ thuộc*: `Services/InputSimulator.cs` (L247,337,1348,1366), `Services/NativeMethods.cs` (L1345: GetKeyState), Action callback MainForm (L853,923).
- *(2) Unit test ngoài*: **Không**. Khởi tạo gắn chặt với Windows Hook thật, Timer WinForms, MidiPlayer và HWND game.
- *(3) Trạng thái dùng chung*: Fields mutable không lock (`_currentState`, `_isTestMode`, `_activeFarmGroup`, `_ctrlFCount`), đọc/ghi từ UI thread và Hook callback thread.

## Phân nhánh OnKeyAction (Services/ControlEngine.cs:717)
- **Hệ thống & Chế độ**: F6 → Bật/tắt TestMode ngoài game; F5 → Reset toàn bộ OCR & đếm ruộng; LWin/RWin → Chặn phím Windows; Enter/Esc → Quét chat tức thì.
- **Phím bổ trợ (Shift/Ctrl/Alt)**: Theo dõi trạng thái nhấn/nhả; Alt tap (<300ms) → Chuẩn bị kích đời 3 (`ExecuteAge3FastUpgrade`); chặn Windows Menu mode.
- **Đặt cờ (FlagMode)**: CapsLock → Giữ Shift ảo, đổi AWSD thành 4 phím mũi tên cuộn bản đồ, phát âm thanh MIDI.
- **Khởi đầu nhanh (F2)**: F2 giữ → H-C liên tục; F2 nhả → Gửi F4 rồi F11.
- **Ngoại giao & Chuyển đồ**: Space/Esc khi mở Diplomacy → Thoát trạng thái; F3 → Mở Diplomacy; F4 → Mở Timeline.
- **Vẩy E & Đạo quân**: Ctrl+E → Vẩy E nhanh; Shift+1..6 → Gán đạo & kéo màn hình (Shift+N -> Ctrl+N -> Space); Ctrl+` → Đạo 0; Phím 1..6 → Nhớ đạo quân.
- **Đạo ruộng 1 & 2**: Ctrl+F / Ctrl+G → Chọn/chuyển đạo ruộng 7/8, bật timer; Shift+F / Shift+G → Làm mới ruộng (Esc -> 7/8 -> S -> Space -> S).
- **Tab công nghệ**: Ctrl+Tab → Gán đạo 9 (Shift+9 -> Ctrl+9); Tab đơn → Chọn đạo 9 / duyệt tiếp nhà công nghệ.
- **Xin quân nhanh (Shift + A,S,Z,X,D,C)**: Lần 1 gửi Ctrl+key; Lần 2+ kiểm tra OCR hàng đợi & loading (queue<=0 hoặc queue=1 & loading>=50% → click mua quân; còn lại bỏ qua).
- **Duyệt nhà binh**: Ctrl + A,S,Z,X,D,C → Ủy quyền sang `Services/MilitaryCycleManager.cs:MilitaryCycleManager`.
- **Xây nhà nhanh**: Phím đơn E,R,T,V,F,G,B,N,A,S,Z,X,D,C → `Services/FastBuildManager.cs:FastBuildManager`.
- **Remap & Click ô**: F12→F3 (pause), `→S (dừng), Q→C (xin dân), W→H (nhà chính), Numpad 1..5 → Click 5 ô biểu tượng góc.

## Nhóm tách được thành hàm / class riêng
- `KeyModifierTracker`: Quản lý trạng thái Shift/Ctrl/Alt, chống Menu mode, kích đời 3.
- `FlagModeManager`: Đặt cờ CapsLock + đổi phím mũi tên AWSD.
- `FastStartManager`: Giữ F2 khởi đầu trận đấu.
- `FarmCycleManager`: Đạo ruộng Ctrl/Shift + F/G và đếm số lần kích hoạt.
- `QuickTrainingManager`: Tách logic Shift + nhà quân kết hợp OCR hàng đợi/loading.
- `KeyRemapManager`: Bảng ánh xạ remap phím tĩnh và Numpad 1..5.

## Bẫy / lưu ý
- `OnKeyAction` dài 765 dòng chứa nhiều biến trạng thái phụ thuộc thứ tự phím; thay đổi dễ gây nuốt phím ngoài ý muốn.
