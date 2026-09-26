using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace AOEKeyboardMacroPro.Services;

public class MouseHookManager : IDisposable
{
    private IntPtr _hookId = IntPtr.Zero;
    private readonly NativeMethods.HookProc _proc;
    private Thread? _hookThread;
    private ApplicationContext? _appContext;
    private readonly ManualResetEventSlim _startedEvent = new(false);
    private readonly object _lock = new();

    private bool _isMiddleDownIntercepted = false;
    private bool _isLeftDownIntercepted = false;
    private bool _isRightDownIntercepted = false;
    public volatile bool BlockMouseClicks = false;

    public event Action? MouseClicked;
    public event Action? RightButtonDown;
    public event Action? RightButtonUp;
    public event Func<int, bool>? MiddleClickActionOccurred;
    public event Func<int, bool>? LeftClickActionOccurred;
    public event Func<int, bool>? RightClickActionOccurred;

    public MouseHookManager()
    {
        _proc = HookCallback;
    }

    public void Start()
    {
        lock (_lock)
        {
            if (_hookId != IntPtr.Zero || _hookThread != null) return;

            _startedEvent.Reset();

            // Cháº¡y Hook trÃªn Dedicated Background Thread cÃ³ Message Loop riÃªng biá»‡t.
            // Giáº£i phÃ³ng 100% UI Thread, triá»‡t tiÃªu hoÃ n toÃ n hiá»‡n tÆ°á»£ng delay con trá» chuá»™t há»‡ thá»‘ng khi kÃ©o cá»­a sá»•.
            _hookThread = new Thread(HookThreadLoop)
            {
                Name = "DedicatedMouseHookThread",
                IsBackground = true,
                Priority = ThreadPriority.Highest // Xá»­ lÃ½ real-time 1000Hz+ khÃ´ng bao giá» bá»‹ ngháº½n
            };
            _hookThread.SetApartmentState(ApartmentState.STA);
            _hookThread.Start();

            // Chá» hook khá»Ÿi táº¡o xong trÆ°á»›c khi tráº£ vá»
            _startedEvent.Wait(1500);
        }
    }

    private void HookThreadLoop()
    {
        try
        {
            using Process curProcess = Process.GetCurrentProcess();
            using ProcessModule? curModule = curProcess.MainModule;
            IntPtr hMod = NativeMethods.GetModuleHandle(curModule?.ModuleName);

            _hookId = NativeMethods.SetWindowsHookEx(NativeMethods.WH_MOUSE_LL, _proc, hMod, 0);
            _appContext = new ApplicationContext();
            _startedEvent.Set();

            if (_hookId != IntPtr.Zero)
            {
                // Message pump riÃªng biá»‡t cá»§a thread hook, pháº£n há»“i tá»©c thá»i <0.01ms
                Application.Run(_appContext);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"DedicatedMouseHook error: {ex.Message}");
            _startedEvent.Set();
        }
        finally
        {
            if (_hookId != IntPtr.Zero)
            {
                NativeMethods.UnhookWindowsHookEx(_hookId);
                _hookId = IntPtr.Zero;
            }
        }
    }

    public void Stop()
    {
        lock (_lock)
        {
            if (_appContext != null)
            {
                try
                {
                    _appContext.ExitThread();
                }
                catch { }
                _appContext = null;
            }

            if (_hookThread != null)
            {
                if (_hookThread.IsAlive)
                {
                    _hookThread.Join(500);
                }
                _hookThread = null;
            }

            if (_hookId != IntPtr.Zero)
            {
                NativeMethods.UnhookWindowsHookEx(_hookId);
                _hookId = IntPtr.Zero;
            }
        }
    }

    public void PromoteHookToTop()
    {
        Stop();
        Start();
    }

    private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0)
        {
            int msg = wParam.ToInt32();

            // Tá»‘i Æ°u cá»±c háº¡n: Bá» qua ngay láº­p tá»©c má»i thÃ´ng Ä‘iá»‡p di chuyá»ƒn chuá»™t (WM_MOUSEMOVE = 0x0200)
            // KhÃ´ng cáº§n marshal struct, khÃ´ng tá»‘n CPU/GC, giÃºp kÃ©o cá»­a sá»• vÃ  lia chuá»™t siÃªu mÆ°á»£t 1000Hz+
            // Bỏ qua ngay lập tức mọi thông điệp không phải click (mouse move, wheel, hover, etc.)
            // Không marshal struct, không lock, phản hồi tức thì <0.001ms
            if (msg == 0x0200 || (msg != 0x0201 && msg != 0x0202 && msg != 0x0204 && msg != 0x0205 && msg != 0x0207 && msg != 0x0208))
            {
                return NativeMethods.CallNextHookEx(_hookId, nCode, wParam, lParam);
            }

            NativeMethods.MOUSEINPUT mouse = Marshal.PtrToStructure<NativeMethods.MOUSEINPUT>(lParam);

            // Skip simulated mouse events from macro
            if (mouse.dwExtraInfo == NativeMethods.MACRO_EXTRA_INFO)
            {
                return NativeMethods.CallNextHookEx(_hookId, nCode, wParam, lParam);
            }

            // Cháº·n toÃ n bá»™ thao tÃ¡c click chuá»™t váº­t lÃ½ cá»§a ngÆ°á»i dÃ¹ng khi Ä‘ang khÃ³a chuá»™t
            if (MouseLockManager.IsLocked)
            {
                return (IntPtr)1;
            }

            // Vô hiệu hóa click chuột trái, phải trong khoảng thời gian Khởi đầu nhanh
            if (BlockMouseClicks && (msg == 0x0201 || msg == 0x0202 || msg == 0x0204 || msg == 0x0205))
            {
                return (IntPtr)1;
            }

            if (msg == 0x0207) // WM_MBUTTONDOWN
            {
                if (MiddleClickActionOccurred != null && MiddleClickActionOccurred.Invoke(msg))
                {
                    _isMiddleDownIntercepted = true;
                    return (IntPtr)1; // Suppress original middle mouse down
                }
                MouseClicked?.Invoke();
            }
            else if (msg == 0x0208) // WM_MBUTTONUP
            {
                if (_isMiddleDownIntercepted)
                {
                    _isMiddleDownIntercepted = false;
                    MiddleClickActionOccurred?.Invoke(msg);
                    return (IntPtr)1; // Suppress original middle mouse up
                }
            }
            else if (msg == 0x0201) // WM_LBUTTONDOWN
            {
                if (LeftClickActionOccurred != null && LeftClickActionOccurred.Invoke(msg))
                {
                    _isLeftDownIntercepted = true;
                    return (IntPtr)1; // Suppress original left mouse down
                }
                MouseClicked?.Invoke();
            }
            else if (msg == 0x0202) // WM_LBUTTONUP
            {
                if (_isLeftDownIntercepted)
                {
                    _isLeftDownIntercepted = false;
                    return (IntPtr)1; // Suppress original left mouse up
                }
            }
            else if (msg == 0x0204) // WM_RBUTTONDOWN
            {
                if (RightClickActionOccurred != null && RightClickActionOccurred.Invoke(msg))
                {
                    _isRightDownIntercepted = true;
                    return (IntPtr)1; // Suppress original right mouse down
                }
                MouseClicked?.Invoke();
                RightButtonDown?.Invoke();
            }
            else if (msg == 0x0205) // WM_RBUTTONUP
            {
                if (_isRightDownIntercepted)
                {
                    _isRightDownIntercepted = false;
                    return (IntPtr)1; // Suppress original right mouse up
                }
                RightButtonUp?.Invoke();
            }
        }

        return NativeMethods.CallNextHookEx(_hookId, nCode, wParam, lParam);
    }

    public void Dispose()
    {
        Stop();
        _startedEvent.Dispose();
        GC.SuppressFinalize(this);
    }
}