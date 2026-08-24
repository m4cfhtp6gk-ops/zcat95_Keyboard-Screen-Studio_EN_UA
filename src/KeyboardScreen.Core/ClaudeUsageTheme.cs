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
    private static readonly Color Warning = Color.FromRgb(232, 165, 71);
    private static readonly Color Critical = Color.FromRgb(238, 78, 88);

    private const double CardHeight = 92;
    private const double CardGap = 14;

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

    private static void DrawWindow(ScreenCanvas canvas, Rect card, ClaudeUsageWindow window)
    {
        canvas.RoundedRect(card, 11, Panel, Stroke);

        double percent = window.EffectivePercent;
        Color fill = percent >= 90 ? Critical : percent >= 75 ? Warning : canvas.AccentColor;
        double inset = 11;
        double contentWidth = card.Width - inset * 2;

        canvas.AlignedText(Label(window), 11, Secondary,
            new Rect(card.Left + inset, card.Top + 9, contentWidth, 15),
            FontWeights.SemiBold, TextAlignment.Left);

        canvas.AlignedText(percent.ToString("0", Loc.Culture) + "%", 30, Colors.White,
            new Rect(card.Left + inset, card.Top + 26, contentWidth * 0.62, 34),
            FontWeights.SemiBold, TextAlignment.Left);
        // The countdown takes the slot the local token tally used to fill, at
        // the size that tally had. "resets in 2h 51m" is the figure a person
        // acts on, and it was previously crammed in at 9 px in the corner.
        canvas.AlignedText(Countdown(window), 11, Secondary,
            new Rect(card.Left + inset + contentWidth * 0.38, card.Top + 35, contentWidth * 0.62, 18),
            FontWeights.Medium, TextAlignment.Right);

        canvas.ProgressBar(new Rect(card.Left + inset, card.Top + 68, contentWidth, 10), percent, Track, fill);
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
