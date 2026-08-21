namespace KeyboardScreen.Core;

public enum PomodoroPhase
{
    Idle,
    Focus,
    Break
}

public sealed record PomodoroSnapshot(
    PomodoroPhase Phase,
    TimeSpan Remaining,
    TimeSpan PhaseLength,
    int CompletedFocusCycles,
    int TargetCycles)
{
    public double ElapsedFraction => PhaseLength <= TimeSpan.Zero
        ? 0
        : Math.Clamp(1 - Remaining.TotalSeconds / PhaseLength.TotalSeconds, 0, 1);
}

public sealed class PomodoroSettings
{
    public int FocusMinutes { get; set; } = 25;

    public int BreakMinutes { get; set; } = 5;

    public int TargetCycles { get; set; } = 4;
}

/// <summary>
/// A wall-clock pomodoro. No background timer runs anywhere: the current phase
/// is derived from the start instant every time it is read, so the state stays
/// correct across refresh ticks, sleep/resume and settings saves.
/// </summary>
public sealed class PomodoroTimer
{
    private readonly object _gate = new();
    private DateTimeOffset? _startedAt;
    private TimeSpan _focus = TimeSpan.FromMinutes(25);
    private TimeSpan _break = TimeSpan.FromMinutes(5);
    private int _targetCycles = 4;

    public event EventHandler? Changed;

    public bool IsRunning
    {
        get
        {
            lock (_gate)
            {
                return _startedAt is not null;
            }
        }
    }

    public void Start(PomodoroSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        lock (_gate)
        {
            _focus = TimeSpan.FromMinutes(Math.Clamp(settings.FocusMinutes, 1, 180));
            _break = TimeSpan.FromMinutes(Math.Clamp(settings.BreakMinutes, 1, 60));
            _targetCycles = Math.Clamp(settings.TargetCycles, 1, 12);
            _startedAt = DateTimeOffset.Now;
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void Stop()
    {
        lock (_gate)
        {
            _startedAt = null;
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }

    public PomodoroSnapshot Read(DateTimeOffset? now = null)
    {
        PomodoroSnapshot snapshot;
        bool completed = false;
        lock (_gate)
        {
            if (_startedAt is not { } startedAt)
            {
                return new PomodoroSnapshot(PomodoroPhase.Idle, TimeSpan.Zero, TimeSpan.Zero, 0, _targetCycles);
            }

            TimeSpan cycle = _focus + _break;
            TimeSpan elapsed = (now ?? DateTimeOffset.Now) - startedAt;
            if (elapsed < TimeSpan.Zero)
            {
                elapsed = TimeSpan.Zero;
            }

            int fullCycles = (int)(elapsed.Ticks / cycle.Ticks);
            TimeSpan intoCycle = TimeSpan.FromTicks(elapsed.Ticks % cycle.Ticks);

            // The run is complete once the final focus block ends - no trailing break.
            if (fullCycles >= _targetCycles ||
                (fullCycles == _targetCycles - 1 && intoCycle >= _focus))
            {
                _startedAt = null;
                completed = true;
                snapshot = new PomodoroSnapshot(PomodoroPhase.Idle, TimeSpan.Zero, TimeSpan.Zero, _targetCycles, _targetCycles);
            }
            else
            {
                snapshot = intoCycle < _focus
                    ? new PomodoroSnapshot(PomodoroPhase.Focus, _focus - intoCycle, _focus, fullCycles, _targetCycles)
                    : new PomodoroSnapshot(PomodoroPhase.Break, cycle - intoCycle, _break, fullCycles + 1, _targetCycles);
            }
        }

        if (completed)
        {
            Changed?.Invoke(this, EventArgs.Empty);
        }

        return snapshot;
    }
}
