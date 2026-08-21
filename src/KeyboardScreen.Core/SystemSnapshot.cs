namespace KeyboardScreen.Core;

public sealed record SystemSnapshot(
    DateTimeOffset Timestamp,
    double CpuPercent,
    double MemoryPercent,
    double DownloadMbps = 0.0,
    double UploadMbps = 0.0,
    MusicSnapshot? Music = null,
    AiQuotaSnapshot? AiQuota = null,
    WeatherSnapshot? Weather = null,
    StockSnapshot? Stocks = null,
    double? GpuPercent = null,
    ClaudeUsageSnapshot? ClaudeUsage = null,
    CurrencySnapshot? Currency = null,
    CryptoSnapshot? Crypto = null)
{
    public static SystemSnapshot DesignSample { get; } = new(
        DateTimeOffset.Now,
        38.4,
        62.7,
        12.8,
        1.6,
        new MusicSnapshot(
            Available: true,
            "Midnight Drive",
            "Keyboard Studio",
            TimeSpan.FromSeconds(94),
            TimeSpan.FromSeconds(225),
            IsPlaying: true,
            null),
        AiQuotaSnapshot.ForSubscription(
            "ChatGPT",
            56,
            remainingCount: 1,
            resetPeriod: AiResetPeriod.Weekly),
        new WeatherSnapshot(true, WeatherSettings.DefaultLocationQuery, 26, 28, 61, 2, true, DateTimeOffset.Now),
        GpuPercent: 45.0,
        ClaudeUsage: new ClaudeUsageSnapshot(
            Available: true,
            Session: new ClaudeUsageWindow(
                ClaudeUsageWindowKind.Session, 42, DateTimeOffset.Now.AddHours(2).AddMinutes(44), 1_180_000),
            Week: new ClaudeUsageWindow(
                ClaudeUsageWindowKind.Week, 73, DateTimeOffset.Now.AddDays(4).AddHours(9), 18_400_000),
            ModelWeek: new ClaudeUsageWindow(
                ClaudeUsageWindowKind.ModelWeek, 91, DateTimeOffset.Now.AddDays(4).AddHours(9), 6_250_000, "Fable"),
            UpdatedAt: DateTimeOffset.Now),
        Currency: new CurrencySnapshot(
            Available: true,
            "USD",
            [new CurrencyRate("EUR", 0.9214), new CurrencyRate("UAH", 41.32), new CurrencyRate("PLN", 3.968)],
            DateTimeOffset.Now),
        Crypto: new CryptoSnapshot(
            [
                new CryptoCoinSnapshot("BTCUSDT", "BTC", 97_412.55, 2.41,
                    [95_100, 95_400, 95_050, 95_800, 96_200, 95_900, 96_450, 96_800, 96_500, 97_050, 97_300, 97_412]),
                new CryptoCoinSnapshot("ETHUSDT", "ETH", 3_412.08, -1.27,
                    [3_480, 3_465, 3_490, 3_450, 3_430, 3_455, 3_420, 3_405, 3_432, 3_418, 3_400, 3_412])
            ],
            DateTimeOffset.Now,
            RedForGain: false));
}
