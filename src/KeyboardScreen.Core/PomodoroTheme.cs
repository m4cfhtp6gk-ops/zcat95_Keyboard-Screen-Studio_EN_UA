namespace KeyboardScreen.Core;

/// <summary>
/// The focus timer as a ring of dots — the app's dot-matrix language rather
/// than a smooth arc — with the countdown in the middle and the cycle tally
/// underneath.
/// </summary>
public sealed class PomodoroTheme : IScreenTheme
{
    private static readonly Color Background = Color.FromRgb(5, 9, 14);
    private static readonly Color Secondary = Color.FromRgb(150, 160, 172);
    private static readonly Color Muted = Color.FromRgb(102, 112, 124);
    private static readonly Color IdleDot = Color.FromRgb(35, 42, 51);
    private static readonly Color BreakColor = Color.FromRgb(86, 156, 214);

    private readonly PomodoroTimer _timer;

    public PomodoroTheme(PomodoroTimer timer)
    {
        _timer = timer;
    }

    public string Id => "pomodoro";
    public string DisplayName => Loc.T("ThemePomodoroName");
    public string Description => Loc.T("ThemePomodoroDescription");
    public string Details => Loc.T("ThemePomodoroDetails");

    public void Draw(ScreenCanvas canvas, SystemSnapshot snapshot)
    {
        Rect safe = canvas.SafeBounds;
        canvas.Fill(Background);
        ThemeHeader.Draw(canvas, snapshot, Loc.T("ScreenTitlePomodoro"));

        PomodoroSnapshot state = _timer.Read(snapshot.Timestamp);
        Color accent = state.Phase == PomodoroPhase.Break ? BreakColor : canvas.AccentColor;

        double centerX = safe.Left + safe.Width / 2;
        double centerY = safe.Top + 150;
        DrawDotRing(canvas, centerX, centerY, 52, state, accent);

        string countdown = state.Phase == PomodoroPhase.Idle
            ? "--:--"
            : $"{(int)state.Remaining.TotalMinutes:00}:{state.Remaining.Seconds:00}";
        canvas.AlignedText(countdown, 25, Colors.White,
            new Rect(safe.Left, centerY - 18, safe.Width, 36), FontWeights.SemiBold, TextAlignment.Center);

        string phase = state.Phase switch
        {
            PomodoroPhase.Focus => Loc.T("ScreenPomodoroFocus"),
            PomodoroPhase.Break => Loc.T("ScreenPomodoroBreak"),
            _ => Loc.T("ScreenPomodoroIdle")
        };
        canvas.AlignedText(phase, 13, state.Phase == PomodoroPhase.Idle ? Secondary : accent,
            new Rect(safe.Left, centerY + 74, safe.Width, 20), FontWeights.SemiBold, TextAlignment.Center);

        if (state.Phase == PomodoroPhase.Idle)
        {
            canvas.AlignedText(Loc.T("ScreenPomodoroStartHint"), 10, Muted,
                new Rect(safe.Left, centerY + 100, safe.Width, 30), FontWeights.Normal, TextAlignment.Center);
        }
        else
        {
            canvas.AlignedText(
                Loc.T("ScreenPomodoroCycles", state.CompletedFocusCycles, state.TargetCycles), 11, Secondary,
                new Rect(safe.Left, centerY + 100, safe.Width, 18), FontWeights.Medium, TextAlignment.Center);
        }

        // Cycle tally along the bottom, one dot per focus block.
        double tallyY = safe.Bottom - 18;
        double spacing = 16;
        double startX = centerX - spacing * (state.TargetCycles - 1) / 2.0;
        for (int index = 0; index < state.TargetCycles; index++)
        {
            bool done = index < state.CompletedFocusCycles;
            canvas.Ellipse(new Rect(startX + index * spacing - 3.5, tallyY - 3.5, 7, 7),
                done ? accent : IdleDot);
        }
    }

    private static void DrawDotRing(
        ScreenCanvas canvas, double centerX, double centerY, double radius,
        PomodoroSnapshot state, Color accent)
    {
        const int dots = 30;
        int lit = state.Phase == PomodoroPhase.Idle ? 0 : (int)Math.Round(state.ElapsedFraction * dots);
        for (int index = 0; index < dots; index++)
        {
            double angle = Math.PI * 2 * index / dots - Math.PI / 2;
            double x = centerX + Math.Cos(angle) * radius;
            double y = centerY + Math.Sin(angle) * radius;
            bool active = index < lit;
            double diameter = active ? 6 : 4;
            canvas.Ellipse(new Rect(x - diameter / 2, y - diameter / 2, diameter, diameter),
                active ? accent : IdleDot);
        }
    }
}
