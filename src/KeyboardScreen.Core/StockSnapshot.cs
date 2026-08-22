namespace KeyboardScreen.Core;

public sealed record StockSnapshot(
    IReadOnlyList<StockQuoteSnapshot> Quotes,
    DateTimeOffset UpdatedAt,
    bool RedForGain,
    bool IsStale = false,
    string? ErrorMessage = null)
{
    public static StockSnapshot Empty { get; } = new([], DateTimeOffset.MinValue, true);
}

public sealed record StockQuoteSnapshot(
    string Symbol,
    string DisplayName,
    double CurrentPrice,
    double ChangePercent,
    DateTimeOffset UpdatedAt,
    IReadOnlyList<double>? FiveDayCloses = null,
    double Quantity = 0);

/// <summary>
/// The holdings math for the portfolio row. Prices are summed as the source
/// returns them, so mixing symbols quoted in different currencies produces a
/// number without a meaning - the settings hint says as much.
/// </summary>
public static class StockPortfolio
{
    /// <summary>Copies the per-symbol quantities from settings onto the quotes.</summary>
    public static StockSnapshot Attach(StockSnapshot snapshot, StockSettings? settings)
    {
        if (settings is null || snapshot.Quotes.Count == 0)
        {
            return snapshot;
        }

        var quantities = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        foreach (StockItemSettings item in settings.Items)
        {
            if (item.Enabled && !string.IsNullOrWhiteSpace(item.Symbol) && item.Quantity > 0)
            {
                quantities[item.Symbol.Trim()] = item.Quantity;
            }
        }

        if (quantities.Count == 0)
        {
            return snapshot;
        }

        return snapshot with
        {
            Quotes = snapshot.Quotes
                .Select(quote => quantities.TryGetValue(quote.Symbol, out double quantity)
                    ? quote with { Quantity = quantity }
                    : quote)
                .ToArray()
        };
    }

    /// <summary>Total value plus the day's absolute and percent change; null without holdings.</summary>
    public static (double Value, double DayChange, double DayChangePercent)? Compute(IReadOnlyList<StockQuoteSnapshot> quotes)
    {
        double value = 0;
        double previous = 0;
        foreach (StockQuoteSnapshot quote in quotes)
        {
            if (quote.Quantity <= 0 || quote.CurrentPrice <= 0)
            {
                continue;
            }

            double position = quote.Quantity * quote.CurrentPrice;
            value += position;
            previous += position / (1 + quote.ChangePercent / 100);
        }

        if (value <= 0 || previous <= 0)
        {
            return null;
        }

        double change = value - previous;
        return (value, change, change / previous * 100);
    }

    public static string FormatValue(double value) => value switch
    {
        >= 1_000_000 => (value / 1_000_000).ToString("0.00") + "M",
        >= 10_000 => value.ToString("#,0"),
        >= 100 => value.ToString("#,0.0"),
        _ => value.ToString("0.00")
    };
}
