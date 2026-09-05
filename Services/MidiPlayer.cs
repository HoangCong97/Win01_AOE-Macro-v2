using System.Runtime.InteropServices;

namespace AOEKeyboardMacroPro.Services;

public static class MidiPlayer
{
    [DllImport("winmm.dll")]
    private static extern int midiOutOpen(out IntPtr handle, int deviceID, IntPtr proc, IntPtr instance, int flags);

    [DllImport("winmm.dll")]
    private static extern int midiOutShortMsg(IntPtr handle, int message);

    [DllImport("winmm.dll")]
    private static extern int midiOutClose(IntPtr handle);

    private static IntPtr _hMidi = IntPtr.Zero;
    private static readonly object _midiLock = new();

    private static void EnsureMidiOpen()
    {
        lock (_midiLock)
        {
            if (_hMidi == IntPtr.Zero)
            {
                try
                {
                    midiOutOpen(out _hMidi, -1, IntPtr.Zero, IntPtr.Zero, 0);
                }
                catch
                {
                    _hMidi = IntPtr.Zero;
                }
            }
        }
    }

    private static void SendMidi(int msg)
    {
        lock (_midiLock)
        {
            if (_hMidi != IntPtr.Zero)
            {
                try
                {
                    midiOutShortMsg(_hMidi, msg);
                }
                catch { }
            }
        }
    }

    // ─── Alarm sound (farm timer) ───────────────────────────────────────

    private static readonly object _alarmLock = new();
    private static CancellationTokenSource? _alarmCts;

    public static void PlayAlarmSound(CancellationToken externalToken)
    {
        lock (_alarmLock)
        {
            StopAlarmInternal();
            _alarmCts = new CancellationTokenSource();
        }

        CancellationToken alarmToken = _alarmCts.Token;

        Task.Run(() =>
        {
            EnsureMidiOpen();

            lock (_alarmLock)
            {
                if (alarmToken.IsCancellationRequested || externalToken.IsCancellationRequested)
                    return;
            }

            // Channel 0 (0x90): Set instrument Tubular Bells (14)
            SendMidi(0x00000EC0);

            // Cảnh báo bíp kép liên tục, mỗi lần trễ 10 giây
            while (!alarmToken.IsCancellationRequested && !externalToken.IsCancellationRequested)
            {
                // Alarm pattern: E6-C6-E6-C6 (urgent two-tone)
                int[] alarmNotes = { 88, 84, 88, 84 };
                foreach (int note in alarmNotes)
                {
                    if (alarmToken.IsCancellationRequested || externalToken.IsCancellationRequested) break;

                    // Channel 0 Note On (0x90)
                    int noteOn = 0x90 | (note << 8) | (110 << 16);
                    SendMidi(noteOn);
                    Thread.Sleep(200);

                    // Channel 0 Note Off (0x80)
                    int noteOff = 0x80 | (note << 8);
                    SendMidi(noteOff);
                    Thread.Sleep(80);
                }

                // Trễ 10 giây trước khi lặp lại cảnh báo tiếp theo (cho phép ngắt ngay khi hủy)
                for (int i = 0; i < 100 && !alarmToken.IsCancellationRequested && !externalToken.IsCancellationRequested; i++)
                {
                    Thread.Sleep(100);
                }
            }
        });
    }

    public static void StopAlarmSound()
    {
        lock (_alarmLock)
        {
            StopAlarmInternal();
        }
    }

    private static void StopAlarmInternal()
    {
        if (_alarmCts != null)
        {
            _alarmCts.Cancel();
            _alarmCts = null;
        }
    }

    // ─── Toggle sounds (F1 on/off) ─────────────────────────────────────

    private static readonly object _toggleLock = new();
    private static CancellationTokenSource? _toggleCts;

    private static void StopCurrentToggleSound()
    {
        if (_toggleCts != null)
        {
            _toggleCts.Cancel();
            _toggleCts = null;
        }
    }

    public static void PlayToggleOnSound()
    {
        CancellationToken token;
        lock (_toggleLock)
        {
            StopCurrentToggleSound();
            _toggleCts = new CancellationTokenSource();
            token = _toggleCts.Token;
        }

        Task.Run(() =>
        {
            EnsureMidiOpen();

            lock (_toggleLock)
            {
                if (token.IsCancellationRequested)
                    return;
            }

            // Channel 1 (0x91): Set instrument Vibraphone / Tubular Bells (11) -> 0xC1 | (11 << 8)
            SendMidi(0x00000BC1);

            // Ascending Arpeggio on Channel 1: C5 (72) -> E5 (76) -> G5 (79) -> C6 (84)
            int[] notes = { 72, 76, 79, 84 };
            foreach (int note in notes)
            {
                if (token.IsCancellationRequested) break;
                int noteOnMsg = 0x91 | (note << 8) | (100 << 16);
                SendMidi(noteOnMsg);
                Thread.Sleep(120);
            }

            for (int i = 0; i < 15 && !token.IsCancellationRequested; i++)
                Thread.Sleep(100);
        });
    }

    public static void PlayToggleOffSound()
    {
        CancellationToken token;
        lock (_toggleLock)
        {
            StopCurrentToggleSound();
            _toggleCts = new CancellationTokenSource();
            token = _toggleCts.Token;
        }

        Task.Run(() =>
        {
            EnsureMidiOpen();

            lock (_toggleLock)
            {
                if (token.IsCancellationRequested)
                    return;
            }

            // Channel 1 (0x91): Set instrument Vibraphone / Tubular Bells (11) -> 0xC1 | (11 << 8)
            SendMidi(0x00000BC1);

            // Descending Arpeggio on Channel 1: C6 (84) -> G5 (79) -> E5 (76) -> C5 (72)
            int[] notes = { 84, 79, 76, 72 };
            foreach (int note in notes)
            {
                if (token.IsCancellationRequested) break;
                int noteOnMsg = 0x91 | (note << 8) | (95 << 16);
                SendMidi(noteOnMsg);
                Thread.Sleep(120);
            }

            for (int i = 0; i < 15 && !token.IsCancellationRequested; i++)
                Thread.Sleep(100);
        });
    }

    // ─── Flag Mode sounds (Chế độ đặt cờ) ───────────────────────────────

    private static readonly object _flagLock = new();
    private static CancellationTokenSource? _flagCts;

    private static void StopCurrentFlagSound()
    {
        if (_flagCts != null)
        {
            _flagCts.Cancel();
            _flagCts = null;
        }
    }

    public static void PlayFlagModeOnSound()
    {
        CancellationToken token;
        lock (_flagLock)
        {
            StopCurrentFlagSound();
            _flagCts = new CancellationTokenSource();
            token = _flagCts.Token;
        }

        Task.Run(() =>
        {
            EnsureMidiOpen();

            lock (_flagLock)
            {
                if (token.IsCancellationRequested)
                    return;
            }

            // Channel 2 (0x92): Set instrument Glockenspiel (9) -> 0xC2 | (9 << 8)
            SendMidi(0x000009C2);

            // Sắc nét, thanh thoát (Double chime): A5 (81) -> E6 (88)
            int[] notes = { 81, 88 };
            foreach (int note in notes)
            {
                if (token.IsCancellationRequested) break;
                int noteOnMsg = 0x92 | (note << 8) | (118 << 16);
                SendMidi(noteOnMsg);
                Thread.Sleep(50);
            }

            Thread.Sleep(80);
            SendMidi(0x82 | (81 << 8));
            SendMidi(0x82 | (88 << 8));
        });
    }

    public static void PlayFlagModeOffSound()
    {
        CancellationToken token;
        lock (_flagLock)
        {
            StopCurrentFlagSound();
            _flagCts = new CancellationTokenSource();
            token = _flagCts.Token;
        }

        Task.Run(() =>
        {
            EnsureMidiOpen();

            lock (_flagLock)
            {
                if (token.IsCancellationRequested)
                    return;
            }

            // Channel 2 (0x92): Set instrument Celesta (8) -> 0xC2 | (8 << 8)
            SendMidi(0x000008C2);

            // Nốt chuông nhẹ, êm dịu và thanh thoát: C6 (84) với âm lượng rõ ràng (velocity 110)
            int noteOnMsg = 0x92 | (84 << 8) | (110 << 16);
            SendMidi(noteOnMsg);
            Thread.Sleep(140);
            SendMidi(0x82 | (84 << 8));
        });
    }
}
