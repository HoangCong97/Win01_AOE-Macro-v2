using System.Diagnostics;
using System.Runtime.InteropServices;

namespace AOEKeyboardMacroPro.Services;

public class MouseHookManager : IDisposable
{
    private IntPtr _hookId = IntPtr.Zero;
    private readonly NativeMethods.HookProc _proc;
    private bool _isMiddleDownIntercepted = false;
    private bool _isLeftDownIntercepted = false;
    private bool _isRightDownIntercepted = false;

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
        if (_hookId == IntPtr.Zero)
        {
            using Process curProcess = Process.GetCurrentProcess();
            using ProcessModule? curModule = curProcess.MainModule;
            IntPtr hMod = NativeMethods.GetModuleHandle(curModule?.ModuleName);

            _hookId = NativeMethods.SetWindowsHookEx(NativeMethods.WH_MOUSE_LL, _proc, hMod, 0);
        }
    }

    public void Stop()
    {
        if (_hookId != IntPtr.Zero)
        {
            NativeMethods.UnhookWindowsHookEx(_hookId);
            _hookId = IntPtr.Zero;
        }
    }

    public void PromoteHookToTop()
    {
        if (_hookId != IntPtr.Zero)
        {
            NativeMethods.UnhookWindowsHookEx(_hookId);
            _hookId = IntPtr.Zero;
        }
        Start();
    }

    private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0)
        {
            NativeMethods.MOUSEINPUT mouse = Marshal.PtrToStructure<NativeMethods.MOUSEINPUT>(lParam);

            // Skip simulated mouse events from macro
            if (mouse.dwExtraInfo == NativeMethods.MACRO_EXTRA_INFO)
            {
                return NativeMethods.CallNextHookEx(_hookId, nCode, wParam, lParam);
            }

            // Chặn toàn bộ thao tác chuột vật lý của người dùng khi đang khóa chuột
            if (MouseLockManager.IsLocked)
            {
                return (IntPtr)1;
            }

            int msg = wParam.ToInt32();

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
        GC.SuppressFinalize(this);
    }
}
