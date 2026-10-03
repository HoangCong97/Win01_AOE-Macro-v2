using System.Windows.Forms;
using AOEKeyboardMacroPro.Models;

namespace AOEKeyboardMacroPro.Services;

public class ControlEngine : IDisposable
{
    private readonly KeyboardHookManager _keyboardHook = new();
    private readonly MouseHookManager _mouseHook = new();
    private readonly GameStateWatcher _gameWatcher = new();
    private readonly ChatDetectionService _chatDetector = new();
    private readonly FarmTimerManager _farmTimerManager = new();
    private readonly VayEManager _vayEManager = new();
    private readonly FastBuildManager _fastBuildManager = new();
    private readonly DeleteManager _deleteManager;
    private readonly MilitaryCycleManager _militaryCycleManager = new();
    private readonly System.Windows.Forms.Timer _f2LoopTimer = new();
    private readonly System.Windows.Forms.Timer _cursorLockTimer = new();
    private readonly SemaphoreSlim _actionLock = new(1, 1);

    public ChatDetectionService ChatDetector => _chatDetector;

    private MacroState _currentState = MacroState.Disabled;
    private MacroState _savedStateBeforeUnfocus = MacroState.Active;
    private bool _hasAppliedCursorLock = false;

    private bool _isF2Pressed = false;
    private bool _isDiplomacyOpen = false;

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

    // Đạo ruộng nhanh tracking state
    private int _ctrlFCount = 0;
    private int _ctrlGCount = 0;
    private int _activeFarmGroup = 0; // 0: None, 1: Ruộng 1 (F), 2: Ruộng 2 (G)
    private bool _isPhysicalShiftDown = false;
    private bool _isPhysicalCtrlDown = false;
    private bool _isRightMouseDown = false;
    private bool _isShiftTemporarilyReleasedForMouse = false;

    // Flag/Waypoint Mode (Chức năng đặt cờ) tracking state
    private bool _isFlagModeActive = false;

    // Farm refresh context (Làm mới ruộng SHIFT+F / SHIFT+G) tracking
    private bool _isFarmRefreshActive = false;
    private DateTime _lastFarmRefreshTime = DateTime.MinValue;

    // Khởi đầu nhanh tự động (Auto Fast Start) tracking state
    private volatile bool _isAutoStartExecuting = false;
    private DateTime _lastAutoStartTime = DateTime.MinValue;
    private bool _hasAutoStartedForCurrentGame = false;
    private bool _hasObservedNonInitialResources = false;

    public event Action<MacroState>? StateChanged;
    public event Action<string, Color>? LogRequested;
    public event Action<int, int>? FarmTimerUpdated;
    public event Action? HouseBeBuildingTriggered;
    public event Action<bool>? TestModeChanged;
    public event Action? RefreshRequested;
    public event Action<LoadingValues>? LoadingProgressUpdated;
    public event Action<LoadingValues>? LoadingCompleted;
    public event Action<UnitQueueValues>? UnitQueueUpdated;

    private LoadingValues? _currentLoading;
    public LoadingValues? CurrentLoading => _currentLoading;
    public int? CurrentLoadingRatio => _currentLoading?.Percentage;

    private UnitQueueValues? _currentUnitQueue;
    public UnitQueueValues? CurrentUnitQueue => _currentUnitQueue;
    public int? CurrentUnitQueueCount => _currentUnitQueue?.PrimaryCount;

    public void NotifyLoadingProgress(LoadingValues loading)
    {
        _currentLoading = loading;
        LoadingProgressUpdated?.Invoke(loading);
    }

    public void NotifyLoadingCompleted(LoadingValues loading)
    {
        _currentLoading = loading;
        LoadingCompleted?.Invoke(loading);
    }

    public void NotifyUnitQueue(UnitQueueValues queue)
    {
        _currentUnitQueue = queue;
        UnitQueueUpdated?.Invoke(queue);
    }

    private LoadingOcrService? _loadingOcrService;
    private UnitQueueOcrService? _unitQueueOcrService;

    public void AttachOcrServices(LoadingOcrService loadingOcr, UnitQueueOcrService queueOcr)
    {
        _loadingOcrService = loadingOcr;
        _unitQueueOcrService = queueOcr;
    }

    private bool _isTestMode = false;
    public bool IsTestMode => _isTestMode;
    public MacroState CurrentState => _currentState;

    public ControlEngine()
    {
        _deleteManager = new DeleteManager(_actionLock);
        _keyboardHook.KeyActionOccurred += OnKeyAction;
        _mouseHook.MouseClicked += OnMouseClick;
        _mouseHook.RightButtonDown += OnRightButtonDown;
        _mouseHook.RightButtonUp += OnRightButtonUp;
        _mouseHook.MiddleClickActionOccurred += OnMiddleClickAction;
        _mouseHook.LeftClickActionOccurred += OnLeftClickAction;
        _mouseHook.RightClickActionOccurred += OnRightClickAction;
        _gameWatcher.InGameStatusChanged += OnInGameStatusChanged;
        _gameWatcher.ChatStatusChanged += OnChatStatusChanged;
        _chatDetector.ChatStatusChanged += OnChatStatusChanged;

        _farmTimerManager.TimerTick += (r1, r2) => FarmTimerUpdated?.Invoke(r1, r2);
        _farmTimerManager.AlarmStateChanged += (msg) => Log($"[Cảnh báo] {msg}", Color.OrangeRed);

        _fastBuildManager.HouseBeTriggered += () =>
        {
            Log("[Cảnh báo POP] Đã nhận lệnh xây Nhà Dân BE -> Tự động ngăn cảnh báo trong 20s", Color.DarkOrange);
            HouseBeBuildingTriggered?.Invoke();
        };

        _f2LoopTimer.Interval = 120; // Fire H -> C every 120ms while holding F2
        _f2LoopTimer.Tick += F2Loop_Tick;

        _cursorLockTimer.Interval = 80; // Định kỳ kiểm tra và giữ chuột không tràn ra màn hình 2 khi chơi game
        _cursorLockTimer.Tick += CursorLockTimer_Tick;
        _cursorLockTimer.Start();
    }

    public void SetFarmTimerInterval(int seconds)
    {
        _farmTimerManager.IntervalSeconds = seconds;
        Log($"[Cấu hình] Đã cài đặt thời gian đếm hạn ruộng thành: {seconds} giây.", Color.DarkCyan);
    }

    private Thread? _hookThread;
    private ApplicationContext? _hookContext;
    private SynchronizationContext? _hookSyncContext;

    private void StartHooks()
    {
        if (_hookThread != null) return;

        using var readyEvent = new ManualResetEventSlim(false);
        _hookThread = new Thread(() =>
        {
            _hookContext = new ApplicationContext();
            _hookSyncContext = SynchronizationContext.Current ?? new WindowsFormsSynchronizationContext();
            _keyboardHook.Start();
            _mouseHook.Start();
            readyEvent.Set();

            Application.Run(_hookContext);

            _mouseHook.Stop();
            _keyboardHook.Stop();
        })
        {
            IsBackground = true,
            Name = "LowLevelHookThread"
        };
        _hookThread.SetApartmentState(ApartmentState.STA);
        _hookThread.Start();
        readyEvent.Wait(2000);
    }

    private void StopHooks()
    {
        if (_hookContext != null)
        {
            try
            {
                _hookContext.ExitThread();
            }
            catch { }
            _hookContext = null;
        }
        _hookThread = null;
        _hookSyncContext = null;
    }

    private void PromoteHooks()
    {
        if (_hookSyncContext != null)
        {
            _hookSyncContext.Post(_ =>
            {
                _keyboardHook.PromoteHookToTop();
                _mouseHook.PromoteHookToTop();
            }, null);
        }
        else
        {
            _keyboardHook.PromoteHookToTop();
            _mouseHook.PromoteHookToTop();
        }
    }

    public void Start()
    {
        MouseLockManager.Initialize();
        StartHooks();
        _gameWatcher.Start();
        _chatDetector.Start();
        SetState(MacroState.SuspendedOutOfGame, "Khởi tạo hệ thống: Macro đã BẬT -> Trạng thái: [Tạm dừng (Chờ vào trận)]");
    }

    public void UpdateInGameStatus(bool inGame)
    {
        _gameWatcher.SetInGameStatus(inGame);
    }

    public void Stop()
    {
        _cursorLockTimer.Stop();
        MouseLockManager.ForceUnlock();
        MouseLockManager.UnpinCursor();
        _hasAppliedCursorLock = false;
        _chatDetector.Stop();
        _vayEManager.Reset();
        _deleteManager.Reset();
        _militaryCycleManager.Reset();
        _isDiplomacyOpen = false;
        SystemPolicyManager.SetLockWorkstationDisabled(false);
        _f2LoopTimer.Stop();
        _farmTimerManager.StopAllTimersAndAlarms();
        _isPhysicalShiftDown = false;
        _isPhysicalCtrlDown = false;
        _isRightMouseDown = false;
        _isShiftTemporarilyReleasedForMouse = false;
        InputSimulator.ReleaseShiftKeysHardware();
        InputSimulator.ReleaseCtrlKeysHardware();
        _isAltDown = false;
        _isAltCombo = false;
        InputSimulator.ReleaseAltKeysHardware();
        ExitFlagMode();
        _isFarmRefreshActive = false;
        _lastFarmRefreshTime = DateTime.MinValue;
        _gameWatcher.Stop();
        StopHooks();
    }

    public void ToggleEnable()
    {
        if (_currentState == MacroState.Disabled)
        {
            MidiPlayer.PlayToggleOnSound();
            PromoteHooks();
            if (_gameWatcher.IsInGame)
            {
                SetState(MacroState.Active, "Macro BẬT -> Trạng thái: [Hoạt động]");
            }
            else
            {
                _savedStateBeforeUnfocus = MacroState.Active;
                SetState(MacroState.SuspendedOutOfGame, "Macro BẬT -> Trạng thái: [Tạm dừng (Chờ vào trận)]");
            }
        }
        else
        {
            ResetAllCountersToInitial();
            _hasAutoStartedForCurrentGame = false;
            _hasObservedNonInitialResources = false;
            MidiPlayer.PlayToggleOffSound();
            SetState(MacroState.Disabled, "Macro TẮT -> Trạng thái: [Vô hiệu hóa]");
        }
    }

    public void ToggleF1() => ToggleEnable();

    public void ToggleTestMode()
    {
        _isTestMode = !_isTestMode;
        if (_isTestMode)
        {
            Log("[Chế độ Test] BẬT (F6) -> Key map hoạt động tự do ngay cả khi không ở In-game (Ấn F6 để tắt)", Color.Magenta);
        }
        else
        {
            Log("[Chế độ Test] TẮT (F6) -> Trở về kiểm tra trạng thái In-game", Color.DarkCyan);
        }
        TestModeChanged?.Invoke(_isTestMode);
    }

    public void ExecuteRefreshF5()
    {
        ResetAllCountersToInitial();
        Log("[Làm mới F5] Đã đưa toàn bộ thông số, bộ đếm ruộng và trạng thái macro về ban đầu.", Color.DodgerBlue);
        RefreshRequested?.Invoke();
    }

    private void OnMouseClick()
    {
        if (_fastBuildManager.IsHoldingKey || _isDiplomacyOpen)
        {
            // Đang giữ phím xây nhà hoặc đang mở Diplomacy -> Không reset chuỗi
            _lastTabTime = DateTime.MinValue;
            _lastShiftMilitaryKey = Keys.None;
            _lastShiftMilitaryTime = DateTime.MinValue;
            return;
        }

        ResetAllChains();
    }

    private void OnRightButtonDown()
    {
        _isRightMouseDown = true;
        _fastBuildManager.Reset();
        if (_isFlagModeActive)
        {
            // Trong chế độ đặt cờ, duy trì SHIFT để chuột phải cắm cờ (waypoint)
            return;
        }

        bool isFarmRefreshWindow = _isFarmRefreshActive && (DateTime.Now - _lastFarmRefreshTime).TotalSeconds <= 6.0;

        if (((_currentState == MacroState.Active && _gameWatcher.IsInGame) || _isTestMode) && _isPhysicalShiftDown && isFarmRefreshWindow)
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
                if (((_currentState == MacroState.Active && _gameWatcher.IsInGame) || _isTestMode) && _isPhysicalShiftDown && !_isShiftTemporarilyReleasedForMouse && !_isRightMouseDown)
                {
                    InputSimulator.SendKeyDown((ushort)Keys.ShiftKey);
                }
            });
        }
    }

    private bool OnLeftClickAction(int msg)
    {
        if ((_currentState != MacroState.Active || !_gameWatcher.IsInGame || !AoeWindowHelper.IsAoeForeground()) && !_isTestMode)
        {
            return false;
        }

        // Vẩy E: Khi đang giữ CTRL sau khi ấn CTRL + E, mỗi click chuột trái sẽ là [Click -> S -> B -> E]
        if (_vayEManager.IsActive)
        {
            bool isCtrlActive = _isPhysicalCtrlDown ||
                                (NativeMethods.GetAsyncKeyState((int)Keys.ControlKey) & 0x8000) != 0 ||
                                (NativeMethods.GetKeyState((int)Keys.ControlKey) & 0x8000) != 0;
            if (isCtrlActive)
            {
                _vayEManager.HandleLeftClick(Log, RunActionSync);
                return true;
            }
            else
            {
                _vayEManager.HandleCtrlUp(Log, RunActionSync);
                return false;
            }
        }

        if (_isFlagModeActive)
        {
            ExitFlagMode();
            Log("[Đặt cờ] Click chuột trái -> Nhả SHIFT, chuyển thành Chuột Phải và Tắt chế độ đặt cờ", Color.Teal);
            MidiPlayer.PlayFlagModeOffSound();

            Task.Run(() =>
            {
                RunActionSync(() =>
                {
                    InputSimulator.ReleaseShiftKeysHardware();
                    Thread.Sleep(15);
                    InputSimulator.SendRightClick(25);
                    Thread.Sleep(10);
                    InputSimulator.ReleaseShiftKeysHardware();
                });
            });

            return true;
        }

        // **Chức năng: Chuyển đồ nhanh** (Khi mở Diplomacy: giữ CTRL + CLICK -> 5 CLICK)
        if (_isDiplomacyOpen)
        {
            bool isCtrlActive = _isPhysicalCtrlDown ||
                                (NativeMethods.GetAsyncKeyState((int)Keys.ControlKey) & 0x8000) != 0 ||
                                (NativeMethods.GetKeyState((int)Keys.ControlKey) & 0x8000) != 0;
            if (isCtrlActive)
            {
                Log("[Chuyển đồ nhanh] CTRL + Click -> Khóa chuột, gửi 5 click chuyển đồ (+500 tài nguyên) và bù chuyển động", Color.Magenta);
                RunActionSync(() =>
                {
                    try
                    {
                        MouseLockManager.ExecuteLockedAction(() =>
                        {
                            InputSimulator.SendMultipleMouseClicks(5, 15, 20);
                        }, pinAtCurrentPos: true);
                    }
                    finally
                    {
                        _mouseHook.ResetInterceptedStates();
                        InputSimulator.ReleaseLeftMouse();
                    }
                });
                return true;
            }
            return false;
        }

        // Duyệt nhà binh: Khi người dùng ấn giữ (CTRL + KEY) và sau đó click chuột trái, cứ mỗi lần click chuột trái sẽ [CLICK -> CTRL + KEYMAP] 1 lần
        if (_militaryCycleManager.IsActive)
        {
            bool isCtrlActive = _isPhysicalCtrlDown ||
                                (NativeMethods.GetAsyncKeyState((int)Keys.ControlKey) & 0x8000) != 0 ||
                                (NativeMethods.GetKeyState((int)Keys.ControlKey) & 0x8000) != 0;
            if (isCtrlActive)
            {
                if (_militaryCycleManager.HandleLeftClick(Log, RunActionSync))
                {
                    return true;
                }
            }
            else
            {
                _militaryCycleManager.Reset();
            }
        }

        // Xây nhà nhanh: Khi đang giữ phím xây dựng, mỗi click chuột trái đặt móng xong sẽ gửi tiếp [B -> Key]
        if (_fastBuildManager.IsHoldingKey)
        {
            _fastBuildManager.HandleLeftClick(Log, RunActionSync);
            return false; // Cho phép click chuột trái vật lý truyền xuống game để đặt móng
        }
        else if (_fastBuildManager.CurrentBuildingKey != Keys.None)
        {
            // Đặt xong móng nhà khi tap đơn
            _fastBuildManager.OnLeftClickWhenNotHolding();
        }

        _lastTabTime = DateTime.MinValue;
        _lastShiftMilitaryKey = Keys.None;
        _lastShiftMilitaryTime = DateTime.MinValue;

        return false;
    }

    private bool OnRightClickAction(int msg)
    {
        if ((_currentState != MacroState.Active || !_gameWatcher.IsInGame || !AoeWindowHelper.IsAoeForeground()) && !_isTestMode)
        {
            return false;
        }

        // Hủy chức năng xây nhanh: nếu đang trong chế độ giữ phím xây nhanh (hoặc đang có móng),
        // khi người dùng ấn chuột phải -> macro ấn ESC và thực thi click chuột phải
        if (_fastBuildManager.IsHoldingKey || _fastBuildManager.CurrentBuildingKey != Keys.None)
        {
            _fastBuildManager.HandleRightClickCancel(Log, RunActionSync);
            return true;
        }

        // **Chức năng: Tự nhả chuột phải**
        // Khi người dùng click chuột phải, macro can thiệp tự động nhả chuột phải ngay lập tức 1-2ms.
        _fastBuildManager.Reset();
        ResetAllChains();

        bool isFarmRefreshWindow = _isFarmRefreshActive && (DateTime.Now - _lastFarmRefreshTime).TotalSeconds <= 6.0;
        bool shouldReleaseShift = _isPhysicalShiftDown && isFarmRefreshWindow;

        Task.Run(() =>
        {
            if (shouldReleaseShift)
            {
                InputSimulator.ReleaseShiftKeysHardware();
                Thread.Sleep(5);
            }

            InputSimulator.SendRightClickFast(2); // Giữ 1-2ms rồi nhả ngay lập tức

            if (shouldReleaseShift)
            {
                Thread.Sleep(5);
                if (((_currentState == MacroState.Active && _gameWatcher.IsInGame) || _isTestMode) && _isPhysicalShiftDown)
                {
                    InputSimulator.SendKeyDown((ushort)Keys.ShiftKey);
                }
            }
        });

        return true;
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
        _isFlagModeActive = false;
        ReleaseAllFlagArrowKeys();
        _isPhysicalShiftDown = false;
        _isShiftTemporarilyReleasedForMouse = false;
        InputSimulator.ReleaseShiftKeysHardware();
    }

    private bool OnMiddleClickAction(int msg)
    {
        if ((_currentState != MacroState.Active || !_gameWatcher.IsInGame || !AoeWindowHelper.IsAoeForeground()) && !_isTestMode)
        {
            return false;
        }

        if (msg == 0x0207) // WM_MBUTTONDOWN
        {
            ResetAllChains();
            _deleteManager.HandleMiddleButtonDown(Log, () => (_currentState == MacroState.Active && _gameWatcher.IsInGame) || _isTestMode);
            return true;
        }
        else if (msg == 0x0208) // WM_MBUTTONUP
        {
            _deleteManager.HandleMiddleButtonUp();
            return true;
        }

        return false;
    }

    private void ResetBuildingState()
    {
        _fastBuildManager.Reset();
    }

    private void ResetAllChains()
    {
        ResetBuildingState();
        _lastTabTime = DateTime.MinValue;
        _lastShiftMilitaryKey = Keys.None;
        _lastShiftMilitaryTime = DateTime.MinValue;
        if (!_isPhysicalCtrlDown)
        {
            _vayEManager.Reset();
            _militaryCycleManager.Reset();
        }
        if (!_deleteManager.IsHolding)
        {
            _deleteManager.Reset();
        }
    }

    private void OnInGameStatusChanged(bool inGame)
    {
        if (!inGame)
        {
            _hasAutoStartedForCurrentGame = false;
            _hasObservedNonInitialResources = false;
            MouseLockManager.ForceUnlock();
            MouseLockManager.UnpinCursor();
            _hasAppliedCursorLock = false;
            _vayEManager.Reset();
            _deleteManager.Reset();
            _isDiplomacyOpen = false;
            _isPhysicalShiftDown = false;
            _isPhysicalCtrlDown = false;
            _isRightMouseDown = false;
            _isShiftTemporarilyReleasedForMouse = false;
            InputSimulator.ReleaseShiftKeysHardware();
            InputSimulator.ReleaseCtrlKeysHardware();
            _isAltDown = false;
            _isAltCombo = false;
            InputSimulator.ReleaseAltKeysHardware();
            ExitFlagMode();
            _chatDetector.Stop();

            // Tắt còi báo ruộng và dừng mọi âm thanh cảnh báo khi ra ngoài game/bị làm mờ
            _farmTimerManager.StopAllTimersAndAlarms();
            MidiPlayer.StopAlarmSound();

            if (_currentState == MacroState.Active || _currentState == MacroState.SuspendedChat)
            {
                _savedStateBeforeUnfocus = _currentState;
                SetState(MacroState.SuspendedOutOfGame, "Không nhận diện được thanh tài nguyên (Ngoài game/Menu). Tạm dừng macro.");
            }
        }
        else
        {
            PromoteHooks();
            _chatDetector.Start();

            if (AoeWindowHelper.TryGetAoeWindowScreenRect(out var aoeRect))
            {
                NativeMethods.ClipCursor(ref aoeRect);
                _hasAppliedCursorLock = true;
            }

            if (_currentState == MacroState.SuspendedOutOfGame)
            {
                SetState(_savedStateBeforeUnfocus, $"Đã nhận diện thanh tài nguyên (Vào game) -> Tiếp tục: {_savedStateBeforeUnfocus.ToDisplayName()}");
            }
        }
    }

    private void CursorLockTimer_Tick(object? sender, EventArgs e)
    {
        // Điều kiện khóa chuột: Macro BẬT + Đang trong trận + Cửa sổ AOE đang active ở tiền cảnh (Foreground)
        bool shouldLock = (_currentState == MacroState.Active || _currentState == MacroState.SuspendedChat)
                          && _gameWatcher.IsInGame
                          && AoeWindowHelper.IsAoeForeground();

        if (shouldLock)
        {
            if (AoeWindowHelper.TryGetAoeWindowScreenRect(out var aoeRect))
            {
                // Kiểm tra xem hiện tại chuột có đang bị tràn ra ngoài biên cửa sổ AOE (sang màn hình thứ 2) không
                if (NativeMethods.GetClipCursor(out var currentClip))
                {
                    // Nếu clip hiện tại rộng hơn cửa sổ AOE (hoặc đang unclipped = toàn màn hình ảo VirtualScreen)
                    if (currentClip.Left < aoeRect.Left || currentClip.Right > aoeRect.Right ||
                        currentClip.Top < aoeRect.Top || currentClip.Bottom > aoeRect.Bottom)
                    {
                        NativeMethods.ClipCursor(ref aoeRect);
                    }
                }
                else
                {
                    NativeMethods.ClipCursor(ref aoeRect);
                }
                _hasAppliedCursorLock = true;
            }
        }
        else
        {
            // Khi không ở trong game hoặc đã Alt-Tab / chuyển sang cửa sổ khác -> Giải phóng chuột tự do ngay lập tức
            if (_hasAppliedCursorLock)
            {
                MouseLockManager.UnpinCursor();
                _hasAppliedCursorLock = false;
            }
        }
    }

    private void OnChatStatusChanged(bool inChat)
    {
        _gameWatcher.SetChatStatus(inChat);
        if (inChat)
        {
            if (_currentState == MacroState.Active)
            {
                _savedStateBeforeUnfocus = MacroState.Active;
                SetState(MacroState.SuspendedChat, "Macro Tạm dừng (Phát hiện mở khung Chat qua điểm ảnh)");
            }
        }
        else
        {
            if (_currentState == MacroState.SuspendedChat)
            {
                SetState(MacroState.Active, "Macro Hoạt động (Khung Chat đã đóng)");
            }
        }
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

        // 1. Phím F6: Toggle Bật/Tắt Chế độ Test (Key map hoạt động ngoài game)
        if (key == Keys.F6 && isKeyDown)
        {
            ToggleTestMode();
            return true;
        }

        // 2. Phím F5: Làm mới toàn bộ thông số, bộ đếm ruộng và trạng thái macro
        if (key == Keys.F5 && isKeyDown)
        {
            ExecuteRefreshF5();
            return true;
        }

        // Nếu Macro ở trạng thái Tắt (Disabled) -> Cho phím đi qua hoàn toàn
        if (_currentState == MacroState.Disabled)
        {
            return false;
        }

        // Phải ở trong game và cửa sổ AOE đang active ở tiền cảnh để thực thi các macro game
        // NGOẠI TRỪ khi đang ở Chế độ Test (_isTestMode == true)
        if ((!_gameWatcher.IsInGame || !AoeWindowHelper.IsAoeForeground()) && !_isTestMode)
        {
            return false;
        }

        // Khóa hoàn toàn chức năng phím Windows khi đang InGame hoặc trong Chế độ Test
        if (key == Keys.LWin || key == Keys.RWin)
        {
            return true;
        }

        // 2. Chat trigger - Kích hoạt quét tức thì qua điểm ảnh khi có phím Enter hoặc Escape
        if (isKeyDown && (key == Keys.Enter || key == Keys.Escape))
        {
            _chatDetector.TriggerImmediateScan();
            return false;
        }

        // Nếu không ở trạng thái Active (Hoạt động) và không ở Chế độ Test -> Cho phím đi qua
        if (_currentState != MacroState.Active && !_isTestMode)
        {
            return false;
        }

        if (key == Keys.ShiftKey || key == Keys.LShiftKey || key == Keys.RShiftKey)
        {
            _isPhysicalShiftDown = isKeyDown;
        }

        if (key == Keys.ControlKey || key == Keys.LControlKey || key == Keys.RControlKey)
        {
            _isPhysicalCtrlDown = isKeyDown;
        }

        bool ctrlPressed = _isPhysicalCtrlDown ||
                           (NativeMethods.GetAsyncKeyState((int)Keys.ControlKey) & 0x8000) != 0 ||
                           (NativeMethods.GetAsyncKeyState((int)Keys.LControlKey) & 0x8000) != 0 ||
                           (NativeMethods.GetAsyncKeyState((int)Keys.RControlKey) & 0x8000) != 0 ||
                           (NativeMethods.GetKeyState((int)Keys.ControlKey) & 0x8000) != 0;

        bool shiftPressed = _isPhysicalShiftDown || _isFlagModeActive ||
                            (!_isShiftTemporarilyReleasedForMouse && (
                                (NativeMethods.GetAsyncKeyState((int)Keys.ShiftKey) & 0x8000) != 0 ||
                                (NativeMethods.GetAsyncKeyState((int)Keys.LShiftKey) & 0x8000) != 0 ||
                                (NativeMethods.GetAsyncKeyState((int)Keys.RShiftKey) & 0x8000) != 0 ||
                                (NativeMethods.GetKeyState((int)Keys.ShiftKey) & 0x8000) != 0
                            ));

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
            _isFarmRefreshActive = false;
            _lastFarmRefreshTime = DateTime.MinValue;
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
            _isPhysicalCtrlDown = false;
            _militaryCycleManager.HandleCtrlUp();

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

            if (_vayEManager.IsActive)
            {
                _vayEManager.HandleCtrlUp(Log, RunActionSync);
            }
        }

        // Ghi nhớ đạo quân đã chọn (1..6)
        if (isKeyDown && key >= Keys.D1 && key <= Keys.D6 && !ctrlPressed && !shiftPressed && !altPressed)
        {
            _vayEManager.RecordMilitaryGroup(key);
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
                if (!_isFlagModeActive)
                {
                    _isFlagModeActive = true;
                    Log("[Đặt cờ] BẬT chế độ đặt cờ -> Giữ SHIFT down, AWSD chuyển thành 4 phím mũi tên", Color.Teal);
                    MidiPlayer.PlayFlagModeOnSound();
                    RunActionSync(() =>
                    {
                        InputSimulator.SendKeyDown((ushort)Keys.ShiftKey);
                    });
                }
                else
                {
                    Log("[Đặt cờ] TẮT chế độ đặt cờ -> Nhả SHIFT, AWSD trở về bình thường", Color.Teal);
                    MidiPlayer.PlayFlagModeOffSound();
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
            if (_militaryCycleManager.HandleKeyUp(key))
            {
                return true;
            }
            if (_fastBuildManager.HandleKeyUp(key, Log, RunActionSync))
            {
                return true;
            }
            return IsMacroKey(key, ctrlPressed, shiftPressed);
        }

        // **Chức năng: Chuyển đồ nhanh** - Thoát Diplomacy bằng SPACE hoặc ESC
        if (_isDiplomacyOpen && key == Keys.Space)
        {
            _isDiplomacyOpen = false;
            _mouseHook.ResetInterceptedStates();
            InputSimulator.ReleaseLeftMouse();
            Log("[Chuyển đồ nhanh] (SPACE) -> Thoát trạng thái Diplomacy và giữ nguyên chức năng SPACE", Color.Magenta);
            return false; // Cho phép phím SPACE gốc đi thẳng vào game
        }

        if (_isDiplomacyOpen && key == Keys.Escape)
        {
            _isDiplomacyOpen = false;
            _mouseHook.ResetInterceptedStates();
            InputSimulator.ReleaseLeftMouse();
            Log("[Chuyển đồ nhanh] (ESC) -> Thoát trạng thái Diplomacy", Color.Magenta);
            return false; // Cho phép ESC đi xuống game đóng dialog
        }

        // Nếu ấn phím khác không phải phím duyệt nhà binh (A, S, Z, X, D, C) trong khi duyệt nhà binh đang active -> Reset
        if (_militaryCycleManager.IsActive && !MilitaryCycleManager.TryGetMilitaryTargetKey(key, out _, out _))
        {
            _militaryCycleManager.Reset();
        }

        // ----------------------------------------------------
        // **Chức năng: Vẩy E** (Giữ CTRL + E)
        // ----------------------------------------------------
        if ((ctrlPressed || _vayEManager.IsActive) && key == Keys.E)
        {
            ResetBuildingState();
            _vayEManager.HandlePressE(Log, RunActionSync);
            return true;
        }


        // ----------------------------------------------------
        // **Chức năng: Đạo quân nhanh** (SHIFT + 1..6, CTRL + `)
        // ----------------------------------------------------
        if (shiftPressed && key >= Keys.D1 && key <= Keys.D6)
        {
            ResetAllChains();
            _vayEManager.RecordMilitaryGroup(key);
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

        if (shiftPressed && key == Keys.F) // SHIFT + F: Làm mới đạo ruộng 1 (ESC -> 7 -> S -> SPACE -> S)
        {
            ResetAllChains();
            _isFarmRefreshActive = true;
            _lastFarmRefreshTime = DateTime.Now;
            Log("[Đạo ruộng 1] SHIFT+F -> Làm mới đạo ruộng 1 (ESC -> 7 -> S -> SPACE -> S)", Color.DarkGreen);
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
                InputSimulator.SendKeyPress((ushort)Keys.S);
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

        if (shiftPressed && key == Keys.G) // SHIFT + G: Làm mới đạo ruộng 2 (ESC -> 8 -> S -> SPACE -> S)
        {
            ResetAllChains();
            _isFarmRefreshActive = true;
            _lastFarmRefreshTime = DateTime.Now;
            Log("[Đạo ruộng 2] SHIFT+G -> Làm mới đạo ruộng 2 (ESC -> 8 -> S -> SPACE -> S)", Color.DarkGreen);
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
                InputSimulator.SendKeyPress((ushort)Keys.S);
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
        // Lần 1: CTRL + Key
        // Từ lần 2 trở đi: [*Kiểm tra hàng đợi quân* -> CTRL + Key]
        //   Nếu hàng đợi null: [Click]
        //   Nếu hàng đợi = 1, loading < 50: [Nothing]
        //   Nếu hàng đợi = 1, loading >= 50: [Click]
        //   Nếu hàng đợi >= 2: [Nothing]
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
                RunActionSync(() =>
                {
                    // *Kiểm tra hàng đợi quân*: Quét lại trực tiếp cả HÀNG ĐỢI và TIẾN ĐỘ LOADING từ màn hình game
                    var queue = _unitQueueOcrService?.CaptureAndRecognize();
                    var loading = _loadingOcrService?.CaptureAndRecognize();

                    _currentUnitQueue = queue;
                    _currentLoading = loading;

                    int? queueCount = (queue != null && queue.IsValid) ? queue.PrimaryCount : null;
                    int loadingVal = (loading != null && loading.IsValid && loading.Percentage.HasValue) 
                        ? loading.Percentage.Value 
                        : 0;

                    string queueInfo = (queueCount != null && queueCount > 0) 
                        ? $"{queueCount.Value} (Ô {queue?.SlotIndex})" 
                        : "null (0)";
                    string loadingInfo = (loading != null && loading.IsValid && loading.Percentage.HasValue) 
                        ? $"{loadingVal}%" 
                        : "0%";

                    bool shouldClick = true;

                    if (queueCount == null || queueCount <= 0)
                    {
                        // Hàng đợi null hoặc <= 0: [Click]
                        shouldClick = true;
                    }
                    else if (queueCount == 1)
                    {
                        // Hàng đợi = 1: kiểm tra loading
                        if (loadingVal < 50)
                        {
                            // Loading < 50%: [Nothing]
                            shouldClick = false;
                        }
                        else
                        {
                            // Loading >= 50%: [Click]
                            shouldClick = true;
                        }
                    }
                    else
                    {
                        // Hàng đợi >= 2: [Nothing]
                        shouldClick = false;
                    }

                    if (shouldClick)
                    {
                        Log($"[Xin quân nhanh] Shift+{key} -> *Kiểm tra*: Hàng đợi: {queueInfo}, Loading: {loadingInfo} -> [CLICK] -> CTRL+{((Keys)shiftTargetVk)} ({shiftMilitaryDesc})", Color.DarkMagenta);
                        bool isShiftDown = (NativeMethods.GetKeyState((int)Keys.ShiftKey) & 0x8000) != 0;
                        if (isShiftDown)
                        {
                            InputSimulator.ReleaseShiftKeysHardware();
                            Thread.Sleep(5);
                        }

                        InputSimulator.SendMouseClickHold(25); // Left Down -> Hold 25ms -> Left Up
                        Thread.Sleep(15);

                        if (isShiftDown)
                        {
                            InputSimulator.SendKeyDown((ushort)Keys.ShiftKey);
                            Thread.Sleep(5);
                        }
                    }
                    else
                    {
                        Log($"[Xin quân nhanh] Shift+{key} -> *Kiểm tra*: Hàng đợi: {queueInfo}, Loading: {loadingInfo} -> [BỎ QUA / NOTHING] -> CTRL+{((Keys)shiftTargetVk)} ({shiftMilitaryDesc})", Color.DarkOrange);
                    }

                    InputSimulator.SendCtrlKeyCombo(shiftTargetVk);
                });
            }
            else
            {
                Log($"[Xin quân nhanh] (Lần 1): Shift+{key} -> CTRL+{((Keys)shiftTargetVk)} ({shiftMilitaryDesc})", Color.DarkMagenta);
                RunActionSync(() =>
                {
                    InputSimulator.SendCtrlKeyCombo(shiftTargetVk);

                    // Cập nhật thông số của nhà vừa chuyển ở lần 1
                    Task.Run(() =>
                    {
                        Thread.Sleep(60);
                        var q = _unitQueueOcrService?.CaptureAndRecognize();
                        var l = _loadingOcrService?.CaptureAndRecognize();
                        _currentUnitQueue = q;
                        _currentLoading = l;
                    });
                });
            }
            return true;
        }

        // ----------------------------------------------------
        // **Chức năng: Duyệt nhà binh** (CTRL + Phím)
        // ----------------------------------------------------
        if (ctrlPressed && _militaryCycleManager.HandleKeyDown(key, ctrlPressed, Log, RunActionSync))
        {
            ResetBuildingState();
            return true;
        }

        // ----------------------------------------------------
        // **Chức năng: Mở bảng ngoại giao / Chuyển đồ nhanh** (F3, F4)
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
        // **Chức năng: Xây các loại nhà nhanh**
        // (E, R, T, V, F, G, B, N, A, S, Z, X, D, C)
        // ----------------------------------------------------
        if (!_vayEManager.IsActive && !_isPhysicalCtrlDown && !ctrlPressed && !shiftPressed)
        {
            if (_fastBuildManager.HandleKeyDown(key, Log, RunActionSync))
            {
                return true;
            }
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
        return MilitaryCycleManager.TryGetMilitaryTargetKey(key, out targetVk, out desc);
    }



    private bool IsMacroKey(Keys key, bool ctrlPressed, bool shiftPressed)
    {
        if (key == Keys.Tab || key == Keys.LWin || key == Keys.RWin || key == Keys.Capital ||
            key == Keys.Menu || key == Keys.LMenu || key == Keys.RMenu) return true;

        if (_isFlagModeActive && (key is Keys.W or Keys.A or Keys.S or Keys.D)) return true;

        if (ctrlPressed || shiftPressed || _vayEManager.IsActive)
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
            Keys.F2 or Keys.F3 or Keys.F4 or Keys.F12 or Keys.Oemtilde or Keys.Q or Keys.W or
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

    public bool TryTriggerAutoFastStart(ResourceValues res, TimerValues? timer)
    {
        // 0. Macro phải đang ở trạng thái Hoạt động (F1 Active)
        if (_currentState != MacroState.Active)
        {
            return false;
        }

        // 1. Disable khi game đã diễn ra qua giai đoạn đầu (> 5 giây).
        // Nếu timer chưa xuất hiện (chưa ấn F11) hoặc timer đang ở thời điểm đầu trận (00:00 - 00:05) -> Cho phép chạy AutoFastStart!
        if (timer != null && timer.IsValid && IsPastEarlyGame(timer))
        {
            return false;
        }

        // 2. Kiểm tra điều kiện tài nguyên khởi đầu trận đấu AOE (Gỗ: 200, Thực: 200)
        bool isInitialResources = (res != null && res.Wood == 200 && res.Food == 200);
        if (!isInitialResources)
        {
            if (res != null && !res.IsEmpty && (res.Wood != 200 || res.Food != 200))
            {
                _hasObservedNonInitialResources = true;
            }
            return false;
        }

        // 3. Phải đang trong game
        if (!_gameWatcher.IsInGame)
        {
            return false;
        }

        // 4. Kiểm soát trigger đè: Nếu đang thực thi chuỗi khởi đầu nhanh thì bỏ qua
        if (_isAutoStartExecuting)
        {
            return false;
        }

        // 5. Kiểm soát trigger lặp lại:
        // Nếu đã từng chạy cho trận này, chỉ cho phép chạy lại nếu tài nguyên đã từng thay đổi khác 200 Gỗ / 200 Thực (restart trận mới)
        // và khoảng cách thời gian tối thiểu >= 5 giây
        if (_hasAutoStartedForCurrentGame)
        {
            if (_hasObservedNonInitialResources && (DateTime.UtcNow - _lastAutoStartTime).TotalSeconds >= 5)
            {
                _hasAutoStartedForCurrentGame = false;
            }
            else
            {
                return false;
            }
        }

        // 6. Giới hạn thời gian tối thiểu giữa các lần kích hoạt (5 giây)
        if ((DateTime.UtcNow - _lastAutoStartTime).TotalSeconds < 5)
        {
            return false;
        }

        _hasAutoStartedForCurrentGame = true;
        _hasObservedNonInitialResources = false;
        _lastAutoStartTime = DateTime.UtcNow;

        ExecuteAutoFastStart();
        return true;
    }

    private void ExecuteAutoFastStart()
    {
        _isAutoStartExecuting = true;
        _mouseHook.BlockMouseClicks = true;
        Log("[Khởi đầu nhanh tự động] Nhận diện tài nguyên khởi đầu (200 Gỗ / 200 Thực) -> Thực thi F4 > F11 và [H > C ^ 6]...", Color.DarkBlue);

        Task.Run(() =>
        {
            try
            {
                // 1. Nhấn F4 > F11
                InputSimulator.SendKeyPress((ushort)Keys.F4, 20);
                Thread.Sleep(30);
                InputSimulator.SendKeyPress((ushort)Keys.F11, 20);
                Thread.Sleep(40);

                // 2. Nhấn H 1 lần (chọn nhà chính), sau đó nhấn C 6 lần (xin dân)
                InputSimulator.SendKeyPress((ushort)Keys.H, 15);
                Thread.Sleep(25);
                for (int i = 0; i < 6; i++)
                {
                    InputSimulator.SendKeyPress((ushort)Keys.C, 10);
                    Thread.Sleep(25);
                }
            }
            catch (Exception ex)
            {
                Log($"[Khởi đầu nhanh tự động] Lỗi: {ex.Message}", Color.Red);
            }
            finally
            {
                // 3. Mở lại click chuột trái, phải
                _mouseHook.BlockMouseClicks = false;
                _isAutoStartExecuting = false;

                // 4. Reset tất cả các bộ đếm về trạng thái sơ khai
                ResetAllCountersToInitial();
                Log("[Khởi đầu nhanh tự động] Hoàn tất [H > C ^ 6] -> Đã mở lại chuột và reset tất cả bộ đếm về trạng thái sơ khai.", Color.DarkGreen);
            }
        });
    }

    private static bool IsPastEarlyGame(TimerValues timer)
    {
        if (string.IsNullOrWhiteSpace(timer.RawText)) return false;
        string raw = timer.RawText.Trim();

        // 00:00, 0:00, 00:01, 00:02, 00:03, 00:04, 00:05 -> Vẫn là thời điểm đầu trận đấu
        if (raw == "00:00" || raw == "0:00" || raw == "00:01" || raw == "0:01" ||
            raw == "00:02" || raw == "0:02" || raw == "00:03" || raw == "0:03" ||
            raw == "00:04" || raw == "0:04" || raw == "00:05" || raw == "0:05")
        {
            return false;
        }

        string[] parts = raw.Split(':');
        if (parts.Length == 2 && int.TryParse(parts[0], out int min) && int.TryParse(parts[1], out int sec))
        {
            return (min * 60 + sec) > 5;
        }
        else if (parts.Length == 3 && int.TryParse(parts[0], out int h) && int.TryParse(parts[1], out int m) && int.TryParse(parts[2], out int s))
        {
            return (h * 3600 + m * 60 + s) > 5;
        }

        return true;
    }

    public void ResetAllCountersToInitial()
    {
        ResetAllChains();
        _deleteManager.Reset();
        _militaryCycleManager.Reset();
        _chatDetector.Reset();
        _isDiplomacyOpen = false;
        _mouseHook.ResetInterceptedStates();
        InputSimulator.ReleaseLeftMouse();
        _ctrlFCount = 0;
        _ctrlGCount = 0;
        _activeFarmGroup = 0;
        _isPhysicalShiftDown = false;
        _isPhysicalCtrlDown = false;
        _isRightMouseDown = false;
        _isShiftTemporarilyReleasedForMouse = false;
        InputSimulator.ReleaseShiftKeysHardware();
        InputSimulator.ReleaseCtrlKeysHardware();
        _isAltDown = false;
        _isAltCombo = false;
        InputSimulator.ReleaseAltKeysHardware();
        ExitFlagMode();
        _isFarmRefreshActive = false;
        _lastFarmRefreshTime = DateTime.MinValue;
        _winKeyState = 0;
        _lastWinKeyTime = DateTime.MinValue;
        _f2LoopTimer.Stop();
        _isF2Pressed = false;
        _farmTimerManager.StopAllTimersAndAlarms();
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
        if (!_isDiplomacyOpen)
        {
            _isDiplomacyOpen = true;
            Log("[Chuyển đồ nhanh] (F3) Mở Diplomacy -> Trạng thái Bơm đồ [Active]. Giữ CTRL + CLICK để gửi 5 click. Nhấn F3 hoặc SPACE để thoát.", Color.Magenta);
            RunActionSync(() => InputSimulator.ClickDiplomacy());
        }
        else
        {
            _isDiplomacyOpen = false;
            _mouseHook.ResetInterceptedStates();
            InputSimulator.ReleaseLeftMouse();
            Log("[Chuyển đồ nhanh] (F3) Đóng Diplomacy -> [ESC]", Color.Magenta);
            RunActionSync(() => InputSimulator.SendKeyPress((ushort)Keys.Escape, 20));
        }
    }

    private void HandleF4Timeline()
    {
        Log("[Chức năng: Mở bảng ngoại giao] (F4) -> Mở timeline [F10 -> Mũi tên xuống * 2 -> Enter]", Color.Magenta);
        RunActionSync(() =>
        {
            MouseLockManager.ExecuteLockedAction(() =>
            {
                InputSimulator.SendKeyPress((ushort)Keys.F10, 15);
                Thread.Sleep(20);
                InputSimulator.SendKeyPress((ushort)Keys.Down, 15);
                Thread.Sleep(20);
                InputSimulator.SendKeyPress((ushort)Keys.Down, 15);
                Thread.Sleep(20);
                InputSimulator.SendKeyPress((ushort)Keys.Enter, 15);
            }, pinAtCurrentPos: true);
        });
    }

    private void Log(string message, Color color)
    {
        LogRequested?.Invoke(message, color);
    }

    public void Dispose()
    {
        Stop();
        MouseLockManager.Dispose();
        _chatDetector.Dispose();
        _cursorLockTimer.Dispose();
        _f2LoopTimer.Dispose();
        _farmTimerManager.Dispose();
        _gameWatcher.Dispose();
        _mouseHook.Dispose();
        _keyboardHook.Dispose();
        _actionLock.Dispose();
        GC.SuppressFinalize(this);
    }
}
