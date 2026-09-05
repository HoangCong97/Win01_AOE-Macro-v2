using System.Diagnostics;
using System.Runtime.InteropServices;
using AOEKeyboardMacroPro.Services;

namespace AOEKeyboardMacroPro.Services;

public class KeyboardHookManager : IDisposable
{
    private IntPtr _hookId = IntPtr.Zero;
    private readonly NativeMethods.HookProc _proc;

    public event Func<uint, bool, bool>? KeyActionOccurred; // (vkCode, isKeyDown) -> returns handled boolean

    public KeyboardHookManager()
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

            _hookId = NativeMethods.SetWindowsHookEx(NativeMethods.WH_KEYBOARD_LL, _proc, hMod, 0);
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

    private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0)
        {
            NativeMethods.KBDLLHOOKSTRUCT kbd = Marshal.PtrToStructure<NativeMethods.KBDLLHOOKSTRUCT>(lParam);

            // Skip inputs sent by our own Macro engine to avoid recursion
            if (kbd.dwExtraInfo == NativeMethods.MACRO_EXTRA_INFO)
            {
                return NativeMethods.CallNextHookEx(_hookId, nCode, wParam, lParam);
            }

            bool isKeyDown = (wParam == (IntPtr)NativeMethods.WM_KEYDOWN || wParam == (IntPtr)NativeMethods.WM_SYSKEYDOWN);
            bool isKeyUp = (wParam == (IntPtr)NativeMethods.WM_KEYUP || wParam == (IntPtr)NativeMethods.WM_SYSKEYUP);

            if ((isKeyDown || isKeyUp) && KeyActionOccurred != null)
            {
                bool handled = KeyActionOccurred.Invoke(kbd.vkCode, isKeyDown);
                if (handled)
                {
                    if (kbd.vkCode == (uint)Keys.LWin || kbd.vkCode == (uint)Keys.RWin)
                    {
                        if (isKeyUp)
                        {
                            InputSimulator.ReleaseWinKeysHardware();
                        }

                        // Send dummy Ctrl to suppress Windows Start Menu popup in Windows OS shell
                        NativeMethods.INPUT[] dummy = new NativeMethods.INPUT[2];
                        dummy[0] = new NativeMethods.INPUT
                        {
                            type = NativeMethods.INPUT_KEYBOARD,
                            U = new NativeMethods.INPUT_UNION
                            {
                                ki = new NativeMethods.KEYBDINPUT
                                {
                                    wVk = (ushort)Keys.ControlKey,
                                    dwFlags = 0,
                                    dwExtraInfo = NativeMethods.MACRO_EXTRA_INFO
                                }
                            }
                        };
                        dummy[1] = new NativeMethods.INPUT
                        {
                            type = NativeMethods.INPUT_KEYBOARD,
                            U = new NativeMethods.INPUT_UNION
                            {
                                ki = new NativeMethods.KEYBDINPUT
                                {
                                    wVk = (ushort)Keys.ControlKey,
                                    dwFlags = NativeMethods.KEYEVENTF_KEYUP,
                                    dwExtraInfo = NativeMethods.MACRO_EXTRA_INFO
                                }
                            }
                        };
                        NativeMethods.SendInput(2, dummy, Marshal.SizeOf(typeof(NativeMethods.INPUT)));
                    }
                    return (IntPtr)1; // Suppress original keypress
                }
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
