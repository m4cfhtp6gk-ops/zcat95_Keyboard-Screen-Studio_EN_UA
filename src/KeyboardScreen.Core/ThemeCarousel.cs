namespace KeyboardScreen.Core;

public sealed class CarouselSettings
{
    public bool Enabled { get; set; }

    /// <summary>How long each theme stays on screen, 10-600 seconds.</summary>
    public int IntervalSeconds { get; set; } = 30;

    public List<string> ThemeIds { get; set; } = [];
}

/// <summary>
/// Rotates the screen through a chosen set of themes. The pick derives from
/// the wall clock, so it is steady across refresh ticks and restarts. The
/// carousel is the lowest layer: media automation and the night schedule are
/// applied after it and win when active.
/// </summary>
public static class ThemeCarousel
{
    public static string ResolveThemeId(CarouselSettings? settings, string requestedThemeId, DateTimeOffset now)
    {
        if (settings is not { Enabled: true })
        {
            return requestedThemeId;
        }

        var ids = (settings.ThemeIds ?? [])
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (ids.Length < 2)
        {
            return requestedThemeId;
        }

        int interval = Math.Clamp(settings.IntervalSeconds, 10, 600);
        long slot = now.ToUnixTimeSeconds() / interval;
        return ids[(int)(slot % ids.Length)];
    }
}
