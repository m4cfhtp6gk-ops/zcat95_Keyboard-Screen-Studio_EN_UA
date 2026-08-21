namespace KeyboardScreen.Core;

public sealed class CryptoSettings
{
    /// <summary>Binance pairs; at most four are shown.</summary>
    public List<StockItemSettings> Items { get; set; } =
    [
        new() { Symbol = "BTCUSDT" },
        new() { Symbol = "ETHUSDT" },
        new() { Symbol = string.Empty },
        new() { Symbol = string.Empty }
    ];

    public bool RedForGain { get; set; }
}

public sealed record CryptoCoinSnapshot(
    string Symbol,
    string DisplayName,
    double Price,
    double ChangePercent24h,
    IReadOnlyList<double> HourlyCloses);

public sealed record CryptoSnapshot(
    IReadOnlyList<CryptoCoinSnapshot> Coins,
    DateTimeOffset UpdatedAt,
    bool RedForGain,
    bool IsStale = false,
    string? ErrorMessage = null)
{
    public static CryptoSnapshot Empty { get; } = new([], DateTimeOffset.MinValue, false);
}
