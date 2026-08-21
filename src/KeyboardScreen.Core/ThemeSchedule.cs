namespace KeyboardScreen.Core;

public sealed class ThemeScheduleSettings
{
    public bool Enabled { get; set; }

    /// <summary>24-hour "HH:mm"; the night window may cross midnight.</summary>
    public string NightStart { get; set; } = "22:00";

    public string NightEnd { get; set; } = "07:00";

    /// <summary>Theme shown during the night window; empty keeps the selected theme.</summary>
    public string NightThemeId { get; set; } = string.Empty;

    public bool DimAtNight { get; set; } = true;

    public int NightBrightnessPercent { get; set; } = 60;
}

/// <summary>
/// Time-of-day automation: an optional night theme plus a render brightness
/// multiplier. Media automation outranks the schedule, so callers apply this
/// only when no media override is active; dimming applies regardless.
/// </summary>
public static class ThemeSchedule
{
    public static bool TryParseTime(string? value, out TimeSpan time)
    {
        time = default;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        string[] parts = value.Trim().Split(':');
        if (parts.Length != 2 ||
            !int.TryParse(parts[0], out int hours) ||
            !int.TryParse(parts[1], out int minutes) ||
            hours is < 0 or > 23 ||
            minutes is < 0 or > 59)
        {
            return false;
        }

        time = new TimeSpan(hours, minutes, 0);
        return true;
    }

    public static bool IsNight(ThemeScheduleSettings? settings, DateTimeOffset now)
    {
        if (settings is not { Enabled: true } ||
            !TryParseTime(settings.NightStart, out TimeSpan start) ||
            !TryParseTime(settings.NightEnd, out TimeSpan end) ||
            start == end)
        {
            return false;
        }

        TimeSpan timeOfDay = now.TimeOfDay;
        return start < end
            ? timeOfDay >= start && timeOfDay < end
            : timeOfDay >= start || timeOfDay < end;
    }

    public static string ResolveThemeId(ThemeScheduleSettings? settings, string requestedThemeId, DateTimeOffset now) =>
        IsNight(settings, now) && !string.IsNullOrWhiteSpace(settings!.NightThemeId)
            ? settings.NightThemeId
            : requestedThemeId;

    /// <summary>100 outside the night window; the configured value inside it.</summary>
    public static int BrightnessPercent(ThemeScheduleSettings? settings, DateTimeOffset now) =>
        IsNight(settings, now) && settings!.DimAtNight
            ? Math.Clamp(settings.NightBrightnessPercent, 20, 100)
            : 100;
}
