using System.Runtime.InteropServices;

namespace AOEKeyboardMacroPro.Services;

/// <summary>
/// Quản lý việc khóa chuột trong lúc macro (F3, F4) thực thi tác vụ
/// và theo dõi chuyển động tương đối (Raw Input) từ cảm biến chuột
/// để bù đắp chính xác vị trí chuột sau khi nhả khóa.
/// </summary>
public static class MouseLockManager
{
    private static readonly object _lockObj = new();
    private static Thread? _rawInputThread;
    private static RawInputReceiver? _receiver;
    private static int _accumulatedDeltaX = 0;
    private static int _accumulatedDeltaY = 0;

    /// <summary>
    /// Cho biết chuột hiện có đang bị khóa (cả về chặn click và ghim vị trí) hay không.
    /// </summary>
    public static bool IsLocked { get; private set; } = false;

    /// <summary>
    /// Khởi tạo bộ thu Raw Input trên một luồng nền độc lập (tách rời hoàn toàn khỏi UI Thread của MainForm)
    /// để không làm nghẽn hoặc giật lag giao diện người dùng khi kéo cửa sổ hoặc lia chuột tốc độ cao.
    /// </summary>
    public static void Initialize()
    {
        lock (_lockObj)
        {
            if (_rawInputThread == null)
            {
                using var initEvent = new ManualResetEventSlim(false);
                _rawInputThread = new Thread(() =>
                {
                    _receiver = new RawInputReceiver();
                    initEvent.Set();
                    Application.Run();
                })
                {
                    IsBackground = true,
                    Name = "RawInputReceiverThread"
                };
                _rawInputThread.SetApartmentState(ApartmentState.STA);
                _rawInputThread.Start();
                initEvent.Wait(2000);
            }
        }
    }

    /// <summary>
    /// Ghim con trỏ chuột vào một vị trí cụ thể (1x1 pixel) trên màn hình bằng ClipCursor.
    /// </summary>
    public static void PinCursor(int x, int y)
    {
        var rect = new NativeMethods.RECT
        {
            Left = x,
            Top = y,
            Right = x + 1,
            Bottom = y + 1
        };
        NativeMethods.ClipCursor(ref rect);
    }

    /// <summary>
    /// Hủy bỏ giới hạn ClipCursor, giải phóng con trỏ tự do.
    /// </summary>
    public static void UnpinCursor()
    {
        NativeMethods.ClipCursor(IntPtr.Zero);
    }

    /// <summary>
    /// Thực thi một hành động có bảo vệ chuột:
    /// - Ghi nhận vị trí ban đầu của chuột.
    /// - Reset bộ tích lũy delta chuyển động (Raw Input).
    /// - Khóa click chuột người dùng để tránh bấm nhầm.
    /// - Tùy chọn ghim vị trí chuột tại chỗ (pinAtCurrentPos).
    /// - Sau khi xong: giải phóng ClipCursor, tính toán vị trí mới = ban đầu + delta, đưa chuột về đích.
    /// </summary>
    public static void ExecuteLockedAction(Action action, bool pinAtCurrentPos = false)
    {
        lock (_lockObj)
        {
            NativeMethods.GetCursorPos(out NativeMethods.POINT initialPt);
            Interlocked.Exchange(ref _accumulatedDeltaX, 0);
            Interlocked.Exchange(ref _accumulatedDeltaY, 0);
            IsLocked = true;

            try
            {
                if (pinAtCurrentPos)
                {
                    PinCursor(initialPt.X, initialPt.Y);
                }

                action();
            }
            finally
            {
                UnpinCursor();
                IsLocked = false;

                int dx = Interlocked.Exchange(ref _accumulatedDeltaX, 0);
                int dy = Interlocked.Exchange(ref _accumulatedDeltaY, 0);

                int finalX = initialPt.X + dx;
                int finalY = initialPt.Y + dy;

                // Giới hạn trong kích thước màn hình ảo (hỗ trợ đa màn hình)
                Rectangle vs = SystemInformation.VirtualScreen;
                finalX = Math.Clamp(finalX, vs.Left, vs.Right - 1);
                finalY = Math.Clamp(finalY, vs.Top, vs.Bottom - 1);

                NativeMethods.SetCursorPos(finalX, finalY);
            }
        }
    }

    /// <summary>
    /// Mở khóa khẩn cấp nếu có sự cố hoặc ứng dụng bị mất focus.
    /// </summary>
    public static void ForceUnlock()
    {
        UnpinCursor();
        IsLocked = false;
        Interlocked.Exchange(ref _accumulatedDeltaX, 0);
        Interlocked.Exchange(ref _accumulatedDeltaY, 0);
    }

    /// <summary>
    /// Dọn dẹp tài nguyên RawInputReceiver khi ứng dụng đóng.
    /// </summary>
    public static void Dispose()
    {
        lock (_lockObj)
        {
            ForceUnlock();
            _receiver?.Dispose();
            _receiver = null;
        }
    }

    /// <summary>
    /// Cửa sổ ẩn NativeWindow nhận thông điệp WM_INPUT toàn cục từ hệ điều hành.
    /// </summary>
    private sealed class RawInputReceiver : NativeWindow, IDisposable
    {
        private readonly IntPtr _buffer = Marshal.AllocHGlobal(256);
        private readonly uint _headerSize = (uint)Marshal.SizeOf<NativeMethods.RAWINPUTHEADER>();
        private bool _disposed = false;

        public RawInputReceiver()
        {
            CreateHandle(new CreateParams());

            var rid = new NativeMethods.RAWINPUTDEVICE
            {
                usUsagePage = NativeMethods.HID_USAGE_PAGE_GENERIC,
                usUsage = NativeMethods.HID_USAGE_GENERIC_MOUSE,
                dwFlags = NativeMethods.RIDEV_INPUTSINK,
                hwndTarget = Handle
            };

            NativeMethods.RegisterRawInputDevices([rid], 1, (uint)Marshal.SizeOf<NativeMethods.RAWINPUTDEVICE>());
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == NativeMethods.WM_INPUT && !_disposed && IsLocked)
            {
                ProcessRawInput(m.LParam);
            }

            base.WndProc(ref m);
        }

        private void ProcessRawInput(IntPtr hRawInput)
        {
            uint size = 256;
            uint ret = NativeMethods.GetRawInputData(hRawInput, NativeMethods.RID_INPUT, _buffer, ref size, _headerSize);
            if (ret != uint.MaxValue && ret > 0)
            {
                var header = Marshal.PtrToStructure<NativeMethods.RAWINPUTHEADER>(_buffer);
                if (header.dwType == NativeMethods.RIM_TYPEMOUSE)
                {
                    IntPtr mousePtr = IntPtr.Add(_buffer, (int)_headerSize);
                    var mouse = Marshal.PtrToStructure<NativeMethods.RAWMOUSE>(mousePtr);

                    // Chỉ cộng dồn chuyển động tương đối (relative deltas)
                    if ((mouse.usFlags & 1) == 0 && IsLocked)
                    {
                        Interlocked.Add(ref _accumulatedDeltaX, mouse.lLastX);
                        Interlocked.Add(ref _accumulatedDeltaY, mouse.lLastY);
                    }
                }
            }
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;
                DestroyHandle();
                Marshal.FreeHGlobal(_buffer);
            }
        }
    }
}
