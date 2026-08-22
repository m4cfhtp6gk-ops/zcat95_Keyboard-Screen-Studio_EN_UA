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
    CryptoSnapshot? Crypto = null,
    HardwareSnapshot? Hardware = null,
    GitHubContributionSnapshot? GitHub = null,
    AirAlertSnapshot? AirAlerts = null,
    DiskSnapshot? Disks = null,
    PingSnapshot? Ping = null,
    WorldClockSnapshot? WorldClocks = null,
    CountdownSnapshot? Countdown = null,
    CalendarSnapshot? Calendar = null)
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
        new WeatherSnapshot(true, WeatherSettings.DefaultLocationQuery, 26, 28, 61, 2, true, DateTimeOffset.Now,
            DailyForecast: SampleForecast(),
            Sunrise: DateTimeOffset.Now.Date.AddHours(6).AddMinutes(12),
            Sunset: DateTimeOffset.Now.Date.AddHours(20).AddMinutes(41),
            EuropeanAqi: 23),
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
            RedForGain: false),
        Hardware: new HardwareSnapshot(
            Available: true,
            Cpu: new HardwareComponentSnapshot("Desktop 8-core CPU", 38.4, 62.5, 4.55, 1240),
            Gpu: new HardwareComponentSnapshot("Discrete GPU 12 GB", 45.0, 58.0, 2.31, 1680, 6_348, 12_288),
            RamUsedGb: 20.1,
            RamTotalGb: 32.0,
            DiskUsedPercent: 71.0,
            UpdatedAt: DateTimeOffset.Now),
        GitHub: SampleGitHub(),
        AirAlerts: new AirAlertSnapshot(
            Available: true,
            AlertActiveAtLocation: true,
            [
                new AirAlertInfo("м. Київ", "air_raid", DateTimeOffset.Now.AddMinutes(-18)),
                new AirAlertInfo("Київська область", "air_raid", DateTimeOffset.Now.AddMinutes(-18)),
                new AirAlertInfo("Чернігівська область", "air_raid", DateTimeOffset.Now.AddMinutes(-42))
            ],
            Location: "м. Київ",
            UpdatedAt: DateTimeOffset.Now),
        Disks: new DiskSnapshot(true,
            [
                new DiskVolume("C:", "System", 372.4, 476.0),
                new DiskVolume("D:", "Data", 1_412.0, 1_863.0),
                new DiskVolume("E:", "Media", 704.5, 931.5)
            ],
            DateTimeOffset.Now),
        Ping: new PingSnapshot(true,
            [
                new PingHostSnapshot("192.168.1.1", 2,
                    [3, 2, 2, 4, 2, 3, 2, 2, 3, 2, 2, 2]),
                new PingHostSnapshot("1.1.1.1", 14,
                    [15, 13, 16, 14, 18, 13, 12, 14, null, 21, 15, 14]),
                new PingHostSnapshot("example.com", 87,
                    [76, 82, 91, 88, 104, 96, 84, 79, 90, 85, 92, 87])
            ],
            DateTimeOffset.Now),
        WorldClocks: new WorldClockSnapshot(
            [
                new WorldClockEntry("Київ", "Europe/Kyiv"),
                new WorldClockEntry("Warszawa", "Europe/Warsaw"),
                new WorldClockEntry("New York", "America/New_York"),
                new WorldClockEntry("Tokyo", "Asia/Tokyo")
            ]),
        Countdown: new CountdownSnapshot(
            [
                new CountdownEntry("Відпустка", DateTimeOffset.Now.Date.AddDays(12).AddHours(9)),
                new CountdownEntry("Реліз проєкту", DateTimeOffset.Now.Date.AddDays(33)),
                new CountdownEntry("День народження", DateTimeOffset.Now.Date.AddDays(58))
            ]),
        Calendar: new CalendarSnapshot(true,
            [
                new CalendarEventInfo("Стендап команди", DateTimeOffset.Now.Date.AddHours(11), false),
                new CalendarEventInfo("Обід з Мартою", DateTimeOffset.Now.Date.AddHours(13.5), false),
                new CalendarEventInfo("Ретроспектива", DateTimeOffset.Now.Date.AddDays(1).AddHours(16), false),
                new CalendarEventInfo("Сплатити оренду", DateTimeOffset.Now.Date.AddDays(2), true),
                new CalendarEventInfo("Виступ на мітапі", DateTimeOffset.Now.Date.AddDays(4).AddHours(18.5), false)
            ],
            DateTimeOffset.Now));

    private static IReadOnlyList<DailyWeatherForecast> SampleForecast()
    {
        DateOnly today = DateOnly.FromDateTime(DateTime.Today);
        int[] codes = [2, 0, 61, 3, 1];
        double[] highs = [28, 30, 24, 25, 27];
        double[] lows = [19, 20, 17, 16, 18];
        var days = new List<DailyWeatherForecast>(5);
        for (int index = 0; index < 5; index++)
        {
            days.Add(new DailyWeatherForecast(today.AddDays(index), codes[index], highs[index], lows[index]));
        }
        return days;
    }

    private static GitHubContributionSnapshot SampleGitHub()
    {
        DateOnly today = DateOnly.FromDateTime(DateTime.Today);
        var days = new List<GitHubContributionDay>();
        for (int back = 17 * 7 - 1; back >= 0; back--)
        {
            // A deterministic bumpy pattern: quiet weekends, active midweeks.
            int wave = (back * 5 + back / 9) % 11;
            int level = wave switch { < 3 => 0, < 6 => 1, < 8 => 2, < 10 => 3, _ => 4 };
            days.Add(new GitHubContributionDay(today.AddDays(-back), level, level * 2));
        }

        return new GitHubContributionSnapshot(true, "zcat95", days, DateTimeOffset.Now);
    }
}
