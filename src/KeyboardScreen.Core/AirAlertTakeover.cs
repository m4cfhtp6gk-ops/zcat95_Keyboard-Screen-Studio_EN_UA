namespace KeyboardScreen.Core;

/// <summary>Per-refresh takeover state; owned by the view model, mutated by <see cref="AirAlertTakeover.Decide"/>.</summary>
public sealed class AirAlertTakeoverState
{
    public DateTimeOffset? EngagedAt { get; set; }
    public DateTimeOffset? ClearSeenAt { get; set; }

    public void Reset()
    {
        EngagedAt = null;
        ClearSeenAt = null;
    }
}

public readonly record struct AirAlertTakeoverDecision(AirAlertTakeoverMode Show, bool IsAllClear)
{
    public bool ShowFullScreen => Show == AirAlertTakeoverMode.FullScreen;
    public bool ShowPopup => Show == AirAlertTakeoverMode.Popup;
    public bool Active => Show != AirAlertTakeoverMode.Off;
}

/// <summary>
/// Decides whether an alert in the watched location takes over the screen.
/// The takeover outranks the carousel, media automation and the night
/// schedule, and holds either until the all-clear or for a fixed number of
/// minutes; the all-clear itself stays visible for a minute so the end of the
/// alert is seen too.
/// </summary>
public static class AirAlertTakeover
{
    public static readonly TimeSpan AllClearLinger = TimeSpan.FromMinutes(1);

    /// <summary>Refresh-loop cap so an armed takeover notices alerts promptly.</summary>
    public const int PollCapSeconds = 30;

    public const int MinMinutes = 1;
    public const int MaxMinutes = 180;

    /// <summary>Armed = a mode is chosen, a token is set and a location is picked.</summary>
    public static bool IsArmed(AirAlertSettings? settings) =>
        settings is { IsConfigured: true } &&
        settings.Takeover != AirAlertTakeoverMode.Off &&
        !string.IsNullOrWhiteSpace(settings.Location);

    public static AirAlertTakeoverDecision Decide(
        AirAlertSettings? settings,
        AirAlertSnapshot? alerts,
        AirAlertTakeoverState state,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (!IsArmed(settings))
        {
            state.Reset();
            return default;
        }

        if (alerts is not { Available: true })
        {
            // A transient outage neither engages nor releases the takeover.
            return default;
        }

        AirAlertTakeoverMode mode = settings!.Takeover;
        if (alerts.AlertActiveAtLocation)
        {
            state.ClearSeenAt = null;
            state.EngagedAt ??= now;
            bool visible = settings.TakeoverUntilClear ||
                now - state.EngagedAt.Value < TimeSpan.FromMinutes(
                    Math.Clamp(settings.TakeoverMinutes, MinMinutes, MaxMinutes));
            return visible ? new AirAlertTakeoverDecision(mode, false) : default;
        }

        if (state.EngagedAt is null)
        {
            return default;
        }

        state.ClearSeenAt ??= now;
        if (now - state.ClearSeenAt.Value < AllClearLinger)
        {
            return new AirAlertTakeoverDecision(mode, true);
        }

        state.Reset();
        return default;
    }
}

/// <summary>
/// The alert banner drawn over whatever theme is on the screen, in the same
/// card language as the Telegram popup: red while the alert is on, green for
/// the minute the all-clear lingers.
/// </summary>
public static class AirAlertPopupOverlay
{
    private static readonly Color AlertBackground = Color.FromArgb(248, 34, 10, 12);
    private static readonly Color AlertAccent = Color.FromRgb(224, 68, 68);
    private static readonly Color ClearBackground = Color.FromArgb(248, 8, 26, 17);
    private static readonly Color ClearAccent = Color.FromRgb(64, 186, 120);
    private static readonly Color Secondary = Color.FromRgb(196, 202, 210);

    public static void Draw(ScreenCanvas canvas, bool allClear, string location)
    {
        Rect safe = canvas.SafeBounds;
        var card = new Rect(safe.Left - 4, safe.Top - 4, safe.Width + 8, 60);
        Color accent = allClear ? ClearAccent : AlertAccent;

        canvas.RoundedRect(card, 12, allClear ? ClearBackground : AlertBackground, accent);
        canvas.RoundedRect(new Rect(card.Left, card.Top + 9, 3.5, card.Height - 18), 1.75, accent);

        // The same stroke-drawn warning triangle as the theme, banner sized.
        double glyphX = card.Left + 12;
        double glyphY = card.Top + 11;
        var triangle = new StreamGeometry();
        using (StreamGeometryContext context = triangle.Open())
        {
            context.BeginFigure(new Point(glyphX + 11, glyphY), !allClear, true);
            context.LineTo(new Point(glyphX + 22, glyphY + 19), true, true);
            context.LineTo(new Point(glyphX, glyphY + 19), true, true);
            context.LineTo(new Point(glyphX + 11, glyphY), true, true);
        }
        canvas.Path(triangle, accent, 2,
            allClear ? null : Color.FromArgb(64, accent.R, accent.G, accent.B));
        canvas.RoundedRect(new Rect(glyphX + 9.9, glyphY + 6.5, 2.2, 6.8), 1.1, accent);
        canvas.Ellipse(new Rect(glyphX + 9.6, glyphY + 15, 2.8, 2.8), accent);

        double textLeft = glyphX + 30;
        double textWidth = card.Right - 10 - textLeft;
        canvas.Text(Loc.T(allClear ? "ScreenAlertsPopupClear" : "ScreenAlertsPopupAlert"), 12, accent,
            new Point(textLeft, card.Top + 10), FontWeights.SemiBold,
            TextAlignment.Left, textWidth, 18);
        canvas.Text(location, 10, Secondary,
            new Point(card.Left + 12, card.Top + 36), FontWeights.Medium,
            TextAlignment.Left, card.Width - 24, 16);
    }
}
