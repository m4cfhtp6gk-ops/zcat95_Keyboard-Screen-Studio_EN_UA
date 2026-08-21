namespace KeyboardScreen.Core;

public sealed class PriceAlertSettings
{
    /// <summary>Binance pair, e.g. BTCUSDT.</summary>
    public string Symbol { get; set; } = string.Empty;

    public double? Above { get; set; }

    public double? Below { get; set; }

    public bool IsConfigured => !string.IsNullOrWhiteSpace(Symbol) && (Above is not null || Below is not null);
}

public sealed class NotificationSettings
{
    /// <summary>Master switch; everything below is opt-in.</summary>
    public bool Enabled { get; set; }

    public bool ClaudeThresholds { get; set; } = true;

    public bool DeviceOffline { get; set; } = true;

    public List<PriceAlertSettings> PriceAlerts { get; set; } = [];

    public bool HasConfiguredAlerts => PriceAlerts.Any(alert => alert.IsConfigured);
}

public sealed record AppNotification(string TriggerKey, string Title, string Message);

public sealed record NotificationInputs(
    ClaudeUsageSnapshot? Claude = null,
    bool? DevicePushFailed = null,
    IReadOnlyDictionary<string, double>? Prices = null);

/// <summary>
/// Decides which desktop notifications fire. Pure state-transition logic with
/// per-trigger cooldowns (the backlog rule: never spam), so it is fully
/// testable with an injected clock; the app layer only shows what comes out.
/// </summary>
public sealed class NotificationEngine
{
    private static readonly int[] ClaudeThresholds = [80, 90, 95];
    private static readonly TimeSpan ClaudeCooldown = TimeSpan.FromHours(1);
    private static readonly TimeSpan OfflineCooldown = TimeSpan.FromMinutes(30);
    private static readonly TimeSpan PriceCooldown = TimeSpan.FromHours(1);

    private readonly Dictionary<string, int> _claudeNotifiedThreshold = [];
    private readonly Dictionary<string, DateTimeOffset> _lastFired = [];
    private readonly Dictionary<string, bool> _priceArmed = [];
    private bool _wasPushFailing;

    public IReadOnlyList<AppNotification> Evaluate(NotificationSettings settings, NotificationInputs inputs, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(inputs);
        if (!settings.Enabled)
        {
            return [];
        }

        var fired = new List<AppNotification>();
        if (settings.ClaudeThresholds && inputs.Claude is { Available: true } claude)
        {
            EvaluateClaudeWindow(fired, "claude-session", Loc.T("NotifyClaudeSession"), claude.Session, now);
            EvaluateClaudeWindow(fired, "claude-week", Loc.T("NotifyClaudeWeek"), claude.Week, now);
            EvaluateClaudeWindow(fired, "claude-model-week", Loc.T("NotifyClaudeModelWeek"), claude.ModelWeek, now);
        }

        if (settings.DeviceOffline && inputs.DevicePushFailed is { } pushFailed)
        {
            if (pushFailed && !_wasPushFailing && CooldownElapsed("device-offline", OfflineCooldown, now))
            {
                fired.Add(new AppNotification("device-offline",
                    Loc.T("NotifyDeviceOfflineTitle"), Loc.T("NotifyDeviceOfflineBody")));
                _lastFired["device-offline"] = now;
            }

            _wasPushFailing = pushFailed;
        }

        if (inputs.Prices is { Count: > 0 } prices)
        {
            foreach (PriceAlertSettings alert in settings.PriceAlerts)
            {
                if (!alert.IsConfigured ||
                    !prices.TryGetValue(BinanceStockSnapshotSource.NormalizeSymbol(alert.Symbol), out double price))
                {
                    continue;
                }

                EvaluatePriceSide(fired, alert, price, above: true, now);
                EvaluatePriceSide(fired, alert, price, above: false, now);
            }
        }

        return fired;
    }

    private void EvaluateClaudeWindow(List<AppNotification> fired, string key, string windowName, ClaudeUsageWindow? window, DateTimeOffset now)
    {
        if (window is null)
        {
            return;
        }

        int percent = (int)window.EffectivePercent;
        int crossed = 0;
        foreach (int threshold in ClaudeThresholds)
        {
            if (percent >= threshold)
            {
                crossed = threshold;
            }
        }

        int previous = _claudeNotifiedThreshold.GetValueOrDefault(key);
        if (crossed == 0)
        {
            // The window reset - arm the thresholds again.
            _claudeNotifiedThreshold[key] = 0;
            return;
        }

        if (crossed > previous && CooldownElapsed(key, ClaudeCooldown, now))
        {
            fired.Add(new AppNotification(key,
                Loc.T("NotifyClaudeTitle"),
                Loc.T("NotifyClaudeBody", windowName, percent)));
            _claudeNotifiedThreshold[key] = crossed;
            _lastFired[key] = now;
        }
    }

    private void EvaluatePriceSide(List<AppNotification> fired, PriceAlertSettings alert, double price, bool above, DateTimeOffset now)
    {
        double? limit = above ? alert.Above : alert.Below;
        if (limit is not { } bound)
        {
            return;
        }

        string symbol = BinanceStockSnapshotSource.NormalizeSymbol(alert.Symbol);
        string key = $"price-{symbol}-{(above ? "above" : "below")}";
        bool triggered = above ? price >= bound : price <= bound;

        // Fire only on the crossing; re-arm once the price is back on the
        // safe side, so a price hovering at the bound cannot spam.
        bool armed = _priceArmed.GetValueOrDefault(key, true);
        if (triggered && armed && CooldownElapsed(key, PriceCooldown, now))
        {
            string display = BinanceStockSnapshotSource.TrimQuoteAsset(symbol);
            fired.Add(new AppNotification(key,
                Loc.T("NotifyPriceTitle", display),
                Loc.T(above ? "NotifyPriceAbove" : "NotifyPriceBelow",
                    display, CurrencyTheme.FormatRate(price), CurrencyTheme.FormatRate(bound))));
            _lastFired[key] = now;
        }

        _priceArmed[key] = !triggered;
    }

    private bool CooldownElapsed(string key, TimeSpan cooldown, DateTimeOffset now) =>
        !_lastFired.TryGetValue(key, out DateTimeOffset last) || now - last >= cooldown;
}
