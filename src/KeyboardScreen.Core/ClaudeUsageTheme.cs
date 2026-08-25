using System.Globalization;

namespace KeyboardScreen.Core;

/// <summary>
/// Claude subscription limits as three horizontal meters: the rolling session
/// window, the weekly window across every model, and the weekly window for one
/// model. Each row carries the percentage the account reports, a reset
/// countdown, and the tokens seen locally for that window.
/// </summary>
public sealed class ClaudeUsageTheme : IScreenTheme
{
    private static readonly Color Background = Color.FromRgb(5, 9, 14);
    private static readonly Color Panel = Color.FromRgb(15, 20, 27);
    private static readonly Color Stroke = Color.FromRgb(29, 36, 45);
    private static readonly Color Secondary = Color.FromRgb(150, 160, 172);
    private static readonly Color Muted = Color.FromRgb(102, 112, 124);
    private static readonly Color Track = Color.FromRgb(35, 42, 51);

    private const double CardHeight = 94;
    private const double CardGap = 11;

    /// <summary>Space between the percentage and the countdown that shares its line.</summary>
    private const double ColumnGap = 8;

    public string Id => "claude-usage";
    public string DisplayName => Loc.T("ThemeClaudeUsageName");
    public string Description => Loc.T("ThemeClaudeUsageDescription");
    public string Details => Loc.T("ThemeClaudeUsageDetails");

    public void Draw(ScreenCanvas canvas, SystemSnapshot snapshot)
    {
        Rect safe = canvas.SafeBounds;
        canvas.Fill(Background);
        ThemeHeader.Draw(canvas, snapshot, Loc.T("ScreenTitleClaude"));

        ClaudeUsageSnapshot? usage = snapshot.ClaudeUsage;
        var windows = usage is { Available: true }
            ? usage.Windows.ToArray()
            : [];

        if (windows.Length == 0)
        {
            DrawUnavailable(canvas, safe, usage);
            return;
        }

        double top = safe.Top + 40;
        foreach (ClaudeUsageWindow window in windows.Take(3))
        {
            DrawWindow(canvas, new Rect(safe.Left, top, safe.Width, CardHeight), window);
            top += CardHeight + CardGap;
        }

        DrawFooter(canvas, safe, usage);
    }

    private static void DrawUnavailable(ScreenCanvas canvas, Rect safe, ClaudeUsageSnapshot? usage)
    {
        canvas.AlignedText(Loc.T("ScreenClaudeNotConnected"), 15, Colors.White,
            new Rect(safe.Left, safe.Top + 150, safe.Width, 24), FontWeights.SemiBold, TextAlignment.Center);
        canvas.AlignedText(usage?.ErrorMessage ?? Loc.T("ScreenClaudeConnectHint"), 10, Secondary,
            new Rect(safe.Left, safe.Top + 182, safe.Width, 18), FontWeights.Normal, TextAlignment.Center);
    }

    /// <summary>
    /// One meter: label and countdown on one line, the percentage on its own,
    /// then the bar.
    ///
    /// The percentage and the countdown used to share a line by fixed fractions
    /// of it - 0.62 each, starting at 0 and 0.38, so they overlapped by a quarter
    /// of the row and any figure past one digit was ellipsized into "6…". On a
    /// screen whose whole job is that number, that is worse than useless.
    ///
    /// Measuring instead of guessing fixed the overlap but not the crowding: a
    /// 30 px figure and a countdown cannot both live in 96 px. Moving the
    /// countdown up beside the label - which is one short word - gives the digits
    /// the full width and costs nothing, because that line was empty.
    /// </summary>
    private static void DrawWindow(ScreenCanvas canvas, Rect card, ClaudeUsageWindow window)
    {
        canvas.RoundedRect(card, 11, Panel, Stroke);

        double percent = window.EffectivePercent;
        double inset = 11;
        double contentWidth = card.Width - inset * 2;

        // The label is the row's identity - which window this is - so it is
        // measured first and never trimmed to make room. The countdown is the
        // secondary figure and takes what is left, shrinking a couple of points
        // rather than losing its tail: "2год 3…" tells you nothing.
        string label = Label(window);
        string countdown = Countdown(window);
        double labelWidth = Math.Min(
            canvas.MeasureText(label, 11, FontWeights.SemiBold), contentWidth * 0.62);
        double countdownRoom = countdown.Length == 0
            ? 0
            : Math.Max(0, contentWidth - labelWidth - ColumnGap);

        double countdownSize = 10;
        while (countdownSize > 8
               && canvas.MeasureText(countdown, countdownSize, FontWeights.Medium) > countdownRoom)
        {
            countdownSize -= 0.5;
        }

        canvas.AlignedText(label, 11, Secondary,
            new Rect(card.Left + inset, card.Top + 9, labelWidth, 14),
            FontWeights.SemiBold, TextAlignment.Left);
        canvas.AlignedText(countdown, countdownSize, Muted,
            new Rect(card.Right - inset - countdownRoom, card.Top + 9, countdownRoom, 14),
            FontWeights.Medium, TextAlignment.Right);

        // Full width now, so "100%" never has to shrink; the loop is kept only
        // for a font whose digits are wider than the one this was measured with.
        string figure = percent.ToString("0", Loc.Culture) + "%";
        double size = 32;
        while (size > 17 && canvas.MeasureText(figure, size, FontWeights.SemiBold) > contentWidth)
        {
            size -= 1;
        }

        canvas.AlignedText(figure, size, Colors.White,
            new Rect(card.Left + inset, card.Top + 24, contentWidth, 34),
            FontWeights.SemiBold, TextAlignment.Left);

        // "In 37 minutes" and "at 14:30" answer different questions, and the
        // second is the one you act on when deciding whether to start something
        // now. It gets its own line rather than crowding the countdown.
        canvas.AlignedText(ResetMoment(window), 9, Muted,
            new Rect(card.Left + inset, card.Top + 59, contentWidth, 12),
            FontWeights.Normal, TextAlignment.Right);

        canvas.ProgressBar(
            new Rect(card.Left + inset, card.Bottom - 18, contentWidth, 9),
            percent, Track, ClaudeUsagePalette.ForPercent(percent));
    }

    private static void DrawFooter(ScreenCanvas canvas, Rect safe, ClaudeUsageSnapshot? usage)
    {
        string footer = usage is { IsStale: true }
            ? Loc.T("ScreenClaudeStale")
            : Loc.T("ScreenWeatherUpdated", DisplayUnits.Time(usage?.UpdatedAt ?? DateTimeOffset.Now));
        canvas.AlignedText(footer, 9, Muted,
            new Rect(safe.Left, safe.Bottom - 14, safe.Width, 13), FontWeights.Normal, TextAlignment.Center);
    }

    private static string Label(ClaudeUsageWindow window) => window.Kind switch
    {
        ClaudeUsageWindowKind.Session => Loc.T("ScreenClaudeSession"),
        ClaudeUsageWindowKind.Week => Loc.T("ScreenClaudeWeek"),
        _ => string.IsNullOrWhiteSpace(window.ScopeName) ? Loc.T("ScreenClaudeWeek") : window.ScopeName
    };

    /// <summary>
    /// The clock time the window comes back, with the weekday when that is not
    /// today - "at 14:30" is ambiguous four days out, and the weekly windows
    /// are. A weekday rather than a date because none of these windows is more
    /// than seven days off, and "29 серп" did not fit where "пт" does.
    /// </summary>
    private static string ResetMoment(ClaudeUsageWindow window)
    {
        if (window.ResetsAt is not { } resetsAt || resetsAt <= DateTimeOffset.Now)
        {
            return string.Empty;
        }

        DateTimeOffset local = resetsAt.ToLocalTime();
        // Two forms rather than one with the weekday spliced in: "скидання о сб
        // 19:27" is not a sentence in any of these languages.
        return local.Date == DateTimeOffset.Now.Date
            ? Loc.T("ScreenClaudeResetsAt", DisplayUnits.Time(local))
            : Loc.T("ScreenClaudeResetsOn", local.ToString("ddd", Loc.Culture), DisplayUnits.Time(local));
    }

    /// <summary>Time left before the window resets, in the coarsest unit that still reads.</summary>
    private static string Countdown(ClaudeUsageWindow window)
    {
        if (window.ResetsAt is not { } resetsAt)
        {
            return string.Empty;
        }

        TimeSpan left = resetsAt - DateTimeOffset.Now;
        if (left <= TimeSpan.Zero)
        {
            return Loc.T("ScreenClaudeResetNow");
        }

        if (left.TotalDays >= 1)
        {
            return Loc.T("ScreenClaudeDaysHours", (int)left.TotalDays, left.Hours);
        }

        return left.TotalHours >= 1
            ? Loc.T("ScreenClaudeHoursMinutes", (int)left.TotalHours, left.Minutes)
            : Loc.T("ScreenClaudeMinutes", Math.Max(1, left.Minutes));
    }
}
