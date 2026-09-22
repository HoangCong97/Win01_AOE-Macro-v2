using System.Drawing;

namespace AOEKeyboardMacroPro.Services;

/// <summary>
/// Quản lý toàn bộ vòng đời và logic của tính năng Xây các loại nhà nhanh:
/// - Nhấn phím đơn (Tap): Thực thi phím xây dựng tương ứng [B -> Key]. Trỏ chuột hiển thị móng nhà.
/// - Nhấn giữ phím (Hold): Bắt đầu gửi [B -> Key], sau đó mỗi lần người chơi click chuột trái đặt móng,
///   hệ thống tự động nhả chuột và gửi tiếp [B -> Key]. Vòng lặp này duy trì cho tới khi nhả phím.
/// - Sau khi nhả giữ phím: Tự động ấn thêm [ESC] để hủy móng thừa đang lơ lửng.
/// - Hủy chức năng xây nhanh bằng chuột phải: Nếu đang trong chế độ giữ phím xây nhanh, khi ấn chuột phải,
///   macro tự động ấn [ESC] để hủy móng và thực thi [Click Chuột Phải] cho người chơi.
/// - Khi đang xây nhà nhanh, nếu đổi sang xây nhà khác, trước tiên tự động gửi [ESC] hủy móng cũ.
/// </summary>
public class FastBuildManager
{
    private Keys _heldKey = Keys.None;
    private Keys _currentBuildingKey = Keys.None;
    private Keys _suppressedRepeatKey = Keys.None;
    private DateTime _lastLeftClickTime = DateTime.MinValue;
    private DateTime _keyPressStartTime = DateTime.MinValue;
    private bool _hasPlacedInCurrentHold = false;
    private readonly object _stateLock = new();

    public bool IsHoldingKey
    {
        get
        {
            lock (_stateLock)
            {
                return _heldKey != Keys.None;
            }
        }
    }

    public Keys HeldKey
    {
        get
        {
            lock (_stateLock)
            {
                return _heldKey;
            }
        }
    }

    public Keys CurrentBuildingKey
    {
        get
        {
            lock (_stateLock)
            {
                return _currentBuildingKey;
            }
        }
    }

    // Cấu hình độ trễ tối ưu tốc độ nhanh
    public const int PostClickDelayMs = 25;       // Chờ 25ms sau khi click chuột đặt móng trước khi nhả chuột và lấy móng mới
    public const int InterKeyDelayMs = 15;        // Khoảng nghỉ giữa các phím bấm (15ms)
    public const int KeyPressHoldMs = 15;         // Thời gian đè giữ từng phím mô phỏng (15ms)
    public const int LeftClickDebounceMs = 120;   // Chống click quá nhanh / nảy switch chuột (120ms)

    /// <summary>
    /// Ánh xạ 14 phím nóng xây nhà dân sự và quân sự trong AOE.
    /// </summary>
    public static bool TryGetBuildingMapping(Keys key, out ushort firstKey, out ushort secondKey, out string name)
    {
        firstKey = (ushort)Keys.B;

        switch (key)
        {
            case Keys.E:
                secondKey = (ushort)Keys.E; name = "Nhà Dân BE"; return true;
            case Keys.R:
                secondKey = (ushort)Keys.S; name = "Nhà Kho BS"; return true;
            case Keys.T:
                secondKey = (ushort)Keys.G; name = "Nhà Chứa Ruộng BG"; return true;
            case Keys.V:
                secondKey = (ushort)Keys.M; name = "Nhà Chợ BM"; return true;
            case Keys.F:
                secondKey = (ushort)Keys.F; name = "Ruộng BF (Đạo 1)"; return true;
            case Keys.G:
                secondKey = (ushort)Keys.F; name = "Ruộng BF (Đạo 2)"; return true;
            case Keys.B:
                secondKey = (ushort)Keys.C; name = "Nhà Chính BC"; return true;
            case Keys.N:
                secondKey = (ushort)Keys.N; name = "Nhà Chòi BN"; return true;

            case Keys.A:
                secondKey = (ushort)Keys.A; name = "Nhà Bắn Cung BA"; return true;
            case Keys.S:
                secondKey = (ushort)Keys.L; name = "Nhà Ngựa Chém BL"; return true;
            case Keys.Z:
                secondKey = (ushort)Keys.K; name = "Nhà Chế Pháo BK"; return true;
            case Keys.X:
                secondKey = (ushort)Keys.Y; name = "Nhà Xọc Xiên BY"; return true;
            case Keys.D:
                secondKey = (ushort)Keys.B; name = "Nhà Lính Chùy BB"; return true;
            case Keys.C:
                secondKey = (ushort)Keys.P; name = "Nhà Phù Thủy BP"; return true;

            default:
                secondKey = 0; name = ""; return false;
        }
    }

    /// <summary>
    /// Xử lý sự kiện nhấn phím (KeyDown) cho 14 phím xây nhà.
    /// Chống lặp phím khi giữ phím (Key Repeat từ Windows) và tự chèn ESC nếu đổi loại nhà.
    /// </summary>
    public bool HandleKeyDown(Keys key, Action<string, Color> log, Action<Action> runAction)
    {
        if (!TryGetBuildingMapping(key, out ushort firstKey, out ushort secondKey, out string name))
        {
            return false;
        }

        lock (_stateLock)
        {
            // Nếu phím này đang được đè giữ rồi hoặc đang bị nén lặp thì bỏ qua sự kiện lặp (Key Repeat)
            if (_heldKey == key || _suppressedRepeatKey == key)
            {
                return true;
            }

            bool isSwitching = (_currentBuildingKey != Keys.None && _currentBuildingKey != key);
            _heldKey = key;
            _currentBuildingKey = key;
            _suppressedRepeatKey = Keys.None;
            _keyPressStartTime = DateTime.Now;
            _hasPlacedInCurrentHold = false;

            log($"[Xây nhà nhanh] {(isSwitching ? "Đổi sang " : "")}{key} ({name}) -> {(isSwitching ? "[ESC -> " : "")}B -> {((Keys)secondKey)}{(isSwitching ? "]" : "")}", Color.DarkCyan);

            runAction(() =>
            {
                if (isSwitching)
                {
                    InputSimulator.SendKeyPress((ushort)Keys.Escape, KeyPressHoldMs);
                    Thread.Sleep(InterKeyDelayMs);
                }
                InputSimulator.SendKeyPress(firstKey, KeyPressHoldMs);
                Thread.Sleep(InterKeyDelayMs);
                InputSimulator.SendKeyPress(secondKey, KeyPressHoldMs);
            });

            return true;
        }
    }

    /// <summary>
    /// Xử lý sự kiện nhả phím (KeyUp).
    /// Nếu là nhả giữ phím (Hold), tự động gửi thêm phím ESC để hủy móng thừa lơ lửng.
    /// </summary>
    public bool HandleKeyUp(Keys key, Action<string, Color> log, Action<Action> runAction)
    {
        bool shouldSendEsc = false;

        lock (_stateLock)
        {
            if (_suppressedRepeatKey == key)
            {
                _suppressedRepeatKey = Keys.None;
            }

            if (_heldKey == key)
            {
                bool isHold = _hasPlacedInCurrentHold || (DateTime.Now - _keyPressStartTime).TotalMilliseconds >= 200;

                _heldKey = Keys.None;
                _hasPlacedInCurrentHold = false;

                if (isHold)
                {
                    _currentBuildingKey = Keys.None;
                    shouldSendEsc = true;
                    log($"[Xây nhà nhanh] Nhả giữ phím {key} -> Gửi [ESC] hủy móng thừa", Color.DarkCyan);
                }
                else
                {
                    log($"[Xây nhà nhanh] Nhả phím {key} (Tap đơn)", Color.DarkCyan);
                }

                if (shouldSendEsc)
                {
                    runAction(() =>
                    {
                        InputSimulator.SendKeyPress((ushort)Keys.Escape, KeyPressHoldMs);
                    });
                }

                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// Xử lý khi người dùng click chuột trái trong lúc đang giữ một phím xây dựng.
    /// Đợi cú click chuột vật lý truyền xuống game để hạ móng, sau đó tự động nhả chuột và gọi tiếp [B -> Key].
    /// </summary>
    public void HandleLeftClick(Action<string, Color> log, Action<Action> runAction)
    {
        Keys held;
        lock (_stateLock)
        {
            held = _heldKey;
            if (held == Keys.None) return;

            DateTime now = DateTime.Now;
            if ((now - _lastLeftClickTime).TotalMilliseconds < LeftClickDebounceMs)
            {
                return;
            }
            _lastLeftClickTime = now;
            _hasPlacedInCurrentHold = true;
        }

        if (!TryGetBuildingMapping(held, out ushort firstKey, out ushort secondKey, out string name))
        {
            return;
        }

        log($"[Xây nhà nhanh] Click đặt móng -> Tự động nhả chuột và gọi tiếp [B -> {((Keys)secondKey)}] ({name})", Color.DarkCyan);

        runAction(() =>
        {
            // 1. Chờ ngắn để game nhận diện móng nhà đã hạ
            Thread.Sleep(PostClickDelayMs);

            // 2. Tự động nhả chuột trái hộ người chơi trước khi bấm B (tránh kẹt chuột đè lệnh)
            InputSimulator.ReleaseLeftMouse();
            Thread.Sleep(5);

            // 3. Kiểm tra lại xem người chơi có còn đang đè giữ phím này hay không
            lock (_stateLock)
            {
                if (_heldKey != held) return;
            }

            // 4. Gửi phím B -> Key để lấy móng mới ở tốc độ cao
            InputSimulator.SendKeyPress(firstKey, KeyPressHoldMs);
            Thread.Sleep(InterKeyDelayMs);
            InputSimulator.SendKeyPress(secondKey, KeyPressHoldMs);
        });
    }

    /// <summary>
    /// Hủy chức năng xây nhanh: nếu đang trong chế độ giữ phím xây nhanh (hoặc đang có móng),
    /// người dùng ấn chuột phải -> macro tự động ấn ESC hủy móng và thực thi click chuột phải sạch sẽ.
    /// </summary>
    public void HandleRightClickCancel(Action<string, Color> log, Action<Action> runAction)
    {
        lock (_stateLock)
        {
            if (_heldKey != Keys.None)
            {
                _suppressedRepeatKey = _heldKey;
            }
            _heldKey = Keys.None;
            _currentBuildingKey = Keys.None;
            _hasPlacedInCurrentHold = false;
        }

        log("[Xây nhà nhanh] Click chuột phải -> Gửi [ESC] hủy móng và thực thi [Chuột Phải]", Color.DarkCyan);

        runAction(() =>
        {
            // 1. Gửi ESC hủy móng đang treo
            InputSimulator.SendKeyPress((ushort)Keys.Escape, KeyPressHoldMs);
            Thread.Sleep(InterKeyDelayMs);

            // 2. Thực thi click chuột phải tự nhả siêu nhanh 1-2ms (di chuyển / chỉ định lệnh trong game)
            InputSimulator.SendRightClickFast(2);
        });
    }

    /// <summary>
    /// Khi người chơi click chuột trái lúc KHÔNG giữ phím nào (ví dụ vừa tap phím đơn và click đặt móng):
    /// Đã đặt xong móng nhà, kết thúc trạng thái đặt nhà.
    /// </summary>
    public void OnLeftClickWhenNotHolding()
    {
        lock (_stateLock)
        {
            _currentBuildingKey = Keys.None;
        }
    }

    /// <summary>
    /// Reset toàn bộ trạng thái xây nhà (khi click chuột phải, ấn ESC, hoặc chuyển đổi cửa sổ).
    /// </summary>
    public void Reset()
    {
        lock (_stateLock)
        {
            if (_heldKey != Keys.None)
            {
                _suppressedRepeatKey = _heldKey;
            }
            _heldKey = Keys.None;
            _currentBuildingKey = Keys.None;
            _hasPlacedInCurrentHold = false;
        }
    }
}
