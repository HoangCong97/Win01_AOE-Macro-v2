using System.Windows.Forms;
using AOEKeyboardMacroPro.Models;

namespace AOEKeyboardMacroPro.Services;

public class ControlEngine : IDisposable
{
    private readonly KeyboardHookManager _keyboardHook = new();
    private readonly MouseHookManager _mouseHook = new();
    private readonly GameStateWatcher _gameWatcher = new();
    private readonly FarmTimerManager _farmTimerManager = new();
    private readonly System.Windows.Forms.Timer _f2LoopTimer = new();
    private readonly SemaphoreSlim _actionLock = new(1, 1);

    private MacroState _currentState = MacroState.Disabled;
    private MacroState _savedStateBeforeUnfocus = MacroState.Active;

    private bool _isF2Pressed = false;

    // Fast Building Mẫu 1 tracking state
    private Keys _lastBuildingKey = Keys.None;
    private DateTime _lastBuildingTime = DateTime.MinValue;

    // Quick Recruit / Military Cycle SHIFT tracking state
    private Keys _lastShiftMilitaryKey = Keys.None;
    private DateTime _lastShiftMilitaryTime = DateTime.MinValue;

    // Tech Tab (Tab công nghệ) tracking state
    private DateTime _lastTabTime = DateTime.MinValue;

    // Age 3 Fast Upgrade (Kích đời 3) tracking state
    private int _winKeyState = 0;
    private DateTime _lastWinKeyTime = DateTime.MinValue;
    private bool _isAltDown = false;
    private bool _isAltCombo = false;
    private DateTime _altPressTime = DateTime.MinValue;

    // Vẩy E tracking state
    private bool _isVayEActive = false;
    private int _vayECount = 0;
    private Keys _previousMilitaryGroup = Keys.D1;

    // Đạo ruộng nhanh tracking state
    private int _ctrlFCount = 0;
    private int _ctrlGCount = 0;
    private int _activeFarmGroup = 0; // 0: None, 1: Ruộng 1 (F), 2: Ruộng 2 (G)
    private bool _isPhysicalShiftDown = false;
    private bool _isRightMouseDown = false;
    private bool _isShiftTemporarilyReleasedForMouse = false;

    // Flag/Waypoint Mode (Chức năng đặt cờ) tracking state
    private bool _isFlagModeActive = false;

    public event Action<MacroState>? StateChanged;
    public event Action<string, Color>? LogRequested;
    public event Action<int, int>? FarmTimerUpdated;

    public MacroState CurrentState => _currentState;

    public ControlEngine()
    {
        _keyboardHook.KeyActionOccurred += OnKeyAction;
        _mouseHook.MouseClicked += OnMouseClick;
        _mouseHook.RightButtonDown += OnRightButtonDown;
        _mouseHook.RightButtonUp += OnRightButtonUp;
        _mouseHook.MiddleClickActionOccurred += OnMiddleClickAction;
        _mouseHook.LeftClickActionOccurred += OnLeftClickAction;
        _gameWatcher.InGameStatusChanged += OnInGameStatusChanged;
        _gameWatcher.ChatStatusChanged += OnChatStatusChanged;

        _farmTimerManager.TimerTick += (r1, r2) => FarmTimerUpdated?.Invoke(r1, r2);
        _farmTimerManager.AlarmStateChanged += (msg) => Log($"[Cảnh báo] {msg}", Color.OrangeRed);

        _f2LoopTimer.Interval = 120; // Fire H -> C every 120ms while holding F2
        _f2LoopTimer.Tick += F2Loop_Tick;
    }

    public void SetFarmTimerInterval(int seconds)
    {
        _farmTimerManager.IntervalSeconds = seconds;
        Log($"[Cấu hình] Đã cài đặt thời gian đếm hạn ruộng thành: {seconds} giây.", Color.DarkCyan);
    }

    public void Start()
    {
        _keyboardHook.Start();
        _mouseHook.Start();
        _gameWatcher.Start();
        SetState(MacroState.Disabled, "Khởi tạo hệ thống: Trạng thái [Tắt] (Bấm F1 để bật Macro).");
    }

    public void Stop()
    {
        SystemPolicyManager.SetLockWorkstationDisabled(false);
        _f2LoopTimer.Stop();
        _farmTimerManager.StopAllTimersAndAlarms();
        _isPhysicalShiftDown = false;
        _isRightMouseDown = false;
        _isShiftTemporarilyReleasedForMouse = false;
        InputSimulator.ReleaseShiftKeysHardware();
        _isAltDown = false;
        _isAltCombo = false;
        InputSimulator.ReleaseAltKeysHardware();
        ExitFlagMode();
        _gameWatcher.Stop();
        _mouseHook.Stop();
        _keyboardHook.Stop();
    }

    public void ToggleF1()
    {
        if (_currentState == MacroState.Disabled)
        {
            MidiPlayer.PlayToggleOnSound();
            _keyboardHook.PromoteHookToTop();
            _mouseHook.PromoteHookToTop();
            if (_gameWatcher.IsInGame)
            {
                SetState(MacroState.Active, "Macro BẬT (F1) -> Trạng thái: [Hoạt động]");
            }
            else
            {
                _savedStateBeforeUnfocus = MacroState.Active;
                SetState(MacroState.SuspendedOutOfGame, "Macro BẬT (F1) -> Trạng thái: [Tạm dừng (Ngoài game)]");
            }
        }
        else
        {
            ResetAllChains();
            _ctrlFCount = 0;
            _ctrlGCount = 0;
            _activeFarmGroup = 0;
            _isPhysicalShiftDown = false;
            _isRightMouseDown = false;
            _isShiftTemporarilyReleasedForMouse = false;
            InputSimulator.ReleaseShiftKeysHardware();
            _isAltDown = false;
            _isAltCombo = false;
            InputSimulator.ReleaseAltKeysHardware();
            ExitFlagMode();
            _winKeyState = 0;
            _lastWinKeyTime = DateTime.MinValue;
            _f2LoopTimer.Stop();
            _isF2Pressed = false;
            _farmTimerManager.StopAllTimersAndAlarms();
            MidiPlayer.PlayToggleOffSound();
            SetState(MacroState.Disabled, "Macro TẮT (F1) -> Trạng thái: [Vô hiệu hóa]");
        }
    }

    private void OnMouseClick()
    {
        ResetAllChains();
    }

    private void OnRightButtonDown()
    {
        _isRightMouseDown = true;
        if (_isFlagModeActive)
        {
            // Trong chế độ đặt cờ, duy trì SHIFT để chuột phải cắm cờ (waypoint)
            return;
        }

        if (_currentState == MacroState.Active && _gameWatcher.IsInGame && _isPhysicalShiftDown)
        {
            _isShiftTemporarilyReleasedForMouse = true;
            InputSimulator.ReleaseShiftKeysHardware();
        }
    }

    private void OnRightButtonUp()
    {
        _isRightMouseDown = false;
        if (_isShiftTemporarilyReleasedForMouse)
        {
            _isShiftTemporarilyReleasedForMouse = false;
            Task.Run(async () =>
            {
                await Task.Delay(15);
                if (_currentState == MacroState.Active && _gameWatcher.IsInGame && _isPhysicalShiftDown && !_isShiftTemporarilyReleasedForMouse && !_isRightMouseDown)
                {
                    InputSimulator.SendKeyDown((ushort)Keys.ShiftKey);
                }
            });
        }
    }

    private bool OnLeftClickAction(int msg)
    {
        if (_currentState != MacroState.Active || !_gameWatcher.IsInGame)
        {
            return false;
        }

        if (_isFlagModeActive)
        {
            ExitFlagMode();
            Log("[Đặt cờ] Click chuột trái -> Nhả SHIFT, chuyển thành Chuột Phải và Tắt chế độ đặt cờ", Color.Teal);
            MidiPlayer.PlayToggleOffSound();

            Task.Run(() =>
            {
                RunActionSync(() =>
                {
                    InputSimulator.ReleaseShiftKeysHardware();
                    Thread.Sleep(15);
                    InputSimulator.SendRightClick(25);
                });
            });

            return true;
        }

        return false;
    }

    private void ReleaseAllFlagArrowKeys()
    {
        InputSimulator.SendKeyUp((ushort)Keys.Up);
        InputSimulator.SendKeyUp((ushort)Keys.Left);
        InputSimulator.SendKeyUp((ushort)Keys.Down);
        InputSimulator.SendKeyUp((ushort)Keys.Right);
    }

    private void ExitFlagMode()
    {
        if (_isFlagModeActive)
        {
            _isFlagModeActive = false;
            ReleaseAllFlagArrowKeys();
            InputSimulator.ReleaseShiftKeysHardware();
        }
    }

    private bool OnMiddleClickAction(int msg)
    {
        if (_currentState != MacroState.Active || !_gameWatcher.IsInGame)
        {
            return false;
        }

        ResetAllChains();
        Log("[Chức năng: Delete] Click chuột giữa -> [CTRL+6 -> Click chuột trái -> Delete -> 6]", Color.Crimson);
        RunActionSync(() =>
        {
            InputSimulator.SendCtrlKeyCombo((ushort)Keys.D6);
            Thread.Sleep(15);
            InputSimulator.SendMouseClickHold(25);
            Thread.Sleep(15);
            InputSimulator.SendKeyPress((ushort)Keys.Delete, 15);
            Thread.Sleep(15);
            InputSimulator.SendKeyPress((ushort)Keys.D6, 10);
        });

        return true;
    }

    private void ResetBuildingState()
    {
        _lastBuildingKey = Keys.None;
        _lastBuildingTime = DateTime.MinValue;
    }

    private void ResetAllChains()
    {
        ResetBuildingState();
        _lastTabTime = DateTime.MinValue;
        _lastShiftMilitaryKey = Keys.None;
        _lastShiftMilitaryTime = DateTime.MinValue;

        if (_isVayEActive)
        {
            _isVayEActive = false;
            _vayECount = 0;
        }
    }

    private void OnInGameStatusChanged(bool inGame)
    {
        if (!inGame)
        {
            _isPhysicalShiftDown = false;
            _isRightMouseDown = false;
            _isShiftTemporarilyReleasedForMouse = false;
            InputSimulator.ReleaseShiftKeysHardware();
            _isAltDown = false;
            _isAltCombo = false;
            InputSimulator.ReleaseAltKeysHardware();
            ExitFlagMode();

            if (_currentState == MacroState.Active || _currentState == MacroState.SuspendedChat)
            {
                _savedStateBeforeUnfocus = _currentState;
                SetState(MacroState.SuspendedOutOfGame, "Game mất focus (Ra ngoài game). Tạm dừng macro.");
            }
        }
        else
        {
            _keyboardHook.PromoteHookToTop();
            _mouseHook.PromoteHookToTop();

            if (_currentState == MacroState.SuspendedOutOfGame)
            {
                SetState(_savedStateBeforeUnfocus, $"Quay lại game -> Tiếp tục: {_savedStateBeforeUnfocus.ToDisplayName()}");
            }
        }
    }

    private void OnChatStatusChanged(bool inChat)
    {
    }

    private void SetState(MacroState newState, string logMsg)
    {
        if (_currentState != newState)
        {
            _currentState = newState;
            _farmTimerManager.IsEnabled = (_currentState != MacroState.Disabled);

            if (_currentState != MacroState.Active)
            {
                ExitFlagMode();
            }

            // Toggle Windows LockWorkstation policy (Win+L screen lock)
            SystemPolicyManager.SetLockWorkstationDisabled(_currentState == MacroState.Active);

            StateChanged?.Invoke(_currentState);
            Log(logMsg, GetStateLogColor(_currentState));
        }
    }

    private static Color GetStateLogColor(MacroState state) => state switch
    {
        MacroState.Active => Color.Green,
        MacroState.SuspendedChat => Color.Orange,
        MacroState.SuspendedOutOfGame => Color.Purple,
        _ => Color.Red
    };

    private bool OnKeyAction(uint vkCode, bool isKeyDown)
    {
        Keys key = (Keys)vkCode;

        // 1. Phím F1 Bật/Tắt luôn được xử lý và tiêu thụ
        if (key == Keys.F1 && isKeyDown)
        {
            ToggleF1();
            return true;
        }

        // Nếu Macro chưa Bật mà nhấn F2 -> Tự động bật Macro lên trước khi thực hiện Khởi đầu nhanh
        if (key == Keys.F2 && isKeyDown && _currentState == MacroState.Disabled)
        {
            ToggleF1();
        }

        // Nếu Macro ở trạng thái Tắt (Disabled) -> Cho phím đi qua hoàn toàn
        if (_currentState == MacroState.Disabled)
        {
            return false;
        }

        // Khóa hoàn toàn chức năng phím Windows khi Macro đang Bật
        if (key == Keys.LWin || key == Keys.RWin)
        {
            return true;
        }

        // Phải ở trong cửa sổ Game để thực thi các macro game
        if (!_gameWatcher.IsInGame)
        {
            return false;
        }

        // 2. Chat toggles (Enter & Escape)
        if (_currentState == MacroState.Active)
        {
            if (isKeyDown && key == Keys.Enter)
            {
                _savedStateBeforeUnfocus = MacroState.SuspendedChat;
                SetState(MacroState.SuspendedChat, "Macro Tạm dừng (Bắt đầu chat...)");
                return false;
            }
        }
        else if (_currentState == MacroState.SuspendedChat)
        {
            if (isKeyDown && (key == Keys.Enter || key == Keys.Escape))
            {
                _savedStateBeforeUnfocus = MacroState.Active;
                SetState(MacroState.Active, "Macro Hoạt động (Gửi chat hoặc Thoát...)");
                return false;
            }
        }

        // Nếu không ở trạng thái Active (Hoạt động) -> Cho phím đi qua
        if (_currentState != MacroState.Active)
        {
            return false;
        }

        if (key == Keys.ShiftKey || key == Keys.LShiftKey || key == Keys.RShiftKey)
        {
            _isPhysicalShiftDown = isKeyDown;
        }

        bool ctrlPressed = (NativeMethods.GetAsyncKeyState((int)Keys.ControlKey) & 0x8000) != 0 ||
                           (NativeMethods.GetAsyncKeyState((int)Keys.LControlKey) & 0x8000) != 0 ||
                           (NativeMethods.GetAsyncKeyState((int)Keys.RControlKey) & 0x8000) != 0 ||
                           (NativeMethods.GetKeyState((int)Keys.ControlKey) & 0x8000) != 0;

        bool shiftPressed = _isPhysicalShiftDown ||
                            (NativeMethods.GetAsyncKeyState((int)Keys.ShiftKey) & 0x8000) != 0 ||
                            (NativeMethods.GetAsyncKeyState((int)Keys.LShiftKey) & 0x8000) != 0 ||
                            (NativeMethods.GetAsyncKeyState((int)Keys.RShiftKey) & 0x8000) != 0 ||
                            (NativeMethods.GetKeyState((int)Keys.ShiftKey) & 0x8000) != 0;

        if (shiftPressed)
        {
            _isPhysicalShiftDown = true;
        }

        bool altPressed = _isAltDown ||
                          (NativeMethods.GetAsyncKeyState((int)Keys.Menu) & 0x8000) != 0 ||
                          (NativeMethods.GetAsyncKeyState((int)Keys.LMenu) & 0x8000) != 0 ||
                          (NativeMethods.GetAsyncKeyState((int)Keys.RMenu) & 0x8000) != 0 ||
                          (NativeMethods.GetKeyState((int)Keys.Menu) & 0x8000) != 0;

        // Xử lý sự kiện nhả phím SHIFT (SHIFT KeyUp)
        if (!isKeyDown && (key == Keys.ShiftKey || key == Keys.LShiftKey || key == Keys.RShiftKey))
        {
            _isPhysicalShiftDown = false;
            _isRightMouseDown = false;
            _isShiftTemporarilyReleasedForMouse = false;
            if (!_isFlagModeActive)
            {
                RunActionSync(() =>
                {
                    InputSimulator.ReleaseShiftKeysHardware();
                });
            }
            else
            {
                RunActionSync(() =>
                {
                    InputSimulator.SendKeyDown((ushort)Keys.ShiftKey);
                });
            }
        }

        // Xử lý sự kiện nhả phím CTRL (CTRL KeyUp)
        if (!isKeyDown && (key == Keys.ControlKey || key == Keys.LControlKey || key == Keys.RControlKey))
        {
            if (_ctrlFCount > 0 || _ctrlGCount > 0 || _activeFarmGroup > 0)
            {
                Log("[Đạo ruộng] Nhả CTRL -> [CTRL up]", Color.DarkGreen);
                RunActionSync(() =>
                {
                    InputSimulator.SendKeyUp((ushort)Keys.ControlKey);
                });
                _ctrlFCount = 0;
                _ctrlGCount = 0;
                _activeFarmGroup = 0;
            }

            if (_isVayEActive)
            {
                _isVayEActive = false;
                _vayECount = 0;
                Log($"[Vẩy E] Nhả CTRL -> Đặt móng cuối & Chọn lại đạo quân trước ({_previousMilitaryGroup})", Color.Crimson);
                RunActionSync(() =>
                {
                    InputSimulator.SendMouseClickHold(25);
                    Thread.Sleep(15);
                    InputSimulator.SendKeyPress((ushort)_previousMilitaryGroup);
                });
            }
        }

        // Ghi nhớ đạo quân đã chọn (1..6)
        if (isKeyDown && key >= Keys.D1 && key <= Keys.D6 && !ctrlPressed && !shiftPressed && !altPressed)
        {
            _previousMilitaryGroup = key;
        }

        bool isAltKey = (key == Keys.Menu || key == Keys.LMenu || key == Keys.RMenu);

        // ----------------------------------------------------
        // **Xử lý phím ALT: Chuẩn bị cho kích đời 3 & chống Menu Mode**
        // ----------------------------------------------------
        if (isAltKey)
        {
            if (isKeyDown)
            {
                if (!_isAltDown)
                {
                    _isAltDown = true;
                    _isAltCombo = (ctrlPressed || shiftPressed);
                    _altPressTime = DateTime.Now;
                }
                return true; // Chặn phím ALT truyền xuống Windows/Game ngay từ đầu để tránh vào Menu mode
            }
            else // ALT KeyUp
            {
                bool wasAltDown = _isAltDown;
                bool wasCombo = _isAltCombo;
                double pressDuration = (DateTime.Now - _altPressTime).TotalMilliseconds;
                _isAltDown = false;
                _isAltCombo = false;

                if (wasAltDown && !wasCombo && pressDuration <= 1500 && !ctrlPressed && !shiftPressed)
                {
                    // Người dùng tap phím ALT thuần túy -> Kích hoạt chuỗi Chuẩn bị cho kích đời 3!
                    ExecuteAge3FastUpgrade();
                    return true; // Tiêu thụ hoàn toàn sự kiện ALT up để Windows không vào Menu Mode
                }
                else
                {
                    InputSimulator.ReleaseAltKeysHardware();
                    return false;
                }
            }
        }

        // Nếu đang giữ ALT mà ấn phím khác -> Đánh dấu là tổ hợp phím (Combo)
        if (_isAltDown && !isAltKey)
        {
            _isAltCombo = true;
            // Cho phép các tổ hợp hệ thống như ALT + TAB, ALT + F4 hoạt động bình thường
            InputSimulator.SendKeyDown((ushort)Keys.Menu);
            if (key == Keys.Tab || key == Keys.F4)
            {
                return false;
            }
        }

        // KHÔNG CHẮN TÍNH NĂNG ALT + TAB
        if (altPressed && key == Keys.Tab)
        {
            return false;
        }

        // ----------------------------------------------------
        // **Chức năng: Đặt cờ (Flag/Waypoint Mode)** (Phím CAPS LOCK toggle)
        // ----------------------------------------------------
        if (key == Keys.Capital)
        {
            if (isKeyDown)
            {
                _isFlagModeActive = !_isFlagModeActive;
                if (_isFlagModeActive)
                {
                    Log("[Đặt cờ] BẬT chế độ đặt cờ -> Giữ SHIFT down, AWSD chuyển thành 4 phím mũi tên", Color.Teal);
                    MidiPlayer.PlayToggleOnSound();
                    RunActionSync(() =>
                    {
                        InputSimulator.SendKeyDown((ushort)Keys.ShiftKey);
                    });
                }
                else
                {
                    Log("[Đặt cờ] TẮT chế độ đặt cờ -> Nhả SHIFT, AWSD trở về bình thường", Color.Teal);
                    MidiPlayer.PlayToggleOffSound();
                    ExitFlagMode();
                }
            }
            return true; // Luôn chặn CAPS LOCK để không làm đảo lộn trạng thái gõ chữ hoa của Windows
        }

        // ----------------------------------------------------
        // **Chế độ đặt cờ: AWSD -> 4 phím mũi tên (Di chuyển góc nhìn)**
        // ----------------------------------------------------
        if (_isFlagModeActive)
        {
            ushort arrowKey = key switch
            {
                Keys.W => (ushort)Keys.Up,
                Keys.A => (ushort)Keys.Left,
                Keys.S => (ushort)Keys.Down,
                Keys.D => (ushort)Keys.Right,
                _ => 0
            };

            if (arrowKey != 0)
            {
                if (isKeyDown)
                {
                    InputSimulator.SendKeyDown(arrowKey);
                }
                else
                {
                    InputSimulator.SendKeyUp(arrowKey);
                }
                return true;
            }
        }

        // ----------------------------------------------------
        // **Chức năng: Khởi đầu nhanh** (F2)
        // ----------------------------------------------------
        if (key == Keys.F2)
        {
            if (isKeyDown && !_isF2Pressed)
            {
                _isF2Pressed = true;
                Log("[Chức năng: Khởi đầu nhanh] Bắt đầu H -> C liên tục (Giữ F2)...", Color.DarkBlue);
                ExecuteH_C();
                _f2LoopTimer.Start();
            }
            else if (!isKeyDown && _isF2Pressed)
            {
                _isF2Pressed = false;
                _f2LoopTimer.Stop();
                Log("[Chức năng: Khởi đầu nhanh] Nhả F2 -> Thực thi [F4 -> F11]", Color.DarkBlue);
                ExecuteF4_F11();
            }
            return true;
        }

        if (!isKeyDown)
        {
            return IsMacroKey(key, ctrlPressed, shiftPressed);
        }

        // ----------------------------------------------------
        // **Chức năng: Vẩy E** (Giữ CTRL + E)
        // ----------------------------------------------------
        if (ctrlPressed && key == Keys.E)
        {
            ResetBuildingState();

            if (!_isVayEActive || _vayECount == 0)
            {
                _isVayEActive = true;
                _vayECount = 1;
                Log("[Vẩy E] Lần 1 (Giữ CTRL+E) -> 7 -> B -> E (Chọn dân đạo 7 & Lấy móng BE)", Color.Crimson);
                RunActionSync(() =>
                {
                    InputSimulator.SendKeyUp((ushort)Keys.ControlKey);
                    Thread.Sleep(5);
                    InputSimulator.SendKeyPress((ushort)Keys.D7, 10);
                    Thread.Sleep(10);
                    InputSimulator.SendKeyPress((ushort)Keys.B, 10);
                    Thread.Sleep(10);
                    InputSimulator.SendKeyPress((ushort)Keys.E, 10);
                    Thread.Sleep(5);
                    InputSimulator.SendKeyDown((ushort)Keys.ControlKey);
                });
            }
            else
            {
                _vayECount++;
                Log($"[Vẩy E] Lần {_vayECount} (Giữ CTRL+E) -> Click Trái -> B -> E (Hạ móng & Lấy móng BE mới)", Color.Crimson);
                RunActionSync(() =>
                {
                    InputSimulator.SendKeyUp((ushort)Keys.ControlKey);
                    Thread.Sleep(5);
                    InputSimulator.SendMouseClickHold(25);
                    Thread.Sleep(15);
                    InputSimulator.SendKeyPress((ushort)Keys.B, 10);
                    Thread.Sleep(10);
                    InputSimulator.SendKeyPress((ushort)Keys.E, 10);
                    Thread.Sleep(5);
                    InputSimulator.SendKeyDown((ushort)Keys.ControlKey);
                });
            }
            return true;
        }


        // ----------------------------------------------------
        // **Chức năng: Đạo quân nhanh** (SHIFT + 1..6, CTRL + `)
        // ----------------------------------------------------
        if (shiftPressed && key >= Keys.D1 && key <= Keys.D6)
        {
            ResetAllChains();
            _previousMilitaryGroup = key;
            int num = (int)(key - Keys.D0);
            ushort numVk = (ushort)key;
            Log($"[Đạo quân nhanh] SHIFT+{num} -> [SHIFT+{num} -> CTRL+{num} -> SPACE]", Color.SeaGreen);
            RunActionSync(() =>
            {
                InputSimulator.SendShiftKeyCombo(numVk);
                Thread.Sleep(10);
                InputSimulator.SendCtrlKeyCombo(numVk);
                Thread.Sleep(10);
                InputSimulator.SendKeyPress((ushort)Keys.Space);
            });
            return true;
        }

        if (ctrlPressed && key == Keys.Oemtilde) // CTRL + `
        {
            ResetAllChains();
            Log("[Đạo quân nhanh] CTRL+` -> CTRL+0", Color.SeaGreen);
            RunActionSync(() =>
            {
                InputSimulator.SendCtrlKeyCombo((ushort)Keys.D0);
            });
            return true;
        }

        // ----------------------------------------------------
        // **Chức năng: Đạo ruộng nhanh** (CTRL+F, SHIFT+F, CTRL+G, SHIFT+G)
        // ----------------------------------------------------
        if (ctrlPressed && key == Keys.F) // CTRL + F: Đạo ruộng 1 (đạo 7)
        {
            ResetBuildingState();
            _farmTimerManager.StartTimer1();

            bool isSwitchingFromG = (_activeFarmGroup == 2);
            if (isSwitchingFromG)
            {
                _ctrlGCount = 0;
                _ctrlFCount = 1;
                _activeFarmGroup = 1;
                Log("[Đạo ruộng 1] Chuyển từ Ruộng 2 sang Ruộng 1 -> [ESC -> 7 -> CTRL down]", Color.DarkGreen);
                RunActionSync(() =>
                {
                    InputSimulator.ReleaseCtrlKeysHardware();
                    Thread.Sleep(10);
                    InputSimulator.SendKeyPress((ushort)Keys.Escape, 10);
                    Thread.Sleep(15);
                    InputSimulator.SendKeyPress((ushort)Keys.D7, 10);
                    Thread.Sleep(10);
                    InputSimulator.SendKeyDown((ushort)Keys.ControlKey);
                });
            }
            else
            {
                _ctrlFCount++;
                _activeFarmGroup = 1;

                if (_ctrlFCount == 1)
                {
                    Log("[Đạo ruộng 1] CTRL+F (Lần 1) -> [7 -> CTRL down]", Color.DarkGreen);
                    RunActionSync(() =>
                    {
                        InputSimulator.ReleaseCtrlKeysHardware();
                        Thread.Sleep(10);
                        InputSimulator.SendKeyPress((ushort)Keys.D7, 10);
                        Thread.Sleep(10);
                        InputSimulator.SendKeyDown((ushort)Keys.ControlKey);
                    });
                }
                else
                {
                    Log($"[Đạo ruộng 1] CTRL+F (Lần {_ctrlFCount}) -> [7]", Color.DarkGreen);
                    RunActionSync(() =>
                    {
                        InputSimulator.SendKeyPress((ushort)Keys.D7, 10);
                    });
                }
            }
            return true;
        }

        if (shiftPressed && key == Keys.F) // SHIFT + F: Làm mới đạo ruộng 1 (ESC -> 7 -> S -> SPACE)
        {
            ResetAllChains();
            Log("[Đạo ruộng 1] SHIFT+F -> Làm mới đạo ruộng 1 (ESC -> 7 -> S -> SPACE)", Color.DarkGreen);
            _farmTimerManager.RestartTimer1();
            RunActionSync(() =>
            {
                InputSimulator.ReleaseShiftKeysHardware();
                Thread.Sleep(10);
                InputSimulator.SendKeyPress((ushort)Keys.Escape);
                Thread.Sleep(10);
                InputSimulator.SendKeyPress((ushort)Keys.D7);
                Thread.Sleep(10);
                InputSimulator.SendKeyPress((ushort)Keys.S);
                Thread.Sleep(10);
                InputSimulator.SendKeyPress((ushort)Keys.Space);
                Thread.Sleep(10);
                if (_isPhysicalShiftDown)
                {
                    InputSimulator.SendKeyDown((ushort)Keys.ShiftKey);
                }
            });
            return true;
        }

        if (ctrlPressed && key == Keys.G) // CTRL + G: Đạo ruộng 2 (đạo 8)
        {
            ResetBuildingState();
            _farmTimerManager.StartTimer2();

            bool isSwitchingFromF = (_activeFarmGroup == 1);
            if (isSwitchingFromF)
            {
                _ctrlFCount = 0;
                _ctrlGCount = 1;
                _activeFarmGroup = 2;
                Log("[Đạo ruộng 2] Chuyển từ Ruộng 1 sang Ruộng 2 -> [ESC -> 8 -> CTRL down]", Color.DarkGreen);
                RunActionSync(() =>
                {
                    InputSimulator.ReleaseCtrlKeysHardware();
                    Thread.Sleep(10);
                    InputSimulator.SendKeyPress((ushort)Keys.Escape, 10);
                    Thread.Sleep(15);
                    InputSimulator.SendKeyPress((ushort)Keys.D8, 10);
                    Thread.Sleep(10);
                    InputSimulator.SendKeyDown((ushort)Keys.ControlKey);
                });
            }
            else
            {
                _ctrlGCount++;
                _activeFarmGroup = 2;

                if (_ctrlGCount == 1)
                {
                    Log("[Đạo ruộng 2] CTRL+G (Lần 1) -> [8 -> CTRL down]", Color.DarkGreen);
                    RunActionSync(() =>
                    {
                        InputSimulator.ReleaseCtrlKeysHardware();
                        Thread.Sleep(10);
                        InputSimulator.SendKeyPress((ushort)Keys.D8, 10);
                        Thread.Sleep(10);
                        InputSimulator.SendKeyDown((ushort)Keys.ControlKey);
                    });
                }
                else
                {
                    Log($"[Đạo ruộng 2] CTRL+G (Lần {_ctrlGCount}) -> [8]", Color.DarkGreen);
                    RunActionSync(() =>
                    {
                        InputSimulator.SendKeyPress((ushort)Keys.D8, 10);
                    });
                }
            }
            return true;
        }

        if (shiftPressed && key == Keys.G) // SHIFT + G: Làm mới đạo ruộng 2 (ESC -> 8 -> S -> SPACE)
        {
            ResetAllChains();
            Log("[Đạo ruộng 2] SHIFT+G -> Làm mới đạo ruộng 2 (ESC -> 8 -> S -> SPACE)", Color.DarkGreen);
            _farmTimerManager.RestartTimer2();
            RunActionSync(() =>
            {
                InputSimulator.ReleaseShiftKeysHardware();
                Thread.Sleep(10);
                InputSimulator.SendKeyPress((ushort)Keys.Escape);
                Thread.Sleep(10);
                InputSimulator.SendKeyPress((ushort)Keys.D8);
                Thread.Sleep(10);
                InputSimulator.SendKeyPress((ushort)Keys.S);
                Thread.Sleep(10);
                InputSimulator.SendKeyPress((ushort)Keys.Space);
                Thread.Sleep(10);
                if (_isPhysicalShiftDown)
                {
                    InputSimulator.SendKeyDown((ushort)Keys.ShiftKey);
                }
            });
            return true;
        }

        // ----------------------------------------------------
        // **Chức năng: Tab công nghệ** (CTRL+TAB, TAB)
        // ----------------------------------------------------
        if (key == Keys.Tab)
        {
            ResetBuildingState();

            if (ctrlPressed)
            {
                Log("[Tab công nghệ] CTRL+TAB -> [SHIFT+9 -> CTRL+9]", Color.DarkOrange);
                RunActionSync(() =>
                {
                    InputSimulator.SendShiftKeyCombo((ushort)Keys.D9);
                    Thread.Sleep(10);
                    InputSimulator.SendCtrlKeyCombo((ushort)Keys.D9);
                });
            }
            else
            {
                DateTime now = DateTime.Now;
                bool isConsecutiveTab = (_lastTabTime != DateTime.MinValue) && ((now - _lastTabTime).TotalSeconds <= 20.0);
                _lastTabTime = now;

                if (isConsecutiveTab)
                {
                    Log("[Tab công nghệ] TAB (Liên tiếp) -> Duyệt nhà công nghệ (TAB)", Color.DarkOrange);
                    RunActionSync(() =>
                    {
                        InputSimulator.SendKeyPress((ushort)Keys.Tab);
                    });
                }
                else
                {
                    Log("[Tab công nghệ] TAB (Lần 1) -> Chọn đạo công nghệ 9 (Phím 9)", Color.DarkOrange);
                    RunActionSync(() =>
                    {
                        InputSimulator.SendKeyPress((ushort)Keys.D9);
                    });
                }
            }

            return true;
        }

        // ----------------------------------------------------
        // **Chức năng: Xin quân nhanh** (SHIFT + Phím)
        // Từ lần 2 trở đi: [Click chuột (Giữ 25ms) -> CTRL + Key]
        // ----------------------------------------------------
        if (shiftPressed && TryGetMilitaryTargetKey(key, out ushort shiftTargetVk, out string shiftMilitaryDesc))
        {
            ResetBuildingState();
            DateTime now = DateTime.Now;
            bool isConsecutive = (key == _lastShiftMilitaryKey) && ((now - _lastShiftMilitaryTime).TotalSeconds <= 20.0);
            _lastShiftMilitaryKey = key;
            _lastShiftMilitaryTime = now;

            if (isConsecutive)
            {
                Log($"[Xin quân nhanh] (Từ lần 2): Shift+{key} -> Click Trái (Giữ 25ms) -> CTRL+{((Keys)shiftTargetVk)} ({shiftMilitaryDesc})", Color.DarkMagenta);
                RunActionSync(() =>
                {
                    InputSimulator.SendMouseClickHold(25); // Left Down -> Hold 25ms -> Left Up
                    Thread.Sleep(10);
                    InputSimulator.SendCtrlKeyCombo(shiftTargetVk);
                });
            }
            else
            {
                Log($"[Xin quân nhanh] (Lần 1): Shift+{key} -> CTRL+{((Keys)shiftTargetVk)} ({shiftMilitaryDesc})", Color.DarkMagenta);
                RunActionSync(() =>
                {
                    InputSimulator.SendCtrlKeyCombo(shiftTargetVk);
                });
            }
            return true;
        }

        // ----------------------------------------------------
        // **Chức năng: Duyệt nhà binh** (CTRL + Phím)
        // ----------------------------------------------------
        if (ctrlPressed && TryGetMilitaryTargetKey(key, out ushort ctrlTargetVk, out string ctrlMilitaryDesc))
        {
            ResetBuildingState();
            Log($"[Duyệt nhà binh] CTRL+{key} -> CTRL+{((Keys)ctrlTargetVk)} ({ctrlMilitaryDesc})", Color.DarkGreen);
            RunActionSync(() =>
            {
                InputSimulator.SendCtrlKeyCombo(ctrlTargetVk);
            });
            return true;
        }

        // ----------------------------------------------------
        // **Chức năng: Mở bảng ngoại giao** (F3, F4)
        // ----------------------------------------------------
        if (key == Keys.F3)
        {
            ResetBuildingState();
            HandleF3Diplomacy();
            return true;
        }

        if (key == Keys.F4)
        {
            ResetBuildingState();
            HandleF4Timeline();
            return true;
        }

        // ----------------------------------------------------
        // **Chức năng: Remap** (F12, `, Q, W)
        // ----------------------------------------------------
        if (key == Keys.F12)
        {
            ResetBuildingState();
            Log("[Remap F12 -> F3] Tạm dừng game (Gửi F3)", Color.Purple);
            RunActionSync(() => InputSimulator.SendKeyPress((ushort)Keys.F3));
            return true;
        }

        if (key == Keys.Oemtilde) // Phím `
        {
            ResetBuildingState();
            Log("[Remap ` -> S] Dừng quân S", Color.Purple);
            RunActionSync(() => InputSimulator.SendKeyPress((ushort)Keys.S));
            return true;
        }

        if (key == Keys.Q)
        {
            ResetBuildingState();
            Log("[Remap Q -> C] Xin dân C", Color.Purple);
            RunActionSync(() => InputSimulator.SendKeyPress((ushort)Keys.C));
            return true;
        }

        if (key == Keys.W)
        {
            ResetBuildingState();
            Log("[Remap W -> H] Nhà chính H", Color.Purple);
            RunActionSync(() => InputSimulator.SendKeyPress((ushort)Keys.H));
            return true;
        }

        // ----------------------------------------------------
        // **Chức năng: Click 5 ô biểu tượng** (Numpad 1..5)
        // ----------------------------------------------------
        if (key >= Keys.NumPad1 && key <= Keys.NumPad5)
        {
            int slotIndex = (int)key - (int)Keys.NumPad1 + 1;
            ResetBuildingState();
            Log($"[Numpad {slotIndex}] Click biểu tượng số {slotIndex}", Color.DarkGreen);
            RunActionSync(() => InputSimulator.ClickActionButton(slotIndex));
            return true;
        }


        // ----------------------------------------------------
        // **Chức năng: Xây các loại nhà nhanh** (Mẫu 1)
        // (E, R, T, V, F, G, B, N, A, S, Z, X, D, C)
        // ----------------------------------------------------
        if (!ctrlPressed && !shiftPressed && TryGetBuildingMapping(key, out ushort firstKey, out ushort secondKey, out string buildingName))
        {
            DateTime now = DateTime.Now;
            bool isConsecutive = (key == _lastBuildingKey) && ((now - _lastBuildingTime).TotalSeconds <= 20.0);
            bool isSwitching = (_lastBuildingKey != Keys.None) && (key != _lastBuildingKey) && ((now - _lastBuildingTime).TotalSeconds <= 20.0);

            _lastBuildingKey = key;
            _lastBuildingTime = now;

            if (isConsecutive)
            {
                Log($"[Xây nhà Mẫu 1] (Liên tiếp) Click Trái -> B -> {key} ({buildingName})", Color.DarkCyan);
                RunActionSync(() =>
                {
                    InputSimulator.SendMouseClick();
                    Thread.Sleep(50); // Đợi game nhận diện móng đã đặt
                    InputSimulator.SendKeyPress(firstKey, 15);
                    Thread.Sleep(15);
                    InputSimulator.SendKeyPress(secondKey, 15);
                });
            }
            else
            {
                Log($"[Xây nhà Mẫu 1] (Lần 1{(isSwitching ? " - Đổi nhà, gửi ESC trước" : "")}) -> B -> {key} ({buildingName})", Color.DarkCyan);
                RunActionSync(() =>
                {
                    if (isSwitching)
                    {
                        InputSimulator.SendKeyPress((ushort)Keys.Escape, 15);
                        Thread.Sleep(15);
                    }
                    InputSimulator.SendKeyPress(firstKey, 15);
                    Thread.Sleep(15);
                    InputSimulator.SendKeyPress(secondKey, 15);
                });
            }

            return true;
        }

        // Any other non-macro key resets building and tab chain
        ResetAllChains();
        return false;
    }

    private void RunActionSync(Action action)
    {
        Task.Run(() =>
        {
            if (_actionLock.Wait(1000))
            {
                try
                {
                    action();
                }
                finally
                {
                    _actionLock.Release();
                }
            }
        });
    }

    private static bool TryGetMilitaryTargetKey(Keys key, out ushort targetVk, out string desc)
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
                targetVk = 0; desc = ""; return false;
        }
    }

    private static bool TryGetBuildingMapping(Keys key, out ushort firstKey, out ushort secondKey, out string name)
    {
        firstKey = (ushort)Keys.B;

        switch (key)
        {
            case Keys.E:
                secondKey = (ushort)Keys.E; name = "Nhà Nhà Dân BE"; return true;
            case Keys.R:
                secondKey = (ushort)Keys.S; name = "Nhà Kho BS"; return true;
            case Keys.T:
                secondKey = (ushort)Keys.G; name = "Nhà Chứa Ruộng BG"; return true;
            case Keys.V:
                secondKey = (ushort)Keys.M; name = "Nhà Chợ BM"; return true;
            case Keys.F:
            case Keys.G:
                secondKey = (ushort)Keys.F; name = "Ruộng BF"; return true;
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

    private bool IsMacroKey(Keys key, bool ctrlPressed, bool shiftPressed)
    {
        if (key == Keys.Tab || key == Keys.LWin || key == Keys.RWin || key == Keys.Capital ||
            key == Keys.Menu || key == Keys.LMenu || key == Keys.RMenu) return true;

        if (_isFlagModeActive && (key is Keys.W or Keys.A or Keys.S or Keys.D)) return true;

        if (ctrlPressed || shiftPressed)
        {
            if (key is Keys.A or Keys.S or Keys.Z or Keys.X or Keys.D or Keys.C or Keys.F or Keys.G or Keys.E)
                return true;

            if (shiftPressed && key >= Keys.D1 && key <= Keys.D6)
                return true;

            if (ctrlPressed && key == Keys.Oemtilde)
                return true;
        }

        return key switch
        {
            Keys.F1 or Keys.F2 or Keys.F3 or Keys.F4 or Keys.F12 or Keys.Oemtilde or Keys.Q or Keys.W or
            Keys.E or Keys.R or Keys.T or Keys.V or Keys.F or Keys.G or Keys.B or Keys.N or
            Keys.A or Keys.S or Keys.Z or Keys.X or Keys.D or Keys.C or
            Keys.NumPad1 or Keys.NumPad2 or Keys.NumPad3 or Keys.NumPad4 or Keys.NumPad5 => true,
            _ => false
        };
    }

    private void F2Loop_Tick(object? sender, EventArgs e)
    {
        if (_isF2Pressed && _currentState == MacroState.Active)
        {
            ExecuteH_C();
        }
    }

    private void ExecuteAge3FastUpgrade()
    {
        ResetBuildingState();
        DateTime now = DateTime.Now;

        if (_winKeyState > 0 && (now - _lastWinKeyTime).TotalSeconds > 30.0)
        {
            _winKeyState = 0;
        }

        if (_winKeyState == 0)
        {
            _winKeyState = 1;
            _lastWinKeyTime = now;
            Log("[Kích đời 3] Lần 1: H -> C -> 2 -> SPACE -> B -> M (Đặt Chợ)", Color.Brown);
            RunActionSync(() =>
            {
                InputSimulator.ReleaseAltKeysHardware();
                Thread.Sleep(5);
                InputSimulator.SendKeyPress((ushort)Keys.H, 10);
                Thread.Sleep(10);
                InputSimulator.SendKeyPress((ushort)Keys.C, 10);
                Thread.Sleep(10);
                InputSimulator.SendKeyPress((ushort)Keys.D2, 10);
                Thread.Sleep(10);
                InputSimulator.SendKeyPress((ushort)Keys.Space, 10);
                Thread.Sleep(10);
                InputSimulator.SendKeyPress((ushort)Keys.B, 10);
                Thread.Sleep(10);
                InputSimulator.SendKeyPress((ushort)Keys.M, 10);
            });
        }
        else if (_winKeyState == 1)
        {
            _winKeyState = 2;
            _lastWinKeyTime = now;
            Log("[Kích đời 3] Lần 2 (<= 30s): 3 -> SPACE -> B -> A (Đặt nhà BA)", Color.Brown);
            RunActionSync(() =>
            {
                InputSimulator.ReleaseAltKeysHardware();
                Thread.Sleep(5);
                InputSimulator.SendKeyPress((ushort)Keys.D3, 10);
                Thread.Sleep(10);
                InputSimulator.SendKeyPress((ushort)Keys.Space, 10);
                Thread.Sleep(10);
                InputSimulator.SendKeyPress((ushort)Keys.B, 10);
                Thread.Sleep(10);
                InputSimulator.SendKeyPress((ushort)Keys.A, 10);
            });
        }
        else if (_winKeyState == 2)
        {
            _winKeyState = 0;
            _lastWinKeyTime = DateTime.MinValue;
            Log("[Kích đời 3] Lần 3 (<= 30s): ESC -> 3 -> SPACE -> B -> L (Đặt nhà BL)", Color.Brown);
            RunActionSync(() =>
            {
                InputSimulator.ReleaseAltKeysHardware();
                Thread.Sleep(5);
                InputSimulator.SendKeyPress((ushort)Keys.Escape, 10);
                Thread.Sleep(10);
                InputSimulator.SendKeyPress((ushort)Keys.D3, 10);
                Thread.Sleep(10);
                InputSimulator.SendKeyPress((ushort)Keys.Space, 10);
                Thread.Sleep(10);
                InputSimulator.SendKeyPress((ushort)Keys.B, 10);
                Thread.Sleep(10);
                InputSimulator.SendKeyPress((ushort)Keys.L, 10);
            });
        }
    }

    private static void ExecuteH_C()
    {
        InputSimulator.SendKeyPress((ushort)Keys.H, 10);
        Thread.Sleep(15);
        InputSimulator.SendKeyPress((ushort)Keys.C, 10);
    }

    private static void ExecuteF4_F11()
    {
        InputSimulator.SendKeyPress((ushort)Keys.F4, 15);
        Thread.Sleep(20);
        InputSimulator.SendKeyPress((ushort)Keys.F11, 15);
    }

    private void HandleF3Diplomacy()
    {
        Log("[Chức năng: Mở bảng ngoại giao] (F3) -> Click nút Diplomacy", Color.Magenta);
        RunActionSync(() => InputSimulator.ClickDiplomacy());
    }

    private void HandleF4Timeline()
    {
        Log("[Chức năng: Mở bảng ngoại giao] (F4) -> Mở timeline [F10 -> Mũi tên xuống * 2 -> Enter]", Color.Magenta);
        RunActionSync(() =>
        {
            InputSimulator.SendKeyPress((ushort)Keys.F10, 15);
            Thread.Sleep(20);
            InputSimulator.SendKeyPress((ushort)Keys.Down, 15);
            Thread.Sleep(20);
            InputSimulator.SendKeyPress((ushort)Keys.Down, 15);
            Thread.Sleep(20);
            InputSimulator.SendKeyPress((ushort)Keys.Enter, 15);
        });
    }

    private void Log(string message, Color color)
    {
        LogRequested?.Invoke(message, color);
    }

    public void Dispose()
    {
        Stop();
        _f2LoopTimer.Dispose();
        _farmTimerManager.Dispose();
        _gameWatcher.Dispose();
        _mouseHook.Dispose();
        _keyboardHook.Dispose();
        _actionLock.Dispose();
        GC.SuppressFinalize(this);
    }
}
