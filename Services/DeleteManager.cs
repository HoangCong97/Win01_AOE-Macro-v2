using System.Windows.Forms;
using System.Drawing;

namespace AOEKeyboardMacroPro.Services;

/// <summary>
/// Quản lý chức năng Delete nhanh bằng chuột giữa (Middle Mouse):
/// - (Click chuột middle): [CTRL + 6 -> Click chuột trái -> Delete -> 6]
/// - (Click giữ chuột middle): [CTRL + 6 -> Click chuột trái -> Delete -> 6] liên tục ở tốc độ cao cho tới khi nhả chuột giữa.
/// Cơ chế hoạt động:
/// 1. CTRL + 6: Lưu đạo quân/đơn vị đang chọn vào đạo 6.
/// 2. Click chuột trái: Chọn đối tượng/móng nhà/ruộng dưới con trỏ chuột.
/// 3. Delete: Xóa đối tượng vừa click.
/// 4. 6: Chọn lại đạo quân 6 ban đầu.
/// </summary>
public class DeleteManager
{
    private volatile bool _isHolding = false;
    private readonly SemaphoreSlim _actionLock;

    public bool IsHolding => _isHolding;

    public DeleteManager(SemaphoreSlim actionLock)
    {
        _actionLock = actionLock;
    }

    /// <summary>
    /// Xử lý khi người chơi nhấn chuột giữa (WM_MBUTTONDOWN).
    /// Luôn thực hiện 1 lần đầu tiên cho thao tác Click, và nếu tiếp tục giữ chuột sẽ lặp lại ở tốc độ cao.
    /// </summary>
    public void HandleMiddleButtonDown(Action<string, Color> log, Func<bool> canExecute)
    {
        if (_isHolding) return;
        _isHolding = true;

        log("[Chức năng: Delete] Chuột giữa -> [CTRL+6 -> Click chuột trái -> Delete -> 6] (Tốc độ cao)", Color.Crimson);

        Task.Run(() =>
        {
            int count = 0;

            // 1. Luôn thực thi lần đầu tiên (đáp ứng Click chuột middle)
            if (_actionLock.Wait(500))
            {
                try
                {
                    if (canExecute())
                    {
                        ExecuteSingleCycle();
                        count++;
                    }
                }
                finally
                {
                    _actionLock.Release();
                }
            }

            // 2. Nếu người dùng tiếp tục ấn giữ chuột giữa -> Lặp lại liên tục ở tốc độ cao
            while (_isHolding && canExecute())
            {
                Thread.Sleep(20);
                if (!_isHolding || !canExecute()) break;

                if (_actionLock.Wait(200))
                {
                    try
                    {
                        if (!_isHolding || !canExecute()) break;
                        ExecuteSingleCycle();
                        count++;
                    }
                    finally
                    {
                        _actionLock.Release();
                    }
                }
            }

            _isHolding = false;

            if (count > 1)
            {
                log($"[Chức năng: Delete] Nhả chuột giữa -> Đã hoàn thành {count} lần xóa liên tiếp", Color.Crimson);
            }
        });
    }

    /// <summary>
    /// Xử lý khi người chơi nhả chuột giữa (WM_MBUTTONUP).
    /// </summary>
    public void HandleMiddleButtonUp()
    {
        _isHolding = false;
    }

    /// <summary>
    /// Thực hiện 1 chu kỳ xóa hoàn chỉnh: [CTRL + 6 -> Click chuột trái -> Delete -> 6]
    /// </summary>
    private void ExecuteSingleCycle()
    {
        // 1. CTRL + 6: Lưu đơn vị hiện tại vào đạo 6
        InputSimulator.SendCtrlKeyCombo((ushort)Keys.D6);
        Thread.Sleep(10);

        // 2. Click chuột trái vào đối tượng cần xóa dưới con trỏ chuột
        InputSimulator.SendMouseClickHold(15);
        Thread.Sleep(10);

        // 3. Delete: Xóa đối tượng vừa click
        InputSimulator.SendKeyPress((ushort)Keys.Delete, 12);
        Thread.Sleep(10);

        // 4. 6: Chọn lại đạo quân 6 ban đầu
        InputSimulator.SendKeyPress((ushort)Keys.D6, 12);
    }

    /// <summary>
    /// Dừng khẩn cấp khi chuyển cửa sổ ra ngoài game hoặc tắt macro.
    /// </summary>
    public void Reset()
    {
        _isHolding = false;
    }
}
