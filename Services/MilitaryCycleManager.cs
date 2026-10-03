using System;
using System.Drawing;
using System.Windows.Forms;

namespace AOEKeyboardMacroPro.Services;

/// <summary>
/// Quản lý toàn bộ vòng đời và logic của tính năng Duyệt nhà binh (CTRL + Phím):
/// - Chi tiết các phím remapping:
///   + (CTRL + A): [CTRL + A] Duyệt nhà bắn cung BA
///   + (CTRL + S): [CTRL + L] Duyệt nhà ngựa chém BL
///   + (CTRL + Z): [CTRL + K] Duyệt nhà chế pháo BK
///   + (CTRL + X): [CTRL + Y] Duyệt nhà xọc xiên BY
///   + (CTRL + D): [CTRL + B] Duyệt nhà lính chùy BB
///   + (CTRL + C): [CTRL + P] Duyệt nhà phù thủy BP
/// - Cho phép người dùng duyệt qua (xoay vòng chọn) các nhà binh đã xây bằng tổ hợp phím CTRL + Phím.
/// - Lưu ý: Nếu người dùng giữ phím CTRL vật lý liên tục và nhấn phím chữ (ví dụ giữ CTRL và nhấn liên tục A, A, A...),
///   hệ thống vẫn nhận diện chính xác và gửi từng lệnh duyệt nhà tương ứng.
/// - Khi người dùng ấn giữ (CTRL + KEY) và sau đó người dùng click chuột, thì cứ mỗi lần click chuột sẽ [CTRL + KEYMAP] 1 lần.
/// </summary>
public class MilitaryCycleManager
{
    private Keys _currentMilitaryKey = Keys.None;
    private ushort _currentTargetVk = 0;
    private string _currentDesc = string.Empty;
    private bool _isKeyHeld = false;
    private DateTime _lastClickTime = DateTime.MinValue;
    private const int ClickDebounceMs = 50;

    public bool IsActive => _currentMilitaryKey != Keys.None;
    public Keys CurrentMilitaryKey => _currentMilitaryKey;
    public ushort CurrentTargetVk => _currentTargetVk;
    public string CurrentDesc => _currentDesc;
    public bool IsKeyHeld => _isKeyHeld;

    /// <summary>
    /// Ánh xạ 6 loại nhà binh trong AOE từ phím gốc sang target phím duyệt trong game.
    /// </summary>
    public static bool TryGetMilitaryTargetKey(Keys key, out ushort targetVk, out string desc)
    {
        switch (key)
        {
            case Keys.A:
                targetVk = (ushort)Keys.A; desc = "Nhà Bắn Cung BA"; return true;
            case Keys.S:
                targetVk = (ushort)Keys.L; desc = "Nhà Ngựa Chém BL"; return true;
            case Keys.Z:
                targetVk = (ushort)Keys.K; desc = "Nhà Chế Pháo BK"; return true;
            case Keys.X:
                targetVk = (ushort)Keys.Y; desc = "Nhà Xọc Xiên BY"; return true;
            case Keys.D:
                targetVk = (ushort)Keys.B; desc = "Nhà Lính Chùy BB"; return true;
            case Keys.C:
                targetVk = (ushort)Keys.P; desc = "Nhà Phù Thủy BP"; return true;
            default:
                targetVk = 0; desc = string.Empty; return false;
        }
    }

    /// <summary>
    /// Xử lý sự kiện nhấn phím KeyDown cho tổ hợp CTRL + Phím duyệt nhà binh.
    /// Chống lặp phím khi giữ phím (typematic repeat từ Windows OS).
    /// </summary>
    public bool HandleKeyDown(Keys key, bool ctrlPressed, Action<string, Color> log, Action<Action> runAction)
    {
        if (!ctrlPressed) return false;
        if (!TryGetMilitaryTargetKey(key, out ushort targetVk, out string desc)) return false;

        // Nếu phím này đang được đè giữ rồi (repeat do OS giữ phím), bỏ qua gửi lặp để chờ click chuột
        if (_isKeyHeld && _currentMilitaryKey == key)
        {
            return true;
        }

        bool isSwitching = (_currentMilitaryKey != Keys.None && _currentMilitaryKey != key);
        _currentMilitaryKey = key;
        _currentTargetVk = targetVk;
        _currentDesc = desc;
        _isKeyHeld = true;

        log($"[Duyệt nhà binh] {(isSwitching ? "Đổi sang " : "")}CTRL+{key} -> CTRL+{((Keys)targetVk)} ({desc})", Color.DarkGreen);
        runAction(() =>
        {
            InputSimulator.SendCtrlKeyCombo(targetVk);
        });

        return true;
    }

    /// <summary>
    /// Xử lý sự kiện nhả phím KeyUp cho phím chữ.
    /// Đánh dấu phím không còn bị đè giữ vật lý, nhưng vẫn duy trì _currentMilitaryKey nếu người dùng vẫn giữ CTRL.
    /// </summary>
    public bool HandleKeyUp(Keys key)
    {
        if (_currentMilitaryKey == key)
        {
            _isKeyHeld = false;
            return true;
        }
        return false;
    }

    /// <summary>
    /// Xử lý khi người dùng nhả phím CTRL (CTRL Up) -> Kết thúc trạng thái duyệt nhà binh.
    /// </summary>
    public void HandleCtrlUp()
    {
        Reset();
    }

    /// <summary>
    /// Xử lý mỗi lần người dùng click chuột trái khi đang giữ (CTRL + KEY):
    /// Thực thi [CLICK -> CTRL + KEYMAP] 1 lần.
    /// </summary>
    public bool HandleLeftClick(Action<string, Color> log, Action<Action> runAction)
    {
        if (!IsActive) return false;

        DateTime now = DateTime.Now;
        if ((now - _lastClickTime).TotalMilliseconds < ClickDebounceMs)
        {
            return true; // Debounce chống nảy switch nhưng vẫn chặn click chuột vật lý xuống game
        }
        _lastClickTime = now;

        ushort targetVk = _currentTargetVk;
        string desc = _currentDesc;

        log($"[Duyệt nhà binh] Click chuột trái -> [CLICK -> CTRL+{((Keys)targetVk)}] ({desc})", Color.DarkGreen);
        runAction(() =>
        {
            InputSimulator.SendMouseClickHold(25);
            Thread.Sleep(15);
            InputSimulator.SendCtrlKeyCombo(targetVk);
        });

        return true;
    }

    /// <summary>
    /// Đưa trạng thái về ban đầu (khi nhả CTRL, ấn phím khác, F5 refresh, v.v.).
    /// </summary>
    public void Reset()
    {
        _currentMilitaryKey = Keys.None;
        _currentTargetVk = 0;
        _currentDesc = string.Empty;
        _isKeyHeld = false;
    }
}
