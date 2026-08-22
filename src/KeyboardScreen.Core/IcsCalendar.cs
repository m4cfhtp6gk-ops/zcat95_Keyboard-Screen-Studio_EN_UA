using System.Globalization;
using System.Net.Http;

namespace KeyboardScreen.Core;

public sealed class CalendarSettings
{
    /// <summary>
    /// A private ICS link usually embeds an access token, so it is treated
    /// like a credential: stored locally, sent only to its own host and
    /// stripped from settings exports.
    /// </summary>
    public string IcsUrl { get; set; } = string.Empty;

    public bool IsConfigured => Uri.TryCreate(IcsUrl?.Trim(), UriKind.Absolute, out Uri? uri)
        && uri.Scheme is "http" or "https" or "webcal";
}

public sealed record CalendarEventInfo(
    string Title,
    DateTimeOffset Start,
    bool IsAllDay);

public sealed record CalendarSnapshot(
    bool Available,
    IReadOnlyList<CalendarEventInfo> Events,
    DateTimeOffset UpdatedAt,
    bool IsStale = false,
    string? ErrorMessage = null)
{
    public static CalendarSnapshot Unavailable(string? message = null) =>
        new(false, [], DateTimeOffset.MinValue, false, message);
}

/// <summary>
/// A bounded iCalendar reader: VEVENT with DTSTART (date or date-time, TZID
/// or UTC), SUMMARY, EXDATE, and the common RRULE shapes - FREQ
/// DAILY/WEEKLY/MONTHLY/YEARLY with INTERVAL, COUNT, UNTIL and weekly BYDAY.
/// Anything fancier is ignored rather than guessed at.
/// </summary>
public static class IcsParser
{
    private static readonly string[] DateTimeFormats = ["yyyyMMdd'T'HHmmss", "yyyyMMdd'T'HHmm"];

    public static List<CalendarEventInfo> Parse(string ics, DateTimeOffset windowStart, DateTimeOffset windowEnd)
    {
        var events = new List<CalendarEventInfo>();
        foreach (Dictionary<string, (string Parameters, string Value)> block in ReadEventBlocks(ics))
        {
            if (!block.TryGetValue("DTSTART", out (string Parameters, string Value) start) ||
                !TryParseStamp(start.Value, start.Parameters, out DateTimeOffset firstStart, out bool isAllDay))
            {
                continue;
            }

            string title = block.TryGetValue("SUMMARY", out (string Parameters, string Value) summary)
                ? Unescape(summary.Value)
                : Loc.T("ScreenCalendarUntitled");

            var excluded = new HashSet<DateTime>();
            if (block.TryGetValue("EXDATE", out (string Parameters, string Value) exdate))
            {
                foreach (string part in exdate.Value.Split(','))
                {
                    if (TryParseStamp(part.Trim(), exdate.Parameters, out DateTimeOffset skip, out _))
                    {
                        excluded.Add(skip.DateTime);
                    }
                }
            }

            IEnumerable<DateTimeOffset> occurrences =
                block.TryGetValue("RRULE", out (string Parameters, string Value) rule)
                    ? ExpandRule(firstStart, rule.Value, windowEnd)
                    : [firstStart];

            foreach (DateTimeOffset occurrence in occurrences)
            {
                if (occurrence < windowStart || occurrence > windowEnd || excluded.Contains(occurrence.DateTime))
                {
                    continue;
                }

                events.Add(new CalendarEventInfo(title, occurrence, isAllDay));
            }
        }

        events.Sort((a, b) => a.Start.CompareTo(b.Start));
        return events;
    }

    /// <summary>Unfolds continuation lines and yields the properties of each VEVENT.</summary>
    private static IEnumerable<Dictionary<string, (string, string)>> ReadEventBlocks(string ics)
    {
        string[] raw = ics.Replace("\r\n", "\n").Split('\n');
        var lines = new List<string>();
        foreach (string line in raw)
        {
            if (line.Length > 0 && (line[0] == ' ' || line[0] == '\t') && lines.Count > 0)
            {
                lines[^1] += line[1..];
            }
            else
            {
                lines.Add(line);
            }
        }

        Dictionary<string, (string, string)>? current = null;
        foreach (string line in lines)
        {
            if (line.Equals("BEGIN:VEVENT", StringComparison.OrdinalIgnoreCase))
            {
                current = new Dictionary<string, (string, string)>(StringComparer.OrdinalIgnoreCase);
                continue;
            }

            if (line.Equals("END:VEVENT", StringComparison.OrdinalIgnoreCase))
            {
                if (current is not null)
                {
                    yield return current;
                }
                current = null;
                continue;
            }

            if (current is null)
            {
                continue;
            }

            int colon = line.IndexOf(':');
            if (colon <= 0)
            {
                continue;
            }

            string head = line[..colon];
            int semicolon = head.IndexOf(';');
            string name = semicolon >= 0 ? head[..semicolon] : head;
            string parameters = semicolon >= 0 ? head[(semicolon + 1)..] : string.Empty;
            current[name] = (parameters, line[(colon + 1)..]);
        }
    }

    private static bool TryParseStamp(string value, string parameters, out DateTimeOffset stamp, out bool isAllDay)
    {
        stamp = default;
        isAllDay = false;
        value = value.Trim();

        if (parameters.Contains("VALUE=DATE", StringComparison.OrdinalIgnoreCase) ||
            (value.Length == 8 && !value.Contains('T')))
        {
            if (DateTime.TryParseExact(value, "yyyyMMdd", CultureInfo.InvariantCulture,
                    DateTimeStyles.None, out DateTime date))
            {
                stamp = new DateTimeOffset(date, DateTimeOffset.Now.Offset);
                isAllDay = true;
                return true;
            }
            return false;
        }

        bool utc = value.EndsWith('Z');
        string text = utc ? value[..^1] : value;
        if (!DateTime.TryParseExact(text, DateTimeFormats, CultureInfo.InvariantCulture,
                DateTimeStyles.None, out DateTime parsed))
        {
            return false;
        }

        if (utc)
        {
            stamp = new DateTimeOffset(parsed, TimeSpan.Zero).ToLocalTime();
            return true;
        }

        string zoneId = ExtractParameter(parameters, "TZID");
        if (zoneId.Length > 0 && WorldClockTheme.TryGetTime(zoneId, DateTimeOffset.UtcNow, out _))
        {
            try
            {
                TimeZoneInfo zone = TimeZoneInfo.FindSystemTimeZoneById(zoneId);
                stamp = new DateTimeOffset(parsed, zone.GetUtcOffset(parsed)).ToLocalTime();
                return true;
            }
            catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException)
            {
            }
        }

        // Floating time (or an unknown zone): read it as local.
        stamp = new DateTimeOffset(parsed, DateTimeOffset.Now.Offset);
        return true;
    }

    private static string ExtractParameter(string parameters, string name)
    {
        foreach (string part in parameters.Split(';'))
        {
            int equals = part.IndexOf('=');
            if (equals > 0 && part[..equals].Equals(name, StringComparison.OrdinalIgnoreCase))
            {
                return part[(equals + 1)..].Trim('"');
            }
        }
        return string.Empty;
    }

    private static IEnumerable<DateTimeOffset> ExpandRule(DateTimeOffset first, string rule, DateTimeOffset windowEnd)
    {
        string frequency = string.Empty;
        int interval = 1;
        int? count = null;
        DateTimeOffset? until = null;
        var byDays = new List<DayOfWeek>();

        foreach (string part in rule.Split(';'))
        {
            int equals = part.IndexOf('=');
            if (equals <= 0)
            {
                continue;
            }

            string key = part[..equals].ToUpperInvariant();
            string value = part[(equals + 1)..];
            switch (key)
            {
                case "FREQ":
                    frequency = value.ToUpperInvariant();
                    break;
                case "INTERVAL" when int.TryParse(value, out int parsedInterval) && parsedInterval > 0:
                    interval = parsedInterval;
                    break;
                case "COUNT" when int.TryParse(value, out int parsedCount) && parsedCount > 0:
                    count = parsedCount;
                    break;
                case "UNTIL" when TryParseStamp(value, string.Empty, out DateTimeOffset parsedUntil, out _):
                    until = parsedUntil;
                    break;
                case "BYDAY":
                    foreach (string day in value.Split(','))
                    {
                        DayOfWeek? weekday = day.Trim().ToUpperInvariant() switch
                        {
                            "MO" => DayOfWeek.Monday,
                            "TU" => DayOfWeek.Tuesday,
                            "WE" => DayOfWeek.Wednesday,
                            "TH" => DayOfWeek.Thursday,
                            "FR" => DayOfWeek.Friday,
                            "SA" => DayOfWeek.Saturday,
                            "SU" => DayOfWeek.Sunday,
                            _ => null
                        };
                        if (weekday is { } parsedDay)
                        {
                            byDays.Add(parsedDay);
                        }
                    }
                    break;
            }
        }

        if (frequency is not ("DAILY" or "WEEKLY" or "MONTHLY" or "YEARLY"))
        {
            yield return first;
            yield break;
        }

        int emitted = 0;
        const int hardCap = 400;

        if (frequency == "WEEKLY" && byDays.Count > 0)
        {
            // Walk day by day from the start of the first week, emitting the
            // listed weekdays of every interval-th week.
            DateTimeOffset weekAnchor = first.AddDays(-WeekdayIndex(first.DayOfWeek));
            for (int week = 0; emitted < hardCap; week += interval)
            {
                DateTimeOffset weekStart = weekAnchor.AddDays(week * 7);
                foreach (DayOfWeek day in byDays.OrderBy(WeekdayIndex))
                {
                    DateTimeOffset occurrence = weekStart.AddDays(WeekdayIndex(day));
                    if (occurrence < first)
                    {
                        continue;
                    }
                    if (occurrence > windowEnd || (until is { } byDayUntil && occurrence > byDayUntil))
                    {
                        yield break;
                    }

                    yield return occurrence;
                    if (++emitted >= (count ?? hardCap))
                    {
                        yield break;
                    }
                }
            }
            yield break;
        }

        DateTimeOffset next = first;
        while (emitted < hardCap)
        {
            if (next > windowEnd || (until is { } untilLimit && next > untilLimit))
            {
                yield break;
            }

            yield return next;
            if (++emitted >= (count ?? hardCap))
            {
                yield break;
            }

            next = frequency switch
            {
                "DAILY" => next.AddDays(interval),
                "WEEKLY" => next.AddDays(7 * interval),
                "MONTHLY" => next.AddMonths(interval),
                _ => next.AddYears(interval)
            };
        }
    }

    private static int WeekdayIndex(DayOfWeek day) => ((int)day + 6) % 7; // Monday first

    private static string Unescape(string value) =>
        value.Replace("\\n", " ").Replace("\\N", " ")
             .Replace("\\,", ",").Replace("\\;", ";").Replace("\\\\", "\\");
}

/// <summary>The user's calendar feed, refreshed every fifteen minutes.</summary>
public sealed class IcsCalendarSource : IDisposable
{
    private readonly HttpClient _client;
    private readonly bool _ownsClient;
    private CalendarSnapshot? _cached;
    private string _cachedUrl = string.Empty;
    private DateTimeOffset _lastFetch;

    public IcsCalendarSource(HttpClient? client = null)
    {
        _ownsClient = client is null;
        _client = client ?? new HttpClient { Timeout = TimeSpan.FromSeconds(12) };
        _client.DefaultRequestHeaders.UserAgent.ParseAdd("KeyboardScreenStudio/1.0");
    }

    public TimeSpan CacheDuration { get; init; } = TimeSpan.FromMinutes(15);

    public async Task<CalendarSnapshot> ReadAsync(CalendarSettings settings, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);
        if (!settings.IsConfigured)
        {
            return CalendarSnapshot.Unavailable();
        }

        string url = settings.IcsUrl.Trim();
        // webcal:// is http(s) in disguise; the checkbox form is common in
        // calendar apps' "public address" dialogs.
        if (url.StartsWith("webcal://", StringComparison.OrdinalIgnoreCase))
        {
            url = "https://" + url["webcal://".Length..];
        }

        DateTimeOffset now = DateTimeOffset.Now;
        if (_cached is not null && _cachedUrl == url && now - _lastFetch < CacheDuration)
        {
            return _cached;
        }

        try
        {
            string ics = await _client.GetStringAsync(url, cancellationToken);
            List<CalendarEventInfo> events = IcsParser.Parse(ics, now.AddHours(-4), now.AddDays(60));
            _cached = new CalendarSnapshot(true, events.Take(24).ToArray(), now);
            _cachedUrl = url;
            _lastFetch = now;
            return _cached;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or UriFormatException)
        {
            return _cached is not null && _cachedUrl == url
                ? _cached with { IsStale = true, ErrorMessage = ex.Message }
                : CalendarSnapshot.Unavailable(ex.Message) with { UpdatedAt = now };
        }
    }

    public void Dispose()
    {
        if (_ownsClient)
        {
            _client.Dispose();
        }
    }
}

/// <summary>Today's and the upcoming events from the ICS feed.</summary>
public sealed class CalendarTheme : IScreenTheme
{
    private static readonly Color Background = Color.FromRgb(5, 9, 14);
    private static readonly Color CardColor = Color.FromRgb(15, 20, 27);
    private static readonly Color CardStroke = Color.FromRgb(29, 36, 45);
    private static readonly Color Secondary = Color.FromRgb(150, 160, 172);
    private static readonly Color Muted = Color.FromRgb(102, 112, 124);

    public string Id => "calendar";
    public string DisplayName => Loc.T("ThemeCalendarName");
    public string Description => Loc.T("ThemeCalendarDescription");
    public string Details => Loc.T("ThemeCalendarDetails");

    public void Draw(ScreenCanvas canvas, SystemSnapshot snapshot)
    {
        Rect safe = canvas.SafeBounds;
        canvas.Fill(Background);
        ThemeHeader.Draw(canvas, snapshot, Loc.T("ScreenTitleCalendar"));

        CalendarSnapshot? calendar = snapshot.Calendar;
        if (calendar is not { Available: true })
        {
            canvas.AlignedText(Loc.T("ScreenCalendarEmpty"), 13, Colors.White,
                new Rect(safe.Left, safe.Top + 150, safe.Width, 24), FontWeights.SemiBold, TextAlignment.Center);
            canvas.AlignedText(calendar?.ErrorMessage ?? Loc.T("ScreenCalendarHint"), 10, Secondary,
                new Rect(safe.Left, safe.Top + 180, safe.Width, 30), FontWeights.Normal, TextAlignment.Center);
            return;
        }

        DateTimeOffset now = snapshot.Timestamp;
        CalendarEventInfo[] upcoming = calendar.Events
            .Where(item => item.Start >= now.AddHours(-4))
            .Take(6)
            .ToArray();
        if (upcoming.Length == 0)
        {
            canvas.AlignedText(Loc.T("ScreenCalendarNoEvents"), 13, Colors.White,
                new Rect(safe.Left, safe.Top + 150, safe.Width, 24), FontWeights.SemiBold, TextAlignment.Center);
        }

        double top = safe.Top + 40;
        DateOnly? lastDay = null;
        foreach (CalendarEventInfo item in upcoming)
        {
            DateOnly day = DateOnly.FromDateTime(item.Start.LocalDateTime.Date);
            if (lastDay != day)
            {
                canvas.AlignedText(DayLabel(day, now), 10, canvas.AccentColor,
                    new Rect(safe.Left + 2, top, safe.Width - 4, 14), FontWeights.SemiBold, TextAlignment.Left);
                top += 18;
                lastDay = day;
            }

            var row = new Rect(safe.Left, top, safe.Width, 40);
            canvas.RoundedRect(row, 9, CardColor, CardStroke);
            canvas.AlignedText(
                item.IsAllDay ? Loc.T("ScreenCalendarAllDay") : DisplayUnits.Time(item.Start),
                10, Secondary,
                new Rect(row.Left + 10, row.Top + 5, 70, 14), FontWeights.Medium, TextAlignment.Left);
            canvas.AlignedText(item.Title, 11, Colors.White,
                new Rect(row.Left + 10, row.Top + 20, row.Width - 20, 15), FontWeights.SemiBold, TextAlignment.Left);
            top += 46;
        }

        string footer = calendar.IsStale
            ? Loc.T("ScreenStocksStale")
            : Loc.T("ScreenWeatherUpdated", DisplayUnits.Time(calendar.UpdatedAt));
        canvas.AlignedText(footer, 9, Muted,
            new Rect(safe.Left, safe.Bottom - 14, safe.Width, 13), FontWeights.Normal, TextAlignment.Center);
    }

    private static string DayLabel(DateOnly day, DateTimeOffset now)
    {
        DateOnly today = DateOnly.FromDateTime(now.LocalDateTime.Date);
        if (day == today)
        {
            return Loc.T("ScreenToday");
        }
        if (day == today.AddDays(1))
        {
            return Loc.T("ScreenCalendarTomorrow");
        }

        DateTime date = day.ToDateTime(TimeOnly.MinValue);
        return Loc.ShortDayName(date) + ", " + Loc.ShortDate(new DateTimeOffset(date, DateTimeOffset.Now.Offset));
    }
}
