using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using KeyboardScreen.Core;

Loc.Instance.Initialize(AppLanguage.English);

var defaults = new AppSettings();
Assert(defaults.SelectedThemeId == "clock-dot-matrix", "first-run theme must default to dot-matrix clock");
Assert(defaults.AccentColor == "#E4694C", "first-run accent color is incorrect");
Assert(defaults.AutoPush && defaults.RefreshSeconds == 1, "first-run automation defaults are incorrect");
Assert(defaults.MinimizeToTray && defaults.CloseToTray, "first-run tray defaults are incorrect");
Assert(defaults.Weather.UseAutomaticLocation, "first-run weather must use automatic location");
Assert(defaults.Weather.LocationQuery == WeatherSettings.DefaultLocationQuery, "first-run weather city is incorrect");
Assert(defaults.Language.Length == 0, "first-run language must defer to the operating system");
Assert(defaults.ClaudeUsage.ModelScope == "Fable" && !defaults.ClaudeUsage.IsConfigured,
    "Claude usage must start unconfigured with the Fable row");
Assert(defaults.SafeArea == new ScreenInsets(10, 52, 10, 12), "first-run safe area is incorrect");
Assert(ScreenFontOption.DefaultId == "builtin:misans", "default font must be built-in MiSans");
Assert(ScreenFontOption.Default.FileName == "MiSans-Medium.ttf", "default font must be MiSans-Medium.ttf");
Assert(ScreenFontOption.Default.IsBuiltIn, "default font must be flagged built-in");
Assert(IsStaticFont(Path.Combine(AppContext.BaseDirectory, "Assets", "Fonts", "Doto.ttf")), "bundled Doto must be a static font (no fvar table)");

var profile = ScreenProfile.KeyboardDisplay;
Assert(profile.SafeArea.Top == 52, "keyboard firmware safe area must reserve the top status pills");
Assert(profile.SafeArea.Left + profile.SafeArea.Right < profile.Width, "safe area horizontal insets are invalid");
Assert(profile.SafeArea.Top + profile.SafeArea.Bottom < profile.Height, "safe area vertical insets are invalid");
var renderer = new ScreenRenderer(profile);
var themes = BuiltInThemes.Create(new ImageTheme(), new PomodoroTimer());
Assert(themes.Count == 26, "built-in theme catalog should contain the 26 supported schemes");
Assert(themes.All(theme => theme.Id is not "calendar" and not "ambient"), "removed calendar/ambient themes must not be registered");
Assert(themes.All(theme => theme.Id != "clock-seconds"), "removed seconds progress theme must not be registered");
Assert(themes.All(theme => theme.Id != "week"), "removed week calendar theme must not be registered");
Assert(themes.Any(theme => theme.Id == "performance-visual"), "performance-visual theme must be registered");
Assert(themes.Single(theme => theme.Id == "clock-dot-matrix").DisplayName == "Dot-Matrix Clock", "dot-matrix clock theme must be registered");
Assert(themes.Single(theme => theme.Id == "clock-weather-dot").DisplayName == "Dot-Matrix Weather Clock", "dot-matrix weather clock theme must be registered");
Assert(themes.Single(theme => theme.Id == "clock-dot-analog").DisplayName == "Dot-Matrix Analog Clock", "dot-matrix analog clock theme must be registered");
Assert(themes.Single(theme => theme.Id == "clock-dot-progress").DisplayName == "Dot-Matrix Progress", "dot-matrix progress theme must be registered");
Assert(themes.Single(theme => theme.Id == "image").DisplayName == "Image Clock", "image theme must be named Image Clock");
Assert(themes.Single(theme => theme.Id == "ai-quota").DisplayName == "AI Usage (Preview)", "AI quota theme must carry the development label");
Assert(themes.Any(theme => theme.Id == "weather-five-day"), "five-day weather theme must be registered");
Assert(themes.Any(theme => theme.Id == "stocks"), "stock theme must be registered");
Assert(themes.Any(theme => theme.Id == "claude-usage"), "Claude usage theme must be registered");
Assert(themes.Any(theme => theme.Id == "currency"), "currency rates theme must be registered");
Assert(themes.Any(theme => theme.Id == "crypto"), "crypto theme must be registered");
Assert(themes.Any(theme => theme.Id == "pomodoro"), "pomodoro theme must be registered");
Assert(themes.Any(theme => theme.Id == "hardware"), "hardware monitor theme must be registered");
Assert(themes.Any(theme => theme.Id == "github"), "GitHub activity theme must be registered");

// The Claude usage theme has to survive both a full snapshot and no data at all:
// the meters only appear once an account actually reports its limits.
var claudeTheme = themes.Single(theme => theme.Id == "claude-usage");
var claudeConnected = renderer.Render(claudeTheme, SystemSnapshot.DesignSample);
Assert(claudeConnected.JpegBytes is [0xFF, 0xD8, ..], "connected Claude usage view did not render");
var claudeMissing = renderer.Render(claudeTheme, SystemSnapshot.DesignSample with { ClaudeUsage = null });
Assert(claudeMissing.JpegBytes is [0xFF, 0xD8, ..], "disconnected Claude usage view did not render");
var claudeError = renderer.Render(
    claudeTheme,
    SystemSnapshot.DesignSample with { ClaudeUsage = ClaudeUsageSnapshot.Unavailable("no key") });
Assert(claudeError.JpegBytes is [0xFF, 0xD8, ..], "errored Claude usage view did not render");
Assert(SystemSnapshot.DesignSample.ClaudeUsage?.Windows.Count() == 3, "the sample should carry all three windows");
Assert(ClaudeUsageTheme.FormatTokens(1_180_000) == "1.2M", "token formatting is incorrect");
Assert(ClaudeUsageTheme.FormatTokens(940) == "940", "small token counts should stay plain");
var expired = new ClaudeUsageWindow(ClaudeUsageWindowKind.Session, 88, DateTimeOffset.Now.AddMinutes(-1));
Assert(expired.EffectivePercent == 0, "a window past its reset time reads as empty");
Console.WriteLine("PASS Claude usage theme states and token formatting");
Assert(themes.Select(theme => theme.Id).Distinct(StringComparer.OrdinalIgnoreCase).Count() == themes.Count, "theme ids should be unique");

var aiQuotaTheme = themes.Single(theme => theme.Id == "ai-quota");
var subscriptionQuota = AiQuotaSnapshot.ForSubscription(
    "ChatGPT",
    56,
    remainingCount: 1,
    resetPeriod: AiResetPeriod.Weekly);
Assert(subscriptionQuota.RemainingDisplay == "56% / 1", "subscription quota display is incorrect");
Assert(subscriptionQuota.ResetPeriod == AiResetPeriod.Weekly, "subscription reset period was not retained");

var tokenBalance = new AiQuotaBalance(
    AiQuotaMetric.Token,
    Used: 440_000,
    Limit: 1_000_000,
    UnitLabel: "token");
var apiKeyQuota = AiQuotaSnapshot.ForApiKey(
    "OpenAI API",
    tokenBalance,
    AiResetPeriod.BillingCycle);
Assert(Math.Abs(apiKeyQuota.ClampedRemainingPercent - 56) < 0.001, "API Key token balance percentage is incorrect");
Assert(apiKeyQuota.Balance?.Metric == AiQuotaMetric.Token, "API Key metric was not retained");
Assert(apiKeyQuota.RemainingDisplay == "56%", "API Key quota display is incorrect");
Assert(AiQuotaSnapshot.ForSubscription("Test", 140).ClampedRemainingPercent == 100, "quota percentage must clamp to 100");

foreach (var theme in themes)
{
    var frame = renderer.Render(theme, SystemSnapshot.DesignSample);
    Assert(frame.Width == 142 && frame.Height == 428, $"{theme.Id}: resolution mismatch");
    Assert(frame.JpegBytes.Length <= profile.MaxJpegBytes, $"{theme.Id}: JPEG exceeds device limit");
    Assert(frame.JpegBytes is [0xFF, 0xD8, ..], $"{theme.Id}: missing JPEG SOI marker");
    Assert(FindStartOfFrame(frame.JpegBytes) == 0xC0, $"{theme.Id}: JPEG is not baseline SOF0");
    Console.WriteLine($"PASS render {theme.Id,-8} {frame.JpegBytes.Length,7} bytes, baseline JPEG 142x428");
}

var subscriptionFrame = renderer.Render(
    aiQuotaTheme,
    SystemSnapshot.DesignSample with { AiQuota = subscriptionQuota });
Assert(subscriptionFrame.JpegBytes is [0xFF, 0xD8, ..], "subscription AI quota theme did not render");

var apiKeyFrame = renderer.Render(
    aiQuotaTheme,
    SystemSnapshot.DesignSample with { AiQuota = apiKeyQuota });
Assert(apiKeyFrame.JpegBytes is [0xFF, 0xD8, ..], "API Key AI quota theme did not render");
Console.WriteLine("PASS AI quota model and single-platform theme for subscription/API Key data");

var weatherResponses = new Queue<string>(new[]
{
    """{"results":[{"name":"北京","latitude":39.9042,"longitude":116.4074}]}""",
    """{"current":{"temperature_2m":31.4,"apparent_temperature":34.2,"relative_humidity_2m":58,"weather_code":2,"is_day":1},"daily":{"time":["2026-07-28","2026-07-29","2026-07-30","2026-07-31","2026-08-01"],"weather_code":[2,3,61,1,0],"temperature_2m_max":[32,31,29,33,34],"temperature_2m_min":[24,23,22,24,25]}}"""
});
var weatherHandler = new SequenceHandler(weatherResponses);
using (var weatherClient = new HttpClient(weatherHandler))
using (var weatherSource = new OpenMeteoWeatherSnapshotSource(weatherClient))
{
    var weatherSnapshot = await weatherSource.ReadAsync(new WeatherSettings { LocationQuery = "北京" });
    Assert(weatherSnapshot.Available, "Open-Meteo weather snapshot must be available");
    Assert(weatherSnapshot.LocationName == "北京", "weather location was not parsed");
    Assert(Math.Abs(weatherSnapshot.TemperatureC - 31.4) < 0.001, "weather temperature was not parsed");
    Assert(weatherSnapshot.RelativeHumidityPercent == 58, "weather humidity was not parsed");
    Assert(weatherSnapshot.ConditionText == "Partly cloudy", "WMO weather condition mapping failed");
    Assert(weatherSnapshot.DailyForecast?.Count == 5, "five-day weather forecast was not parsed");
    Assert(weatherSnapshot.DailyForecast![2].ConditionText == "Rain", "daily WMO weather condition mapping failed");
    var cachedWeather = await weatherSource.ReadAsync(new WeatherSettings { LocationQuery = "北京" });
    Assert(ReferenceEquals(weatherSnapshot, cachedWeather), "weather snapshot should use the ten-minute cache");
    Assert(weatherHandler.RequestCount == 2, "cached weather read must not call the APIs again");
    var fiveDayFrame = renderer.Render(themes.Single(theme => theme.Id == "weather-five-day"), SystemSnapshot.DesignSample with { Weather = weatherSnapshot });
    Assert(fiveDayFrame.JpegBytes is [0xFF, 0xD8, ..], "five-day weather data view did not render");
}
Console.WriteLine("PASS Open-Meteo geocoding/current weather parser and cache");

var automaticWeatherResponses = new Queue<string>(new[]
{
    """{"current":{"temperature_2m":27.2,"apparent_temperature":28.1,"relative_humidity_2m":64,"weather_code":1,"is_day":1},"daily":{"time":["2026-07-29"],"weather_code":[1],"temperature_2m_max":[30],"temperature_2m_min":[24]}}"""
});
var automaticWeatherHandler = new SequenceHandler(automaticWeatherResponses);
using (var automaticWeatherClient = new HttpClient(automaticWeatherHandler))
using (var automaticWeatherSource = new OpenMeteoWeatherSnapshotSource(automaticWeatherClient))
{
    var automaticWeather = await automaticWeatherSource.ReadAsync(new WeatherSettings
    {
        LocationQuery = "上海",
        UseAutomaticLocation = true,
        Latitude = 22.5431,
        Longitude = 114.0579,
        AutomaticLocationName = "当前位置"
    });
    Assert(automaticWeather.Available, "automatic-location weather snapshot must be available");
    Assert(automaticWeather.LocationName == "当前位置", "automatic-location display name was not retained");
    Assert(automaticWeatherHandler.RequestCount == 1, "automatic coordinates must bypass city geocoding");
}
Console.WriteLine("PASS automatic weather coordinates and geocoding bypass");

var reverseGeocodeResponses = new Queue<string>(new[]
{
    """{"city":"深圳市","locality":"福田区","principalSubdivision":"广东省"}"""
});
var reverseGeocodeHandler = new SequenceHandler(reverseGeocodeResponses);
using (var reverseGeocodeClient = new HttpClient(reverseGeocodeHandler))
using (var reverseGeocoder = new BigDataCloudReverseGeocoder(reverseGeocodeClient))
{
    string? city = await reverseGeocoder.ResolveCityAsync(22.5431, 114.0579);
    Assert(city == "深圳市", "reverse geocoder did not prefer the city name");
    Assert(reverseGeocodeHandler.RequestCount == 1, "reverse geocoder should issue one request");
}
Console.WriteLine("PASS automatic-location city reverse geocoding");
var stockResponses = new Queue<string>(new[]
{
    """{"chart":{"result":[{"meta":{"symbol":"AAPL","regularMarketPrice":105.0,"chartPreviousClose":100.0,"regularMarketTime":1785200000},"timestamp":[1785200000,1785286400,1785372800,1785459200,1785545600,1785632000],"indicators":{"quote":[{"close":[100.0,101.5,102.0,101.0,103.5,105.0]}]}}],"error":null}}"""
});
var stockHandler = new SequenceHandler(stockResponses);
using (var stockClient = new HttpClient(stockHandler))
using (var stockSource = new YahooStockSnapshotSource(stockClient))
{
    var stockSettings = new StockSettings
    {
        RedForGain = false,
        Items = [new StockItemSettings { Symbol = "aapl", Alias = "苹果" }]
    };
    var stockSnapshot = await stockSource.ReadAsync(stockSettings);
    Assert(stockSnapshot.Quotes.Count == 1, "stock quote was not parsed");
    Assert(stockSnapshot.Quotes[0].Symbol == "AAPL", "stock symbol must be normalized");
    Assert(stockSnapshot.Quotes[0].DisplayName == "苹果", "stock alias was not applied");
    Assert(Math.Abs(stockSnapshot.Quotes[0].ChangePercent - 5.0) < 0.001, "stock change percent is incorrect");
    Assert(stockSnapshot.Quotes[0].FiveDayCloses is { Count: 5 } closes
        && Math.Abs(closes[0] - 101.5) < 0.001
        && Math.Abs(closes[^1] - 105.0) < 0.001, "Yahoo five-day closes were not parsed");
    Assert(!stockSnapshot.RedForGain, "stock color preference was not preserved");
    var cachedStocks = await stockSource.ReadAsync(stockSettings);
    Assert(ReferenceEquals(stockSnapshot, cachedStocks), "stock snapshot should use the fifteen-minute cache");
    Assert(stockHandler.RequestCount == 1, "cached stock read must not call the API again");
    var stockFrame = renderer.Render(themes.Single(theme => theme.Id == "stocks"), SystemSnapshot.DesignSample with { Stocks = stockSnapshot });
    Assert(stockFrame.JpegBytes is [0xFF, 0xD8, ..], "stock data view did not render");
}
Console.WriteLine("PASS Yahoo chart parser, aliases, color preference and cache");

Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
Assert(TencentStockSnapshotSource.MapSymbol("600519.SS") == "sh600519", "Shanghai A-share symbol mapping is wrong");
Assert(TencentStockSnapshotSource.MapSymbol("000001.SZ") == "sz000001", "Shenzhen A-share symbol mapping is wrong");
Assert(TencentStockSnapshotSource.MapSymbol("0700.HK") == "hk00700", "Hong Kong symbol mapping is wrong");
Assert(TencentStockSnapshotSource.MapSymbol("AAPL") == "usAAPL", "US symbol mapping is wrong");
Assert(TencentStockSnapshotSource.MapSymbol("BTC-USD") is null, "crypto symbols must be reported as unsupported");

string tencentSample = """
v_sh600519="1~贵州茅台~600519~1420.00~1410.00~1405.00~12345~0~0~1420.00~0~0~0~0~0~0~0~0~0~0~0~0~0~0~0~0~0~0~0~~20260807161431~10.00~0.71~1425.00~1390.00";
v_sz000001="51~平安银行~000001~11.19~11.27~11.23~882977~0~0~11.18~0~0~0~0~0~0~0~0~0~0~0~0~0~0~0~0~0~0~0~~2026/08/07 16:14:36~-0.08~-0.71~11.26~11.10";
v_sz301707="51~N展芯股份~301707~116.52~23.45~108.00~214773~0~0~116.52~0~0~0~0~0~0~0~0~0~0~0~0~0~0~0~0~0~0~0~~20260807161454~93.07~396.89~129.90~106.88";
v_hk00700="100~腾讯控股~00700~478.800~479.200~479.000~16319939.0~0~0~478.800~0~0~0~0~0~0~0~0~0~478.800~0~0~0~0~0~0~0~0~0~16319939.0~2026-08-07 16:08:23~-0.400~-0.08~483.200~475.400";
v_usAAPL="200~苹果~AAPL.OQ~313.33~312.41~311.45~34437191~0~0~313.13~200~0~0~0~0~0~0~0~0~313.25~40~0~0~0~0~0~0~0~0~~2026-08-07 16:00:01~0.92~0.29~314.81~310.74";
""";
var tencentHandler = new GbkHandler(tencentSample);
using (var tencentClient = new HttpClient(tencentHandler))
using (var tencentSource = new TencentStockSnapshotSource(tencentClient))
{
    var tencentSettings = new StockSettings
    {
        RedForGain = false,
        Items =
        [
            new StockItemSettings { Symbol = "600519.SS", Alias = "茅台" },
            new StockItemSettings { Symbol = "0700.HK" },
            new StockItemSettings { Symbol = "AAPL", Alias = "苹果" }
        ]
    };
    var tencentSnapshot = await tencentSource.ReadAsync(tencentSettings);
    Assert(tencentHandler.LastRequestUri?.AbsoluteUri == "https://qt.gtimg.cn/q=sh600519,hk00700,usAAPL", "the Tencent request must use the mapped codes");
    Assert(tencentSnapshot.Quotes.Count == 3, "the Tencent feed should yield three quotes");
    Assert(tencentSnapshot.Quotes[0].DisplayName == "茅台", "the A-share alias was not applied");
    Assert(Math.Abs(tencentSnapshot.Quotes[0].CurrentPrice - 1420.00) < 0.001, "the A-share last price was parsed incorrectly");
    Assert(Math.Abs(tencentSnapshot.Quotes[0].ChangePercent - 0.71) < 0.001, "the A-share change percent (field 32) was parsed incorrectly");
    Assert(tencentSnapshot.Quotes[0].UpdatedAt.ToString("yyyyMMddHHmmss") == "20260807161431", "the A-share timestamp was parsed incorrectly");
    Assert(Math.Abs(tencentSnapshot.Quotes[1].ChangePercent - (-0.08)) < 0.001, "the Hong Kong change percent was parsed incorrectly");
    Assert(tencentSnapshot.Quotes[1].UpdatedAt.ToString("yyyy-MM-dd HH:mm:ss") == "2026-08-07 16:08:23", "the Hong Kong timestamp was parsed incorrectly");
    Assert(tencentSnapshot.Quotes[2].DisplayName == "苹果", "the US alias was not applied");
    Assert(Math.Abs(tencentSnapshot.Quotes[2].ChangePercent - 0.29) < 0.001, "the US change percent was parsed incorrectly");
    Assert(!tencentSnapshot.RedForGain, "the Tencent color preference was not preserved");
    var tencentCached = await tencentSource.ReadAsync(tencentSettings);
    Assert(ReferenceEquals(tencentSnapshot, tencentCached), "Tencent quotes should use the fifteen-minute cache");
    Assert(tencentHandler.RequestCount == 1, "a cached Tencent read must not call the API again");
    var tencentFrame = renderer.Render(themes.Single(theme => theme.Id == "stocks"), SystemSnapshot.DesignSample with { Stocks = tencentSnapshot });
    Assert(tencentFrame.JpegBytes is [0xFF, 0xD8, ..], "the Tencent quote view did not render");
}

string tencentKline = """
{"code":0,"data":{"sh600519":{"qfqday":[["2026-08-03","1350.600","1358.980","1363.350","1346.000","36147"],["2026-08-04","1350.060","1328.360","1350.940","1328.360","37450"],["2026-08-05","1328.360","1306.450","1333.800","1303.500","42689"],["2026-08-06","1310.000","1308.550","1314.400","1300.010","25463"],["2026-08-07","1308.660","1309.220","1315.280","1301.000","24976"]]},"sz301707":{"day":[["2026-08-07","108.000","116.520","129.900","106.880","214773"]]},"hk00700":{"day":[["2026-08-03","482.000","490.400","491.000","480.600","38649910",{"cqr":"2026-08-03","FHcontent":"","HGcontent":"回购178.80万股","paixiri":"","hgcgContent":"","ggContent":""}],["2026-08-04","491.000","487.600","494.800","480.800","24860019"],["2026-08-05","493.400","492.200","497.800","482.200","25662478"],["2026-08-06","491.000","479.200","491.000","479.000","21801977"],["2026-08-07","479.000","478.800","483.200","475.400","16319939"]]},"usAAPL.OQ":{"day":[["2026-08-03","309.580","303.420","311.800","302.560","75051951"],["2026-08-04","302.730","309.380","310.420","301.320","68000969"],["2026-08-05","309.360","311.000","311.710","305.670","49438763"],["2026-08-06","314.340","312.410","316.290","309.230","46139901"],["2026-08-07","311.450","313.330","314.810","310.740","34437191"]]}}}
""";
var trendHandler = new TencentStockHandler(tencentSample, tencentKline);
using (var trendClient = new HttpClient(trendHandler))
using (var trendSource = new TencentStockSnapshotSource(trendClient))
{
    var trendSettings = new StockSettings
    {
        Items =
        [
            new StockItemSettings { Symbol = "600519.SS" },
            new StockItemSettings { Symbol = "0700.HK" }
        ]
    };
    var trendSnapshot = await trendSource.ReadAsync(trendSettings);
    Assert(trendHandler.KlineRequests == 2, "exactly two A/HK symbols should trigger two daily-candle requests");
    Assert(trendHandler.KlineQueries.All(uri => uri.Contains("fqkline") && uri.Contains("param=")), "the daily-candle request parameters are wrong");
    Assert(trendSnapshot.Quotes.Count == 2, "both quotes should be parsed");
    var aCloses = trendSnapshot.Quotes[0].FiveDayCloses;
    var hkCloses = trendSnapshot.Quotes[1].FiveDayCloses;
    Assert(aCloses is { Count: 5 } && Math.Abs(aCloses[^1] - 1309.22) < 0.001, "the A-share qfqday trend was not parsed");
    Assert(hkCloses is { Count: 5 } && Math.Abs(hkCloses[^1] - 478.8) < 0.001, "the Hong Kong day trend was not parsed");
}

var usTrendHandler = new TencentStockHandler(tencentSample, tencentKline);
using (var usTrendClient = new HttpClient(usTrendHandler))
using (var usTrendSource = new TencentStockSnapshotSource(usTrendClient))
{
    var usTrendSnapshot = await usTrendSource.ReadAsync(new StockSettings
    {
        Items =
        [
            new StockItemSettings { Symbol = "600519.SS" },
            new StockItemSettings { Symbol = "AAPL" }
        ]
    });
    Assert(usTrendHandler.KlineRequests == 2, "two symbols including a US one should trigger two daily-candle requests");
    Assert(usTrendHandler.KlineQueries.Any(uri => uri.Contains("usAAPL.OQ")), "US daily candles must use the .OQ form");
    Assert(usTrendSnapshot.Quotes[1].FiveDayCloses is { Count: 5 } usCloses && Math.Abs(usCloses[^1] - 313.33) < 0.001, "the US .OQ trend was not parsed");
}

var newStockHandler = new TencentStockHandler(tencentSample, tencentKline);
using (var newStockClient = new HttpClient(newStockHandler))
using (var newStockSource = new TencentStockSnapshotSource(newStockClient))
{
    var newStockSnapshot = await newStockSource.ReadAsync(new StockSettings
    {
        Items =
        [
            new StockItemSettings { Symbol = "600519.SS" },
            new StockItemSettings { Symbol = "301707.SZ" }
        ]
    });
    Assert(newStockHandler.KlineRequests == 2, "two A-shares should trigger two daily-candle requests");
    Assert(newStockSnapshot.Quotes.Count == 2, "both A-share quotes should be parsed");
    Assert(newStockSnapshot.Quotes[0].FiveDayCloses is { Count: 5 }, "the established symbol's five-day trend was not parsed");
    Assert(newStockSnapshot.Quotes[1].FiveDayCloses is { Count: 1 }, "a first-day listing should keep a single close sample");
    var newStockFrame = renderer.Render(themes.Single(theme => theme.Id == "stocks"),
        SystemSnapshot.DesignSample with { Stocks = newStockSnapshot });
    Assert(newStockFrame.JpegBytes is [0xFF, 0xD8, ..], "the two-symbol view with a first-day listing did not render");
}

var twoTrendStocks = new StockSnapshot(
    [
        new StockQuoteSnapshot("600519.SS", "贵州茅台", 1309.22, 0.05, DateTimeOffset.Now, [1358.98, 1328.36, 1306.45, 1308.55, 1309.22]),
        new StockQuoteSnapshot("0700.HK", "腾讯控股", 478.80, -0.08, DateTimeOffset.Now, [490.4, 487.6, 492.2, 479.2, 478.8])
    ],
    DateTimeOffset.Now,
    RedForGain: true);
var twoTrendFrame = renderer.Render(themes.Single(theme => theme.Id == "stocks"), SystemSnapshot.DesignSample with { Stocks = twoTrendStocks });
Assert(twoTrendFrame.JpegBytes is [0xFF, 0xD8, ..], "the two-symbol trend view did not render");

var fiveStocks = new StockSnapshot(
    [
        new StockQuoteSnapshot("S1", "股票一", 11.0, 3.5, DateTimeOffset.Now),
        new StockQuoteSnapshot("S2", "股票二", 12.0, -2.5, DateTimeOffset.Now),
        new StockQuoteSnapshot("S3", "股票三", 13.0, 1.5, DateTimeOffset.Now),
        new StockQuoteSnapshot("S4", "股票四", 14.0, -1.2, DateTimeOffset.Now),
        new StockQuoteSnapshot("S5", "股票五", 15.0, 0.8, DateTimeOffset.Now)
    ],
    DateTimeOffset.Now,
    RedForGain: true);
var fiveStocksFrame = renderer.Render(themes.Single(theme => theme.Id == "stocks"), SystemSnapshot.DesignSample with { Stocks = fiveStocks });
Assert(fiveStocksFrame.JpegBytes is [0xFF, 0xD8, ..], "the five-symbol view did not render");

var extremeStocks = new StockSnapshot(
    [new StockQuoteSnapshot("X", "极端涨幅", 49.69, 396.89, DateTimeOffset.Now)],
    DateTimeOffset.Now,
    RedForGain: true);
var extremeFrame = renderer.Render(themes.Single(theme => theme.Id == "stocks"), SystemSnapshot.DesignSample with { Stocks = extremeStocks });
Assert(extremeFrame.JpegBytes is [0xFF, 0xD8, ..] && extremeFrame.JpegBytes.Length <= profile.MaxJpegBytes, "the extreme 396.89% view did not render or exceeded the size limit");

using (var partialClient = new HttpClient(new GbkHandler("v_usAAPL=\"200~苹果~AAPL.OQ~313.33~312.41~311.45~0~0~0~313.13~0~0~0~0~0~0~0~0~0~0~0~0~0~0~0~0~0~0~0~~2026-08-07 16:00:01~0.92~0.29~314.81~310.74\";")))
using (var partialSource = new TencentStockSnapshotSource(partialClient))
{
    var partialSnapshot = await partialSource.ReadAsync(new StockSettings
    {
        Items =
        [
            new StockItemSettings { Symbol = "BTC-USD" },
            new StockItemSettings { Symbol = "AAPL" }
        ]
    });
    Assert(partialSnapshot.Quotes.Count == 1 && partialSnapshot.Quotes[0].Symbol == "AAPL", "unsupported symbols must be skipped while the rest still parse");
}

using (var emptyClient = new HttpClient(new GbkHandler("v_sz000001=\"51~平安银行~000001~11.19~11.27~11.23\";")))
using (var emptySource = new TencentStockSnapshotSource(emptyClient))
{
    var failedSnapshot = await emptySource.ReadAsync(new StockSettings
    {
        Items = [new StockItemSettings { Symbol = "600519.SS" }]
    });
    Assert(failedSnapshot.Quotes.Count == 0 && !string.IsNullOrWhiteSpace(failedSnapshot.ErrorMessage), "an all-failed read must return an error snapshot");
}
Console.WriteLine("PASS Tencent GBK parser, symbol mapping, skip/fallback and cache");

var imageTheme = (ImageTheme)themes.Single(theme => theme.Id == "image");
imageTheme.ImagePath = Path.Combine(Directory.GetCurrentDirectory(), "docs", "images", "keyboard-screen-studio-hero.png");
foreach (ImageClockStyle clockStyle in Enum.GetValues<ImageClockStyle>())
{
    var customLayoutOptions = new ScreenDisplayOptions(
        ImageTimePlacement.Top,
        DotMatrixProgressPeriod.Today,
        clockStyle,
        ImageTimeBackground: true,
        ImageTextColor.White,
        ImageTextAlignment.Right,
        ImageWeatherVisible: true,
        ImageTimeFontSize: 34,
        ImageDateFontSize: 15,
        ImageWeatherFontSize: 13,
        ImageDigitalOrder: ImageDigitalOrder.WeatherTimeDate,
        ImageLargeTimeFontSize: 43,
        ImageAnalogClockSize: 96,
        ImageAnalogOrder: ImageAnalogOrder.WeatherClockDate,
        ImageFlipTimeFontSize: 36);
    var imageTimeFrame = renderer.Render(imageTheme, SystemSnapshot.DesignSample, displayOptions: customLayoutOptions);
    Assert(imageTimeFrame.JpegBytes is [0xFF, 0xD8, ..], $"image {clockStyle} clock option did not render");
    Assert(imageTimeFrame.JpegBytes.Length <= profile.MaxJpegBytes, $"image {clockStyle} clock exceeded device limit");
}
Console.WriteLine("PASS image digital/large/analog/flip clocks with integer sizing, ordering and weather render");

var maxQualityFrame = renderer.Render(themes[0], SystemSnapshot.DesignSample, jpegQuality: 100);
Assert(maxQualityFrame.JpegBytes.Length <= profile.MaxJpegBytes, "highest-quality JPEG exceeded device limit");
Console.WriteLine("PASS fixed highest JPEG quality stays within device limit");

var usageOptions = TokscaleDataSource.ParseUsage(
    """
    [{"provider":"Codex","account":"person@example.com","plan":"Plus","metrics":[{"label":"Weekly","used_percent":83,"remaining_percent":17,"resets_at":"2026-08-09T11:07:00Z"}]}]
    """);
Assert(usageOptions.Count == 1, "Tokscale usage JSON was not parsed");
Assert(Math.Abs(usageOptions[0].RemainingPercent!.Value - 17d) < 0.001, "Tokscale remaining percentage is incorrect");

var modelOptions = TokscaleDataSource.ParseModels(
    """
    {"groupBy":"client,provider,model","entries":[{"client":"WorkBuddy","provider":"DeepSeek","model":"deepseek-v4-flash","input":124700000,"output":322000,"cacheRead":123900000,"cacheWrite":0,"cost":17.89}]}
    """);
Assert(modelOptions.Count == 1, "Tokscale model JSON was not parsed");
Assert(modelOptions[0].Tokens == 248_922_000m, "Tokscale total tokens are incorrect");
Assert(modelOptions[0].Cost == 17.89m, "Tokscale model cost is incorrect");

var tokScaleCatalog = new TokscaleCatalog(TokscaleStatus.Ready, modelOptions, "ready", DateTimeOffset.Now);
var tokScaleSnapshot = TokscaleDataSource.CreateSnapshot(
    new AiQuotaSettings
    {
        DataKind = AiUsageDataKind.ModelTokens,
        SelectedItemKey = modelOptions[0].Key,
        DisplayName = "DeepSeek",
        ProgressTarget = 500_000_000m
    },
    tokScaleCatalog);
Assert(tokScaleSnapshot.Available && tokScaleSnapshot.PlatformName == "DeepSeek", "Tokscale snapshot selection failed");
Assert(tokScaleSnapshot.PrimaryDisplay == "248.9M", "Tokscale compact token display is incorrect");
Assert(Math.Abs(tokScaleSnapshot.ClampedRemainingPercent - 49.7844) < 0.001, "Tokscale target progress is incorrect");
Console.WriteLine("PASS Tokscale usage/model JSON parsing and snapshot mapping");

if (args.Contains("--tokscale-live", StringComparer.OrdinalIgnoreCase))
{
    TokscaleCatalog liveCatalog = await new TokscaleDataSource().ReadCatalogAsync(force: true);
    Assert(liveCatalog.Status != TokscaleStatus.NotInstalled, "installed Tokscale was not discovered");
    Console.WriteLine($"PASS live Tokscale discovery status={liveCatalog.Status}, items={liveCatalog.Options.Count}");
}

var aiPreviewArgumentIndex = Array.IndexOf(args, "--ai-preview");
if (aiPreviewArgumentIndex >= 0)
{
    Assert(aiPreviewArgumentIndex + 1 < args.Length, "--ai-preview requires an output path");
    var previewPath = Path.GetFullPath(args[aiPreviewArgumentIndex + 1]);
    Directory.CreateDirectory(Path.GetDirectoryName(previewPath)!);
    await File.WriteAllBytesAsync(previewPath, subscriptionFrame.JpegBytes);
    Console.WriteLine($"PASS wrote AI quota preview to {previewPath}");
}

var handler = new RecordingHandler();
using var client = new HttpClient(handler);
using var transport = new HttpImageDeviceTransport(client);
var testFrame = renderer.Render(themes[0], SystemSnapshot.DesignSample);
var pushResult = await transport.PushAsync(new Uri("http://device.test/image/upload"), testFrame);
Assert(pushResult.Success, "transport should report success");
Assert(handler.LastMethod == HttpMethod.Post, "transport must use POST");
Assert(handler.LastContentType == "image/jpeg", "transport must send image/jpeg");
Assert(handler.LastBody?.SequenceEqual(testFrame.JpegBytes) == true, "transport body must contain exact JPEG bytes");
Console.WriteLine("PASS transport POST image/jpeg with exact frame bytes");

if (OperatingSystem.IsWindows())
{
    var probe = new TcpListener(IPAddress.Loopback, 0);
    probe.Start();
    int loopbackPort = ((IPEndPoint)probe.LocalEndpoint).Port;
    probe.Stop();

    using var httpListener = new HttpListener();
    httpListener.Prefixes.Add($"http://127.0.0.1:{loopbackPort}/");
    httpListener.Start();
    var receiveTask = Task.Run(async () =>
    {
        HttpListenerContext context = await httpListener.GetContextAsync();
        using var bodyStream = new MemoryStream();
        await context.Request.InputStream.CopyToAsync(bodyStream);
        byte[] body = bodyStream.ToArray();
        context.Response.StatusCode = 200;
        context.Response.Close();
        return (context.Request.HttpMethod, context.Request.ContentType, body);
    });

    using var realTransport = new HttpImageDeviceTransport();
    var loopbackResult = await realTransport.PushAsync(
        new Uri($"http://127.0.0.1:{loopbackPort}/image/upload"),
        testFrame);
    var (loopbackMethod, loopbackContentType, loopbackBody) = await receiveTask;
    Assert(loopbackResult.Success, "real loopback device push must succeed");
    Assert(loopbackMethod == "POST" && loopbackContentType == "image/jpeg", "real loopback must receive image/jpeg POST");
    Assert(loopbackBody.AsSpan().SequenceEqual(testFrame.JpegBytes), "real loopback must receive exact JPEG bytes");
    Console.WriteLine("PASS real HTTP loopback device push");
}

var settingsPath = Path.Combine(Path.GetTempPath(), $"keyboard-screen-settings-{Guid.NewGuid():N}.json");
try
{
    var settingsStore = new JsonSettingsStore(settingsPath);
    var settings = new AppSettings { SelectedThemeId = "music", RefreshSeconds = 17, AccentColor = "#A23BFF", SelectedFontId = "file:test.ttf|test", SafeArea = new ScreenInsets(11, 53, 9, 13), AiQuota = new AiQuotaSettings { DataKind = AiUsageDataKind.ModelCost, SelectedItemKey = "model:test", DisplayName = "My AI", ProgressTarget = 25 }, Weather = new WeatherSettings { LocationQuery = "上海", UseAutomaticLocation = true }, Stocks = new StockSettings { SourceKind = StockSourceKind.Yahoo, RedForGain = false, Items = [new StockItemSettings { Symbol = "0700.HK", Alias = "腾讯", Enabled = false }] }, ImageTimePlacement = ImageTimePlacement.Top, ImageClockStyle = ImageClockStyle.Flip, ImageTimeBackground = false, ImageTextColor = ImageTextColor.Black, ImageTextAlignment = ImageTextAlignment.Right, ImageWeatherVisible = true, ImageTimeFontSize = 34, ImageDateFontSize = 15, ImageWeatherFontSize = 13, ImageDigitalOrder = ImageDigitalOrder.WeatherTimeDate, ImageLargeTimeFontSize = 42, ImageAnalogClockSize = 94, ImageAnalogOrder = ImageAnalogOrder.DateWeatherClock, ImageFlipTimeFontSize = 35, IgnoreBrowserMediaSessions = false, UiThemeMode = UiThemeMode.Dark, Language = "uk", ClaudeUsage = new ClaudeUsageSettings { SessionKey = "sk-ant-persist", OrganizationId = "org-7", ModelScope = "Fable", CountLocalTokens = false }, HasAcknowledgedClaudeNotice = true, DotMatrixProgressPeriod = DotMatrixProgressPeriod.Quarter, DotMatrixProgressHeaderFontSize = 18, LaunchAtStartup = true, AutoMediaThemeSwitch = true, MediaPlayingThemeId = "music-poster", MediaIdleThemeId = "clock-neon" , HasCompletedOnboarding = true, HasAcknowledgedStockNotice = true, HasAcknowledgedAiUsageNotice = true };
    await settingsStore.SaveAsync(settings);
    var loadedSettings = await settingsStore.LoadAsync();
    Assert(loadedSettings.SelectedThemeId == "music", "settings theme did not persist");
    Assert(loadedSettings.RefreshSeconds == 17, "settings refresh interval did not persist");
    Assert(loadedSettings.AccentColor == "#A23BFF", "settings accent color did not persist");
    Assert(loadedSettings.SelectedFontId == "file:test.ttf|test", "settings font did not persist");
    Assert(loadedSettings.SafeArea == settings.SafeArea, "settings safe area did not persist");
    Assert(loadedSettings.AiQuota.DisplayName == "My AI" && loadedSettings.AiQuota.DataKind == AiUsageDataKind.ModelCost && loadedSettings.AiQuota.ProgressTarget == 25, "AI Tokscale settings did not persist");
    Assert(loadedSettings.Weather.LocationQuery == "上海" && loadedSettings.Weather.UseAutomaticLocation, "weather location settings did not persist");
    Assert(loadedSettings.LaunchAtStartup, "launch-at-startup setting did not persist");
    Assert(loadedSettings.HasCompletedOnboarding, "onboarding completion did not persist");
    Assert(loadedSettings.HasAcknowledgedStockNotice && loadedSettings.HasAcknowledgedAiUsageNotice,
        "feature notice acknowledgements did not persist");
    Assert(!loadedSettings.Stocks.RedForGain && loadedSettings.Stocks.SourceKind == StockSourceKind.Yahoo && loadedSettings.Stocks.Items[0].Alias == "腾讯" && !loadedSettings.Stocks.Items[0].Enabled, "stock settings did not persist");
    Assert(loadedSettings.ImageTimePlacement == ImageTimePlacement.Top, "image time placement did not persist");
    Assert(loadedSettings.ImageClockStyle == ImageClockStyle.Flip && !loadedSettings.ImageTimeBackground, "image clock style/background did not persist");
    Assert(loadedSettings.ImageTextColor == ImageTextColor.Black && loadedSettings.ImageTextAlignment == ImageTextAlignment.Right, "image text options did not persist");
    Assert(loadedSettings.ImageWeatherVisible, "image weather visibility did not persist");
    Assert(loadedSettings.ImageTimeFontSize == 34 && loadedSettings.ImageDateFontSize == 15 && loadedSettings.ImageWeatherFontSize == 13, "image font sizes did not persist");
    Assert(loadedSettings.ImageDigitalOrder == ImageDigitalOrder.WeatherTimeDate, "image digital order did not persist");
    Assert(loadedSettings.ImageLargeTimeFontSize == 42 && loadedSettings.ImageAnalogClockSize == 94 && loadedSettings.ImageFlipTimeFontSize == 35, "image style-specific sizes did not persist");
    Assert(loadedSettings.ImageAnalogOrder == ImageAnalogOrder.DateWeatherClock, "image analog order did not persist");
    Assert(!loadedSettings.IgnoreBrowserMediaSessions, "browser media filter setting did not persist");
    Assert(loadedSettings.UiThemeMode == UiThemeMode.Dark, "control UI theme mode did not persist");
    Assert(loadedSettings.Language == "uk" && AppLanguageInfo.Parse(loadedSettings.Language) == AppLanguage.Ukrainian, "language setting did not persist");
    Assert(loadedSettings.ClaudeUsage.SessionKey == "sk-ant-persist"
        && loadedSettings.ClaudeUsage.OrganizationId == "org-7"
        && loadedSettings.ClaudeUsage.ModelScope == "Fable"
        && !loadedSettings.ClaudeUsage.CountLocalTokens, "Claude usage settings did not persist");
    Assert(loadedSettings.HasAcknowledgedClaudeNotice, "the Claude notice acknowledgement did not persist");
    Assert(loadedSettings.DotMatrixProgressPeriod == DotMatrixProgressPeriod.Quarter, "dot-matrix progress period did not persist");
    Assert(loadedSettings.DotMatrixProgressHeaderFontSize == 18, "dot-matrix progress header font size did not persist");
    Assert(loadedSettings.AutoMediaThemeSwitch, "media theme automation flag did not persist");
    Assert(loadedSettings.MediaPlayingThemeId == "music-poster", "playing theme did not persist");
    Assert(loadedSettings.MediaIdleThemeId == "clock-neon", "idle theme did not persist");
    Console.WriteLine("PASS settings JSON round-trip");
}
finally
{
    if (File.Exists(settingsPath)) File.Delete(settingsPath);
}

var concurrentSettingsPath = Path.Combine(Path.GetTempPath(), $"keyboard-screen-concurrent-{Guid.NewGuid():N}.json");
try
{
    var concurrentStore = new JsonSettingsStore(concurrentSettingsPath);
    var concurrentTasks = new List<Task>();
    for (int index = 0; index < 10; index++)
    {
        int refreshSeconds = index + 1;
        concurrentTasks.Add(concurrentStore.SaveAsync(new AppSettings { RefreshSeconds = refreshSeconds }));
        concurrentTasks.Add(concurrentStore.LoadAsync());
    }
    await Task.WhenAll(concurrentTasks);
    var concurrentLoaded = await concurrentStore.LoadAsync();
    Assert(concurrentLoaded.RefreshSeconds is >= 1 and <= 10, "concurrent settings must remain loadable");
    Console.WriteLine("PASS settings concurrent save/load");
}
finally
{
    if (File.Exists(concurrentSettingsPath)) File.Delete(concurrentSettingsPath);
}

Assert(ReleaseUpdateChecker.TryParseTagVersion("v1.1.2") == new Version(1, 1, 2), "tag version parsing failed");
Assert(ReleaseUpdateChecker.TryParseTagVersion("not-a-version") is null, "invalid tag must not parse");
Assert(ReleaseUpdateChecker.IsNewerThan("v1.0.3", new Version(1, 0, 2)), "newer tag must be detected");
Assert(!ReleaseUpdateChecker.IsNewerThan("v1.0.2", new Version(1, 0, 2)), "same tag must not be newer");
Console.WriteLine("PASS release update version parsing and comparison");

var perfVisualTheme = (PerformanceVisualTheme)themes.Single(theme => theme.Id == "performance-visual");
perfVisualTheme.SetModulesEnabled(cpu: false, memory: true, download: true, upload: false, gpu: false);
var partialPerfFrame = renderer.Render(perfVisualTheme, SystemSnapshot.DesignSample);
Assert(partialPerfFrame.JpegBytes is [0xFF, 0xD8, ..], "performance-visual theme with partial modules did not render");
perfVisualTheme.SetModulesEnabled(cpu: true, memory: true, download: true, upload: true, gpu: true);
Assert(SystemSnapshot.DesignSample.GpuPercent is double, "design sample must carry a GPU sample");
Console.WriteLine("PASS performance-visual module toggles and GPU sample");

var perfSettingsPath = Path.Combine(Path.GetTempPath(), $"keyboard-screen-perf-settings-{Guid.NewGuid():N}.json");
try
{
    var perfStore = new JsonSettingsStore(perfSettingsPath);
    var perfSettings = new AppSettings { PerfVisualUploadEnabled = false, PerfVisualGpuEnabled = false };
    await perfStore.SaveAsync(perfSettings);
    var perfLoaded = await perfStore.LoadAsync();
    Assert(!perfLoaded.PerfVisualUploadEnabled && !perfLoaded.PerfVisualGpuEnabled, "performance-visual module toggles did not persist");
    Console.WriteLine("PASS performance-visual settings persistence");
}
finally
{
    if (File.Exists(perfSettingsPath)) File.Delete(perfSettingsPath);
}

var idleMusicSnapshot = SystemSnapshot.DesignSample with { Music = MusicSnapshot.Unavailable };
foreach (string musicThemeId in new[] { "music", "music-minimal", "music-poster" })
{
    var idleMusicFrame = renderer.Render(themes.Single(theme => theme.Id == musicThemeId), idleMusicSnapshot);
    Assert(idleMusicFrame.JpegBytes is [0xFF, 0xD8, ..], $"{musicThemeId} idle music must render");
    Assert(idleMusicFrame.JpegBytes.Length <= profile.MaxJpegBytes, $"{musicThemeId} idle music exceeded device limit");
}
Console.WriteLine("PASS music themes idle rendering");

var mediaAutomation = new AppSettings
{
    AutoMediaThemeSwitch = true,
    MediaPlayingThemeId = "music-minimal",
    MediaIdleThemeId = "dashboard"
};
Assert(MediaThemeAutomation.IsMusicThemeId("music-poster"), "music poster must be classified as a music theme");
Assert(!MediaThemeAutomation.IsMusicThemeId("dashboard"), "dashboard must not be classified as a music theme");
Assert(MediaThemeAutomation.ResolveThemeId(mediaAutomation, true, "clock") == "music-minimal", "playing media theme resolution failed");
Assert(MediaThemeAutomation.ResolveThemeId(mediaAutomation, false, "clock") == "dashboard", "idle media theme resolution failed");
mediaAutomation.MediaPlayingThemeId = "clock";
mediaAutomation.MediaIdleThemeId = "music";
Assert(MediaThemeAutomation.ResolveThemeId(mediaAutomation, true, "clock") == "music", "invalid playing theme must fall back to music");
Assert(MediaThemeAutomation.ResolveThemeId(mediaAutomation, false, "clock") == "system", "invalid idle theme must fall back to system");
Console.WriteLine("PASS bidirectional media theme automation");

Assert(WindowsMusicSnapshotSource.IsBrowserSessionId("Chrome.exe"), "Chrome session should be identified as browser media");
Assert(WindowsMusicSnapshotSource.IsBrowserSessionId("Microsoft.MicrosoftEdge_8wekyb3d8bbwe!MSEdge"), "Edge session should be identified as browser media");
Assert(!WindowsMusicSnapshotSource.IsBrowserSessionId("Spotify.exe"), "Spotify must not be identified as browser media");
Console.WriteLine("PASS browser media session identification");
var fontTestFolder = Path.Combine(Path.GetTempPath(), $"keyboard-screen-fonts-{Guid.NewGuid():N}");
Directory.CreateDirectory(fontTestFolder);
try
{
    using var emptyCatalog = new FontFolderCatalog(fontTestFolder);
    var initialFonts = emptyCatalog.Scan();
    Assert(initialFonts.Count == 1 && initialFonts[0].IsBuiltIn, "empty font folder must expose the built-in fallback");

    if (OperatingSystem.IsWindows())
    {
        var systemFont = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Fonts", "segoeui.ttf");
        if (File.Exists(systemFont))
        {
            var fontChangeDetected = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            emptyCatalog.FontsChanged += (_, _) => fontChangeDetected.TrySetResult();
            File.Copy(systemFont, Path.Combine(fontTestFolder, "test-font.ttf"));
            var watcherResult = await Task.WhenAny(fontChangeDetected.Task, Task.Delay(3000));
            Assert(watcherResult == fontChangeDetected.Task, "font folder watcher did not report the new font");
            var discoveredFonts = emptyCatalog.Scan();
            Assert(discoveredFonts.Count >= 2, "TTF file was not discovered");
            var customFont = discoveredFonts.First(font => !font.IsBuiltIn);
            var customFontFrame = renderer.Render(themes[0], SystemSnapshot.DesignSample, fontFamily: customFont.FontFamily);
            Assert(customFontFrame.JpegBytes is [0xFF, 0xD8, ..], "custom font preview did not render");
            var customFontNetworkFrame = renderer.Render(themes.Single(theme => theme.Id == "network"), SystemSnapshot.DesignSample, fontFamily: customFont.FontFamily);
            Assert(customFontNetworkFrame.JpegBytes is [0xFF, 0xD8, ..], "ink-centered network summary did not render with a custom font");
            Console.WriteLine($"PASS font folder watch, scan and ink-centered network render {customFont.DisplayName}");
        }
    }
}
finally
{
    Directory.Delete(fontTestFolder, true);
}

var endpointArgumentIndex = Array.IndexOf(args, "--endpoint");
if (endpointArgumentIndex >= 0)
{
    Assert(endpointArgumentIndex + 1 < args.Length, "--endpoint requires a URL");
    Assert(Uri.TryCreate(args[endpointArgumentIndex + 1], UriKind.Absolute, out var parsedEndpoint), "--endpoint must be an absolute URL");
    var endpoint = parsedEndpoint!;
    using var liveTransport = new HttpImageDeviceTransport();
    var liveFrame = renderer.Render(themes[2], SystemSnapshot.DesignSample);
    var liveResult = await liveTransport.PushAsync(endpoint, liveFrame);
    Assert(liveResult.Success, $"live device push failed: {liveResult.Message}");
    Console.WriteLine($"PASS live device HTTP {liveResult.StatusCode} in {liveResult.Elapsed.TotalMilliseconds:0} ms");
}

if (OperatingSystem.IsWindows())
{
    var source = new WindowsSystemSnapshotSource();
    _ = await source.ReadAsync();
    await Task.Delay(120);
    var snapshot = await source.ReadAsync();
    Assert(snapshot.CpuPercent is >= 0 and <= 100, "CPU must be within 0..100");
    Assert(snapshot.MemoryPercent is >= 0 and <= 100, "memory must be within 0..100");
    Console.WriteLine($"PASS system source CPU={snapshot.CpuPercent:0.0}% MEM={snapshot.MemoryPercent:0.0}%");
}

// ---- Claude usage --------------------------------------------------------
// Legacy payload shape: the per-model weekly window is its own seven_day_* key.
var claudeLegacy = new Queue<string>(new[]
{
    """[{"uuid":"org-123","name":"Personal","capabilities":["chat"]}]""",
    """{"five_hour":{"utilization":42,"resets_at":"2026-08-21T21:59:59Z"},"seven_day":{"utilization":"73%","resets_at":"2026-08-25T16:59:59Z"},"seven_day_opus":{"utilization":5,"resets_at":null},"seven_day_fable":{"utilization":91.5,"resets_at":"2026-08-25T16:59:59Z"}}"""
});
var claudeHandler = new ClaudeHandler(claudeLegacy);
using (var claudeClient = new HttpClient(claudeHandler))
using (var claudeSource = new ClaudeUsageSnapshotSource(claudeClient, new ClaudeCodeTokenReader(Path.Combine(Path.GetTempPath(), "kss-no-transcripts")))
       { BaseUrl = "https://claude.test/api" })
{
    var claudeSettings = new ClaudeUsageSettings { SessionKey = "sk-ant-test" };
    var claudeSnapshot = await claudeSource.ReadAsync(claudeSettings);
    Assert(claudeSnapshot.Available, "Claude usage snapshot must be available");
    Assert(claudeSettings.OrganizationId == "org-123", "the organization id should be resolved and cached");
    Assert(claudeHandler.Requests.Count == 2, "one organization lookup plus one usage read");
    Assert(claudeHandler.Requests[1].Contains("/organizations/org-123/usage"), "usage must be read for the resolved org");
    Assert(claudeHandler.Cookies.All(cookie => cookie.Contains("sessionKey=sk-ant-test")), "every request must carry the session cookie");
    Assert(Math.Abs(claudeSnapshot.Session!.UtilizationPercent - 42) < 0.001, "session utilization was not parsed");
    Assert(Math.Abs(claudeSnapshot.Week!.UtilizationPercent - 73) < 0.001, "a percent string like \"73%\" must parse");
    Assert(Math.Abs(claudeSnapshot.ModelWeek!.UtilizationPercent - 91.5) < 0.001, "the legacy seven_day_fable window was not parsed");
    Assert(claudeSnapshot.ModelWeek.ScopeName == "Fable", "the model window keeps its scope name");
    Assert(claudeSnapshot.Session.ResetsAt is not null && claudeSnapshot.Week.ResetsAt is not null, "reset times were not parsed");
    Assert(claudeSnapshot.Windows.Count() == 3, "all three windows should be present");

    var cached = await claudeSource.ReadAsync(claudeSettings);
    Assert(ReferenceEquals(claudeSnapshot, cached), "a second read inside the cache window must not call the API");
    Assert(claudeHandler.Requests.Count == 2, "the cached read issued extra requests");
}

// Newer payload shape: seven_day_* per-model keys are nulled out and the real
// figure arrives in limits[]. The array must win.
var claudeModern = new Queue<string>(new[]
{
    """{"five_hour":{"utilization":10,"resets_at":"2026-08-21T21:59:59Z"},"seven_day":{"utilization":20,"resets_at":"2026-08-25T16:59:59Z"},"seven_day_fable":{"utilization":0,"resets_at":null},"limits":[{"kind":"weekly_scoped","percent":64,"resets_at":"2026-08-25T16:59:59Z","scope":{"model":{"id":null,"display_name":"Fable"}}},{"kind":"weekly_scoped","percent":3,"resets_at":null,"scope":{"model":{"id":"claude-opus-5","display_name":"Opus"}}}]}"""
});
using (var modernClient = new HttpClient(new ClaudeHandler(claudeModern)))
using (var modernSource = new ClaudeUsageSnapshotSource(modernClient, new ClaudeCodeTokenReader(Path.Combine(Path.GetTempPath(), "kss-no-transcripts")))
       { BaseUrl = "https://claude.test/api" })
{
    var modernSnapshot = await modernSource.ReadAsync(
        new ClaudeUsageSettings { SessionKey = "sk-ant-test", OrganizationId = "org-9" });
    Assert(Math.Abs(modernSnapshot.ModelWeek!.UtilizationPercent - 64) < 0.001, "limits[] must override the nulled legacy key");
    Assert(modernSnapshot.ModelWeek.ScopeName == "Fable", "the scoped window should be the requested model");
}

// An unconfigured source reports unavailable without touching the network.
using (var idleSource = new ClaudeUsageSnapshotSource(new HttpClient(new ClaudeHandler(new Queue<string>()))))
{
    var idle = await idleSource.ReadAsync(new ClaudeUsageSettings());
    Assert(!idle.Available, "a source with no session key must not be available");
}

// Local token counting: only records inside the window count, and the model
// window only counts records from that model.
var transcriptRoot = Path.Combine(Path.GetTempPath(), $"kss-claude-{Guid.NewGuid():N}", "projects", "demo");
Directory.CreateDirectory(transcriptRoot);
try
{
    var recent = DateTimeOffset.Now.AddMinutes(-30).ToString("o");
    var midWeek = DateTimeOffset.Now.AddDays(-3).ToString("o");
    var tooOld = DateTimeOffset.Now.AddDays(-30).ToString("o");
    // Built by concatenation: the payload's own braces make an interpolated raw
    // string literal ambiguous.
    static string Record(string stamp, string model, string usage) =>
        "{\"type\":\"assistant\",\"timestamp\":\"" + stamp + "\",\"message\":{\"model\":\"" + model
        + "\",\"usage\":{" + usage + "}}}";

    await File.WriteAllLinesAsync(Path.Combine(transcriptRoot, "session.jsonl"),
    [
        Record(recent, "claude-fable-5",
            "\"input_tokens\":100,\"output_tokens\":50,\"cache_creation_input_tokens\":10,\"cache_read_input_tokens\":900000"),
        Record(midWeek, "claude-opus-5",
            "\"input_tokens\":200,\"output_tokens\":100,\"cache_creation_input_tokens\":0"),
        Record(tooOld, "claude-fable-5", "\"input_tokens\":9999,\"output_tokens\":9999"),
        """{"type":"user","message":{"content":"no usage block here"}}"""
    ]);

    var reader = new ClaudeCodeTokenReader(Path.Combine(transcriptRoot, ".."));
    var totals = reader.Read(DateTimeOffset.Now.AddHours(-5), DateTimeOffset.Now.AddDays(-7), "fable");
    Assert(totals.Available, "the reader should have found transcripts");
    Assert(totals.Session == 160, $"the 5h window should count only the recent record, got {totals.Session}");
    Assert(totals.Week == 460, $"the weekly window should skip the 30-day-old record, got {totals.Week}");
    Assert(totals.ModelWeek == 160, $"the model window should count Fable only, got {totals.ModelWeek}");

    var noScope = reader.Read(DateTimeOffset.Now.AddHours(-5), DateTimeOffset.Now.AddDays(-7), null);
    Assert(noScope.ModelWeek == 0, "no model scope means no model total");
}
finally
{
    try { Directory.Delete(Path.GetDirectoryName(transcriptRoot)!, recursive: true); } catch (IOException) { }
}

var missingReader = new ClaudeCodeTokenReader(Path.Combine(Path.GetTempPath(), $"kss-absent-{Guid.NewGuid():N}"));
Assert(!missingReader.Read(DateTimeOffset.Now.AddHours(-5), DateTimeOffset.Now.AddDays(-7), "fable").Available,
    "a missing transcripts directory must report nothing rather than throwing");
Console.WriteLine("PASS Claude usage source, limits[] override, cache and local token counting");

// ---- Binance crypto source -----------------------------------------------
Assert(BinanceStockSnapshotSource.NormalizeSymbol("btcusdt") == "BTCUSDT", "Binance pairs must upper-case");
Assert(BinanceStockSnapshotSource.NormalizeSymbol("BTC-USD") == "BTCUSDT", "Yahoo-style BTC-USD must fold to BTCUSDT");
Assert(BinanceStockSnapshotSource.NormalizeSymbol("eth/usdt") == "ETHUSDT", "slash pairs must fold");

var binanceResponses = new Queue<string>(new[]
{
    """[{"symbol":"BTCUSDT","lastPrice":"64250.10","priceChangePercent":"2.35"},{"symbol":"ETHUSDT","lastPrice":"3120.55","priceChangePercent":"-1.20"}]""",
    """[[0,"0","0","0","63000.0",0],[0,"0","0","0","63500.5",0],[0,"0","0","0","63800.0",0],[0,"0","0","0","64000.0",0],[0,"0","0","0","64250.1",0]]""",
    """[[0,"0","0","0","3200.0",0],[0,"0","0","0","3180.0",0],[0,"0","0","0","3150.0",0],[0,"0","0","0","3130.0",0],[0,"0","0","0","3120.55",0]]"""
});
var binanceHandler = new ClaudeHandler(binanceResponses);
using (var binanceClient = new HttpClient(binanceHandler))
using (var binanceSource = new BinanceStockSnapshotSource(binanceClient) { BaseUrl = "https://binance.test" })
{
    var binanceSettings = new StockSettings
    {
        SourceKind = StockSourceKind.Binance,
        RedForGain = false,
        Items =
        [
            new StockItemSettings { Symbol = "BTC-USD", Alias = "Bitcoin" },
            new StockItemSettings { Symbol = "ETHUSDT" }
        ]
    };
    var binanceSnapshot = await binanceSource.ReadAsync(binanceSettings);
    Assert(binanceHandler.Requests[0].Contains("/api/v3/ticker/24hr?symbols=")
        && binanceHandler.Requests[0].Contains("BTCUSDT")
        && binanceHandler.Requests[0].Contains("ETHUSDT"),
        "the ticker request must carry the normalized symbol list");
    Assert(binanceSnapshot.Quotes.Count == 2, "both Binance quotes should be parsed");
    Assert(binanceSnapshot.Quotes[0].DisplayName == "Bitcoin", "the Binance alias was not applied");
    Assert(Math.Abs(binanceSnapshot.Quotes[0].CurrentPrice - 64250.10) < 0.001, "the Binance last price was parsed incorrectly");
    Assert(Math.Abs(binanceSnapshot.Quotes[1].ChangePercent - (-1.20)) < 0.001, "the Binance change percent was parsed incorrectly");
    Assert(binanceSnapshot.Quotes[0].FiveDayCloses is { Count: 5 } btcCloses && Math.Abs(btcCloses[^1] - 64250.1) < 0.001,
        "Binance daily closes must feed the five-day chart");
    Assert(!binanceSnapshot.RedForGain, "the Binance color preference was not preserved");
    var binanceCached = await binanceSource.ReadAsync(binanceSettings);
    Assert(ReferenceEquals(binanceSnapshot, binanceCached), "Binance reads inside the cache window must not call the API");
    Assert(binanceHandler.Requests.Count == 3, "one ticker call plus two klines calls expected");
    var binanceFrame = renderer.Render(themes.Single(theme => theme.Id == "stocks"), SystemSnapshot.DesignSample with { Stocks = binanceSnapshot });
    Assert(binanceFrame.JpegBytes is [0xFF, 0xD8, ..], "the stocks view with Binance data did not render");
}
Console.WriteLine("PASS Binance source: normalization, tickers, klines, cache");

// ---- currency rates source -------------------------------------------------
Assert(CurrencySnapshotSource.NormalizeCode(" uah ", "USD") == "UAH", "currency codes must trim and upper-case");
Assert(CurrencySnapshotSource.NormalizeCode("x1", "USD") == "USD", "invalid currency codes must fall back");
Assert(CurrencyTheme.FormatRate(41.327) == "41.33", "mid-size rates keep two decimals");
Assert(CurrencyTheme.FormatRate(0.92141) == "0.9214", "sub-unit rates keep four decimals");
Assert(CurrencyTheme.FormatRate(1523.4) == "1523", "large rates drop the fraction");

var currencyResponses = new Queue<string>(new[]
{
    """{"result":"success","base_code":"USD","rates":{"USD":1,"EUR":0.9214,"UAH":41.32,"PLN":3.968}}"""
});
var currencyHandler = new ClaudeHandler(currencyResponses);
using (var currencyClient = new HttpClient(currencyHandler))
using (var currencySource = new CurrencySnapshotSource(currencyClient) { BaseUrl = "https://rates.test/v6/latest/" })
{
    // USD duplicates the base and "bad" is not a code: both must be dropped.
    var currencySettings = new CurrencySettings { BaseCurrency = "usd", QuoteCurrencies = ["EUR", "uah", "PLN", "USD", "bad"] };
    var currencySnapshot = await currencySource.ReadAsync(currencySettings);
    Assert(currencyHandler.Requests[0] == "https://rates.test/v6/latest/USD", "the base currency must form the request path");
    Assert(currencySnapshot.Available && currencySnapshot.Rates.Count == 3, "three valid quote currencies should parse");
    Assert(currencySnapshot.Rates[1].Code == "UAH" && Math.Abs(currencySnapshot.Rates[1].Rate - 41.32) < 0.0001,
        "the UAH rate was parsed incorrectly");
    var currencyCached = await currencySource.ReadAsync(currencySettings);
    Assert(ReferenceEquals(currencySnapshot, currencyCached), "currency reads inside the cache window must not call the API");
    Assert(currencyHandler.Requests.Count == 1, "exactly one rates request expected");
    var currencyTheme = themes.Single(theme => theme.Id == "currency");
    var currencyFrame = renderer.Render(currencyTheme, SystemSnapshot.DesignSample with { Currency = currencySnapshot });
    Assert(currencyFrame.JpegBytes is [0xFF, 0xD8, ..], "the currency view did not render");
    var currencyEmpty = renderer.Render(currencyTheme, SystemSnapshot.DesignSample with { Currency = null });
    Assert(currencyEmpty.JpegBytes is [0xFF, 0xD8, ..], "the empty currency view did not render");
}

using (var noQuotesClient = new HttpClient(new ClaudeHandler(new Queue<string>())))
using (var noQuotesSource = new CurrencySnapshotSource(noQuotesClient))
{
    var noQuotes = await noQuotesSource.ReadAsync(new CurrencySettings { QuoteCurrencies = [] });
    Assert(!noQuotes.Available && noQuotes.ErrorMessage is not null,
        "no configured quotes must surface a message without a request");
}
Console.WriteLine("PASS currency source: normalization, parsing, cache");

// ---- crypto source ---------------------------------------------------------
Assert(BinanceStockSnapshotSource.TrimQuoteAsset("BTCUSDT") == "BTC", "BTCUSDT must display as BTC");
Assert(BinanceStockSnapshotSource.TrimQuoteAsset("USDT") == "USDT", "the bare USDT symbol must survive trimming");

var cryptoResponses = new Queue<string>(new[]
{
    """[{"symbol":"BTCUSDT","lastPrice":"97412.55","priceChangePercent":"2.41"},{"symbol":"ETHUSDT","lastPrice":"3412.08","priceChangePercent":"-1.27"}]""",
    """[[0,"0","0","0","96100.0",0],[0,"0","0","0","96550.5",0],[0,"0","0","0","96900.0",0],[0,"0","0","0","97412.55",0]]""",
    """[[0,"0","0","0","3450.0",0],[0,"0","0","0","3433.0",0],[0,"0","0","0","3420.0",0],[0,"0","0","0","3412.08",0]]"""
});
var cryptoHandler = new ClaudeHandler(cryptoResponses);
using (var cryptoClient = new HttpClient(cryptoHandler))
using (var cryptoSource = new BinanceStockSnapshotSource(cryptoClient) { BaseUrl = "https://binance.test" })
{
    var cryptoSettings = new CryptoSettings();
    var cryptoSnapshot = await cryptoSource.ReadCryptoAsync(cryptoSettings);
    Assert(cryptoSnapshot.Coins.Count == 2, "the two default pairs should load and empty slots must be skipped");
    Assert(cryptoHandler.Requests[1].Contains("/api/v3/klines?symbol=BTCUSDT&interval=1h&limit=24"),
        "the sparkline must come from 24 hourly candles");
    Assert(cryptoSnapshot.Coins[0].DisplayName == "BTC", "BTCUSDT should display as BTC without an alias");
    Assert(Math.Abs(cryptoSnapshot.Coins[0].Price - 97412.55) < 0.001, "the BTC price was parsed incorrectly");
    Assert(Math.Abs(cryptoSnapshot.Coins[1].ChangePercent24h - (-1.27)) < 0.001, "the ETH change was parsed incorrectly");
    Assert(cryptoSnapshot.Coins[0].HourlyCloses.Count == 4 && Math.Abs(cryptoSnapshot.Coins[0].HourlyCloses[^1] - 97412.55) < 0.001,
        "hourly closes must feed the sparkline");
    var cryptoCached = await cryptoSource.ReadCryptoAsync(cryptoSettings);
    Assert(ReferenceEquals(cryptoSnapshot, cryptoCached), "crypto reads inside the cache window must not call the API");
    Assert(cryptoHandler.Requests.Count == 3, "one ticker call plus two klines calls expected");
    var cryptoTheme = themes.Single(theme => theme.Id == "crypto");
    var cryptoFrame = renderer.Render(cryptoTheme, SystemSnapshot.DesignSample with { Crypto = cryptoSnapshot });
    Assert(cryptoFrame.JpegBytes is [0xFF, 0xD8, ..], "the crypto view did not render");
    var cryptoEmpty = renderer.Render(cryptoTheme, SystemSnapshot.DesignSample with { Crypto = CryptoSnapshot.Empty });
    Assert(cryptoEmpty.JpegBytes is [0xFF, 0xD8, ..], "the empty crypto view did not render");
    var cryptoMissing = renderer.Render(cryptoTheme, SystemSnapshot.DesignSample with { Crypto = null });
    Assert(cryptoMissing.JpegBytes is [0xFF, 0xD8, ..], "the missing crypto view did not render");
}
Console.WriteLine("PASS crypto source: trimming, tickers, hourly closes, cache");

// ---- pomodoro timer --------------------------------------------------------
// The phase is derived from the wall clock, so the test anchors t0 right before
// Start and probes with explicit instants; the anchor drift is microseconds
// against minute-scale boundaries.
var pomodoro = new PomodoroTimer();
Assert(!pomodoro.IsRunning && pomodoro.Read().Phase == PomodoroPhase.Idle, "a fresh pomodoro must be idle");
int pomodoroChanges = 0;
pomodoro.Changed += (_, _) => pomodoroChanges++;
var pomodoroStart = DateTimeOffset.Now;
pomodoro.Start(new PomodoroSettings { FocusMinutes = 25, BreakMinutes = 5, TargetCycles = 2 });
Assert(pomodoroChanges == 1 && pomodoro.IsRunning, "starting must raise Changed and mark the timer running");

var focusState = pomodoro.Read(pomodoroStart + TimeSpan.FromMinutes(10));
Assert(focusState.Phase == PomodoroPhase.Focus && focusState.CompletedFocusCycles == 0,
    "ten minutes in, the first focus block should be active");
Assert(focusState.Remaining >= TimeSpan.FromMinutes(15) && focusState.Remaining < TimeSpan.FromMinutes(15.1),
    "the focus countdown is off");
Assert(Math.Abs(focusState.ElapsedFraction - 0.4) < 0.01, "the focus progress fraction is off");

var breakState = pomodoro.Read(pomodoroStart + TimeSpan.FromMinutes(26));
Assert(breakState.Phase == PomodoroPhase.Break && breakState.CompletedFocusCycles == 1,
    "minute 26 falls in the first break with one focus block done");

var secondFocus = pomodoro.Read(pomodoroStart + TimeSpan.FromMinutes(31));
Assert(secondFocus.Phase == PomodoroPhase.Focus && secondFocus.CompletedFocusCycles == 1,
    "minute 31 falls in the second focus block");

var pomodoroDone = pomodoro.Read(pomodoroStart + TimeSpan.FromMinutes(56));
Assert(pomodoroDone.Phase == PomodoroPhase.Idle && pomodoroDone.CompletedFocusCycles == 2,
    "the run must complete when the final focus block ends, with no trailing break");
Assert(!pomodoro.IsRunning, "a completed run must leave the timer stopped");
Assert(pomodoroChanges == 2, "auto-completion must raise Changed exactly once");

pomodoro.Start(new PomodoroSettings { FocusMinutes = 1, BreakMinutes = 1, TargetCycles = 1 });
pomodoro.Stop();
Assert(!pomodoro.IsRunning && pomodoro.Read().Phase == PomodoroPhase.Idle && pomodoroChanges == 4,
    "stopping must return to idle and raise Changed");

var pomodoroThemeTimer = new PomodoroTimer();
var pomodoroTheme = new PomodoroTheme(pomodoroThemeTimer);
var pomodoroIdleFrame = renderer.Render(pomodoroTheme, SystemSnapshot.DesignSample);
Assert(pomodoroIdleFrame.JpegBytes is [0xFF, 0xD8, ..], "the idle pomodoro view did not render");
pomodoroThemeTimer.Start(new PomodoroSettings());
var pomodoroRunningFrame = renderer.Render(pomodoroTheme, SystemSnapshot.DesignSample with { Timestamp = DateTimeOffset.Now.AddMinutes(5) });
Assert(pomodoroRunningFrame.JpegBytes is [0xFF, 0xD8, ..], "the running pomodoro view did not render");
Console.WriteLine("PASS pomodoro timer: phases, cycle tally, auto-complete, renders");

// ---- theme schedule + night dimming ----------------------------------------
Assert(ThemeSchedule.TryParseTime("22:00", out var parsedNight) && parsedNight == new TimeSpan(22, 0, 0),
    "HH:mm must parse");
Assert(ThemeSchedule.TryParseTime("7:5", out var parsedShort) && parsedShort == new TimeSpan(7, 5, 0),
    "single-digit H:m must parse");
Assert(!ThemeSchedule.TryParseTime("24:00", out _) && !ThemeSchedule.TryParseTime("aa:bb", out _)
    && !ThemeSchedule.TryParseTime("2200", out _) && !ThemeSchedule.TryParseTime("", out _),
    "invalid times must be rejected");

static DateTimeOffset At(int hour, int minute) => new(2026, 1, 15, hour, minute, 0, TimeSpan.Zero);
var nightSchedule = new ThemeScheduleSettings { Enabled = true, NightStart = "22:00", NightEnd = "07:00" };
Assert(ThemeSchedule.IsNight(nightSchedule, At(23, 30)), "23:30 falls inside a 22-07 night");
Assert(ThemeSchedule.IsNight(nightSchedule, At(6, 59)), "06:59 falls inside a 22-07 night");
Assert(ThemeSchedule.IsNight(nightSchedule, At(22, 0)), "the night starts exactly at its start time");
Assert(!ThemeSchedule.IsNight(nightSchedule, At(7, 0)), "the night ends exactly at its end time");
Assert(!ThemeSchedule.IsNight(nightSchedule, At(12, 0)), "noon is not night");
var afternoonWindow = new ThemeScheduleSettings { Enabled = true, NightStart = "13:00", NightEnd = "15:00" };
Assert(ThemeSchedule.IsNight(afternoonWindow, At(14, 0)), "a same-day window must work");
Assert(!ThemeSchedule.IsNight(afternoonWindow, At(15, 0)), "a same-day window ends at its end time");
Assert(!ThemeSchedule.IsNight(new ThemeScheduleSettings { Enabled = false, NightStart = "22:00", NightEnd = "07:00" }, At(23, 0)),
    "a disabled schedule is never night");
Assert(!ThemeSchedule.IsNight(new ThemeScheduleSettings { Enabled = true, NightStart = "22:00", NightEnd = "22:00" }, At(23, 0)),
    "a zero-length window is never night");
Assert(!ThemeSchedule.IsNight(new ThemeScheduleSettings { Enabled = true, NightStart = "junk", NightEnd = "07:00" }, At(23, 0)),
    "an unparseable time disables the window");

var nightTheme = new ThemeScheduleSettings { Enabled = true, NightStart = "22:00", NightEnd = "07:00", NightThemeId = "clock" };
Assert(ThemeSchedule.ResolveThemeId(nightTheme, "system", At(23, 0)) == "clock", "the night theme must apply at night");
Assert(ThemeSchedule.ResolveThemeId(nightTheme, "system", At(12, 0)) == "system", "the day keeps the requested theme");
Assert(ThemeSchedule.ResolveThemeId(nightSchedule, "system", At(23, 0)) == "system",
    "an empty night theme keeps the requested theme");
Assert(ThemeSchedule.ResolveThemeId(null, "system", At(23, 0)) == "system", "missing settings keep the requested theme");

var dimSchedule = new ThemeScheduleSettings { Enabled = true, NightStart = "22:00", NightEnd = "07:00", DimAtNight = true, NightBrightnessPercent = 60 };
Assert(ThemeSchedule.BrightnessPercent(dimSchedule, At(23, 0)) == 60, "the night brightness must apply at night");
Assert(ThemeSchedule.BrightnessPercent(dimSchedule, At(12, 0)) == 100, "the day stays at full brightness");
Assert(ThemeSchedule.BrightnessPercent(
        new ThemeScheduleSettings { Enabled = true, NightStart = "22:00", NightEnd = "07:00", DimAtNight = false },
        At(23, 0)) == 100,
    "dimming off keeps full brightness at night");
Assert(ThemeSchedule.BrightnessPercent(new ThemeScheduleSettings { Enabled = true, NightStart = "22:00", NightEnd = "07:00", NightBrightnessPercent = 5 }, At(23, 0)) == 20,
    "the brightness floor is 20 percent");

var dimTestTheme = themes.Single(theme => theme.Id == "clock-dot-matrix");
var fullFrame = renderer.Render(dimTestTheme, SystemSnapshot.DesignSample);
var dimFrame = renderer.Render(dimTestTheme, SystemSnapshot.DesignSample, brightnessPercent: 40);
Assert(dimFrame.JpegBytes is [0xFF, 0xD8, ..], "the dimmed frame did not render");
Assert(MeanLuma(dimFrame.JpegBytes) < MeanLuma(fullFrame.JpegBytes) * 0.75,
    "a 40 percent brightness render must be visibly darker");
Console.WriteLine("PASS theme schedule: windows, night theme, brightness and dimmed render");

// ---- hardware monitor theme ------------------------------------------------
Assert(HardwareMonitorTheme.PageIndex(DateTimeOffset.FromUnixTimeSeconds(0), 5, 2) == 0
    && HardwareMonitorTheme.PageIndex(DateTimeOffset.FromUnixTimeSeconds(4), 5, 2) == 0
    && HardwareMonitorTheme.PageIndex(DateTimeOffset.FromUnixTimeSeconds(5), 5, 2) == 1
    && HardwareMonitorTheme.PageIndex(DateTimeOffset.FromUnixTimeSeconds(10), 5, 2) == 0,
    "pages must rotate on the wall clock at the configured dwell");
Assert(HardwareMonitorTheme.PageIndex(DateTimeOffset.FromUnixTimeSeconds(3), 0, 2) == 1,
    "the dwell must clamp to the 3-second floor");
Assert(HardwareMonitorTheme.FormatPercent(38.4) == "38%" && HardwareMonitorTheme.FormatPercent(null) == "—",
    "percent formatting is wrong");
Assert(HardwareMonitorTheme.FormatTemperature(62.4) == "62°" && HardwareMonitorTheme.FormatTemperature(null) == "—",
    "temperature formatting is wrong");
Assert(HardwareMonitorTheme.FormatClock(4.553) == "4.55" && HardwareMonitorTheme.FormatClock(null) == "—",
    "clock formatting is wrong");
Assert(HardwareMonitorTheme.FormatFan(1240) == "1240" && HardwareMonitorTheme.FormatFan(null) == "—",
    "fan formatting is wrong");
Assert(HardwareMonitorTheme.FormatVram(6_348, 12_288) == "6.2 / 12.0 GB" && HardwareMonitorTheme.FormatVram(null, 12_288) == "—",
    "VRAM formatting is wrong");
Assert(HardwareMonitorTheme.FormatRam(20.1, 32) == "20.1 / 32.0 GB" && HardwareMonitorTheme.FormatRam(null, null) == "",
    "RAM formatting is wrong");

var hardwareTheme = new HardwareMonitorTheme();
var hwPageOne = renderer.Render(hardwareTheme,
    SystemSnapshot.DesignSample with { Timestamp = DateTimeOffset.FromUnixTimeSeconds(0) });
Assert(hwPageOne.JpegBytes is [0xFF, 0xD8, ..], "the hardware devices page did not render");
var hwPageTwo = renderer.Render(hardwareTheme,
    SystemSnapshot.DesignSample with { Timestamp = DateTimeOffset.FromUnixTimeSeconds(5) });
Assert(hwPageTwo.JpegBytes is [0xFF, 0xD8, ..], "the hardware system page did not render");
Assert(!hwPageOne.JpegBytes.AsSpan().SequenceEqual(hwPageTwo.JpegBytes), "the two hardware pages must differ");
var hwFallback = renderer.Render(hardwareTheme, SystemSnapshot.DesignSample with { Hardware = null });
Assert(hwFallback.JpegBytes is [0xFF, 0xD8, ..], "the counters-only hardware page did not render");
var hwPartial = renderer.Render(hardwareTheme, SystemSnapshot.DesignSample with
{
    Timestamp = DateTimeOffset.FromUnixTimeSeconds(0),
    Hardware = new HardwareSnapshot(true, new HardwareComponentSnapshot("CPU", 38.4), null, null, null, null, DateTimeOffset.Now)
});
Assert(hwPartial.JpegBytes is [0xFF, 0xD8, ..], "a partial hardware snapshot did not render");
Console.WriteLine("PASS hardware monitor: page rotation, formatting, renders");

// ---- GitHub contributions ---------------------------------------------------
// Week grouping is Sunday-based like GitHub's calendar.
Assert(GitHubContributionTheme.WeeksBack(new DateOnly(2026, 8, 21), new DateOnly(2026, 8, 17)) == 0,
    "Friday and Monday of the same week must land in week 0");
Assert(GitHubContributionTheme.WeeksBack(new DateOnly(2026, 8, 21), new DateOnly(2026, 8, 16)) == 0,
    "Sunday starts the week that contains the following Friday");
Assert(GitHubContributionTheme.WeeksBack(new DateOnly(2026, 8, 21), new DateOnly(2026, 8, 15)) == 1,
    "Saturday belongs to the previous Sunday-based week");

// Attribute order inside the td varies, and counts join through tool-tips.
const string gitHubHtml = """
    <table><tbody><tr>
    <td id="contribution-day-component-0-1" data-level="2" class="ContributionCalendar-day" data-date="2026-08-17"></td>
    <td class="ContributionCalendar-day" data-date="2026-08-18" id="contribution-day-component-1-1" data-level="0"></td>
    <td data-date="2026-08-19" data-level="4" class="ContributionCalendar-day"></td>
    </tr></tbody></table>
    <tool-tip for="contribution-day-component-0-1" class="sr-only">1,205 contributions on August 17th.</tool-tip>
    <tool-tip for="contribution-day-component-1-1" class="sr-only">No contributions on August 18th.</tool-tip>
    """;
var parsedDays = GitHubContributionSource.ParseContributionHtml(gitHubHtml);
Assert(parsedDays.Count == 3, "all three calendar cells should parse");
Assert(parsedDays[0].Date == new DateOnly(2026, 8, 17) && parsedDays[0].Level == 2 && parsedDays[0].Count == 1205,
    "the tool-tip count must join its cell through the id");
Assert(parsedDays[1].Count == 0, "'No contributions' must read as zero");
Assert(parsedDays[2].Level == 4 && parsedDays[2].Count == -1, "a cell without a tool-tip keeps an unknown count");

var gitHubHtmlHandler = new ClaudeHandler(new Queue<string>(new[] { gitHubHtml }));
using (var gitHubClient = new HttpClient(gitHubHtmlHandler))
using (var gitHubSource = new GitHubContributionSource(gitHubClient) { HtmlBaseUrl = "https://github.test/users/" })
{
    var gitHubSettings = new GitHubSettings { Username = "@ZCat95 " };
    var gitHubSnapshot = await gitHubSource.ReadAsync(gitHubSettings);
    Assert(gitHubHtmlHandler.Requests[0] == "https://github.test/users/ZCat95/contributions",
        "the username must be trimmed of @ and whitespace before the request");
    Assert(gitHubSnapshot.Available && gitHubSnapshot.Days.Count == 3 && gitHubSnapshot.Username == "ZCat95",
        "the HTML snapshot did not load");
    var gitHubCached = await gitHubSource.ReadAsync(gitHubSettings);
    Assert(ReferenceEquals(gitHubSnapshot, gitHubCached), "GitHub reads inside the cache window must not call the API");
    Assert(gitHubHtmlHandler.Requests.Count == 1, "exactly one contributions request expected");
}

const string gitHubGraphQl = """
    {"data":{"user":{"contributionsCollection":{"contributionCalendar":{"weeks":[
    {"contributionDays":[
      {"date":"2026-08-16","contributionCount":0,"contributionLevel":"NONE"},
      {"date":"2026-08-17","contributionCount":7,"contributionLevel":"THIRD_QUARTILE"}
    ]}]}}}}}
    """;
var gitHubGraphQlHandler = new ClaudeHandler(new Queue<string>(new[] { gitHubGraphQl }));
using (var gitHubGraphQlClient = new HttpClient(gitHubGraphQlHandler))
using (var gitHubGraphQlSource = new GitHubContributionSource(gitHubGraphQlClient) { GraphQlUrl = "https://api.github.test/graphql" })
{
    var tokenSnapshot = await gitHubGraphQlSource.ReadAsync(new GitHubSettings { Username = "zcat95", Token = "github_pat_test" });
    Assert(gitHubGraphQlHandler.Requests[0] == "https://api.github.test/graphql", "the token path must use GraphQL");
    Assert(tokenSnapshot.Available && tokenSnapshot.Days.Count == 2, "the GraphQL snapshot did not load");
    Assert(tokenSnapshot.Days[1].Level == 3 && tokenSnapshot.Days[1].Count == 7,
        "GraphQL levels and counts were mapped incorrectly");
}

var gitHubTheme = themes.Single(theme => theme.Id == "github");
var gitHubFrame = renderer.Render(gitHubTheme, SystemSnapshot.DesignSample);
Assert(gitHubFrame.JpegBytes is [0xFF, 0xD8, ..], "the GitHub view did not render");
var gitHubEmpty = renderer.Render(gitHubTheme, SystemSnapshot.DesignSample with { GitHub = null });
Assert(gitHubEmpty.JpegBytes is [0xFF, 0xD8, ..], "the unconfigured GitHub view did not render");
var gitHubError = renderer.Render(gitHubTheme,
    SystemSnapshot.DesignSample with { GitHub = GitHubContributionSnapshot.Unavailable("GitHub returned 502") });
Assert(gitHubError.JpegBytes is [0xFF, 0xD8, ..], "the errored GitHub view did not render");
Console.WriteLine("PASS GitHub contributions: week math, HTML+GraphQL parsing, cache, renders");

// ---- Telegram popup overlay -------------------------------------------------
var telegramDefaults = new TelegramSettings();
Assert(!telegramDefaults.IsConfigured && telegramDefaults.PopupsEnabled
    && telegramDefaults.PrivacyMode == TelegramPrivacyMode.Sender && telegramDefaults.PopupSeconds == 6,
    "Telegram defaults are wrong");
Assert(new TelegramSettings { ApiId = "123", ApiHash = "abc", PhoneNumber = "+380" }.IsConfigured,
    "a filled Telegram config must count as configured");
Assert(TelegramPopupOverlay.TruncatePreview("line one\nline two") == "line one line two",
    "previews must flatten line breaks");
Assert(TelegramPopupOverlay.TruncatePreview(new string('x', 200)).Length == 96
    && TelegramPopupOverlay.TruncatePreview(new string('x', 200)).EndsWith('…'),
    "long previews must truncate with an ellipsis");
Assert(TelegramPopupOverlay.TruncatePreview(null) == "", "a null preview must flatten to empty");
// Skia drops a whole text run when it meets an astral-plane rune the font
// lacks, so emoji must be stripped before drawing.
Assert(TelegramPopupOverlay.TruncatePreview("Привіт! 🎉 Це працює 🚀") == "Привіт! Це працює",
    "emoji must be stripped from previews");

var overlayBase = renderer.Render(themes.Single(theme => theme.Id == "clock-dot-matrix"), SystemSnapshot.DesignSample);
var overlayFrame = renderer.Render(
    themes.Single(theme => theme.Id == "clock-dot-matrix"),
    SystemSnapshot.DesignSample,
    overlay: canvas => TelegramPopupOverlay.Draw(canvas, "Ruslan",
        TelegramPopupOverlay.TruncatePreview("Привіт! Глянь, що вийшло 🎉")));
Assert(overlayFrame.JpegBytes is [0xFF, 0xD8, ..], "the popup overlay did not render");
Assert(!overlayFrame.JpegBytes.AsSpan().SequenceEqual(overlayBase.JpegBytes),
    "the overlay must change the frame");
var overlayNoPreview = renderer.Render(
    themes.Single(theme => theme.Id == "clock-dot-matrix"),
    SystemSnapshot.DesignSample,
    overlay: canvas => TelegramPopupOverlay.Draw(canvas, "3 new messages", null));
Assert(overlayNoPreview.JpegBytes is [0xFF, 0xD8, ..], "the counter-mode popup did not render");
// At night the popup draws after dimming, so it stays bright over a dim theme.
var overlayDimmed = renderer.Render(
    themes.Single(theme => theme.Id == "clock-dot-matrix"),
    SystemSnapshot.DesignSample,
    brightnessPercent: 40,
    overlay: canvas => TelegramPopupOverlay.Draw(canvas, "Ruslan", "hello"));
Assert(overlayDimmed.JpegBytes is [0xFF, 0xD8, ..], "the popup over a dimmed frame did not render");
Console.WriteLine("PASS Telegram popup: defaults, truncation, overlay renders");

// ---- notification engine ----------------------------------------------------
var notifyEngine = new NotificationEngine();
var notifySettings = new NotificationSettings
{
    Enabled = true,
    PriceAlerts = [new PriceAlertSettings { Symbol = "btc-usd", Above = 100_000 }]
};
var notifyT0 = new DateTimeOffset(2026, 8, 21, 12, 0, 0, TimeSpan.Zero);

static ClaudeUsageSnapshot ClaudeAt(int sessionPercent) => new(
    Available: true,
    Session: new ClaudeUsageWindow(ClaudeUsageWindowKind.Session, sessionPercent, DateTimeOffset.Now.AddHours(2)),
    Week: new ClaudeUsageWindow(ClaudeUsageWindowKind.Week, 10, DateTimeOffset.Now.AddDays(3)),
    ModelWeek: null,
    UpdatedAt: DateTimeOffset.Now);

Assert(notifyEngine.Evaluate(new NotificationSettings(), new NotificationInputs(ClaudeAt(99)), notifyT0).Count == 0,
    "a disabled master switch must silence everything");
Assert(notifyEngine.Evaluate(notifySettings, new NotificationInputs(ClaudeAt(50)), notifyT0).Count == 0,
    "50% must not cross any threshold");
var notify80 = notifyEngine.Evaluate(notifySettings, new NotificationInputs(ClaudeAt(85)), notifyT0);
Assert(notify80.Count == 1 && notify80[0].TriggerKey == "claude-session",
    "85% must fire the session threshold once");
Assert(notifyEngine.Evaluate(notifySettings, new NotificationInputs(ClaudeAt(86)), notifyT0.AddMinutes(1)).Count == 0,
    "the same threshold must not refire");
var notify95 = notifyEngine.Evaluate(notifySettings, new NotificationInputs(ClaudeAt(96)), notifyT0.AddHours(2));
Assert(notify95.Count == 1 && notify95[0].Message.Contains("96"),
    "crossing a higher threshold must fire again with the current percent");
Assert(notifyEngine.Evaluate(notifySettings, new NotificationInputs(ClaudeAt(5)), notifyT0.AddHours(3)).Count == 0,
    "a reset window fires nothing by itself");
var notifyRearmed = notifyEngine.Evaluate(notifySettings, new NotificationInputs(ClaudeAt(85)), notifyT0.AddHours(4));
Assert(notifyRearmed.Count == 1, "after a window reset the thresholds must be armed again");

Assert(notifyEngine.Evaluate(notifySettings, new NotificationInputs(DevicePushFailed: false), notifyT0).Count == 0,
    "a healthy push fires nothing");
var notifyOffline = notifyEngine.Evaluate(notifySettings, new NotificationInputs(DevicePushFailed: true), notifyT0.AddHours(5));
Assert(notifyOffline.Count == 1 && notifyOffline[0].TriggerKey == "device-offline",
    "the first failed push must fire the offline notice");
Assert(notifyEngine.Evaluate(notifySettings, new NotificationInputs(DevicePushFailed: true), notifyT0.AddHours(5).AddMinutes(1)).Count == 0,
    "a still-failing push must not refire");

var priceOk = new Dictionary<string, double> { ["BTCUSDT"] = 99_000 };
var priceHigh = new Dictionary<string, double> { ["BTCUSDT"] = 100_500 };
Assert(notifyEngine.Evaluate(notifySettings, new NotificationInputs(Prices: priceOk), notifyT0.AddHours(6)).Count == 0,
    "a price inside the bound fires nothing");
var notifyPrice = notifyEngine.Evaluate(notifySettings, new NotificationInputs(Prices: priceHigh), notifyT0.AddHours(7));
Assert(notifyPrice.Count == 1 && notifyPrice[0].TriggerKey == "price-BTCUSDT-above",
    "crossing the bound must fire the price alert");
Assert(notifyEngine.Evaluate(notifySettings, new NotificationInputs(Prices: priceHigh), notifyT0.AddHours(9)).Count == 0,
    "a price still beyond the bound must stay silent until it re-arms");
Assert(notifyEngine.Evaluate(notifySettings, new NotificationInputs(Prices: priceOk), notifyT0.AddHours(10)).Count == 0,
    "returning inside the bound only re-arms");
Assert(notifyEngine.Evaluate(notifySettings, new NotificationInputs(Prices: priceHigh), notifyT0.AddHours(11)).Count == 1,
    "a re-armed alert must fire on the next crossing");
Console.WriteLine("PASS notification engine: thresholds, offline transition, price re-arm, cooldowns");

// ---- settings export/import -------------------------------------------------
var portSource = new AppSettings
{
    AccentColor = "#123456",
    SelectedThemeId = "hardware",
    ClaudeUsage = new ClaudeUsageSettings { SessionKey = "sk-ant-sid01-secret", OrganizationId = "org-1", ModelScope = "Fable" },
    GitHub = new GitHubSettings { Username = "zcat95", Token = "github_pat_secret" },
    Telegram = new TelegramSettings { ApiId = "12345", ApiHash = "hash-secret", PhoneNumber = "+380501234567", PopupSeconds = 9 },
    Notifications = new NotificationSettings { Enabled = true, PriceAlerts = [new PriceAlertSettings { Symbol = "BTCUSDT", Above = 100_000 }] }
};
string exported = SettingsPorter.ExportJson(portSource);
Assert(!exported.Contains("sk-ant-sid01-secret") && !exported.Contains("github_pat_secret")
    && !exported.Contains("hash-secret") && !exported.Contains("+380501234567") && !exported.Contains("org-1"),
    "an export must never carry credentials");
Assert(exported.Contains("#123456") && exported.Contains("\"hardware\"") && exported.Contains("zcat95")
    && exported.Contains("BTCUSDT") && exported.Contains("Fable"),
    "an export must keep the non-secret settings");
Assert(portSource.ClaudeUsage.SessionKey == "sk-ant-sid01-secret",
    "exporting must not touch the live settings object");

var portImported = SettingsPorter.ImportJson(exported, portSource);
Assert(portImported.ClaudeUsage.SessionKey == "sk-ant-sid01-secret"
    && portImported.ClaudeUsage.OrganizationId == "org-1"
    && portImported.GitHub.Token == "github_pat_secret"
    && portImported.Telegram.ApiHash == "hash-secret"
    && portImported.Telegram.PhoneNumber == "+380501234567",
    "importing a stripped file must keep the secrets already on this machine");
Assert(portImported.AccentColor == "#123456" && portImported.Telegram.PopupSeconds == 9
    && portImported.Notifications.PriceAlerts.Count == 1,
    "importing must carry the non-secret values through");
var portForeign = SettingsPorter.ImportJson(
    """{"AccentColor":"#ABCDEF","ClaudeUsage":{"SessionKey":"sk-other"}}""", portSource);
Assert(portForeign.ClaudeUsage.SessionKey == "sk-other" && portForeign.AccentColor == "#ABCDEF",
    "a file that does carry a secret must win over the local one");
bool portThrew = false;
try
{
    SettingsPorter.ImportJson("{not json", portSource);
}
catch (InvalidOperationException)
{
    portThrew = true;
}
Assert(portThrew, "invalid JSON must be rejected with the localized message");
Console.WriteLine("PASS settings porter: secrets stripped, merge on import, invalid input");

// ---- localization ---------------------------------------------------------
AppLanguage[] shippedLanguages = AppLanguageInfo.All.Select(info => info.Language).ToArray();
Assert(shippedLanguages.Length == 3, "three languages should ship");
Assert(AppLanguageInfo.All.Select(info => info.Id).SequenceEqual(new[] { "en", "uk", "zh-Hans" }),
    "language identifiers changed; settings written by older builds would stop resolving");

Loc.Instance.Initialize(AppLanguage.English);
var englishKeys = Loc.Instance.Keys(AppLanguage.English).ToHashSet(StringComparer.Ordinal);
Assert(englishKeys.Count > 300, "the English catalogue did not load from the embedded resource");
var englishPlaceholders = englishKeys.ToDictionary(key => key, key => PlaceholderIndexes(Loc.T(key)), StringComparer.Ordinal);

foreach (AppLanguage language in shippedLanguages)
{
    var keys = Loc.Instance.Keys(language).ToHashSet(StringComparer.Ordinal);
    string[] missing = [.. englishKeys.Except(keys).Order()];
    string[] extra = [.. keys.Except(englishKeys).Order()];
    Assert(missing.Length == 0, $"{language}: catalogue is missing {missing.Length} key(s), first: {missing.FirstOrDefault()}");
    Assert(extra.Length == 0, $"{language}: catalogue has {extra.Length} unknown key(s), first: {extra.FirstOrDefault()}");
}

foreach (AppLanguage language in shippedLanguages)
{
    Loc.Instance.Initialize(language);
    foreach (string key in englishKeys)
    {
        string value = Loc.T(key);
        Assert(value.Length > 0, $"{language}: '{key}' is empty");
        Assert(value != key, $"{language}: '{key}' fell through to the key itself");
        Assert(PlaceholderIndexes(value).SequenceEqual(englishPlaceholders[key]),
            $"{language}: '{key}' does not take the same {{0}}..{{n}} placeholders as English");
    }
}

// Every theme must render in every language: this catches a missing key, a bad
// format string, or text that overflows the 142x428 JPEG budget.
foreach (AppLanguage language in shippedLanguages)
{
    Loc.Instance.Initialize(language);
    foreach (var theme in BuiltInThemes.Create(new ImageTheme(), new PomodoroTimer()))
    {
        var localizedFrame = renderer.Render(theme, SystemSnapshot.DesignSample);
        Assert(localizedFrame.JpegBytes is [0xFF, 0xD8, ..], $"{language}/{theme.Id}: did not render");
        Assert(localizedFrame.JpegBytes.Length <= profile.MaxJpegBytes, $"{language}/{theme.Id}: JPEG exceeds device limit");
        Assert(theme.DisplayName.Length > 0 && theme.Description.Length > 0 && theme.Details.Length > 0,
            $"{language}/{theme.Id}: theme catalogue text is incomplete");
    }
}

Loc.Instance.Initialize(AppLanguage.English);
Assert(Loc.T("AboutVersion", "1.2.3") == "Version 1.2.3", "formatted lookup is broken");
Assert(Loc.T("ThisKeyDoesNotExist") == "ThisKeyDoesNotExist", "an unknown key should fall back to itself");
Loc.Instance.Initialize(AppLanguage.Ukrainian);
Assert(Loc.T("ThemeMusicName") == "Музика", "Ukrainian catalogue did not load");
Assert(Loc.LongDate(new DateTimeOffset(2026, 8, 21, 0, 0, 0, TimeSpan.Zero)).Contains("2026"),
    "Ukrainian long date lost the year");
Loc.Instance.Initialize(AppLanguage.ChineseSimplified);
Assert(Loc.T("ThemeMusicName") == "音乐", "Simplified Chinese catalogue did not load");
Loc.Instance.Initialize(AppLanguage.English);
Console.WriteLine($"PASS localization catalogues: {englishKeys.Count} keys x {shippedLanguages.Length} languages");

Console.WriteLine("All smoke tests passed.");
return;

static int[] PlaceholderIndexes(string value)
{
    var found = new SortedSet<int>();
    for (int index = 0; index + 2 < value.Length; index++)
    {
        if (value[index] == '{' && char.IsDigit(value[index + 1]) && value[index + 2] == '}')
        {
            found.Add(value[index + 1] - '0');
        }
    }
    return [.. found];
}

static void Assert(bool condition, string message)
{
    if (!condition)
    {
        throw new InvalidOperationException(message);
    }
}

static double MeanLuma(byte[] jpeg)
{
    using var bitmap = SkiaSharp.SKBitmap.Decode(jpeg)
        ?? throw new InvalidOperationException("the JPEG could not be decoded");
    double sum = 0;
    for (int y = 0; y < bitmap.Height; y += 4)
    {
        for (int x = 0; x < bitmap.Width; x += 4)
        {
            var pixel = bitmap.GetPixel(x, y);
            sum += 0.2126 * pixel.Red + 0.7152 * pixel.Green + 0.0722 * pixel.Blue;
        }
    }

    return sum / ((bitmap.Height + 3) / 4 * ((bitmap.Width + 3) / 4));
}

static bool IsStaticFont(string path)
{
    byte[] bytes = File.ReadAllBytes(path);
    int numTables = (bytes[4] << 8) | bytes[5];
    for (int index = 0; index < numTables; index++)
    {
        int offset = 12 + index * 16;
        string tag = Encoding.ASCII.GetString(bytes, offset, 4);
        if (tag == "fvar")
        {
            return false;
        }
    }

    return true;
}

static byte FindStartOfFrame(byte[] jpeg)
{
    for (var index = 2; index + 3 < jpeg.Length;)
    {
        if (jpeg[index] != 0xFF)
        {
            index++;
            continue;
        }

        while (index < jpeg.Length && jpeg[index] == 0xFF)
        {
            index++;
        }

        if (index >= jpeg.Length)
        {
            break;
        }

        var marker = jpeg[index++];
        if (marker is >= 0xC0 and <= 0xCF and not 0xC4 and not 0xC8 and not 0xCC)
        {
            return marker;
        }

        if (marker is 0xD8 or 0xD9 || index + 1 >= jpeg.Length)
        {
            continue;
        }

        var length = (jpeg[index] << 8) | jpeg[index + 1];
        if (length < 2)
        {
            break;
        }

        index += length;
    }

    return 0;
}

/// Records what was asked for so the tests can assert on headers and paths.
sealed class ClaudeHandler : HttpMessageHandler
{
    private readonly Queue<string> _responses;
    public List<string> Requests { get; } = [];
    public List<string> Cookies { get; } = [];

    public ClaudeHandler(Queue<string> responses)
    {
        _responses = responses;
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Requests.Add(request.RequestUri?.ToString() ?? string.Empty);
        Cookies.Add(request.Headers.TryGetValues("Cookie", out var values) ? string.Join("; ", values) : string.Empty);
        if (_responses.Count == 0)
        {
            throw new InvalidOperationException("No mocked Claude response remains.");
        }
        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(_responses.Dequeue())
        });
    }
}

sealed class SequenceHandler : HttpMessageHandler
{
    private readonly Queue<string> _responses;
    public int RequestCount { get; private set; }

    public SequenceHandler(Queue<string> responses)
    {
        _responses = responses;
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        RequestCount++;
        if (_responses.Count == 0)
        {
            throw new InvalidOperationException("No mocked weather response remains.");
        }
        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(_responses.Dequeue())
        });
    }
}

sealed class GbkHandler : HttpMessageHandler
{
    private readonly byte[] _payload;
    public int RequestCount { get; private set; }
    public Uri? LastRequestUri { get; private set; }

    public GbkHandler(string payload)
    {
        _payload = Encoding.GetEncoding("GBK").GetBytes(payload);
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        RequestCount++;
        LastRequestUri = request.RequestUri;
        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new ByteArrayContent(_payload)
        });
    }
}

sealed class TencentStockHandler : HttpMessageHandler
{
    private readonly byte[] _quoteGbk;
    private readonly byte[] _klineUtf8;
    public int KlineRequests { get; private set; }
    public List<string> KlineQueries { get; } = [];

    public TencentStockHandler(string quotePayload, string klinePayload)
    {
        _quoteGbk = Encoding.GetEncoding("GBK").GetBytes(quotePayload);
        _klineUtf8 = Encoding.UTF8.GetBytes(klinePayload);
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (request.RequestUri!.Host.Contains("ifzq", StringComparison.OrdinalIgnoreCase))
        {
            KlineRequests++;
            KlineQueries.Add(request.RequestUri.AbsoluteUri);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(_klineUtf8)
            });
        }

        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new ByteArrayContent(_quoteGbk)
        });
    }
}

sealed class RecordingHandler : HttpMessageHandler
{
    public HttpMethod? LastMethod { get; private set; }
    public string? LastContentType { get; private set; }
    public byte[]? LastBody { get; private set; }
    public string ResponseBody { get; init; } = string.Empty;
    public string? LastAuthorization { get; private set; }
    public Uri? LastUri { get; private set; }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        LastMethod = request.Method;
        LastContentType = request.Content?.Headers.ContentType?.MediaType;
        LastBody = request.Content is null ? null : await request.Content.ReadAsByteArrayAsync(cancellationToken);
        LastAuthorization = request.Headers.Authorization?.ToString();
        LastUri = request.RequestUri;
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(ResponseBody)
        };
    }
}
