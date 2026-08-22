using System.Globalization;

namespace KeyboardScreen.Core;

public sealed class CountdownItem
{
    public string Title { get; set; } = string.Empty;

    /// <summary>"2026-12-31", "31.12.2026" or either with " HH:mm".</summary>
    public string Date { get; set; } = string.Empty;
}

public sealed class CountdownSettings
{
    public List<CountdownItem> Items { get; set; } = [new(), new(), new()];

    private static readonly string[] DateFormats =
    [
        "yyyy-MM-dd HH:mm", "yyyy-MM-dd",
        "dd.MM.yyyy HH:mm", "dd.MM.yyyy",
        "dd/MM/yyyy HH:mm", "dd/MM/yyyy"
    ];

    public static bool TryParseDate(string? value, out DateTimeOffset target)
    {
        target = default;
        string text = (value ?? string.Empty).Trim();
        if (text.Length == 0)
        {
            return false;
        }

        if (DateTime.TryParseExact(text, DateFormats, CultureInfo.InvariantCulture,
                DateTimeStyles.None, out DateTime parsed))
        {
            target = new DateTimeOffset(parsed, DateTimeOffset.Now.Offset);
            return true;
        }

        return false;
    }
}

public sealed record CountdownEntry(string Title, DateTimeOffset Target);

public sealed record CountdownSnapshot(IReadOnlyList<CountdownEntry> Events)
{
    public static CountdownSnapshot From(CountdownSettings? settings)
    {
        var events = new List<CountdownEntry>();
        foreach (CountdownItem item in settings?.Items ?? [])
        {
            if (!string.IsNullOrWhiteSpace(item.Title) &&
                CountdownSettings.TryParseDate(item.Date, out DateTimeOffset target))
            {
                events.Add(new CountdownEntry(item.Title.Trim(), target));
            }
        }

        events.Sort((a, b) => a.Target.CompareTo(b.Target));
        return new CountdownSnapshot(events.Take(3).ToArray());
    }
}

/// <summary>The nearest upcoming event large, the rest in rows below.</summary>
public sealed class CountdownTheme : IScreenTheme
{
    private static readonly Color Background = Color.FromRgb(5, 9, 14);
    private static readonly Color CardColor = Color.FromRgb(15, 20, 27);
    private static readonly Color CardStroke = Color.FromRgb(29, 36, 45);
    private static readonly Color Secondary = Color.FromRgb(150, 160, 172);
    private static readonly Color DoneGreen = Color.FromRgb(64, 186, 120);

    public string Id => "countdown";
    public string DisplayName => Loc.T("ThemeCountdownName");
    public string Description => Loc.T("ThemeCountdownDescription");
    public string Details => Loc.T("ThemeCountdownDetails");

    public void Draw(ScreenCanvas canvas, SystemSnapshot snapshot)
    {
        Rect safe = canvas.SafeBounds;
        canvas.Fill(Background);
        ThemeHeader.Draw(canvas, snapshot, Loc.T("ScreenTitleCountdown"));

        IReadOnlyList<CountdownEntry> events = snapshot.Countdown?.Events ?? [];
        if (events.Count == 0)
        {
            canvas.AlignedText(Loc.T("ScreenCountdownEmpty"), 13, Colors.White,
                new Rect(safe.Left, safe.Top + 150, safe.Width, 24), FontWeights.SemiBold, TextAlignment.Center);
            canvas.AlignedText(Loc.T("ScreenCountdownHint"), 10, Secondary,
                new Rect(safe.Left, safe.Top + 180, safe.Width, 30), FontWeights.Normal, TextAlignment.Center);
            return;
        }

        DateTimeOffset now = snapshot.Timestamp;
        CountdownEntry next = events.FirstOrDefault(entry => entry.Target > now) ?? events[^1];

        // The hero card for the nearest event.
        var hero = new Rect(safe.Left, safe.Top + 42, safe.Width, 150);
        canvas.RoundedRect(hero, 14, CardColor, CardStroke);
        canvas.AlignedText(next.Title, 12, Colors.White,
            new Rect(hero.Left + 12, hero.Top + 12, hero.Width - 24, 18), FontWeights.SemiBold, TextAlignment.Center);

        TimeSpan left = next.Target - now;
        if (left > TimeSpan.Zero)
        {
            (string big, string unit) = left.TotalDays >= 1
                ? (Math.Ceiling(left.TotalDays).ToString("0"), Loc.T("ScreenCountdownDays"))
                : left.TotalHours >= 1
                    ? (((int)left.TotalHours).ToString(), Loc.T("ScreenCountdownHours"))
                    : (Math.Max(1, (int)left.TotalMinutes).ToString(), Loc.T("ScreenCountdownMinutes"));
            canvas.AlignedText(big, 52, canvas.AccentColor,
                new Rect(hero.Left, hero.Top + 34, hero.Width, 62), FontWeights.SemiBold, TextAlignment.Center);
            canvas.AlignedText(unit, 12, Secondary,
                new Rect(hero.Left, hero.Top + 100, hero.Width, 18), FontWeights.Medium, TextAlignment.Center);
        }
        else
        {
            canvas.AlignedText(Loc.T("ScreenCountdownArrived"), 24, DoneGreen,
                new Rect(hero.Left, hero.Top + 52, hero.Width, 36), FontWeights.SemiBold, TextAlignment.Center);
        }

        canvas.AlignedText(FormatTarget(next.Target), 10, Secondary,
            new Rect(hero.Left, hero.Bottom - 26, hero.Width, 15), FontWeights.Normal, TextAlignment.Center);

        // The remaining events as compact rows.
        double top = hero.Bottom + 10;
        foreach (CountdownEntry entry in events.Where(entry => entry != next).Take(2))
        {
            var row = new Rect(safe.Left, top, safe.Width, 56);
            canvas.RoundedRect(row, 10, CardColor, CardStroke);
            var content = new Rect(row.Left + 11, row.Top + 8, row.Width - 22, row.Height - 16);
            canvas.AlignedText(entry.Title, 11, Colors.White,
                new Rect(content.Left, content.Top, content.Width - 44, 15), FontWeights.SemiBold, TextAlignment.Left);
            canvas.AlignedText(Loc.ShortDate(entry.Target), 9, Secondary,
                new Rect(content.Left, content.Top + 1, content.Width, 13), FontWeights.Normal, TextAlignment.Right);
            TimeSpan rowLeft = entry.Target - now;
            string remaining = rowLeft <= TimeSpan.Zero
                ? Loc.T("ScreenCountdownArrived")
                : rowLeft.TotalDays >= 1
                    ? Loc.T("ScreenCountdownDaysShort", Math.Ceiling(rowLeft.TotalDays))
                    : Loc.T("ScreenClaudeHoursMinutes", (int)rowLeft.TotalHours, rowLeft.Minutes);
            canvas.AlignedText(remaining, 12, rowLeft <= TimeSpan.Zero ? DoneGreen : canvas.AccentColor,
                new Rect(content.Left, content.Top + 17, content.Width, 18), FontWeights.SemiBold, TextAlignment.Left);
            top += 62;
        }
    }

    private static string FormatTarget(DateTimeOffset target) =>
        target.TimeOfDay == TimeSpan.Zero
            ? Loc.ShortDate(target) + " " + target.Year
            : Loc.ShortDate(target) + " " + DisplayUnits.Time(target);
}
