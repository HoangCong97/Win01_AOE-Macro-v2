# Module: input-native
> `Services/NativeMethods.cs`, `Services/InputSimulator.cs`, `Services/*HookManager.cs`, `Services/MouseLockManager.cs`, `Services/AoeWindowHelper.cs`, `Services/SystemPolicyManager.cs` · ~1.7k dòng · cập nhật 2026-10-06 · [tổng quan](../PROJECT.md)

**Trách nhiệm:** Tương tác cấp thấp với Windows: bắt hook bàn phím/chuột toàn cục, giả lập gõ phím/chuột qua SendInput, khoá chuột bằng RawInput, và truy vấn cửa sổ game AOE.
**Không làm:** Không chứa logic nghiệp vụ trò chơi (chỉ cung cấp primitives).

## Thành phần chính & 3 câu hỏi kiểm định
- `Services/NativeMethods.cs:NativeMethods` - Khai báo P/Invoke Win32 API.
  *(1)* Định nghĩa API OS trực tiếp. *(2)* **Không**, phụ thuộc OS. *(3)* Static hằng số, `MACRO_EXTRA_INFO`.
- `Services/InputSimulator.cs:InputSimulator` - Wrapper phát xung bàn phím, click chuột, combo Ctrl/Shift.
  *(1)* Dùng `Services/NativeMethods.cs` (L20-40: SendInput, GetAsyncKeyState). *(2)* **Không nên**, tác động chuột/phím thật của OS. *(3)* Static lock `_rightClickLock` (L377).
- `Services/KeyboardHookManager.cs:KeyboardHookManager` - Windows LL Keyboard Hook (`WH_KEYBOARD_LL`).
  *(1)* Dùng `Services/NativeMethods.cs` (L10-42). *(2)* **Không**, cần Windows message loop và hook thật. *(3)* Mutable `_hookId`, không lock.
- `Services/MouseHookManager.cs:MouseHookManager` - Windows LL Mouse Hook (`WH_MOUSE_LL`).
  *(1)* Dùng `Services/NativeMethods.cs` (L12-102). *(2)* **Không**, cần hook thật. *(3)* Lock `_lock` (L37,94) bảo vệ hook handle & cờ click.
- `Services/MouseLockManager.cs:MouseLockManager` - Khoá trỏ chuột trong cửa sổ game qua RawInput & `ClipCursor`.
  *(1)* Dùng `Services/NativeMethods.cs` (L56,63). *(2)* **Không**, chạy thread RawInput độc lập. *(3)* Static lock `_lockObj` (L29,93,171).
- `Services/AoeWindowHelper.cs:AoeWindowHelper` - Tìm kiếm và xác định cửa sổ AOE đang active.
  *(1)* Dùng `Services/NativeMethods.cs` (L103,110,138). *(2)* **Có** cho logic regex tên/tiến trình, **Không** cho HWND thật. *(3)* Static cache tiến trình, lock `_cacheLock` (L81).
- `Services/SystemPolicyManager.cs:SystemPolicyManager` - Tinh chỉnh power plan và DPI Windows.
  *(1)* **Không** NativeMethods (dùng `Process.Start`). *(2)* **Có**. *(3)* Static methods, không lưu trạng thái.

## Luồng dữ liệu
- Bắt sự kiện: OS Hook (`WH_KEYBOARD_LL`) → `KeyboardHookManager` → Callback `OnKeyAction` trong `core-engine`.
- Phát lệnh: `core-engine` → `InputSimulator.Send*` → `NativeMethods.SendInput` → Hệ điều hành Windows.

## Phụ thuộc
- Dùng: Win32 API (`user32.dll`, `kernel32.dll`, `winmm.dll`).
- Được dùng bởi: Toàn bộ các module khác (`core-engine`, `macro-features`, `ocr-vision`, `ui`).

## Bẫy / lưu ý
- Lệnh giả lập phím gán cờ `MACRO_EXTRA_INFO` để các hook tự bỏ qua, tránh đệ quy lặp vô tận (infinite hook loop).
- Mouse Hook và InputSimulator cần spin-wait chính xác (1-2ms) để game AOE nhận diện sự kiện Down/Up.
