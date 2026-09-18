using System.Globalization;
using System.IO;

namespace KeyboardScreen.Core;

public sealed class ComposerWidgetSettings
{
    public string Kind { get; set; } = string.Empty;

    /// <summary>Free text for the "text" widget; other kinds ignore it.</summary>
    public string Text { get; set; } = string.Empty;

    /// <summary>
    /// Draw this widget's numbers in the dot-matrix face.
    ///
    /// Numbers only, never the labels: Doto carries 320 glyphs - Latin, digits
    /// and punctuation - and no Cyrillic at all, so a label like "Пам'ять" would
    /// silently fall back to a system face and look like neither. Kinds whose
    /// value is text rather than a number do not offer this at all.
    /// </summary>
    public bool DotFont { get; set; }

    /// <summary>This widget's own accent as #RRGGBB, or empty to follow the theme's.</summary>
    public string Accent { get; set; } = string.Empty;
}

public sealed class ComposerSettings
{
    /// <summary>Top-to-bottom order on the screen. The starter set shows the idea.</summary>
    public List<ComposerWidgetSettings> Widgets { get; set; } =
    [
        new() { Kind = "clock" },
        new() { Kind = "date" },
        new() { Kind = "cpu" },
        new() { Kind = "ram" },
        new() { Kind = "net" }
    ];
}

/// <param name="Kind">Stable id stored in settings.</param>
/// <param name="Height">Row height on the 142-wide screen.</param>
/// <param name="NameKey">Localization key of the name shown in the editor.</param>
/// <param name="Source">Data source the widget needs beyond the always-on system counters, or null.</param>
/// <param name="HasNumber">
/// Whether this kind draws a number the dot-matrix face could render. False for
/// the ones whose value is prose - the editor hides the font switch there rather
/// than offering a control that would quietly do the wrong thing.
/// </param>
public sealed record ComposerWidgetInfo(string Kind, double Height, string NameKey, string? Source, bool HasNumber = false);

public sealed record ComposerPlacement(ComposerWidgetSettings Widget, ComposerWidgetInfo Info, Rect Bounds);

/// <summary>
/// The widget catalog and the layout math - everything the editor and the
/// smoke tests need without touching Skia. Widgets stack top to bottom; the
/// first one that no longer fits is dropped along with everything after it,
/// and the editor shows the same budget so nothing is clipped by surprise.
/// </summary>
public static class ComposerWidgets
{
    public const double Gap = 6;

    public static readonly IReadOnlyList<ComposerWidgetInfo> Catalog =
    [
        new("clock", 52, "ComposerWidgetClock", null, true),
        new("date", 44, "ComposerWidgetDate", null),
        new("cpu", 42, "ComposerWidgetCpu", null, true),
        new("ram", 42, "ComposerWidgetRam", null, true),
        new("gpu", 42, "ComposerWidgetGpu", null, true),
        new("net", 46, "ComposerWidgetNet", null, true),
        new("hardware", 64, "ComposerWidgetHardware", "hardware", true),
        new("weather", 60, "ComposerWidgetWeather", "weather"),
        new("currency", 50, "ComposerWidgetCurrency", "currency", true),
        new("crypto", 46, "ComposerWidgetCrypto", "crypto", true),
        new("ping", 50, "ComposerWidgetPing", "ping", true),
        new("alerts", 38, "ComposerWidgetAlerts", "alerts"),
        new("claude", 58, "ComposerWidgetClaude", "claude-usage", true),
        new("pomodoro", 52, "ComposerWidgetPomodoro", null, true),
        new("music", 54, "ComposerWidgetMusic", null),
        new("calendar-next", 48, "ComposerWidgetCalendarNext", "calendar"),
        new("countdown-next", 46, "ComposerWidgetCountdownNext", null, true),
        new("world-clock", 48, "ComposerWidgetWorldClock", null, true),
        new("github", 44, "ComposerWidgetGitHub", "github", true),
        new("text", 40, "ComposerWidgetText", null),
        new("spacer", 14, "ComposerWidgetSpacer", null)
    ];

    public static ComposerWidgetInfo? Find(string? kind) =>
        Catalog.FirstOrDefault(info =>
            string.Equals(info.Kind, kind, StringComparison.OrdinalIgnoreCase));

    /// <summary>Sources the render loop must fetch for this layout.</summary>
    public static IReadOnlyCollection<string> RequiredSources(ComposerSettings? settings)
    {
        var sources = new HashSet<string>(StringComparer.Ordinal);
        foreach (ComposerWidgetSettings widget in settings?.Widgets ?? [])
        {
            if (Find(widget.Kind)?.Source is { } source)
            {
                sources.Add(source);
            }
        }
        return sources;
    }

    /// <summary>Widgets placed into <paramref name="bounds"/>; the ones that no longer fit are absent.</summary>
    public static IReadOnlyList<ComposerPlacement> Arrange(
        IReadOnlyList<ComposerWidgetSettings>? widgets, Rect bounds)
    {
        var placements = new List<ComposerPlacement>();
        double top = bounds.Top;
        foreach (ComposerWidgetSettings widget in widgets ?? [])
        {
            if (Find(widget.Kind) is not { } info)
            {
                continue;
            }

            if (top + info.Height > bounds.Bottom + 0.5)
            {
                break;
            }

            placements.Add(new ComposerPlacement(
                widget, info, new Rect(bounds.Left, top, bounds.Width, info.Height)));
            top += info.Height + Gap;
        }
        return placements;
    }

    /// <summary>Total stacked height, for the editor's budget line.</summary>
    public static double UsedHeight(IReadOnlyList<ComposerWidgetSettings>? widgets)
    {
        double total = 0;
        int placed = 0;
        foreach (ComposerWidgetSettings widget in widgets ?? [])
        {
            if (Find(widget.Kind) is not { } info)
            {
                continue;
            }

            total += (placed > 0 ? Gap : 0) + info.Height;
            placed++;
        }
        return total;
    }
}

/// <summary>
/// The user-assembled screen: an ordered stack of small widgets, every one a
/// compact re-cut of an existing theme so the data, formatting and languages
/// are already proven. The widget list is swapped in whole by the settings UI;
/// the render loop only reads it.
/// </summary>
public sealed class ComposerTheme : IScreenTheme
{
    private static readonly FontFamily Doto =
        new("Doto", Path.Combine(AppContext.BaseDirectory, "Assets", "Fonts", "Doto.ttf"));

    private readonly PomodoroTimer _pomodoro;

    /// <summary>
    /// Set once per widget in <see cref="DrawWidget"/> and read by the drawing
    /// helpers, so every widget's own font and accent apply without threading
    /// two extra arguments through twenty call sites.
    /// </summary>
    private FontFamily? _valueFont;

    private Color _accent;

    public ComposerTheme(PomodoroTimer pomodoro)
    {
        _pomodoro = pomodoro;
    }

    public string Id => "composer";

    public string DisplayName => Loc.T("ThemeComposerName");

    public string Description => Loc.T("ThemeComposerDescription");

    public string Details => Loc.T("ThemeComposerDetails");

    public IReadOnlyList<ComposerWidgetSettings> Widgets { get; set; } = new ComposerSettings().Widgets;

    private static readonly Color CardFill = Color.FromRgb(15, 20, 26);
    private static readonly Color CardBorder = Color.FromRgb(29, 36, 45);
    private static readonly Color LabelGray = Color.FromRgb(148, 160, 173);
    private static readonly Color DimGray = Color.FromRgb(116, 128, 141);
    private static readonly Color TrackGray = Color.FromRgb(35, 42, 51);
    private static readonly Color GainGreen = Color.FromRgb(63, 185, 80);
    private static readonly Color LossRed = Color.FromRgb(229, 83, 75);
    private static readonly Color AlertRed = Color.FromRgb(197, 48, 40);

    public void Draw(ScreenCanvas c, SystemSnapshot s)
    {
        c.Fill(Color.FromRgb(7, 10, 14));
        Rect safe = c.SafeBounds;
        IReadOnlyList<ComposerPlacement> placements = ComposerWidgets.Arrange(Widgets, safe);
        if (placements.Count == 0)
        {
            c.AlignedText(Loc.T("ComposerEmptyLine1"), 12, LabelGray,
                new Rect(safe.Left, safe.Top + safe.Height / 2 - 34, safe.Width, 22),
                FontWeights.SemiBold, TextAlignment.Center);
            c.AlignedText(Loc.T("ComposerEmptyLine2"), 10, DimGray,
                new Rect(safe.Left, safe.Top + safe.Height / 2 - 8, safe.Width, 18),
                FontWeights.Medium, TextAlignment.Center);
            c.AlignedText(Loc.T("ComposerEmptyLine3"), 10, DimGray,
                new Rect(safe.Left, safe.Top + safe.Height / 2 + 12, safe.Width, 18),
                FontWeights.Medium, TextAlignment.Center);
            return;
        }

        foreach (ComposerPlacement placement in placements)
        {
            DrawWidget(c, s, placement);
        }
    }

    private void DrawWidget(ScreenCanvas c, SystemSnapshot s, ComposerPlacement p)
    {
        Rect r = p.Bounds;
        // Numbers only: Doto has no Cyrillic, so a label drawn in it would fall
        // back to a system face. Kinds without a number never offer the switch.
        _valueFont = p.Widget.DotFont && p.Info.HasNumber ? Doto : null;
        _accent = ParseAccent(p.Widget.Accent) ?? c.AccentColor;
        switch (p.Info.Kind)
        {
            case "clock":
                c.AlignedText(DisplayUnits.Time(s.Timestamp), 34, Colors.White, r,
                    FontWeights.SemiBold, TextAlignment.Center, _valueFont);
                break;
            case "date":
                c.AlignedText(Loc.DayName(s.Timestamp), 12, _accent,
                    new Rect(r.Left, r.Top, r.Width, 20), FontWeights.SemiBold, TextAlignment.Center);
                c.AlignedText(Loc.LongDate(s.Timestamp), 13, Colors.White,
                    new Rect(r.Left, r.Top + 22, r.Width, 20), FontWeights.Medium, TextAlignment.Center);
                break;
            case "cpu":
                PercentBar(c, r, Loc.T("ScreenLabelCpu"), s.CpuPercent);
                break;
            case "ram":
                PercentBar(c, r, Loc.T("ScreenLabelMemory"), s.MemoryPercent);
                break;
            case "gpu":
                PercentBar(c, r, Loc.T("ScreenLabelGpu"), s.GpuPercent);
                break;
            case "net":
                Card(c, r);
                MetricRow(c, new Rect(r.Left, r.Top + 6, r.Width, 15),
                    "↓ " + Loc.T("ScreenLabelDownload"), $"{s.DownloadMbps:0.0}M", Colors.White);
                MetricRow(c, new Rect(r.Left, r.Top + 24, r.Width, 15),
                    "↑ " + Loc.T("ScreenLabelUpload"), $"{s.UploadMbps:0.0}M", Colors.White);
                break;
            case "hardware":
                DrawHardware(c, s, r);
                break;
            case "weather":
                DrawWeather(c, s, r);
                break;
            case "currency":
                DrawCurrency(c, s, r);
                break;
            case "crypto":
                DrawCrypto(c, s, r);
                break;
            case "ping":
                DrawPing(c, s, r);
                break;
            case "alerts":
                DrawAlerts(c, s, r);
                break;
            case "claude":
                DrawClaude(c, s, r);
                break;
            case "pomodoro":
                DrawPomodoro(c, s, r);
                break;
            case "music":
                DrawMusic(c, s, r);
                break;
            case "calendar-next":
                DrawCalendarNext(c, s, r);
                break;
            case "countdown-next":
                DrawCountdownNext(c, s, r);
                break;
            case "world-clock":
                DrawWorldClock(c, s, r);
                break;
            case "github":
                DrawGitHub(c, s, r);
                break;
            case "text":
                c.AlignedText(p.Widget.Text, 13, Colors.White, r,
                    FontWeights.SemiBold, TextAlignment.Center);
                break;
        }
    }

    private static void Card(ScreenCanvas c, Rect r) => c.RoundedRect(r, 9, CardFill, CardBorder);

    private void MetricRow(ScreenCanvas c, Rect row, string label, string value, Color valueColor)
    {
        c.AlignedText(label, 9, LabelGray,
            new Rect(row.Left + 9, row.Top, row.Width - 18, row.Height), FontWeights.SemiBold);
        c.AlignedText(value, 11, valueColor,
            new Rect(row.Left + 9, row.Top, row.Width - 18, row.Height),
            FontWeights.SemiBold, TextAlignment.Right, _valueFont);
    }

    /// <summary>A widget's own accent, or null to inherit the theme's.</summary>
    private static Color? ParseAccent(string? value)
    {
        string text = (value ?? string.Empty).Trim();
        if (text.Length != 7 || text[0] != '#')
        {
            return null;
        }

        return int.TryParse(text[1..], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int rgb)
            ? Color.FromRgb((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb)
            : null;
    }

    private void PercentBar(ScreenCanvas c, Rect r, string label, double? percent)
    {
        Card(c, r);
        MetricRow(c, new Rect(r.Left, r.Top + 6, r.Width, 15),
            label, percent is { } value ? $"{value:0}%" : "—", Colors.White);
        c.ProgressBar(new Rect(r.Left + 9, r.Bottom - 12, r.Width - 18, 5),
            percent ?? 0, TrackGray, _accent);
    }

    private void DrawHardware(ScreenCanvas c, SystemSnapshot s, Rect r)
    {
        Card(c, r);
        HardwareComponentSnapshot? cpu = s.Hardware?.Cpu;
        MetricRow(c, new Rect(r.Left, r.Top + 7, r.Width, 14),
            Loc.T("ScreenHwTemp"),
            cpu?.TemperatureC is { } temp and > 0 ? DisplayUnits.TemperatureShort(temp) : "—",
            Colors.White);
        MetricRow(c, new Rect(r.Left, r.Top + 25, r.Width, 14),
            Loc.T("ScreenHwClock"),
            cpu?.ClockGhz is { } clock and > 0
                ? clock.ToString("0.00", CultureInfo.CurrentCulture)
                : "—",
            Colors.White);
        MetricRow(c, new Rect(r.Left, r.Top + 43, r.Width, 14),
            Loc.T("ScreenHwFan"),
            cpu?.FanRpm is { } fan and > 0 ? $"{fan:0}" : "—",
            Colors.White);
    }

    private void DrawWeather(ScreenCanvas c, SystemSnapshot s, Rect r)
    {
        Card(c, r);
        if (s.Weather is not { Available: true } weather)
        {
            c.AlignedText(Loc.T("ScreenWeatherWaiting"), 10, DimGray, r,
                FontWeights.Medium, TextAlignment.Center);
            return;
        }

        c.AlignedText(DisplayUnits.TemperatureShort(weather.TemperatureC), 21, Colors.White,
            new Rect(r.Left + 9, r.Top + 6, r.Width - 18, 24), FontWeights.SemiBold);
        c.AlignedText($"{Loc.T("ScreenLabelHumidity")} {weather.RelativeHumidityPercent}%", 9, LabelGray,
            new Rect(r.Left + 9, r.Top + 6, r.Width - 18, 24),
            FontWeights.Medium, TextAlignment.Right);
        c.AlignedText(weather.LocationName, 9, DimGray,
            new Rect(r.Left + 9, r.Bottom - 22, r.Width - 18, 15), FontWeights.Medium);
    }

    private void DrawCurrency(ScreenCanvas c, SystemSnapshot s, Rect r)
    {
        Card(c, r);
        IReadOnlyList<CurrencyRate> rates = s.Currency is { Available: true } currency
            ? currency.Rates
            : [];
        if (rates.Count == 0)
        {
            c.AlignedText("—", 11, DimGray, r, FontWeights.Medium, TextAlignment.Center);
            return;
        }

        for (int index = 0; index < Math.Min(2, rates.Count); index++)
        {
            CurrencyRate rate = rates[index];
            MetricRow(c, new Rect(r.Left, r.Top + 8 + index * 18, r.Width, 15),
                $"{s.Currency!.BaseCurrency}→{rate.Code}",
                rate.Rate.ToString(rate.Rate >= 100 ? "0.#" : "0.00##", CultureInfo.CurrentCulture),
                Colors.White);
        }
    }

    private void DrawCrypto(ScreenCanvas c, SystemSnapshot s, Rect r)
    {
        Card(c, r);
        if (s.Crypto is not { Coins.Count: > 0 } crypto)
        {
            c.AlignedText("—", 11, DimGray, r, FontWeights.Medium, TextAlignment.Center);
            return;
        }

        CryptoCoinSnapshot coin = crypto.Coins[0];
        bool gain = coin.ChangePercent24h >= 0;
        Color changeColor = gain == !crypto.RedForGain ? GainGreen : LossRed;
        MetricRow(c, new Rect(r.Left, r.Top + 6, r.Width, 15),
            coin.DisplayName,
            coin.Price.ToString(coin.Price >= 100 ? "#,0" : "0.####", CultureInfo.CurrentCulture),
            Colors.White);
        c.AlignedText($"{(gain ? "+" : "")}{coin.ChangePercent24h:0.00}%", 10, changeColor,
            new Rect(r.Left + 9, r.Top + 24, r.Width - 18, 15),
            FontWeights.SemiBold, TextAlignment.Right);
    }

    private void DrawPing(ScreenCanvas c, SystemSnapshot s, Rect r)
    {
        Card(c, r);
        IReadOnlyList<PingHostSnapshot> hosts = s.Ping is { Available: true } ping ? ping.Hosts : [];
        if (hosts.Count == 0)
        {
            c.AlignedText("—", 11, DimGray, r, FontWeights.Medium, TextAlignment.Center);
            return;
        }

        for (int index = 0; index < Math.Min(2, hosts.Count); index++)
        {
            PingHostSnapshot host = hosts[index];
            Rect row = new(r.Left, r.Top + 8 + index * 18, r.Width, 15);
            c.AlignedText(host.Host, 9, LabelGray,
                new Rect(row.Left + 9, row.Top, row.Width - 52, row.Height), FontWeights.Medium);
            c.AlignedText(host.LatencyMs is { } latency ? $"{latency:0} ms" : "—", 10,
                host.LatencyMs is null ? LossRed : Colors.White,
                new Rect(row.Left + 9, row.Top, row.Width - 18, row.Height),
                FontWeights.SemiBold, TextAlignment.Right);
        }
    }

    private void DrawAlerts(ScreenCanvas c, SystemSnapshot s, Rect r)
    {
        if (s.AirAlerts is not { Available: true } alerts)
        {
            Card(c, r);
            c.AlignedText(Loc.T("ScreenAlertsNotConfigured"), 10, DimGray, r,
                FontWeights.Medium, TextAlignment.Center);
            return;
        }

        bool active = alerts.AlertActiveAtLocation;
        c.RoundedRect(r, 9, active ? AlertRed : CardFill,
            active ? Color.FromRgb(236, 100, 92) : CardBorder);
        string location = alerts.Location?.Trim() ?? string.Empty;
        string status = Loc.T(active ? "ScreenAlertsActive" : "ScreenAlertsClear");
        Color statusColor = active ? Colors.White : GainGreen;
        if (location.Length == 0)
        {
            c.AlignedText(status, 11, statusColor, ShrinkX(r, 9),
                FontWeights.SemiBold, TextAlignment.Center);
            return;
        }

        c.AlignedText(status, 11, statusColor,
            new Rect(r.Left + 9, r.Top + 4, r.Width - 18, 15), FontWeights.SemiBold, TextAlignment.Center);
        c.AlignedText(location, 9, active ? Color.FromRgb(255, 214, 210) : LabelGray,
            new Rect(r.Left + 9, r.Top + 20, r.Width - 18, 13), FontWeights.Medium, TextAlignment.Center);
    }

    private void DrawClaude(ScreenCanvas c, SystemSnapshot s, Rect r)
    {
        Card(c, r);
        if (s.ClaudeUsage is not { Available: true } usage)
        {
            // There is only one way to be unavailable now: Claude Code has not
            // written any transcripts on this machine. Nothing to retry, nothing
            // to re-authenticate - so say the one thing that would fix it.
            c.AlignedText(Loc.T("ScreenClaudeNotConnected"),
                10, DimGray,
                new Rect(r.Left + 9, r.Top + 10, r.Width - 18, 16),
                FontWeights.Medium, TextAlignment.Center);
            c.AlignedText(Loc.T("ScreenClaudeConnectHint"),
                8.5, DimGray,
                new Rect(r.Left + 9, r.Top + 30, r.Width - 18, 14),
                FontWeights.Medium, TextAlignment.Center);
            return;
        }

        UsageBar(c, new Rect(r.Left, r.Top + 6, r.Width, 22),
            Loc.T("ScreenClaudeSession"), usage.Session?.UtilizationPercent);
        UsageBar(c, new Rect(r.Left, r.Top + 31, r.Width, 22),
            Loc.T("ScreenClaudeWeek"), usage.Week?.UtilizationPercent);
    }

    private void UsageBar(ScreenCanvas c, Rect row, string label, double? percent)
    {
        MetricRow(c, new Rect(row.Left, row.Top, row.Width, 13),
            label, percent is { } value ? $"{value:0}%" : "—", Colors.White);
        c.ProgressBar(new Rect(row.Left + 9, row.Bottom - 5, row.Width - 18, 4),
            percent ?? 0, TrackGray,
            percent >= 90 ? LossRed : _accent);
    }

    private void DrawPomodoro(ScreenCanvas c, SystemSnapshot s, Rect r)
    {
        Card(c, r);
        PomodoroSnapshot pomodoro = _pomodoro.Read(s.Timestamp);
        if (pomodoro.Phase == PomodoroPhase.Idle)
        {
            c.AlignedText(Loc.T("ScreenPomodoroIdle") + " · " + Loc.T("ScreenPomodoroStartHint"),
                9, DimGray, ShrinkX(r, 9), FontWeights.Medium, TextAlignment.Center);
            return;
        }

        string phase = Loc.T(pomodoro.Phase == PomodoroPhase.Focus ? "ScreenPomodoroFocus" : "ScreenPomodoroBreak");
        MetricRow(c, new Rect(r.Left, r.Top + 7, r.Width, 15),
            phase, $"{(int)pomodoro.Remaining.TotalMinutes}:{pomodoro.Remaining.Seconds:00}",
            _accent);
        c.ProgressBar(new Rect(r.Left + 9, r.Bottom - 13, r.Width - 18, 5),
            pomodoro.ElapsedFraction * 100, TrackGray, _accent);
    }

    private void DrawMusic(ScreenCanvas c, SystemSnapshot s, Rect r)
    {
        Card(c, r);
        MusicSnapshot music = s.Music ?? MusicSnapshot.Unavailable;
        if (!music.Available)
        {
            c.AlignedText(Loc.T("ScreenMusicStopped"), 10, DimGray, r,
                FontWeights.Medium, TextAlignment.Center);
            return;
        }

        c.AlignedText(music.Title, 11, Colors.White,
            new Rect(r.Left + 9, r.Top + 6, r.Width - 18, 16), FontWeights.SemiBold);
        c.AlignedText(music.Artist, 9, LabelGray,
            new Rect(r.Left + 9, r.Top + 23, r.Width - 18, 13), FontWeights.Medium);
        double percent = music.Duration.TotalSeconds <= 0
            ? (music.IsPlaying ? 100 : 0)
            : music.Position.TotalSeconds / music.Duration.TotalSeconds * 100;
        c.ProgressBar(new Rect(r.Left + 9, r.Bottom - 11, r.Width - 18, 4),
            percent, TrackGray, _accent);
    }

    private void DrawCalendarNext(ScreenCanvas c, SystemSnapshot s, Rect r)
    {
        Card(c, r);
        CalendarEventInfo? next = s.Calendar is { Available: true } calendar
            ? calendar.Events.FirstOrDefault()
            : null;
        if (next is null)
        {
            c.AlignedText(Loc.T("ScreenCalendarEmpty"), 10, DimGray, r,
                FontWeights.Medium, TextAlignment.Center);
            return;
        }

        bool today = next.Start.LocalDateTime.Date == s.Timestamp.LocalDateTime.Date;
        string when = next.IsAllDay
            ? Loc.ShortDate(next.Start)
            : today
                ? DisplayUnits.Time(next.Start)
                : Loc.ShortDate(next.Start) + " " + DisplayUnits.Time(next.Start);
        c.AlignedText(when, 10, _accent,
            new Rect(r.Left + 9, r.Top + 6, r.Width - 18, 15), FontWeights.SemiBold);
        c.AlignedText(next.Title, 10, Colors.White,
            new Rect(r.Left + 9, r.Top + 24, r.Width - 18, 16), FontWeights.Medium);
    }

    private void DrawCountdownNext(ScreenCanvas c, SystemSnapshot s, Rect r)
    {
        Card(c, r);
        CountdownEntry? next = s.Countdown?.Events
            .OrderBy(entry => entry.Target)
            .FirstOrDefault(entry => entry.Target > s.Timestamp)
            ?? s.Countdown?.Events.OrderBy(entry => entry.Target).LastOrDefault();
        if (next is null)
        {
            c.AlignedText(Loc.T("ScreenCountdownEmpty"), 10, DimGray, r,
                FontWeights.Medium, TextAlignment.Center);
            return;
        }

        double daysLeft = (next.Target - s.Timestamp).TotalDays;
        string value = daysLeft <= 0
            ? Loc.T("ScreenCountdownArrived")
            : Loc.T("ScreenCountdownDaysShort", Math.Ceiling(daysLeft).ToString("0", CultureInfo.InvariantCulture));
        c.AlignedText(next.Title, 9, LabelGray,
            new Rect(r.Left + 9, r.Top + 6, r.Width - 18, 14), FontWeights.Medium);
        c.AlignedText(value, 13, Colors.White,
            new Rect(r.Left + 9, r.Top + 22, r.Width - 18, 18), FontWeights.SemiBold);
    }

    private void DrawWorldClock(ScreenCanvas c, SystemSnapshot s, Rect r)
    {
        Card(c, r);
        IReadOnlyList<WorldClockEntry> clocks = s.WorldClocks?.Clocks ?? [];
        if (clocks.Count == 0)
        {
            c.AlignedText("—", 11, DimGray, r, FontWeights.Medium, TextAlignment.Center);
            return;
        }

        for (int index = 0; index < Math.Min(2, clocks.Count); index++)
        {
            WorldClockEntry entry = clocks[index];
            string time = WorldClockTheme.TryGetTime(entry.TimeZoneId, s.Timestamp, out DateTimeOffset local)
                ? DisplayUnits.Time(local)
                : "—";
            MetricRow(c, new Rect(r.Left, r.Top + 8 + index * 18, r.Width, 15),
                entry.Label, time, Colors.White);
        }
    }

    private void DrawGitHub(ScreenCanvas c, SystemSnapshot s, Rect r)
    {
        Card(c, r);
        if (s.GitHub is not { Available: true, Days.Count: > 0 } gitHub)
        {
            c.AlignedText(Loc.T("ScreenGitHubNotConfigured"), 10, DimGray, r,
                FontWeights.Medium, TextAlignment.Center);
            return;
        }

        MetricRow(c, new Rect(r.Left, r.Top + 6, r.Width, 15),
            Loc.T("ScreenGitHubToday"), gitHub.Days[^1].Count.ToString(CultureInfo.InvariantCulture),
            Colors.White);
        c.AlignedText(gitHub.Username, 9, DimGray,
            new Rect(r.Left + 9, r.Top + 23, r.Width - 18, 13), FontWeights.Medium);
    }

    private static Rect ShrinkX(Rect r, double amount) =>
        new(r.Left + amount, r.Top, r.Width - amount * 2, r.Height);
}
