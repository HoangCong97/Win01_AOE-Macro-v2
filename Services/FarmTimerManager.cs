namespace AOEKeyboardMacroPro.Services;

public class FarmTimerManager : IDisposable
{
    private CancellationTokenSource? _cts1;
    private CancellationTokenSource? _cts2;
    private CancellationTokenSource? _beepCts;
    private bool _isEnabled = true;

    public int IntervalSeconds { get; set; } = 200;

    public int RemainingSeconds1 { get; private set; } = -1;
    public int RemainingSeconds2 { get; private set; } = -1;

    public bool IsAlarm1Active { get; private set; } = false;
    public bool IsAlarm2Active { get; private set; } = false;

    public bool IsEnabled
    {
        get => _isEnabled;
        set
        {
            _isEnabled = value;
            if (!_isEnabled)
            {
                StopAllTimersAndAlarms();
            }
        }
    }

    public event Action<int, int>? TimerTick; // (remaining1, remaining2)
    public event Action<string>? AlarmStateChanged;

    public void StartTimer1()
    {
        if (!_isEnabled) return;

        if (_cts1 == null || _cts1.Token.IsCancellationRequested || RemainingSeconds1 <= 0)
        {
            RestartTimer1();
        }
    }

    public void RestartTimer1()
    {
        if (!_isEnabled) return;

        IsAlarm1Active = false;
        _cts1?.Cancel();
        _cts1 = null;
        RemainingSeconds1 = IntervalSeconds;

        CheckBeepLoop();

        _cts1 = new CancellationTokenSource();
        Task.Run(() => RunLoop1(_cts1.Token));
    }

    public void StopTimer1()
    {
        IsAlarm1Active = false;
        _cts1?.Cancel();
        _cts1 = null;
        RemainingSeconds1 = -1;
        CheckBeepLoop();
        TimerTick?.Invoke(RemainingSeconds1, RemainingSeconds2);
    }

    public void StartTimer2()
    {
        if (!_isEnabled) return;

        if (_cts2 == null || _cts2.Token.IsCancellationRequested || RemainingSeconds2 <= 0)
        {
            RestartTimer2();
        }
    }

    public void RestartTimer2()
    {
        if (!_isEnabled) return;

        IsAlarm2Active = false;
        _cts2?.Cancel();
        _cts2 = null;
        RemainingSeconds2 = IntervalSeconds;

        CheckBeepLoop();

        _cts2 = new CancellationTokenSource();
        Task.Run(() => RunLoop2(_cts2.Token));
    }

    public void StopTimer2()
    {
        IsAlarm2Active = false;
        _cts2?.Cancel();
        _cts2 = null;
        RemainingSeconds2 = -1;
        CheckBeepLoop();
        TimerTick?.Invoke(RemainingSeconds1, RemainingSeconds2);
    }

    public void StopAllTimersAndAlarms()
    {
        IsAlarm1Active = false;
        IsAlarm2Active = false;

        _cts1?.Cancel();
        _cts1 = null;
        RemainingSeconds1 = -1;

        _cts2?.Cancel();
        _cts2 = null;
        RemainingSeconds2 = -1;

        if (_beepCts != null)
        {
            _beepCts.Cancel();
            _beepCts = null;
        }

        MidiPlayer.StopAlarmSound();
        TimerTick?.Invoke(RemainingSeconds1, RemainingSeconds2);
    }

    private async Task RunLoop1(CancellationToken token)
    {
        while (!token.IsCancellationRequested && RemainingSeconds1 > 0 && _isEnabled)
        {
            await Task.Delay(1000, token).ConfigureAwait(false);
            RemainingSeconds1--;
            TimerTick?.Invoke(RemainingSeconds1, RemainingSeconds2);
        }

        if (!token.IsCancellationRequested && RemainingSeconds1 <= 0 && _isEnabled)
        {
            IsAlarm1Active = true;
            AlarmStateChanged?.Invoke("Đạo ruộng 1 HẾT HẠN! (Bấm SHIFT+F để làm mới)");
            CheckBeepLoop();
        }
    }

    private async Task RunLoop2(CancellationToken token)
    {
        while (!token.IsCancellationRequested && RemainingSeconds2 > 0 && _isEnabled)
        {
            await Task.Delay(1000, token).ConfigureAwait(false);
            RemainingSeconds2--;
            TimerTick?.Invoke(RemainingSeconds1, RemainingSeconds2);
        }

        if (!token.IsCancellationRequested && RemainingSeconds2 <= 0 && _isEnabled)
        {
            IsAlarm2Active = true;
            AlarmStateChanged?.Invoke("Đạo ruộng 2 HẾT HẠN! (Bấm SHIFT+G để làm mới)");
            CheckBeepLoop();
        }
    }

    private void CheckBeepLoop()
    {
        // Cancel active alarm and stop sound immediately
        if (_beepCts != null)
        {
            _beepCts.Cancel();
            _beepCts = null;
        }
        MidiPlayer.StopAlarmSound();

        bool needAlarm = (IsAlarm1Active || IsAlarm2Active) && _isEnabled;

        if (needAlarm)
        {
            _beepCts = new CancellationTokenSource();
            MidiPlayer.PlayAlarmSound(_beepCts.Token);
        }
    }

    public void Dispose()
    {
        StopAllTimersAndAlarms();
        GC.SuppressFinalize(this);
    }
}
