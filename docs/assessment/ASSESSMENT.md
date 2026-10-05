---
project: Win01_AOE-Macro-v2 (AOE Keyboard Macro Pro)
assessed: 2026-10-06
commit: 5f674d7
recommendation: Refactor
decision: Refactor
---

# Đánh giá project — AOE Keyboard Macro Pro (v2)

## Bối cảnh
- Mục tiêu sắp tới (người dùng nói 2026-10-06): tính năng gần như xong hết, không đổi nhiều nữa → mục tiêu thực chất là **dễ sửa / dễ bảo trì**, không phải thêm chức năng. Repo `Win01_AOE-Macro-v3` + `T-L01` (rút yêu cầu v2) đã khởi động theo hướng làm lại.
- Đang chạy thật: **có** — v2 đang được dùng; người dùng không dùng nó khi phát triển nhưng **không được sửa trực tiếp vào bản đang chạy**. Dùng cá nhân, không có hệ thống khác phụ thuộc.
- Ràng buộc: chưa chốt có đổi stack không (người dùng hỏi lại khái niệm; mặc định giữ C# .NET 9 WinForms).
- Nguồn đã đọc: `measure.sh` · `dotnet build` (Debug) · README trong `AI env/` · `Services/ControlEngine.cs` (cấu trúc, độ dài hàm) · `MainForm.cs` (grep) · `docs/code-summary/` (Gemini, commit 5f674d7, 6 module, kiểm lại 3 điểm bằng grep: `RunActionSync` là delegate do ControlEngine truyền vào manager `ControlEngine.cs:373,378,441` → xác nhận; các manager khởi tạo bằng `new()` `ControlEngine.cs:12-16`).

## Số đo chính
- 33 file `.cs`, 10.849 dòng. `Services/` 7.914 dòng (73%), gốc 2.603, `Models/` 332.
- File lớn nhất: `Services/ControlEngine.cs` 1850 dòng; `MainForm.cs` 1107 (2 file > 1000, 7 file > 500).
- Test: **0 file**. CI: không. Lint/format: không. TODO/FIXME: 0.
- Git: 21 commit (2026-09-05 → 2026-10-03), 1 người viết, ~19% commit sửa lỗi (mẫu < 30 → độ tin thấp), tiêu đề commit đều rõ nghĩa.
- File nóng: `ControlEngine.cs` đổi 16/21 commit và sửa lỗi 3 lần; `MouseHookManager`, `InputSimulator`, `MouseLockManager` sửa lỗi 2 lần mỗi file.
- Build: `dotnet build` → 0 warning, 0 error (6,5 giây), .NET SDK 9.0.314.
- `ControlEngine.cs`: hàm `OnKeyAction` dài **764 dòng** (~41% file); 143 câu `if`; 69 chỗ dùng lock/Thread/Timer trong cùng một class.

## Bảng dấu hiệu
| # | Dấu hiệu | Hướng | Bằng chứng |
|---|---|---|---|
| 1 | Kiến trúc, mô hình dữ liệu | Refactor | Có tách `Services/` theo tính năng (FastBuild, VayE, Delete, MouseLock, 5 dịch vụ OCR…) nhưng `ControlEngine` là god-class điều phối + trạng thái + xử lý phím cho tất cả; mô hình dữ liệu nhỏ (`Models/` 332 dòng). Khung dùng được. |
| 2 | Lưới an toàn (test) | Refactor | 0 test. Test được từng phần: FastBuild, FarmTimer, MilitaryCycle, VayE, Delete, GameStateWatcher, ConfigService, các OCR parser (`ParsePopString`, `RecognizeFromBitmap`) khởi tạo độc lập, input đi qua delegate `RunActionSync` mock được (summary core-engine/macro-features/ocr-vision; kiểm `ControlEngine.cs:373-441`). `ControlEngine`/`OnKeyAction` thì **không** test được nguyên khối (gắn hook + Timer + MidiPlayer, `core-engine.md`) — nhưng đã có 6 nhóm tách được (`KeyModifierTracker`, `FlagModeManager`, `FastStartManager`, `FarmCycleManager`, `QuickTrainingManager`, `KeyRemapManager`). Còn 1 điểm chưa kiểm: có tách được mà giữ nguyên thứ tự xử lý phím không → xử lý bằng bước test đặc tả đầu tiên. |
| 3 | Chạy / build được | Giữ | `dotnet build` 0 lỗi 0 cảnh báo. |
| 4 | Stack, phụ thuộc | Giữ | `net9.0-windows` hiện hành, csproj không có gói NuGet ngoài (chỉ 1 TargetFramework, không PackageReference). |
| 5 | Mật độ lỗi, điểm nóng | Refactor (thiên) | 19% sửa lỗi nhưng lỗi tập trung ở `ControlEngine` (3/10 lần sửa lỗi top) và nhóm hook/input — đúng mẫu "vài file nóng". Chỉ 21 commit → độ tin thấp. |
| 6 | Cấu trúc code | Refactor | Hàm 764 dòng, 143 `if` trong một file; `MainForm.cs` 1107 dòng trộn UI với logic (clamp, tính còn lại pop suppress, `MainForm.cs:50,328,371`). Cục bộ ở 2–3 file, các service khác 400–580 dòng. Không có TODO/commit vô nghĩa. |
| 7 | Ràng buộc tương thích | Refactor (nhẹ) | v2 đang chạy thật, không được sửa trực tiếp; `config.json` (`ConfigService`) nên đọc được tiếp. Không có API/hệ thống ngoài. Làm trên bản sao/nhánh riêng (v3 hoặc branch) là đủ. |
| 8 | Phạm vi sắp tới | Refactor | Người dùng: tính năng gần như xong, không đổi nhiều → chức năng giữ nguyên, cần dễ sửa hơn. |

Kết luận theo `signals.md`: dòng 1, 2 không phải "Làm lại" → luật 1 không khớp; chưa có yêu cầu đổi stack → luật 2 không khớp; dòng 5, 6 là Refactor → luật 3 không khớp → **Refactor** (luật 4). Sau khi có bản tóm tắt và câu trả lời người dùng, còn 0/8 dòng Chưa rõ; độ tin **khá cao**. Rủi ro còn lại: dòng 1, 5 chỉ dựa 21 commit; dòng 2 chưa chứng minh bằng test thật.

## Chi phí ước lượng
| Hướng | Task (ước) | Giả định |
|---|---|---|
| Giữ | 0 riêng, +30–50% cho task chạm `ControlEngine`/hook | Không đáng giữ nguyên nếu còn thêm tính năng: file nóng đổi 16/21 commit. |
| Refactor | ~13–18 | `ControlEngine` vùng lớn: 1 test đặc tả + 4–8 bước; `MainForm` 1 + 2–3; dịch vụ OCR/hook 2–3 task nhỏ; +2 hạ tầng test (chưa có dự án test). |
| Làm lại | ~23–38 | ~15 chức năng (xây nhanh, Vẩy E, xóa nhanh, khóa chuột, timer ruộng, 5 OCR, chat, HUD, hiệu chỉnh crop, MIDI…) × 1–2 + khung 3 + rút `docs/legacy/` 3 + dữ liệu cấu hình 2. |

Chênh lệch > 30% (Refactor rẻ hơn ~40–50%). Phần "rút `docs/legacy/`" của Làm lại đã làm một phần (T-L01), nên khoảng cách thực tế nhỏ hơn.

## Đề xuất
**Refactor** — khung dùng được, build sạch, stack mới (dòng 1, 3, 4); vấn đề tập trung ở 2–3 file (dòng 5, 6); Làm lại tốn gấp ~2 lần mà không có dòng nào chạm ngưỡng "sai từ gốc". Rủi ro nếu chọn Làm lại: mất cân chỉnh hành vi đã tinh chỉnh trong 21 commit (thời gian chuột 1–2 ms, chống IME, hook LIFO…) vì chúng khó tái hiện mà không có test. Rủi ro nếu Refactor: không có test và `OnKeyAction` gắn chặt API hệ thống → bước 1 (test đặc tả) có thể không làm được, khi đó vùng này nên viết song song rồi thay thế.

**Điều kiện đổi kết luận sang Làm lại:** người dùng muốn đổi stack (dòng 4), hoặc bước test đặc tả đầu tiên cho `OnKeyAction` thất bại (dòng 2 → Làm lại, kết hợp dòng 1/6 → luật 1). Vùng `OnKeyAction` không test được thì làm kiểu "viết mới song song rồi thay thế" cho đúng vùng đó.

## Nợ cần biết
- `Services/ControlEngine.cs` — god-class 1850 dòng, `OnKeyAction` 764 dòng, trộn trạng thái + hook + chuỗi phím + timer; file đổi nhiều nhất — tách theo trạng thái / theo tính năng (bảng ánh xạ phím → handler).
- `MainForm.cs` — 1107 dòng, có logic nghiệp vụ trong form (pop suppress, clamp cấu hình) — tách ra presenter/service.
- `Services/MouseHookManager.cs`, `InputSimulator.cs`, `MouseLockManager.cs` — sửa lỗi lặp lại; hành vi tinh chỉnh thời gian (spin-wait 1–2 ms, Raw Input), cẩn thận khi động vào.
- Không có test, CI, lint — mọi thay đổi chỉ kiểm bằng chạy thật trong game.
- Tài liệu AI (`AI env/*.md`, 51 KB) và `Control.md` đổi 12 lần: dễ lệch code, kiểm lại trước khi tin.
- Thư mục gốc có file nhị phân (`AOEKeyboardMacroPro_v27.exe`, ảnh mẫu, `bin/`, `obj/`) — lưu ý khi dọn repo.

## Quyết định
2026-10-06 — người dùng chọn **Refactor**; giữ nguyên công nghệ (C# .NET 9 WinForms); **không làm vào v2** (đang chạy thật) mà làm ở project `Win01_AOE-Macro-v3`; tách `OnKeyAction` nếu có lợi (quyết theo kết quả test đặc tả). Skill kế tiếp: `code-refactor` (chế độ leader).

## Lịch sử
(Lần đánh giá đầu tiên.)
