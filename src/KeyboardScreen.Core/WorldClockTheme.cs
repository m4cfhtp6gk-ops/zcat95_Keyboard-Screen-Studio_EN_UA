namespace KeyboardScreen.Core;

public sealed class WorldClockItem
{
    public string Label { get; set; } = string.Empty;

    /// <summary>IANA or Windows time-zone id; .NET 8 accepts either on both platforms.</summary>
    public string TimeZoneId { get; set; } = string.Empty;
}

public sealed class WorldClockSettings
{
    public List<WorldClockItem> Items { get; set; } = [new(), new(), new(), new()];
}

public sealed record WorldClockEntry(string Label, string TimeZoneId);

public sealed record WorldClockSnapshot(IReadOnlyList<WorldClockEntry> Clocks)
{
    public static WorldClockSnapshot From(WorldClockSettings? settings) =>
        new((settings?.Items ?? [])
            .Where(item => !string.IsNullOrWhiteSpace(item.TimeZoneId))
            .Take(4)
            .Select(item => new WorldClockEntry(item.Label.Trim(), item.TimeZoneId.Trim()))
            .ToArray());
}

/// <summary>Up to four cities with their local time and the offset from here.</summary>
public sealed class WorldClockTheme : IScreenTheme
{
    private static readonly Color Background = Color.FromRgb(5, 9, 14);
    private static readonly Color CardColor = Color.FromRgb(15, 20, 27);
    private static readonly Color CardStroke = Color.FromRgb(29, 36, 45);
    private static readonly Color Secondary = Color.FromRgb(150, 160, 172);
    private static readonly Color NightBlue = Color.FromRgb(122, 148, 196);

    public string Id => "world-clock";
    public string DisplayName => Loc.T("ThemeWorldClockName");
    public string Description => Loc.T("ThemeWorldClockDescription");
    public string Details => Loc.T("ThemeWorldClockDetails");

    public void Draw(ScreenCanvas canvas, SystemSnapshot snapshot)
    {
        Rect safe = canvas.SafeBounds;
        canvas.Fill(Background);
        ThemeHeader.Draw(canvas, snapshot, Loc.T("ScreenTitleWorldClock"));

        IReadOnlyList<WorldClockEntry> clocks = snapshot.WorldClocks?.Clocks ?? [];
        if (clocks.Count == 0)
        {
            canvas.AlignedText(Loc.T("ScreenWorldClockEmpty"), 13, Colors.White,
                new Rect(safe.Left, safe.Top + 150, safe.Width, 24), FontWeights.SemiBold, TextAlignment.Center);
            canvas.AlignedText(Loc.T("ScreenWorldClockHint"), 10, Secondary,
                new Rect(safe.Left, safe.Top + 180, safe.Width, 30), FontWeights.Normal, TextAlignment.Center);
            return;
        }

        double rowHeight = clocks.Count <= 2 ? 118 : clocks.Count == 3 ? 96 : 80;
        const double gap = 7;
        double top = safe.Top + 42;
        foreach (WorldClockEntry entry in clocks)
        {
            var row = new Rect(safe.Left, top, safe.Width, rowHeight);
            canvas.RoundedRect(row, 12, CardColor, CardStroke);
            var content = new Rect(row.Left + 12, row.Top + 8, row.Width - 24, row.Height - 16);

            if (TryGetTime(entry.TimeZoneId, snapshot.Timestamp, out DateTimeOffset localTime))
            {
                bool night = localTime.Hour is < 7 or >= 22;
                string label = entry.Label.Length > 0 ? entry.Label : ShortZoneName(entry.TimeZoneId);
                canvas.AlignedText(label, 11, night ? NightBlue : canvas.AccentColor,
                    new Rect(content.Left, content.Top, content.Width, 15), FontWeights.SemiBold, TextAlignment.Left);
                canvas.AlignedText(DayMark(snapshot.Timestamp, localTime), 10, Secondary,
                    new Rect(content.Left, content.Top, content.Width, 15), FontWeights.Medium, TextAlignment.Right);
                canvas.AlignedText(DisplayUnits.Time(localTime), rowHeight >= 96 ? 34 : 27, Colors.White,
                    new Rect(content.Left, content.Top + 14, content.Width, rowHeight - 40), FontWeights.SemiBold, TextAlignment.Left);
                canvas.AlignedText(OffsetText(snapshot.Timestamp, localTime), 10, Secondary,
                    new Rect(content.Left, content.Bottom - 14, content.Width, 14), FontWeights.Normal, TextAlignment.Left);
            }
            else
            {
                canvas.AlignedText(entry.Label.Length > 0 ? entry.Label : entry.TimeZoneId, 11, Secondary,
                    new Rect(content.Left, content.Top, content.Width, 15), FontWeights.SemiBold, TextAlignment.Left);
                canvas.AlignedText(Loc.T("ScreenWorldClockBadZone"), 12, Colors.White,
                    new Rect(content.Left, content.Top + 18, content.Width, 20), FontWeights.Medium, TextAlignment.Left);
            }

            top += rowHeight + gap;
        }
    }

    private static readonly Dictionary<string, TimeZoneInfo?> ZoneCache = new(StringComparer.OrdinalIgnoreCase);

    public static bool TryGetTime(string timeZoneId, DateTimeOffset now, out DateTimeOffset local)
    {
        TimeZoneInfo? zone;
        lock (ZoneCache)
        {
            if (!ZoneCache.TryGetValue(timeZoneId, out zone))
            {
                try
                {
                    zone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
                }
                catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException)
                {
                    zone = null;
                }
                ZoneCache[timeZoneId] = zone;
            }
        }

        if (zone is null)
        {
            local = default;
            return false;
        }

        local = TimeZoneInfo.ConvertTime(now, zone);
        return true;
    }

    /// <summary>"+1д"/"-1д" when the remote calendar day differs from the local one.</summary>
    private static string DayMark(DateTimeOffset here, DateTimeOffset there)
    {
        int shift = there.Date.DayNumber() - here.Date.DayNumber();
        return shift == 0 ? string.Empty : Loc.T(shift > 0 ? "ScreenWorldClockNextDay" : "ScreenWorldClockPrevDay");
    }

    private static string OffsetText(DateTimeOffset here, DateTimeOffset there)
    {
        TimeSpan delta = there.Offset - here.Offset;
        if (delta == TimeSpan.Zero)
        {
            return Loc.T("ScreenWorldClockSameTime");
        }

        string sign = delta > TimeSpan.Zero ? "+" : "−";
        TimeSpan magnitude = delta.Duration();
        string hours = magnitude.Minutes == 0
            ? magnitude.Hours.ToString()
            : $"{magnitude.Hours}:{magnitude.Minutes:00}";
        return Loc.T("ScreenWorldClockOffset", sign + hours);
    }

    private static string ShortZoneName(string timeZoneId)
    {
        int slash = timeZoneId.LastIndexOf('/');
        return (slash >= 0 ? timeZoneId[(slash + 1)..] : timeZoneId).Replace('_', ' ');
    }
}

internal static class DateExtensions
{
    public static int DayNumber(this DateTime date) => DateOnly.FromDateTime(date).DayNumber;
}
