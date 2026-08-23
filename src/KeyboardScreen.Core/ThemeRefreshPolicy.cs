namespace KeyboardScreen.Core;

/// <summary>
/// Per-theme refresh cadence. Live screens (clocks with seconds, system
/// counters, music progress) follow the global refresh setting; data screens
/// default to a slower built-in cadence, and the user can override any theme
/// from its settings page. Slow ticks are re-aligned to the minute boundary so
/// the header clock still flips on time, and a running carousel caps the wait
/// so rotations are never missed.
/// </summary>
public static class ThemeRefreshPolicy
{
    public const int MaxSeconds = 600;

    /// <summary>Built-in cadence; 0 means "follow the global refresh setting".</summary>
    public static int DefaultSeconds(string? themeId) => themeId switch
    {
        "weather-five-day" or "stocks" or "currency" or "ai-quota" => 60,
        // Claude limits shift slowly, and every poll is a chance for Cloudflare
        // to challenge the non-browser client - fewer requests, fewer blocks.
        "claude-usage" => 120,
        "crypto" or "clock-weather-dot" or "clock-dot-progress" or "alerts" or "ping" => 30,
        "disks" or "countdown" => 60,
        "github" or "calendar" => 300,
        "world-clock" => 5,
        _ => 0
    };

    public static int EffectiveSeconds(AppSettings settings, string? themeId, int globalSeconds)
    {
        ArgumentNullException.ThrowIfNull(settings);
        if (themeId is not null &&
            settings.ThemeRefreshOverrides is { } overrides &&
            overrides.TryGetValue(themeId, out int overrideSeconds) &&
            overrideSeconds > 0)
        {
            return Math.Clamp(overrideSeconds, 1, MaxSeconds);
        }

        int builtIn = DefaultSeconds(themeId);
        return builtIn > 0 ? builtIn : Math.Clamp(globalSeconds, 1, MaxSeconds);
    }

    /// <summary>
    /// How long the refresh loop may sleep. <paramref name="pollCapSeconds"/>
    /// keeps polling fast while something outside the theme needs watching
    /// (media automation waiting for playback to start or stop).
    /// </summary>
    public static TimeSpan NextDelay(DateTimeOffset now, int themeSeconds, CarouselSettings? carousel = null, int? pollCapSeconds = null)
    {
        double delay = Math.Clamp(themeSeconds, 1, MaxSeconds);

        // A slow theme still shows the header clock: land right after the flip.
        if (delay > 5)
        {
            double untilMinute = 60.4 - (now.Second + now.Millisecond / 1000.0);
            delay = Math.Min(delay, Math.Max(1, untilMinute));
        }

        if (carousel is { Enabled: true } &&
            (carousel.ThemeIds?.Count(id => !string.IsNullOrWhiteSpace(id)) ?? 0) >= 2)
        {
            int interval = Math.Clamp(carousel.IntervalSeconds, 10, MaxSeconds);
            double untilBoundary = interval - now.ToUnixTimeSeconds() % interval + 0.3;
            delay = Math.Min(delay, Math.Max(1, untilBoundary));
        }

        if (pollCapSeconds is { } cap)
        {
            delay = Math.Min(delay, Math.Max(1, cap));
        }

        return TimeSpan.FromSeconds(delay);
    }
}
