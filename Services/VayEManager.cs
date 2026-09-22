namespace AOEKeyboardMacroPro.Services;

/// <summary>
/// Quản lý toàn bộ vòng đời và logic của tính năng Vẩy E:
/// - Khi ấn giữ CTRL + E (nhả E nhưng vẫn giữ CTRL): [7 -> B -> E] (Chọn đạo dân 7 và lấy móng BE).
/// - Mỗi lần click chuột trái (hoặc bấm tiếp E khi vẫn giữ CTRL): [Click -> S -> B -> E]
///   (Click hạ móng trước, phím S dừng dân không cho chạy ra xây, và phím B -> E lấy móng BE mới).
/// - Cho tới khi thả CTRL: [ESC -> Chọn lại đạo quân trước đó (1..6)].
/// </summary>
public class VayEManager
{
    private bool _isActive = false;
    private int _stepCount = 0;
    private Keys _previousMilitaryGroup = Keys.D1; // Mặc định đạo quân 1
    private readonly Keys _villagerGroup = Keys.D7; // Đạo dân 7

    public bool IsActive => _isActive;
    public int StepCount => _stepCount;
    public Keys PreviousMilitaryGroup => _previousMilitaryGroup;

    /// <summary>
    /// Ghi nhận đạo quân người chơi vừa chọn (1..6) để khi kết thúc vẩy E sẽ hoàn trả đúng đạo quân.
    /// </summary>
    public void RecordMilitaryGroup(Keys key)
    {
        if (key >= Keys.D1 && key <= Keys.D6)
        {
            _previousMilitaryGroup = key;
        }
    }

    /// <summary>
    /// Xử lý khi người chơi nhấn phím E trong lúc đang giữ CTRL.
    /// </summary>
    public void HandlePressE(Action<string, Color> log, Action<Action> runAction)
    {
        if (!_isActive)
        {
            _isActive = true;
            _stepCount = 0;
            log("[Vẩy E] Bắt đầu (Giữ CTRL + E) -> [7 -> B -> E] (Chọn dân đạo 7 & lấy móng BE)", Color.Crimson);

            runAction(() =>
            {
                InputSimulator.ReleaseCtrlKeysHardware();
                Thread.Sleep(10);
                InputSimulator.SendKeyPress((ushort)_villagerGroup, 15);
                Thread.Sleep(20);
                InputSimulator.SendKeyPress((ushort)Keys.B, 15);
                Thread.Sleep(20);
                InputSimulator.SendKeyPress((ushort)Keys.E, 15);
            });
        }
        else
        {
            ExecuteStep(log, runAction, "Phím E");
        }
    }

    /// <summary>
    /// Xử lý mỗi lần click chuột trái trong khi Vẩy E đang active (người dùng đang giữ CTRL).
    /// </summary>
    public void HandleLeftClick(Action<string, Color> log, Action<Action> runAction)
    {
        if (_isActive)
        {
            ExecuteStep(log, runAction, "Click");
        }
    }

    /// <summary>
    /// Thực hiện 1 bước vẩy móng: [Click -> S -> B -> E]
    /// </summary>
    private void ExecuteStep(Action<string, Color> log, Action<Action> runAction, string triggerSource)
    {
        _stepCount++;
        log($"[Vẩy E] {triggerSource} lần {_stepCount} -> [Click -> S -> B -> E] (Hạ móng, Dừng dân & Lấy móng BE mới)", Color.Crimson);

        runAction(() =>
        {
            // 1. Nhả sạch toàn bộ trạng thái Ctrl phần cứng và phần mềm trước khi click
            InputSimulator.ReleaseCtrlKeysHardware();
            Thread.Sleep(10);

            // 2. Click chuột trái đặt móng
            InputSimulator.SendMouseClickHold(20);
            
            // Chờ game AOE xử lý xong thao tác đặt móng và chuyển villager sang task xây
            Thread.Sleep(25);

            // 3. Phím S dừng dân không cho chạy ra xây móng
            InputSimulator.ReleaseCtrlKeysHardware();
            InputSimulator.SendKeyPress((ushort)Keys.S, 15);
            Thread.Sleep(20);

            // 4. Phím B -> E lấy móng BE tiếp theo
            InputSimulator.ReleaseCtrlKeysHardware();
            InputSimulator.SendKeyPress((ushort)Keys.B, 15);
            Thread.Sleep(20);
            InputSimulator.SendKeyPress((ushort)Keys.E, 15);
        });
    }

    /// <summary>
    /// Xử lý khi người chơi nhả phím CTRL (kết thúc chuỗi vẩy E):
    /// [ESC -> Chọn lại đạo quân trước đó].
    /// </summary>
    public void HandleCtrlUp(Action<string, Color> log, Action<Action> runAction)
    {
        if (!_isActive) return;

        _isActive = false;
        _stepCount = 0;
        Keys targetGroup = _previousMilitaryGroup;
        log($"[Vẩy E] Thả CTRL -> [ESC hủy móng thừa -> Chọn lại đạo {targetGroup.ToString().Replace("D", "")}]", Color.Crimson);

        runAction(() =>
        {
            InputSimulator.ReleaseCtrlKeysHardware();
            Thread.Sleep(10);
            // Dừng dân đề phòng
            InputSimulator.SendKeyPress((ushort)Keys.S, 15);
            Thread.Sleep(15);
            // Hủy móng BE đang lơ lửng trên con trỏ chuột
            InputSimulator.SendKeyPress((ushort)Keys.Escape, 15);
            Thread.Sleep(20);
            // Chọn lại đạo quân trước đó (1..6)
            InputSimulator.SendKeyPress((ushort)targetGroup, 15);
        });
    }

    /// <summary>
    /// Reset trạng thái Vẩy E khi người dùng click chuột ngoài macro, bấm phím khác hoặc ra ngoài game.
    /// </summary>
    public void Reset()
    {
        _isActive = false;
        _stepCount = 0;
    }
}
