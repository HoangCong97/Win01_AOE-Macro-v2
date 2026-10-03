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
    /// Lấy phạm vi màn hình thực tế của cửa sổ game AOE (Client area quy đổi ra tọa độ màn hình, kẹp chặt trong màn hình chứa cửa sổ).
    /// </summary>
    public static bool TryGetAoeWindowScreenRect(out NativeMethods.RECT screenRect)
    {
        return AoeWindowHelper.TryGetAoeWindowScreenRect(out screenRect);
    }

    /// <summary>
    /// Thực thi một hành động có bảo vệ chuột:
    /// - Ghi nhận vị trí ban đầu của chuột và lưu lại vùng ClipCursor hiện hành của game.
    /// - Reset bộ tích lũy delta chuyển động (Raw Input).
    /// - Khóa click chuột người dùng để tránh bấm nhầm.
    /// - Tùy chọn ghim vị trí chuột tại chỗ (pinAtCurrentPos).
    /// - Sau khi xong: khôi phục nguyên vẹn vùng ClipCursor trước đó của game AOE,
    ///   giới hạn tọa độ bù chuột chặt chẽ trong khung cửa sổ game AOE (không văng sang màn hình 2).
    /// </summary>
    public static void ExecuteLockedAction(Action action, bool pinAtCurrentPos = false)
    {
        lock (_lockObj)
        {
            NativeMethods.GetCursorPos(out NativeMethods.POINT initialPt);
            NativeMethods.GetClipCursor(out NativeMethods.RECT savedClip);

            Interlocked.Exchange(ref _accumulatedDeltaX, 0);
            Interlocked.Exchange(ref _accumulatedDeltaY, 0);
            IsLocked = true;
            _receiver?.SetSinkEnabled(true);

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
                // Khôi phục lại đúng vùng khóa chuột gốc của cửa sổ game AOE trước khi macro chạy.
                // TUYỆT ĐỐI KHÔNG GỌI UnpinCursor() vì sẽ hủy toàn bộ ClipCursor của game trên Windows!
                bool restored = false;
                if (savedClip.Right > savedClip.Left + 10 && savedClip.Bottom > savedClip.Top + 10)
                {
                    NativeMethods.ClipCursor(ref savedClip);
                    restored = true;
                }

                if (!restored && TryGetAoeWindowScreenRect(out NativeMethods.RECT aoeScreenRect))
                {
                    NativeMethods.ClipCursor(ref aoeScreenRect);
                }

                _receiver?.SetSinkEnabled(false);
                IsLocked = false;

                int dx = Interlocked.Exchange(ref _accumulatedDeltaX, 0);
                int dy = Interlocked.Exchange(ref _accumulatedDeltaY, 0);

                int finalX = initialPt.X + dx;
                int finalY = initialPt.Y + dy;

                // Giới hạn tọa độ bù chuột chặt chẽ trong khung cửa sổ game AOE để không văng ra ngoài màn hình 2
                if (TryGetAoeWindowScreenRect(out NativeMethods.RECT aoeLimit))
                {
                    finalX = Math.Clamp(finalX, aoeLimit.Left, aoeLimit.Right - 1);
                    finalY = Math.Clamp(finalY, aoeLimit.Top, aoeLimit.Bottom - 1);
                }
                else if (savedClip.Right > savedClip.Left && savedClip.Bottom > savedClip.Top)
                {
                    finalX = Math.Clamp(finalX, savedClip.Left, savedClip.Right - 1);
                    finalY = Math.Clamp(finalY, savedClip.Top, savedClip.Bottom - 1);
                }

                NativeMethods.SetCursorPos(finalX, finalY);
            }
        }
    }

    /// <summary>
    /// Mở khóa khẩn cấp: giải phóng hoàn toàn con trỏ chuột và reset các trạng thái khóa.
    /// </summary>
    public static void ForceUnlock()
    {
        UnpinCursor();
        _receiver?.SetSinkEnabled(false);
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
        }

        public void SetSinkEnabled(bool enabled)
        {
            if (_disposed || Handle == IntPtr.Zero) return;

            try
            {
                var rid = new NativeMethods.RAWINPUTDEVICE
                {
                    usUsagePage = NativeMethods.HID_USAGE_PAGE_GENERIC,
                    usUsage = NativeMethods.HID_USAGE_GENERIC_MOUSE,
                    dwFlags = enabled ? NativeMethods.RIDEV_INPUTSINK : NativeMethods.RIDEV_REMOVE,
                    hwndTarget = enabled ? Handle : IntPtr.Zero
                };

                NativeMethods.RegisterRawInputDevices([rid], 1, (uint)Marshal.SizeOf<NativeMethods.RAWINPUTDEVICE>());
            }
            catch { }
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
