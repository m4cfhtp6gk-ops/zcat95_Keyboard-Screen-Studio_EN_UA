namespace KeyboardScreen.Core;

/// <summary>
/// Air-raid alert status. With a location set: a large ALERT/CLEAR state with
/// a stroke-drawn warning triangle and the start time; without one, a
/// whole-country summary listing the regions under alert. A permanent footer
/// notes this is an informational display, never the only warning source.
/// </summary>
public sealed class AirAlertTheme : IScreenTheme
{
    private static readonly Color Background = Color.FromRgb(5, 9, 14);
    private static readonly Color AlertRed = Color.FromRgb(224, 68, 68);
    private static readonly Color ClearGreen = Color.FromRgb(64, 186, 120);
    private static readonly Color Secondary = Color.FromRgb(150, 160, 172);
    private static readonly Color Muted = Color.FromRgb(102, 112, 124);

    public string Id => "alerts";
    public string DisplayName => Loc.T("ThemeAlertsName");
    public string Description => Loc.T("ThemeAlertsDescription");
    public string Details => Loc.T("ThemeAlertsDetails");

    public void Draw(ScreenCanvas canvas, SystemSnapshot snapshot)
    {
        Rect safe = canvas.SafeBounds;
        canvas.Fill(Background);
        ThemeHeader.Draw(canvas, snapshot, Loc.T("ScreenTitleAlerts"));

        AirAlertSnapshot? alerts = snapshot.AirAlerts;
        if (alerts is not { Available: true })
        {
            canvas.AlignedText(Loc.T("ScreenAlertsNotConfigured"), 15, Colors.White,
                new Rect(safe.Left, safe.Top + 150, safe.Width, 24), FontWeights.SemiBold, TextAlignment.Center);
            canvas.AlignedText(alerts?.ErrorMessage ?? Loc.T("ScreenAlertsHint"), 10, Secondary,
                new Rect(safe.Left, safe.Top + 182, safe.Width, 30), FontWeights.Normal, TextAlignment.Center);
            DrawDisclaimer(canvas, safe);
            return;
        }

        if (alerts.Location.Length > 0)
        {
            DrawLocationState(canvas, safe, alerts);
        }
        else
        {
            DrawCountrySummary(canvas, safe, alerts);
        }

        string footer = alerts.IsStale
            ? Loc.T("ScreenStocksStale")
            : Loc.T("ScreenWeatherUpdated", DisplayUnits.Time(alerts.UpdatedAt));
        canvas.AlignedText(footer, 9, Muted,
            new Rect(safe.Left, safe.Bottom - 28, safe.Width, 13), FontWeights.Normal, TextAlignment.Center);
        DrawDisclaimer(canvas, safe);
    }

    private static void DrawLocationState(ScreenCanvas canvas, Rect safe, AirAlertSnapshot alerts)
    {
        bool active = alerts.AlertActiveAtLocation;
        Color statusColor = active ? AlertRed : ClearGreen;

        DrawWarningTriangle(canvas, safe.Left + safe.Width / 2, safe.Top + 118, 42, statusColor, active);

        canvas.AlignedText(Loc.T(active ? "ScreenAlertsActive" : "ScreenAlertsClear"), 24, statusColor,
            new Rect(safe.Left, safe.Top + 176, safe.Width, 34), FontWeights.SemiBold, TextAlignment.Center);
        canvas.AlignedText(alerts.Location, 12, Colors.White,
            new Rect(safe.Left, safe.Top + 216, safe.Width, 20), FontWeights.SemiBold, TextAlignment.Center);

        if (active)
        {
            AirAlertInfo? mine = alerts.ActiveAlerts
                .FirstOrDefault(alert => AirAlertSource.MatchesLocation(alert.LocationTitle, alerts.Location));
            if (mine?.StartedAt is { } startedAt)
            {
                canvas.AlignedText(Loc.T("ScreenAlertsSince", DisplayUnits.Time(startedAt.ToLocalTime())), 11, Secondary,
                    new Rect(safe.Left, safe.Top + 242, safe.Width, 18), FontWeights.Medium, TextAlignment.Center);
            }
        }

        canvas.AlignedText(Loc.T("ScreenAlertsCount", alerts.ActiveAlerts.Count), 11, Secondary,
            new Rect(safe.Left, safe.Top + 276, safe.Width, 18), FontWeights.Medium, TextAlignment.Center);
    }

    private static void DrawCountrySummary(ScreenCanvas canvas, Rect safe, AirAlertSnapshot alerts)
    {
        bool any = alerts.ActiveAlerts.Count > 0;
        canvas.AlignedText(alerts.ActiveAlerts.Count.ToString(), 44, any ? AlertRed : ClearGreen,
            new Rect(safe.Left, safe.Top + 44, safe.Width, 56), FontWeights.SemiBold, TextAlignment.Center);
        canvas.AlignedText(Loc.T("ScreenAlertsRegions"), 11, Secondary,
            new Rect(safe.Left, safe.Top + 104, safe.Width, 18), FontWeights.SemiBold, TextAlignment.Center);

        double top = safe.Top + 136;
        foreach (AirAlertInfo alert in alerts.ActiveAlerts.Take(11))
        {
            canvas.Ellipse(new Rect(safe.Left + 2, top + 5, 5, 5), AlertRed);
            canvas.Text(alert.LocationTitle, 10, Colors.White,
                new Point(safe.Left + 14, top), FontWeights.Medium,
                TextAlignment.Left, safe.Width - 14, 15);
            top += 17;
        }

        if (alerts.ActiveAlerts.Count > 11)
        {
            canvas.Text("+" + (alerts.ActiveAlerts.Count - 11), 10, Secondary,
                new Point(safe.Left + 14, top), FontWeights.Medium);
        }
    }

    /// <summary>A warning triangle from strokes; hollow when the sky is clear.</summary>
    private static void DrawWarningTriangle(ScreenCanvas canvas, double centerX, double centerY, double size, Color color, bool filled)
    {
        double half = size / 2;
        var triangle = new StreamGeometry();
        using (StreamGeometryContext context = triangle.Open())
        {
            context.BeginFigure(new Point(centerX, centerY - half), filled, true);
            context.LineTo(new Point(centerX + half, centerY + half * 0.72), true, true);
            context.LineTo(new Point(centerX - half, centerY + half * 0.72), true, true);
            context.LineTo(new Point(centerX, centerY - half), true, true);
        }
        canvas.Path(triangle, color, 3, filled ? Color.FromArgb(56, color.R, color.G, color.B) : null);

        Color mark = filled ? color : color;
        canvas.RoundedRect(new Rect(centerX - 2, centerY - half * 0.42, 4, half * 0.72), 2, mark);
        canvas.Ellipse(new Rect(centerX - 2.5, centerY + half * 0.44, 5, 5), mark);
    }

    private static void DrawDisclaimer(ScreenCanvas canvas, Rect safe) =>
        canvas.AlignedText(Loc.T("ScreenAlertsDisclaimer"), 8, Muted,
            new Rect(safe.Left, safe.Bottom - 12, safe.Width, 12), FontWeights.Normal, TextAlignment.Center);
}
