namespace KeyboardScreen.Core;

/// <summary>
/// The contribution calendar as a dot matrix: 17 weeks stacked newest-first,
/// one row per week and one column per weekday, with this week's count and the
/// window total underneath. Dot size and brightness follow GitHub's 0-4 level.
/// </summary>
public sealed class GitHubContributionTheme : IScreenTheme
{
    private static readonly Color Background = Color.FromRgb(5, 9, 14);
    private static readonly Color Secondary = Color.FromRgb(150, 160, 172);
    private static readonly Color Muted = Color.FromRgb(102, 112, 124);
    private static readonly Color EmptyDot = Color.FromRgb(28, 34, 42);

    private const int WeeksShown = 17;

    public string Id => "github";
    public string DisplayName => Loc.T("ThemeGitHubName");
    public string Description => Loc.T("ThemeGitHubDescription");
    public string Details => Loc.T("ThemeGitHubDetails");

    /// <summary>Whole weeks between the week containing <paramref name="day"/> and the one containing <paramref name="reference"/> (Sunday-based, like GitHub).</summary>
    public static int WeeksBack(DateOnly reference, DateOnly day) =>
        ((reference.DayNumber - (int)reference.DayOfWeek) - (day.DayNumber - (int)day.DayOfWeek)) / 7;

    public void Draw(ScreenCanvas canvas, SystemSnapshot snapshot)
    {
        Rect safe = canvas.SafeBounds;
        canvas.Fill(Background);
        ThemeHeader.Draw(canvas, snapshot, Loc.T("ScreenTitleGitHub"));

        GitHubContributionSnapshot? contributions = snapshot.GitHub;
        if (contributions is not { Available: true } || contributions.Days.Count == 0)
        {
            canvas.AlignedText(Loc.T("ScreenGitHubNotConfigured"), 15, Colors.White,
                new Rect(safe.Left, safe.Top + 150, safe.Width, 24), FontWeights.SemiBold, TextAlignment.Center);
            canvas.AlignedText(contributions?.ErrorMessage ?? Loc.T("ScreenGitHubHint"), 10, Secondary,
                new Rect(safe.Left, safe.Top + 182, safe.Width, 30), FontWeights.Normal, TextAlignment.Center);
            return;
        }

        canvas.Text("@" + contributions.Username, 11, Secondary,
            new Point(safe.Left, safe.Top + 36), FontWeights.SemiBold,
            TextAlignment.Left, safe.Width, 16);

        DateOnly newest = contributions.Days[^1].Date;
        int thisWeek = 0;
        int windowTotal = 0;
        bool countsKnown = true;

        double pitch = 13;
        double gridLeft = safe.Left + (safe.Width - pitch * 7) / 2 + pitch / 2;
        double gridTop = safe.Top + 66;

        foreach (GitHubContributionDay day in contributions.Days)
        {
            int row = WeeksBack(newest, day.Date);
            if (row is < 0 or >= WeeksShown)
            {
                continue;
            }

            if (day.Count < 0)
            {
                countsKnown = false;
            }
            else
            {
                windowTotal += day.Count;
                if (row == 0)
                {
                    thisWeek += day.Count;
                }
            }

            double x = gridLeft + (int)day.Date.DayOfWeek * pitch;
            double y = gridTop + row * pitch + pitch / 2;
            double diameter = day.Level switch { 0 => 4, 1 => 6, 2 => 7, 3 => 8, _ => 9 };
            canvas.Ellipse(new Rect(x - diameter / 2, y - diameter / 2, diameter, diameter),
                day.Level == 0 ? EmptyDot : Dot(canvas.AccentColor, day.Level));
        }

        double statsTop = gridTop + WeeksShown * pitch + 10;
        if (countsKnown)
        {
            canvas.Text(Loc.T("ScreenGitHubThisWeek"), 11, Secondary,
                new Point(safe.Left, statsTop), FontWeights.SemiBold);
            canvas.Text(thisWeek.ToString(), 15, Colors.White,
                new Point(safe.Left, statsTop - 3), FontWeights.SemiBold, TextAlignment.Right, safe.Width);
            canvas.Text(Loc.T("ScreenGitHubWindow", WeeksShown), 11, Secondary,
                new Point(safe.Left, statsTop + 24), FontWeights.SemiBold);
            canvas.Text(windowTotal.ToString(), 15, Colors.White,
                new Point(safe.Left, statsTop + 21), FontWeights.SemiBold, TextAlignment.Right, safe.Width);
        }

        string footer = contributions.IsStale
            ? Loc.T("ScreenStocksStale")
            : Loc.T("ScreenWeatherUpdated", DisplayUnits.Time(contributions.UpdatedAt));
        canvas.AlignedText(footer, 9, Muted,
            new Rect(safe.Left, safe.Bottom - 14, safe.Width, 13), FontWeights.Normal, TextAlignment.Center);
    }

    /// <summary>Accent shades for levels 1-4: dimmed to full strength.</summary>
    private static Color Dot(Color accent, int level)
    {
        double factor = level switch { 1 => 0.4, 2 => 0.6, 3 => 0.8, _ => 1.0 };
        return Color.FromRgb(
            (byte)(accent.R * factor),
            (byte)(accent.G * factor),
            (byte)(accent.B * factor));
    }
}
