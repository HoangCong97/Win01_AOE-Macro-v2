using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Text;
using System.Windows.Forms;

namespace AOEKeyboardMacroPro.Services;

/// <summary>
/// Lớp chuyên trách nhận diện và quản lý cửa sổ game Age of Empires (AOE).
/// Đảm bảo tuyệt đối không nhận nhầm các ứng dụng bên ngoài (trình duyệt, IDE, chat, desktop)
/// và xử lý giải phóng/khóa chuột chuẩn xác.
/// </summary>
public static class AoeWindowHelper
{
    // Danh sách tên tiến trình (ProcessName) chuẩn của các phiên bản AOE
    private static readonly HashSet<string> KnownAoeProcesses = new(StringComparer.OrdinalIgnoreCase)
    {
        "empiresx",   // Age of Empires: Rise of Rome (UPatch, EGoplay, GameTV Plus)
        "empires",    // Age of Empires 1 bản gốc
        "age",        // Một số bản mod/repack AoE
        "aoe",        // Bản mod/repack AoE
        "aoede",      // Age of Empires: Definitive Edition
        "aoede_s",    // AoE: DE Microsoft Store / Steam
        "aoe2de",     // Age of Empires II: Definitive Edition
        "age2_x1",    // AoE II The Conquerors
        "aok hd",     // AoE II HD
        "aok_hd"
    };

    // Danh sách đen các tiến trình làm việc, văn phòng, trình duyệt để chặn tuyệt đối việc nhận nhầm
    private static readonly HashSet<string> NonGameProcesses = new(StringComparer.OrdinalIgnoreCase)
    {
        "chrome", "msedge", "firefox", "opera", "brave", "vivaldi", "iexplore", "safari",
        "code", "devenv", "rider64", "idea64", "visualstudio", "git-bash",
        "explorer", "taskmgr", "cmd", "powershell", "pwsh", "windowsterminal", "mmc",
        "discord", "telegram", "slack", "teams", "skype", "zalo", "messenger",
        "notepad", "notepad++", "wordpad", "winword", "excel", "powerpnt",
        "applicationframehost", "shellexperiencehost", "searchapp", "startmenuexperiencehost",
        "systemsettings", "lockapp"
    };

    // Các từ khóa nhận diện trong tiêu đề cửa sổ game AOE
    private static readonly string[] KnownAoeTitleKeywords = new[]
    {
        "Age of Empires",
        "Empiresx",
        "Rise of Rome",
        "AoEDE",
        "AoE: DE",
        "AoE DE",
        "Definitive Edition"
    };

    // Các tên lớp cửa sổ (Window Class) chuẩn của AOE
    private static readonly string[] KnownAoeClassNames = new[]
    {
        "Age of Empires",
        "AoE",
        "DirectDraw",
        "SplashWindowClass",
        "Empires"
    };

    // Cache kết quả kiểm tra theo HWND để đạt hiệu năng cực hạn, tránh gọi Process API liên tục
    private static IntPtr _lastHwnd = IntPtr.Zero;
    private static bool _lastResult = false;
    private static long _lastCheckTimestamp = 0;
    private static readonly long CacheDurationTicks = Stopwatch.Frequency / 4; // 250ms cache
    private static readonly object _cacheLock = new();

    /// <summary>
    /// Kiểm tra xem một Handle cửa sổ có phải là cửa sổ game AOE hay không.
    /// </summary>
    public static bool IsAoeWindow(IntPtr hWnd)
    {
        if (hWnd == IntPtr.Zero) return false;

        long now = Stopwatch.GetTimestamp();
        lock (_cacheLock)
        {
            if (hWnd == _lastHwnd && (now - _lastCheckTimestamp) < CacheDurationTicks)
            {
                return _lastResult;
            }
        }

        bool result = EvaluateIsAoeWindow(hWnd);

        lock (_cacheLock)
        {
            _lastHwnd = hWnd;
            _lastResult = result;
            _lastCheckTimestamp = now;
        }

        return result;
    }

    private static bool EvaluateIsAoeWindow(IntPtr hWnd)
    {
        if (hWnd == IntPtr.Zero || !NativeMethods.IsWindow(hWnd) || NativeMethods.IsIconic(hWnd))
        {
            return false;
        }

        try
        {
            NativeMethods.GetWindowThreadProcessId(hWnd, out uint pid);
            if (pid == 0 || pid == Environment.ProcessId)
            {
                return false;
            }

            string procName = string.Empty;
            try
            {
                using var proc = Process.GetProcessById((int)pid);
                procName = proc.ProcessName;
            }
            catch
            {
                return false;
            }

            // 1. Phủ định ngay lập tức nếu là các ứng dụng làm việc, duyệt web, chat, hệ thống
            if (NonGameProcesses.Contains(procName))
            {
                return false;
            }

            // 2. Kiểm tra nếu Process thuộc danh sách game AOE đã biết
            bool isKnownAoeProcess = KnownAoeProcesses.Contains(procName);

            // 3. Lấy Window Title và Window Class
            StringBuilder titleSb = new(256);
            NativeMethods.GetWindowText(hWnd, titleSb, titleSb.Capacity);
            string title = titleSb.ToString();

            StringBuilder classSb = new(256);
            NativeMethods.GetClassName(hWnd, classSb, classSb.Capacity);
            string className = classSb.ToString();

            bool titleMatches = false;
            foreach (var kw in KnownAoeTitleKeywords)
            {
                if (title.Contains(kw, StringComparison.OrdinalIgnoreCase))
                {
                    titleMatches = true;
                    break;
                }
            }

            bool classMatches = false;
            foreach (var cls in KnownAoeClassNames)
            {
                if (className.Contains(cls, StringComparison.OrdinalIgnoreCase))
                {
                    classMatches = true;
                    break;
                }
            }

            // Phải khớp hoặc tiến trình AOE, hoặc tiêu đề / class name của AOE
            if (!isKnownAoeProcess && !titleMatches && !classMatches)
            {
                return false;
            }

            // 4. Kiểm tra kích thước cửa sổ game thực tế (tối thiểu 640x480 cho một trận AOE)
            if (NativeMethods.GetClientRect(hWnd, out NativeMethods.RECT clientRect))
            {
                int w = clientRect.Right - clientRect.Left;
                int h = clientRect.Bottom - clientRect.Top;
                if (w < 640 || h < 480)
                {
                    return false;
                }
            }
            else
            {
                return false;
            }

            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Kiểm tra cửa sổ đang Active ở tiền cảnh (Foreground) có phải là game AOE hay không.
    /// </summary>
    public static bool IsAoeForeground()
    {
        IntPtr fgHwnd = NativeMethods.GetForegroundWindow();
        return IsAoeWindow(fgHwnd);
    }

    /// <summary>
    /// Lấy Handle của cửa sổ AOE nếu nó đang ở tiền cảnh (Foreground), ngược lại trả về IntPtr.Zero.
    /// </summary>
    public static IntPtr GetAoeWindow()
    {
        IntPtr fgHwnd = NativeMethods.GetForegroundWindow();
        return IsAoeWindow(fgHwnd) ? fgHwnd : IntPtr.Zero;
    }

    /// <summary>
    /// Tìm cửa sổ AOE (ưu tiên cửa sổ tiền cảnh, nếu đang bấm test trên tool thì tìm cửa sổ AOE đang chạy).
    /// </summary>
    public static IntPtr FindAnyAoeWindow()
    {
        IntPtr fgHwnd = NativeMethods.GetForegroundWindow();
        if (IsAoeWindow(fgHwnd)) return fgHwnd;

        IntPtr found = IntPtr.Zero;
        try
        {
            NativeMethods.EnumWindows((hWnd, lParam) =>
            {
                if (NativeMethods.IsWindowVisible(hWnd) && IsAoeWindow(hWnd))
                {
                    found = hWnd;
                    return false; // Dừng tìm kiếm khi đã thấy
                }
                return true;
            }, IntPtr.Zero);
        }
        catch { }

        return found;
    }

    /// <summary>
    /// Lấy tọa độ màn hình chuẩn xác của cửa sổ AOE (Client area kẹp chặt trong màn hình chứa cửa sổ).
    /// Chỉ trả về true khi cửa sổ AOE đang active ở tiền cảnh.
    /// </summary>
    public static bool TryGetAoeWindowScreenRect(out NativeMethods.RECT screenRect)
    {
        screenRect = default;
        IntPtr fgHwnd = GetAoeWindow();
        if (fgHwnd == IntPtr.Zero)
        {
            return false;
        }

        if (NativeMethods.GetClientRect(fgHwnd, out NativeMethods.RECT clientRect))
        {
            NativeMethods.POINT topLeft = new() { X = 0, Y = 0 };
            if (NativeMethods.ClientToScreen(fgHwnd, ref topLeft))
            {
                int w = clientRect.Right - clientRect.Left;
                int h = clientRect.Bottom - clientRect.Top;
                if (w >= 640 && h >= 480)
                {
                    var screen = Screen.FromHandle(fgHwnd);
                    Rectangle bounds = screen.Bounds;

                    int left = Math.Max(bounds.Left, topLeft.X);
                    int top = Math.Max(bounds.Top, topLeft.Y);
                    int right = Math.Min(bounds.Right, topLeft.X + w);
                    int bottom = Math.Min(bounds.Bottom, topLeft.Y + h);

                    screenRect = new NativeMethods.RECT
                    {
                        Left = left,
                        Top = top,
                        Right = right,
                        Bottom = bottom
                    };
                    return true;
                }
            }
        }

        return false;
    }
}
