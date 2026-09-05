using System.Windows.Forms;
using AOEKeyboardMacroPro.Services;

namespace AOEKeyboardMacroPro.Services;

public static class InputSimulator
{
    public static void SendKeyPress(ushort vkCode, int holdTimeMs = 10)
    {
        SendKeyDown(vkCode);
        if (holdTimeMs > 0) Thread.Sleep(holdTimeMs);
        SendKeyUp(vkCode);
    }

    public static void SendKeyDown(ushort vkCode)
    {
        // Auto-release sticky Windows key before sending any non-Win key (prevents Win+E, Win+M, Win+L, etc.)
        if (vkCode != (ushort)Keys.LWin && vkCode != (ushort)Keys.RWin)
        {
            if ((NativeMethods.GetAsyncKeyState((int)Keys.LWin) & 0x8000) != 0 ||
                (NativeMethods.GetAsyncKeyState((int)Keys.RWin) & 0x8000) != 0 ||
                (NativeMethods.GetKeyState((int)Keys.LWin) & 0x8000) != 0 ||
                (NativeMethods.GetKeyState((int)Keys.RWin) & 0x8000) != 0)
            {
                ReleaseWinKeysHardware();
            }
        }

        ushort scanCode = (ushort)NativeMethods.MapVirtualKey(vkCode, NativeMethods.MAPVK_VK_TO_VSC);
        uint flags = 0;
        if (IsExtendedKey(vkCode))
        {
            flags |= NativeMethods.KEYEVENTF_EXTENDEDKEY;
        }

        NativeMethods.INPUT[] inputs = new NativeMethods.INPUT[1];
        inputs[0] = new NativeMethods.INPUT
        {
            type = NativeMethods.INPUT_KEYBOARD,
            U = new NativeMethods.INPUT_UNION
            {
                ki = new NativeMethods.KEYBDINPUT
                {
                    wVk = vkCode,
                    wScan = scanCode,
                    dwFlags = flags,
                    time = 0,
                    dwExtraInfo = NativeMethods.MACRO_EXTRA_INFO
                }
            }
        };

        NativeMethods.SendInput(1, inputs, System.Runtime.InteropServices.Marshal.SizeOf(typeof(NativeMethods.INPUT)));
    }

    public static void SendKeyUp(ushort vkCode)
    {
        ushort scanCode = (ushort)NativeMethods.MapVirtualKey(vkCode, NativeMethods.MAPVK_VK_TO_VSC);
        uint flags = NativeMethods.KEYEVENTF_KEYUP;
        if (IsExtendedKey(vkCode))
        {
            flags |= NativeMethods.KEYEVENTF_EXTENDEDKEY;
        }

        NativeMethods.INPUT[] inputs = new NativeMethods.INPUT[1];
        inputs[0] = new NativeMethods.INPUT
        {
            type = NativeMethods.INPUT_KEYBOARD,
            U = new NativeMethods.INPUT_UNION
            {
                ki = new NativeMethods.KEYBDINPUT
                {
                    wVk = vkCode,
                    wScan = scanCode,
                    dwFlags = flags,
                    time = 0,
                    dwExtraInfo = NativeMethods.MACRO_EXTRA_INFO
                }
            }
        };

        NativeMethods.SendInput(1, inputs, System.Runtime.InteropServices.Marshal.SizeOf(typeof(NativeMethods.INPUT)));
    }

    private static bool IsExtendedKey(ushort vkCode)
    {
        return vkCode is (ushort)Keys.Up or (ushort)Keys.Down or (ushort)Keys.Left or (ushort)Keys.Right
            or (ushort)Keys.Insert or (ushort)Keys.Delete or (ushort)Keys.Home or (ushort)Keys.End
            or (ushort)Keys.PageUp or (ushort)Keys.PageDown or (ushort)Keys.RControlKey or (ushort)Keys.RMenu
            or (ushort)Keys.LWin or (ushort)Keys.RWin;
    }

    public static void SendCtrlKeyCombo(ushort targetVk)
    {
        bool isCtrlPhysicallyPressed = (NativeMethods.GetKeyState((int)Keys.ControlKey) & 0x8000) != 0;

        if (isCtrlPhysicallyPressed)
        {
            // User is ALREADY holding CTRL physically! Just send target key.
            SendKeyPress(targetVk, 10);
        }
        else
        {
            // Send CTRL down + Key + CTRL up combo fast
            SendKeyDown((ushort)Keys.ControlKey);
            Thread.Sleep(3);
            SendKeyPress(targetVk, 10);
            Thread.Sleep(3);
            SendKeyUp((ushort)Keys.ControlKey);
        }
    }

    public static void SendShiftKeyCombo(ushort targetVk)
    {
        bool isCtrlPhysicallyPressed = (NativeMethods.GetKeyState((int)Keys.ControlKey) & 0x8000) != 0;

        if (isCtrlPhysicallyPressed)
        {
            SendKeyUp((ushort)Keys.ControlKey);
            Thread.Sleep(3);
        }

        SendKeyDown((ushort)Keys.ShiftKey);
        Thread.Sleep(3);
        SendKeyPress(targetVk, 10);
        Thread.Sleep(3);
        SendKeyUp((ushort)Keys.ShiftKey);

        if (isCtrlPhysicallyPressed)
        {
            Thread.Sleep(3);
            SendKeyDown((ushort)Keys.ControlKey);
        }
    }

    public static void ReleaseCtrlKeysHardware()
    {
        SendKeyUp((ushort)Keys.ControlKey);
        SendKeyUp((ushort)Keys.LControlKey);
        SendKeyUp((ushort)Keys.RControlKey);
    }

    public static void ReleaseShiftKeysHardware()
    {
        SendKeyUp((ushort)Keys.ShiftKey);
        SendKeyUp((ushort)Keys.LShiftKey);
        SendKeyUp((ushort)Keys.RShiftKey);

        NativeMethods.INPUT[] inputs = new NativeMethods.INPUT[2];
        inputs[0] = new NativeMethods.INPUT
        {
            type = NativeMethods.INPUT_KEYBOARD,
            U = new NativeMethods.INPUT_UNION
            {
                ki = new NativeMethods.KEYBDINPUT
                {
                    wVk = (ushort)Keys.LShiftKey,
                    wScan = 0x2A,
                    dwFlags = NativeMethods.KEYEVENTF_KEYUP,
                    dwExtraInfo = NativeMethods.MACRO_EXTRA_INFO
                }
            }
        };
        inputs[1] = new NativeMethods.INPUT
        {
            type = NativeMethods.INPUT_KEYBOARD,
            U = new NativeMethods.INPUT_UNION
            {
                ki = new NativeMethods.KEYBDINPUT
                {
                    wVk = (ushort)Keys.RShiftKey,
                    wScan = 0x36,
                    dwFlags = NativeMethods.KEYEVENTF_KEYUP,
                    dwExtraInfo = NativeMethods.MACRO_EXTRA_INFO
                }
            }
        };
        NativeMethods.SendInput(2, inputs, System.Runtime.InteropServices.Marshal.SizeOf(typeof(NativeMethods.INPUT)));
    }

    public static void ReleaseAltKeysHardware()
    {
        SendKeyUp((ushort)Keys.Menu);
        SendKeyUp((ushort)Keys.LMenu);
        SendKeyUp((ushort)Keys.RMenu);
    }

    public static void ReleaseWinKeysHardware()
    {
        SendKeyUp((ushort)Keys.LWin);
        SendKeyUp((ushort)Keys.RWin);

        NativeMethods.INPUT[] inputs = new NativeMethods.INPUT[2];
        inputs[0] = new NativeMethods.INPUT
        {
            type = NativeMethods.INPUT_KEYBOARD,
            U = new NativeMethods.INPUT_UNION
            {
                ki = new NativeMethods.KEYBDINPUT
                {
                    wVk = (ushort)Keys.LWin,
                    wScan = 0x5B,
                    dwFlags = NativeMethods.KEYEVENTF_KEYUP | NativeMethods.KEYEVENTF_SCANCODE,
                    dwExtraInfo = NativeMethods.MACRO_EXTRA_INFO
                }
            }
        };
        inputs[1] = new NativeMethods.INPUT
        {
            type = NativeMethods.INPUT_KEYBOARD,
            U = new NativeMethods.INPUT_UNION
            {
                ki = new NativeMethods.KEYBDINPUT
                {
                    wVk = (ushort)Keys.RWin,
                    wScan = 0x5C,
                    dwFlags = NativeMethods.KEYEVENTF_KEYUP | NativeMethods.KEYEVENTF_SCANCODE,
                    dwExtraInfo = NativeMethods.MACRO_EXTRA_INFO
                }
            }
        };
        NativeMethods.SendInput(2, inputs, System.Runtime.InteropServices.Marshal.SizeOf(typeof(NativeMethods.INPUT)));
    }

    public static void SendMouseClick()
    {
        SendMouseClickHold(10);
    }

    public static void SendMouseClickHold(int holdTimeMs = 25)
    {
        // Left Mouse Down
        NativeMethods.INPUT[] inputsDown = new NativeMethods.INPUT[1];
        inputsDown[0] = new NativeMethods.INPUT
        {
            type = NativeMethods.INPUT_MOUSE,
            U = new NativeMethods.INPUT_UNION
            {
                mi = new NativeMethods.MOUSEINPUT
                {
                    dwFlags = NativeMethods.MOUSEEVENTF_LEFTDOWN,
                    dwExtraInfo = NativeMethods.MACRO_EXTRA_INFO
                }
            }
        };
        NativeMethods.SendInput(1, inputsDown, System.Runtime.InteropServices.Marshal.SizeOf(typeof(NativeMethods.INPUT)));

        // Hold for holdTimeMs (25ms)
        if (holdTimeMs > 0) Thread.Sleep(holdTimeMs);

        // Left Mouse Up
        NativeMethods.INPUT[] inputsUp = new NativeMethods.INPUT[1];
        inputsUp[0] = new NativeMethods.INPUT
        {
            type = NativeMethods.INPUT_MOUSE,
            U = new NativeMethods.INPUT_UNION
            {
                mi = new NativeMethods.MOUSEINPUT
                {
                    dwFlags = NativeMethods.MOUSEEVENTF_LEFTUP,
                    dwExtraInfo = NativeMethods.MACRO_EXTRA_INFO
                }
            }
        };
        NativeMethods.SendInput(1, inputsUp, System.Runtime.InteropServices.Marshal.SizeOf(typeof(NativeMethods.INPUT)));
    }

    public static void SendRightClick(int holdTimeMs = 25)
    {
        NativeMethods.INPUT[] inputsDown = new NativeMethods.INPUT[1];
        inputsDown[0] = new NativeMethods.INPUT
        {
            type = NativeMethods.INPUT_MOUSE,
            U = new NativeMethods.INPUT_UNION
            {
                mi = new NativeMethods.MOUSEINPUT
                {
                    dwFlags = NativeMethods.MOUSEEVENTF_RIGHTDOWN,
                    dwExtraInfo = NativeMethods.MACRO_EXTRA_INFO
                }
            }
        };
        NativeMethods.SendInput(1, inputsDown, System.Runtime.InteropServices.Marshal.SizeOf(typeof(NativeMethods.INPUT)));

        if (holdTimeMs > 0) Thread.Sleep(holdTimeMs);

        NativeMethods.INPUT[] inputsUp = new NativeMethods.INPUT[1];
        inputsUp[0] = new NativeMethods.INPUT
        {
            type = NativeMethods.INPUT_MOUSE,
            U = new NativeMethods.INPUT_UNION
            {
                mi = new NativeMethods.MOUSEINPUT
                {
                    dwFlags = NativeMethods.MOUSEEVENTF_RIGHTUP,
                    dwExtraInfo = NativeMethods.MACRO_EXTRA_INFO
                }
            }
        };
        NativeMethods.SendInput(1, inputsUp, System.Runtime.InteropServices.Marshal.SizeOf(typeof(NativeMethods.INPUT)));
    }

    public static void SendRightUp()
    {
        NativeMethods.INPUT[] inputs = new NativeMethods.INPUT[1];
        inputs[0] = new NativeMethods.INPUT
        {
            type = NativeMethods.INPUT_MOUSE,
            U = new NativeMethods.INPUT_UNION
            {
                mi = new NativeMethods.MOUSEINPUT
                {
                    dwFlags = NativeMethods.MOUSEEVENTF_RIGHTUP,
                    dwExtraInfo = NativeMethods.MACRO_EXTRA_INFO
                }
            }
        };
        NativeMethods.SendInput(1, inputs, System.Runtime.InteropServices.Marshal.SizeOf(typeof(NativeMethods.INPUT)));
    }

    public static void ClickDiplomacy()
    {
        IntPtr hwnd = NativeMethods.GetForegroundWindow();
        if (hwnd == IntPtr.Zero) return;

        if (NativeMethods.GetClientRect(hwnd, out NativeMethods.RECT rect))
        {
            int W = rect.Right - rect.Left;
            NativeMethods.POINT pt = new NativeMethods.POINT { X = W - 90, Y = 10 };
            if (NativeMethods.ClientToScreen(hwnd, ref pt))
            {
                if (NativeMethods.GetCursorPos(out NativeMethods.POINT originalPt))
                {
                    // Move to Diplomacy button
                    NativeMethods.SetCursorPos(pt.X, pt.Y);
                    Thread.Sleep(25);

                    // Perform click
                    SendMouseClick();
                    Thread.Sleep(25);

                    // Return to original position
                    NativeMethods.SetCursorPos(originalPt.X, originalPt.Y);
                }
            }
        }
    }

    public static int NumpadOffsetX = 172;
    public static int NumpadOffsetY = 115;
    public static int NumpadSlotWidth = 43;

    public static void ClickActionButton(int slotIndex)
    {
        if (slotIndex < 1 || slotIndex > 5) return;

        IntPtr hwnd = NativeMethods.GetForegroundWindow();
        if (hwnd == IntPtr.Zero) return;

        if (NativeMethods.GetClientRect(hwnd, out NativeMethods.RECT rect))
        {
            int H = rect.Bottom - rect.Top;

            int targetX = NumpadOffsetX + (slotIndex - 1) * NumpadSlotWidth;
            int targetY = H - NumpadOffsetY;

            NativeMethods.POINT pt = new NativeMethods.POINT { X = targetX, Y = targetY };
            if (NativeMethods.ClientToScreen(hwnd, ref pt))
            {
                if (NativeMethods.GetCursorPos(out NativeMethods.POINT originalPt))
                {
                    // Instant batch input: Move -> Down + Up -> Move Back in microsecond execution (<0.05ms)
                    int inputSize = System.Runtime.InteropServices.Marshal.SizeOf(typeof(NativeMethods.INPUT));
                    NativeMethods.INPUT[] inputs = new NativeMethods.INPUT[2];
                    inputs[0] = new NativeMethods.INPUT
                    {
                        type = NativeMethods.INPUT_MOUSE,
                        U = new NativeMethods.INPUT_UNION
                        {
                            mi = new NativeMethods.MOUSEINPUT
                            {
                                dwFlags = NativeMethods.MOUSEEVENTF_LEFTDOWN,
                                dwExtraInfo = NativeMethods.MACRO_EXTRA_INFO
                            }
                        }
                    };
                    inputs[1] = new NativeMethods.INPUT
                    {
                        type = NativeMethods.INPUT_MOUSE,
                        U = new NativeMethods.INPUT_UNION
                        {
                            mi = new NativeMethods.MOUSEINPUT
                            {
                                dwFlags = NativeMethods.MOUSEEVENTF_LEFTUP,
                                dwExtraInfo = NativeMethods.MACRO_EXTRA_INFO
                            }
                        }
                    };

                    NativeMethods.SetCursorPos(pt.X, pt.Y);
                    NativeMethods.SendInput(2, inputs, inputSize);
                    NativeMethods.SetCursorPos(originalPt.X, originalPt.Y);
                }
            }
        }
    }
}



