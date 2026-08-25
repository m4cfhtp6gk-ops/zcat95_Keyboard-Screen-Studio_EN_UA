using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
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
Assert(defaults.ClaudeUsage.ModelScope == "opus", "Claude usage must start on the Opus row");
// Nothing to configure and no cached organization: the screen borrows the
// Claude Code login, so a fresh machine reports "not signed in", never "no key".
Assert(defaults.ClaudeUsage.IsConfigured && defaults.ClaudeUsage.ModelScope == "opus",
    "Claude usage must start with nothing to configure");
// The shipped default used to be an id nothing could produce, so the font
// drop-down was empty on every first launch while the screen rendered in MiSans.
Assert(defaults.SelectedFontId == ScreenFontOption.DefaultId,
    "the default font id must be one the catalogue can actually offer");
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
Assert(themes.Count == 33, "built-in theme catalog should contain the 33 supported schemes");
Assert(themes.Any(theme => theme.Id == "composer"), "the screen-builder theme must be registered");
Assert(themes.All(theme => theme.Id != "ambient"), "the removed ambient theme must not be registered");
Assert(themes.Any(theme => theme.Id == "calendar"), "the ICS calendar theme must be registered");
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
Assert(themes.Any(theme => theme.Id == "alerts"), "air alerts theme must be registered");

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
var expired = new ClaudeUsageWindow(ClaudeUsageWindowKind.Session, 88, DateTimeOffset.Now.AddMinutes(-1));
Assert(expired.EffectivePercent == 0, "a window past its reset time reads as empty");
Console.WriteLine("PASS Claude usage theme states");
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
    """{"current":{"temperature_2m":31.4,"apparent_temperature":34.2,"relative_humidity_2m":58,"weather_code":2,"is_day":1},"daily":{"time":["2026-07-28","2026-07-29","2026-07-30","2026-07-31","2026-08-01"],"weather_code":[2,3,61,1,0],"temperature_2m_max":[32,31,29,33,34],"temperature_2m_min":[24,23,22,24,25],"sunrise":["2026-07-28T05:12"],"sunset":["2026-07-28T19:33"]}}""",
    """{"current":{"european_aqi":37.4}}"""
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
    Assert(weatherSnapshot.Sunrise?.Hour == 5 && weatherSnapshot.Sunrise?.Minute == 12, "sunrise was not parsed");
    Assert(weatherSnapshot.Sunset?.Hour == 19 && weatherSnapshot.Sunset?.Minute == 33, "sunset was not parsed");
    Assert(weatherSnapshot.EuropeanAqi == 37, "the European AQI was not parsed");
    Assert(WeatherSnapshot.AqiLabel(37) == Loc.T("ScreenAqiFair"), "the AQI band label is wrong");
    var cachedWeather = await weatherSource.ReadAsync(new WeatherSettings { LocationQuery = "北京" });
    Assert(ReferenceEquals(weatherSnapshot, cachedWeather), "weather snapshot should use the ten-minute cache");
    Assert(weatherHandler.RequestCount == 3, "cached weather read must not call the APIs again");
    var fiveDayFrame = renderer.Render(themes.Single(theme => theme.Id == "weather-five-day"), SystemSnapshot.DesignSample with { Weather = weatherSnapshot });
    Assert(fiveDayFrame.JpegBytes is [0xFF, 0xD8, ..], "five-day weather data view did not render");
}
Console.WriteLine("PASS Open-Meteo geocoding/current weather parser and cache");

var automaticWeatherResponses = new Queue<string>(new[]
{
    """{"current":{"temperature_2m":27.2,"apparent_temperature":28.1,"relative_humidity_2m":64,"weather_code":1,"is_day":1},"daily":{"time":["2026-07-29"],"weather_code":[1],"temperature_2m_max":[30],"temperature_2m_min":[24]}}""",
    """{"error":true}"""
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
    Assert(automaticWeatherHandler.RequestCount == 2, "automatic coordinates must bypass city geocoding");
    Assert(automaticWeather.EuropeanAqi is null && automaticWeather.Sunrise is null,
        "a weather response without sun or air data must leave those readings out");
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
    var settings = new AppSettings { SelectedThemeId = "music", RefreshSeconds = 17, AccentColor = "#A23BFF", SelectedFontId = "file:test.ttf|test", SafeArea = new ScreenInsets(11, 53, 9, 13), AiQuota = new AiQuotaSettings { DataKind = AiUsageDataKind.ModelCost, SelectedItemKey = "model:test", DisplayName = "My AI", ProgressTarget = 25 }, Weather = new WeatherSettings { LocationQuery = "上海", UseAutomaticLocation = true }, Stocks = new StockSettings { SourceKind = StockSourceKind.Yahoo, RedForGain = false, Items = [new StockItemSettings { Symbol = "0700.HK", Alias = "腾讯", Enabled = false }] }, ImageTimePlacement = ImageTimePlacement.Top, ImageClockStyle = ImageClockStyle.Flip, ImageTimeBackground = false, ImageTextColor = ImageTextColor.Black, ImageTextAlignment = ImageTextAlignment.Right, ImageWeatherVisible = true, ImageTimeFontSize = 34, ImageDateFontSize = 15, ImageWeatherFontSize = 13, ImageDigitalOrder = ImageDigitalOrder.WeatherTimeDate, ImageLargeTimeFontSize = 42, ImageAnalogClockSize = 94, ImageAnalogOrder = ImageAnalogOrder.DateWeatherClock, ImageFlipTimeFontSize = 35, IgnoreBrowserMediaSessions = false, UiThemeMode = UiThemeMode.Dark, Language = "uk", ClaudeUsage = new ClaudeUsageSettings { ModelScope = "sonnet" }, HasAcknowledgedClaudeNotice = true, DotMatrixProgressPeriod = DotMatrixProgressPeriod.Quarter, DotMatrixProgressHeaderFontSize = 18, LaunchAtStartup = true, AutoMediaThemeSwitch = true, MediaPlayingThemeId = "music-poster", MediaIdleThemeId = "clock-neon" , HasCompletedOnboarding = true, HasAcknowledgedStockNotice = true, HasAcknowledgedAiUsageNotice = true };
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
    Assert(loadedSettings.ClaudeUsage.ModelScope == "sonnet"
        && loadedSettings.ClaudeUsage.ModelScope == "sonnet", "Claude usage settings did not persist");
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

// The readouts are right-aligned, and aligning against an unbounded width put
// them at x = +infinity: every panel drew its label and silently lost its number.
// Two fresh themes hold one sample each, so neither draws a curve and the numbers
// are the only thing that can differ between the frames.
var perfValuesOn = new PerformanceVisualTheme();
var perfValuesOff = new PerformanceVisualTheme();
perfValuesOff.SetValuesVisible(false);
var perfWithValues = renderer.Render(perfValuesOn, SystemSnapshot.DesignSample);
var perfWithoutValues = renderer.Render(perfValuesOff, SystemSnapshot.DesignSample);
Assert(!perfWithValues.JpegBytes.AsSpan().SequenceEqual(perfWithoutValues.JpegBytes),
    "the numeric readout toggle changed nothing on screen");
Assert(MeanLuma(perfWithValues.JpegBytes) > MeanLuma(perfWithoutValues.JpegBytes) + 0.3,
    "the performance-visual readouts drew no ink; right-aligned text needs the box it aligns in");
Console.WriteLine("PASS performance-visual numeric readouts");

var perfSettingsPath = Path.Combine(Path.GetTempPath(), $"keyboard-screen-perf-settings-{Guid.NewGuid():N}.json");
try
{
    var perfStore = new JsonSettingsStore(perfSettingsPath);
    var perfSettings = new AppSettings { PerfVisualUploadEnabled = false, PerfVisualGpuEnabled = false };
    await perfStore.SaveAsync(perfSettings);
    var perfLoaded = await perfStore.LoadAsync();
    Assert(!perfLoaded.PerfVisualUploadEnabled && !perfLoaded.PerfVisualGpuEnabled, "performance-visual module toggles did not persist");
    Assert(new AppSettings().PerfVisualValuesEnabled, "the numeric readout must be on for a first run");
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
// The screen asks claude.ai for the account's own windows, authenticated with
// the OAuth token Claude Code already holds. The endpoint was never the problem
// - the very first design called this same URL - so these tests pin the two
// things that were: finding the credential, and reading the payload.

// Credentials: the file's shape is not a documented contract, so the token is
// found by walking the JSON rather than by one assumed path.
var credFlat = ClaudeCodeCredentials.Parse("""{"accessToken":"tok-flat"}""", "test");
Assert(credFlat?.AccessToken == "tok-flat", "a token at the root must be found");

var credNested = ClaudeCodeCredentials.Parse(
    """{"claudeAiOauth":{"accessToken":"tok-nested","refreshToken":"r","expiresAt":4102444800000}}""",
    "test");
Assert(credNested?.AccessToken == "tok-nested", "a nested token must be found");
Assert(credNested?.ExpiresAt?.Year == 2100, "a millisecond epoch expiry must be read");
Assert(!credNested!.IsExpired, "an expiry far in the future is not expired");

Assert(ClaudeCodeCredentials.Parse("""{"a":{"b":{"access_token":"tok-snake"}}}""", "t")?.AccessToken == "tok-snake",
    "the snake_case spelling must be accepted too");
Assert(ClaudeCodeCredentials.Parse("""{"expiresAt":1,"accessToken":"tok-old"}""", "t")!.IsExpired,
    "a past expiry must read as expired");
Assert(ClaudeCodeCredentials.Parse("""{"accessToken":""}""", "t") is null,
    "an empty token is not a credential");
Assert(ClaudeCodeCredentials.Parse("not json", "t") is null, "a torn file must not throw");

// A file that holds only a refresh token holds nothing we can use. Presenting
// one as a bearer fails, and it is the more sensitive half of the pair, so it
// must never be picked up by a looser name match.
Assert(ClaudeCodeCredentials.Parse("""{"claudeAiOauth":{"refreshToken":"ref-only"}}""", "t") is null,
    "a refresh token is never the credential");
Assert(ClaudeCodeCredentials.Parse(
    """{"a":{"refreshToken":"ref"},"b":{"accessToken":"acc"}}""", "t")?.AccessToken == "acc",
    "and a real access token beside one still wins");

// A real token wins over a weaker name no matter which comes first in the file.
Assert(ClaudeCodeCredentials.Parse(
    """{"gateway":{"token":"weak"},"claudeAiOauth":{"accessToken":"strong"}}""", "t")?.AccessToken == "strong",
    "an access token outranks a bare 'token' found earlier");
Assert(ClaudeCodeCredentials.Parse("""{"gateway":{"token":"only-this"}}""", "t")?.AccessToken == "only-this",
    "but a bare 'token' is better than giving up");

// Some stores keep the credential as one serialized blob inside a string.
Assert(ClaudeCodeCredentials.Parse(
    """{"payload":"{\"accessToken\":\"nested-blob\"}"}""", "t")?.AccessToken == "nested-blob",
    "a JSON document inside a string value must be stepped into");

// The same file stores the OAuth state of every MCP server a plugin connected.
// Each of those has its own accessToken belonging to Linear, Notion or whoever
// else - sending one to api.anthropic.com would fail and would hand a third
// party's credential to a fourth. Loosening the name match in v1.10.3 opened
// that door on a real user's file; it stays shut.
const string mcpOnly = """
{"mcpOAuth":{"plugin:design:linear|638130da58374":{"serverName":"linear",
"accessToken":"lin_oauth_SECRET","clientId":"c1"},
"plugin:design:notion|eac663db915250e7":{"serverName":"notion",
"accessToken":"ntn_SECRET","clientId":"c2"}}}
""";
Assert(ClaudeCodeCredentials.Parse(mcpOnly, "t") is null,
    "an MCP server's own access token is never Claude Code's login");

Assert(ClaudeCodeCredentials.Parse(
    """{"mcpOAuth":{"plugin:x|1":{"accessToken":"third-party"}},"claudeAiOauth":{"accessToken":"ours"}}""",
    "t")?.AccessToken == "ours",
    "and the real login is still found in a file that holds both");

// The shape report must reach the top-level keys. Walking depth first hit the
// cap inside the first branch, so on the file above it listed one MCP server's
// fields and never reached claudeAiOauth - which was the whole question.
string mcpShape = ClaudeCodeCredentials.DescribeShape(mcpOnly);
Assert(mcpShape.StartsWith("mcpOAuth", StringComparison.Ordinal),
    "the top-level key comes first");
Assert(!mcpShape.Contains("SECRET", StringComparison.Ordinal), "and never a value");

string bothShape = ClaudeCodeCredentials.DescribeShape(
    """{"mcpOAuth":{"a":{"b":{"c":{"d":{"e":1}}}}},"claudeAiOauth":{"accessToken":"x"}}""");
Assert(bothShape.Contains("claudeAiOauth", StringComparison.Ordinal),
    "a top-level key is never buried by a deep branch that came before it");

// When there is genuinely no token, the shape is what tells us which login the
// machine actually has - names only, never values.
string shape = ClaudeCodeCredentials.DescribeShape("""{"claudeApiKey":{"apiKey":"sk-secret"}}""");
Assert(shape.Contains("claudeApiKey", StringComparison.Ordinal) && shape.Contains("apiKey", StringComparison.Ordinal),
    "the property names are reported");
Assert(!shape.Contains("sk-secret", StringComparison.Ordinal), "and no value ever is");
Assert(ClaudeCodeCredentials.Read(Path.Combine(Path.GetTempPath(), $"kss-no-creds-{Guid.NewGuid():N}.json")) is null,
    "a missing credentials file simply means Claude Code is not signed in");

// Every way this can fail has to be told apart. The first version collapsed all
// of them into one null, and the settings page then said "no login at <path>"
// even when the file was sitting right there - which sends the user off to
// reinstall something that was never missing.
string credDir = Path.Combine(Path.GetTempPath(), $"kss-creds-{Guid.NewGuid():N}");
string credFile = Path.Combine(credDir, ".credentials.json");
Assert(ClaudeCodeCredentials.Locate(credFile).Problem == ClaudeCredentialProblem.NoDirectory,
    "a folder that does not exist means Claude Code has not run for this user");

Directory.CreateDirectory(credDir);
try
{
    File.WriteAllText(Path.Combine(credDir, "settings.json"), "{}");
    var missing = ClaudeCodeCredentials.Locate(credFile);
    Assert(missing.Problem == ClaudeCredentialProblem.NoFile, "an existing folder without the file is its own case");
    Assert(missing.Detail.Contains("settings.json", StringComparison.Ordinal),
        "the folder's contents distinguish 'never signed in' from 'wrong folder'");

    File.WriteAllText(credFile, "{ this is not json");
    Assert(ClaudeCodeCredentials.Locate(credFile).Problem == ClaudeCredentialProblem.Unparseable,
        "a half-written file is a bad moment, not a missing login");

    File.WriteAllText(credFile, """{"somethingElse":{"note":"no token here"}}""");
    Assert(ClaudeCodeCredentials.Locate(credFile).Problem == ClaudeCredentialProblem.NoTokenInside,
        "valid JSON with no token means this is not the file we want");

    File.WriteAllText(credFile, """{"claudeAiOauth":{"accessToken":"tok-real"}}""");
    var found = ClaudeCodeCredentials.Locate(credFile);
    Assert(found.Problem == ClaudeCredentialProblem.None && found.Credential?.AccessToken == "tok-real",
        "and the happy path still reads the token");

    // Claude Code refreshes this file about hourly and may hold it open while it
    // does. A read that cannot share was reported as "no login at <path>".
    using (var held = new FileStream(credFile, FileMode.Open, FileAccess.ReadWrite, FileShare.Read))
    {
        Assert(ClaudeCodeCredentials.Locate(credFile).Credential?.AccessToken == "tok-real",
            "a refresh in flight must not read as a missing login");
    }
}
finally
{
    Directory.Delete(credDir, recursive: true);
}

// The search must cover more than the one documented path. Knowing exactly one
// place is what made the previous version useless to anyone whose Claude Code
// lives somewhere else - and it could only repeat that one path back at them.
var searchPaths = ClaudeCodeCredentials.SearchPaths(includeSlowPaths: false);
Assert(searchPaths.Count >= 2, "more than one location must be searched");
Assert(searchPaths.All(path => Path.GetFileName(path) == ".credentials.json"),
    "every candidate is a credentials file, dot included");
Assert(searchPaths.Distinct(StringComparer.OrdinalIgnoreCase).Count() == searchPaths.Count,
    "a duplicate candidate would report the same miss twice");
Assert(searchPaths.Any(path => path.Contains(".claude", StringComparison.Ordinal)),
    "the documented location stays in the list");

// CLAUDE_CONFIG_DIR still wins, and a login there is found by the full search
// rather than only by an explicitly supplied path.
string movedDir = Path.Combine(Path.GetTempPath(), $"kss-moved-{Guid.NewGuid():N}");
Directory.CreateDirectory(movedDir);
string? previousConfigDir = Environment.GetEnvironmentVariable("CLAUDE_CONFIG_DIR");
try
{
    File.WriteAllText(Path.Combine(movedDir, ".credentials.json"),
        """{"claudeAiOauth":{"accessToken":"tok-moved"}}""");
    Environment.SetEnvironmentVariable("CLAUDE_CONFIG_DIR", movedDir);
    Assert(ClaudeCodeCredentials.SearchPaths(includeSlowPaths: false)[0].StartsWith(movedDir, StringComparison.Ordinal),
        "a moved config directory is searched first");
    Assert(ClaudeCodeCredentials.Locate().Credential?.AccessToken == "tok-moved",
        "and the login inside it is what the full search returns");
}
finally
{
    Environment.SetEnvironmentVariable("CLAUDE_CONFIG_DIR", previousConfigDir);
    Directory.Delete(movedDir, recursive: true);
}

// The env var outranks every file, exactly as it does for Claude Code itself.
string? previousToken = Environment.GetEnvironmentVariable(ClaudeCodeCredentials.TokenEnvironmentVariable);
try
{
    Environment.SetEnvironmentVariable(ClaudeCodeCredentials.TokenEnvironmentVariable, "tok-env");
    var viaEnvironment = ClaudeCodeCredentials.Locate();
    Assert(viaEnvironment.Credential?.AccessToken == "tok-env", "the environment variable wins");
    Assert(viaEnvironment.Credential?.Source == ClaudeCodeCredentials.TokenEnvironmentVariable,
        "and it is reported by name, never by value");
}
finally
{
    Environment.SetEnvironmentVariable(ClaudeCodeCredentials.TokenEnvironmentVariable, previousToken);
}

// A failed search has to name where it went, or the user cannot tell us it
// missed their install.
var missedEverywhere = ClaudeCodeCredentials.Locate();
if (missedEverywhere.Credential is null)
{
    Assert(missedEverywhere.Searched.Count >= 2, "a failed search reports every place it tried");
}

// No credential at all: the screen says so rather than inventing a number.
using (var signedOut = new ClaudeUsageSnapshotSource(
    new HttpClient(new ClaudeHandler(new Queue<string>())),
    () => new ClaudeCredentialLookup(null, ClaudeCredentialProblem.NoFile, "C:\\x\\.credentials.json", "settings.json")))
{
    var none = await signedOut.ReadAsync(new ClaudeUsageSettings());
    Assert(!none.Available, "no Claude Code login must read as unavailable");
    var report = await signedOut.CheckAsync(new ClaudeUsageSettings());
    Assert(!report.Success, "the check must fail when there is no login");
    Assert(report.Detail.Contains("settings.json", StringComparison.Ordinal),
        "and it must report what it actually saw, not just repeat the path");
}

using (var staleLogin = new ClaudeUsageSnapshotSource(
    new HttpClient(new ClaudeHandler(new Queue<string>())),
    () => ClaudeCredentialLookup.From(new ClaudeCodeCredential("tok", DateTimeOffset.Now.AddMinutes(-1), "test"))))
{
    Assert(!(await staleLogin.ReadAsync(new ClaudeUsageSettings())).Available,
        "an expired login must read as unavailable");
}

// The live path. The token was never the problem after the cookie was dropped;
// the host was. claude.ai/api authenticates a browser session, and the Claude
// Code token is a bearer credential for api.anthropic.com - which is also
// scoped by the token, so there is no organization to resolve and one call does
// the whole job.
var claudeResponses = new Queue<string>(new[]
{
    """{"five_hour":{"utilization":42,"resets_at":"2099-08-21T21:59:59Z"},"seven_day":{"utilization":"73%","resets_at":"2099-08-25T16:59:59Z"},"seven_day_opus":{"utilization":91.5,"resets_at":"2099-08-25T16:59:59Z"}}"""
});
var claudeHandler2 = new ClaudeHandler(claudeResponses);
using (var claudeClient = new HttpClient(claudeHandler2))
using (var live = new ClaudeUsageSnapshotSource(claudeClient,
           () => ClaudeCredentialLookup.From(new ClaudeCodeCredential("tok-live", null, "test")))
       { BaseUrl = "https://anthropic.test" })
{
    var settings = new ClaudeUsageSettings { ModelScope = "opus" };
    var snapshot = await live.ReadAsync(settings);
    Assert(snapshot.Available, "a good token and payload must produce a snapshot");
    Assert(Math.Abs(snapshot.Session!.UtilizationPercent - 42) < 0.001, "the session percent is the account's own");
    Assert(Math.Abs(snapshot.Week!.UtilizationPercent - 73) < 0.001, "a percent string must parse");
    Assert(Math.Abs(snapshot.ModelWeek!.UtilizationPercent - 91.5) < 0.001, "the per-model window must be read");
    Assert(snapshot.Session.ResetsAt?.Year == 2099, "the reset time must be carried through");
    Assert(claudeHandler2.Requests is [ "https://anthropic.test/api/oauth/usage" ],
        "exactly one call, to the endpoint that takes a bearer token");
    Assert(claudeHandler2.AuthorizationHeaders.All(h => h == "Bearer tok-live"),
        "every call must present the Claude Code token as a bearer, never a cookie");
    Assert(claudeHandler2.CookieHeaders.Count == 0, "no cookie may be sent");
    Assert(claudeHandler2.BetaHeaders.All(h => h == "oauth-2025-04-20"),
        "the OAuth contract is opt-in through anthropic-beta");
    // A different user agent is served by a bucket that throttles hard enough
    // to look like a broken feature, so this one is load-bearing, not cosmetic.
    Assert(claudeHandler2.UserAgents.All(h => h.StartsWith("claude-code/", StringComparison.Ordinal)),
        "the request must identify as the Claude Code client it borrows its login from");
}

// A refusal must not become a habit: retrying 429 on the next refresh is how a
// minute of throttling turns into an hour of it.
var throttled = new ClaudeHandler(new Queue<string>()) { Status = HttpStatusCode.TooManyRequests };
using (var throttledClient = new HttpClient(throttled))
using (var limited = new ClaudeUsageSnapshotSource(throttledClient,
           () => ClaudeCredentialLookup.From(new ClaudeCodeCredential("tok-live", null, "test")))
       { BaseUrl = "https://anthropic.test" })
{
    var first = await limited.ReadAsync(new ClaudeUsageSettings());
    var second = await limited.ReadAsync(new ClaudeUsageSettings());
    Assert(!first.Available && !second.Available, "a throttled call cannot produce a snapshot");
    Assert(throttled.Requests.Count == 1, "the second read must be answered from the backoff, not the network");
    Assert(first.ErrorMessage == second.ErrorMessage && (first.ErrorMessage ?? string.Empty).Length > 0,
        "the screen keeps saying what went wrong while it waits");
}

// Both spellings of every field, because the two descriptions of this payload
// in the wild disagree and guessing wrong costs a release to discover.
using (var alt = JsonDocument.Parse(
    """{"five_hour":{"utilization_pct":12,"reset_at":"2099-01-01T00:00:00Z"}}"""))
{
    var parsed = ClaudeUsageSnapshotSource.Parse(alt.RootElement, "opus", DateTimeOffset.Now);
    Assert(parsed.Available && Math.Abs(parsed.Session!.UtilizationPercent - 12) < 0.001,
        "utilization_pct must be accepted as well as utilization");
    Assert(parsed.Session!.ResetsAt?.Year == 2099, "reset_at must be accepted as well as resets_at");
}

// Newer payloads null the per-model object and report it in a limits array.
using (var limits = JsonDocument.Parse(
    """{"five_hour":{"utilization":5},"limits":[{"kind":"weekly_scoped","percent":64,"scope":{"model":{"id":"claude-opus-5","display_name":"Opus"}}}]}"""))
{
    var parsed = ClaudeUsageSnapshotSource.Parse(limits.RootElement, "opus", DateTimeOffset.Now);
    Assert(parsed.ModelWeek is { ScopeName: "Opus" } && Math.Abs(parsed.ModelWeek.UtilizationPercent - 64) < 0.001,
        "a limits[] entry must win for the per-model window");
}

// The bar colour is a ramp, not three steps: at 4% and at 74% the old rule drew
// exactly the same colour, so only the number carried the change.
var low = ClaudeUsagePalette.ForPercent(0);
var mid = ClaudeUsagePalette.ForPercent(55);
var high = ClaudeUsagePalette.ForPercent(100);
Assert(low.G > low.R && low.G > low.B, "empty reads green");
Assert(high.R > high.G && high.R > high.B, "full reads red");
Assert(mid.R > 180 && mid.G > 130 && mid.B < 120, "the middle is amber, not muddy olive");
Assert(ClaudeUsagePalette.ForPercent(20).R < ClaudeUsagePalette.ForPercent(40).R,
    "and it moves continuously rather than in steps");
Assert(ClaudeUsagePalette.ForPercent(-10) == low && ClaudeUsagePalette.ForPercent(140) == high,
    "out-of-range figures clamp to the ends");

// Which models the account is metered on is Anthropic's decision. A scope it
// does not report used to leave the third row missing with no explanation.
using (var scopes = JsonDocument.Parse(
    """{"five_hour":{"utilization":5},"seven_day_opus":{"utilization":10},"seven_day_sonnet":{"utilization":20}}"""))
{
    var parsed = ClaudeUsageSnapshotSource.Parse(scopes.RootElement, "fable", DateTimeOffset.Now);
    Assert(parsed.Available, "the session and week still come through");
    Assert(parsed.ModelWeek is null, "a model the account does not meter has no window");
    Assert(parsed.AvailableModelScopes.OrderBy(x => x).SequenceEqual(new[] { "opus", "sonnet" }),
        "and the ones it does meter are reported, so the screen can say which");
}

// A window whose reset has passed reads as empty, not as its last percentage.
var expiredWindow = new ClaudeUsageWindow(ClaudeUsageWindowKind.Session, 88, DateTimeOffset.Now.AddMinutes(-1));
Assert(expiredWindow.HasReset && expiredWindow.EffectivePercent == 0, "a window past its reset must read empty");

Console.WriteLine("PASS Claude usage: credential discovery, bearer auth on api.anthropic.com, throttle backoff, payload spellings, limits[]");

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
    """{"result":"success","base_code":"USD","time_last_update_unix":1787990402,"rates":{"USD":1,"EUR":0.8613,"UAH":45.05,"PLN":3.664}}"""
});
var currencyHandler = new ClaudeHandler(currencyResponses);
using (var currencyClient = new HttpClient(currencyHandler))
using (var currencySource = new CurrencySnapshotSource(currencyClient) { BaseUrl = "https://rates.test/v6/latest/" })
{
    // USD duplicates the base and "bad" is not a code: both must be dropped.
    var currencySettings = new CurrencySettings
    {
        SourceKind = CurrencySourceKind.ExchangeRateApi,
        BaseCurrency = "usd",
        QuoteCurrencies = ["EUR", "uah", "PLN", "USD", "bad"]
    };
    var currencySnapshot = await currencySource.ReadAsync(currencySettings);
    Assert(currencyHandler.Requests[0] == "https://rates.test/v6/latest/USD", "the base currency must form the request path");
    Assert(currencySnapshot.Available && currencySnapshot.Rates.Count == 3, "three valid quote currencies should parse");
    Assert(currencySnapshot.Rates[1].Code == "UAH" && Math.Abs(currencySnapshot.Rates[1].Rate - 45.05) < 0.0001,
        "the UAH rate was parsed incorrectly");
    Assert(currencySnapshot.DataDate == DateOnly.FromDateTime(DateTimeOffset.FromUnixTimeSeconds(1787990402).ToLocalTime().Date),
        "the ExchangeRate-API data date must come from time_last_update_unix");
    var currencyCached = await currencySource.ReadAsync(currencySettings);
    Assert(ReferenceEquals(currencySnapshot, currencyCached), "currency reads inside the cache window must not call the API");
    Assert(currencyHandler.Requests.Count == 1, "exactly one rates request expected");
    var currencyTheme = themes.Single(theme => theme.Id == "currency");
    var currencyFrame = renderer.Render(currencyTheme, SystemSnapshot.DesignSample with { Currency = currencySnapshot });
    Assert(currencyFrame.JpegBytes is [0xFF, 0xD8, ..], "the currency view did not render");
    var currencyEmpty = renderer.Render(currencyTheme, SystemSnapshot.DesignSample with { Currency = null });
    Assert(currencyEmpty.JpegBytes is [0xFF, 0xD8, ..], "the empty currency view did not render");
}

// The default source: the currency-api dataset with lowercase codes.
var cdnHandler = new ClaudeHandler(new Queue<string>(new[]
{
    """{"date":"2026-08-22","usd":{"eur":0.8613,"uah":45.02,"pln":3.664,"usd":1}}"""
}));
using (var cdnClient = new HttpClient(cdnHandler))
using (var cdnSource = new CurrencySnapshotSource(cdnClient) { CurrencyApiBaseUrl = "https://cdn.test/currencies/" })
{
    var cdnSnapshot = await cdnSource.ReadAsync(new CurrencySettings
    {
        BaseCurrency = "USD",
        QuoteCurrencies = ["EUR", "UAH"]
    });
    Assert(cdnHandler.Requests[0] == "https://cdn.test/currencies/usd.json",
        "the currency-api request must use the lowercase base");
    Assert(cdnSnapshot.Available && cdnSnapshot.Rates.Count == 2
        && Math.Abs(cdnSnapshot.Rates[1].Rate - 45.02) < 0.0001,
        "currency-api lowercase rates must parse");
    Assert(cdnSnapshot.DataDate == new DateOnly(2026, 8, 22), "the currency-api data date must parse");
}

// The NBU official table: UAH per unit, other pairs crossed through UAH.
const string nbuJson = """
    [
      {"r030":840,"txt":"Долар США","rate":45.05,"cc":"USD","exchangedate":"22.08.2026"},
      {"r030":978,"txt":"Євро","rate":52.31,"cc":"EUR","exchangedate":"22.08.2026"},
      {"r030":985,"txt":"Злотий","rate":12.29,"cc":"PLN","exchangedate":"22.08.2026"}
    ]
    """;
using (var nbuClient = new HttpClient(new ClaudeHandler(new Queue<string>(new[] { nbuJson }))))
using (var nbuSource = new CurrencySnapshotSource(nbuClient) { NbuBaseUrl = "https://nbu.test/exchange?json" })
{
    var nbuSnapshot = await nbuSource.ReadAsync(new CurrencySettings
    {
        SourceKind = CurrencySourceKind.Nbu,
        BaseCurrency = "USD",
        QuoteCurrencies = ["UAH", "EUR"]
    });
    Assert(nbuSnapshot.Available && nbuSnapshot.Rates.Count == 2, "the NBU table must produce both quotes");
    Assert(Math.Abs(nbuSnapshot.Rates[0].Rate - 45.05) < 0.0001, "USD to UAH must be the official rate itself");
    Assert(Math.Abs(nbuSnapshot.Rates[1].Rate - 45.05 / 52.31) < 0.0001, "USD to EUR must cross through UAH");
    Assert(nbuSnapshot.DataDate == new DateOnly(2026, 8, 22), "the NBU exchangedate must parse");
}

// A blocked CDN must fall back to ExchangeRate-API without surfacing an error.
var fallbackHandler = new RoutingHandler(url =>
    url.Contains("cdn.test", StringComparison.Ordinal)
        ? null
        : """{"result":"success","time_last_update_unix":1787990402,"rates":{"UAH":45.05}}""");
using (var fallbackClient = new HttpClient(fallbackHandler))
using (var fallbackSource = new CurrencySnapshotSource(fallbackClient)
{
    CurrencyApiBaseUrl = "https://cdn.test/currencies/",
    BaseUrl = "https://rates.test/v6/latest/"
})
{
    var fallbackSnapshot = await fallbackSource.ReadAsync(new CurrencySettings
    {
        BaseCurrency = "USD",
        QuoteCurrencies = ["UAH"]
    });
    Assert(fallbackSnapshot.Available && !fallbackSnapshot.IsStale
        && Math.Abs(fallbackSnapshot.Rates[0].Rate - 45.05) < 0.0001,
        "a blocked CDN must fall back to ExchangeRate-API silently");
}

using (var noQuotesClient = new HttpClient(new ClaudeHandler(new Queue<string>())))
using (var noQuotesSource = new CurrencySnapshotSource(noQuotesClient))
{
    var noQuotes = await noQuotesSource.ReadAsync(new CurrencySettings { QuoteCurrencies = [] });
    Assert(!noQuotes.Available && noQuotes.ErrorMessage is not null,
        "no configured quotes must surface a message without a request");
}
Console.WriteLine("PASS currency source: three providers, NBU cross-rates, CDN fallback, data dates, cache");

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
Assert(HardwareMonitorTheme.FormatTemperature(0) == "—" && HardwareMonitorTheme.FormatClock(0) == "—"
    && HardwareMonitorTheme.FormatFan(0) == "—",
    "flat-zero sensor readings must draw as missing");
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

// A dash alone never says why a reading is missing, and the two reasons need
// different words: one the user can act on, one they cannot.
var hwSensorFrames = new List<(HardwareSensorAccess Access, byte[] Jpeg)>();
foreach (HardwareSensorAccess access in new[]
         {
             HardwareSensorAccess.Full,
             HardwareSensorAccess.NeedsAdministrator,
             HardwareSensorAccess.Unsupported
         })
{
    var frame = renderer.Render(hardwareTheme, SystemSnapshot.DesignSample with
    {
        Timestamp = DateTimeOffset.FromUnixTimeSeconds(0),
        Hardware = new HardwareSnapshot(true, new HardwareComponentSnapshot("CPU", 38.4), null, null, null, null,
            DateTimeOffset.Now, null, access)
    });
    Assert(frame.JpegBytes is [0xFF, 0xD8, ..], $"the hardware page did not render for {access}");
    hwSensorFrames.Add((access, frame.JpegBytes));
}

Assert(!hwSensorFrames[0].Jpeg.AsSpan().SequenceEqual(hwSensorFrames[1].Jpeg),
    "a driver that needs administrator rights must say so on the screen, not just dash the cell");
Assert(!hwSensorFrames[1].Jpeg.AsSpan().SequenceEqual(hwSensorFrames[2].Jpeg),
    "an elevated process with no sensors must not repeat the run-as-administrator advice");
Assert(HardwareSnapshot.Unavailable("driver").SensorAccess == HardwareSensorAccess.Full
    && HardwareSnapshot.Unavailable("driver", HardwareSensorAccess.NeedsAdministrator).SensorAccess
        == HardwareSensorAccess.NeedsAdministrator,
    "the unavailable snapshot must carry the sensor access it was given");
Console.WriteLine("PASS hardware monitor: page rotation, formatting, renders, sensor-gap reason");

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
    ClaudeUsage = new ClaudeUsageSettings { ModelScope = "opus" },
    GitHub = new GitHubSettings { Username = "zcat95", Token = "github_pat_secret" },
    Telegram = new TelegramSettings { ApiId = "12345", ApiHash = "hash-secret", PhoneNumber = "+380501234567", PopupSeconds = 9 },
    Notifications = new NotificationSettings { Enabled = true, PriceAlerts = [new PriceAlertSettings { Symbol = "BTCUSDT", Above = 100_000 }] },
    AirAlerts = new AirAlertSettings { Token = "alerts-token-secret", Location = "м. Київ", Takeover = AirAlertTakeoverMode.Popup }
};
string exported = SettingsPorter.ExportJson(portSource);
Assert(!exported.Contains("github_pat_secret")
    && !exported.Contains("hash-secret") && !exported.Contains("+380501234567")
    && !exported.Contains("alerts-token-secret"),
    "an export must never carry credentials");
Assert(exported.Contains("#123456") && exported.Contains("\"hardware\"") && exported.Contains("zcat95")
    && exported.Contains("BTCUSDT") && exported.Contains("opus"),
    "an export must keep the non-secret settings");
Assert(portSource.GitHub.Token == "github_pat_secret",
    "exporting must not touch the live settings object");

var portImported = SettingsPorter.ImportJson(exported, portSource);
Assert(portImported.ClaudeUsage.ModelScope == "opus"
    && portImported.GitHub.Token == "github_pat_secret"
    && portImported.Telegram.ApiHash == "hash-secret"
    && portImported.Telegram.PhoneNumber == "+380501234567"
    && portImported.AirAlerts.Token == "alerts-token-secret",
    "importing a stripped file must keep the secrets already on this machine");
Assert(portImported.AccentColor == "#123456" && portImported.Telegram.PopupSeconds == 9
    && portImported.Notifications.PriceAlerts.Count == 1
    && portImported.AirAlerts.Location == "м. Київ"
    && portImported.AirAlerts.Takeover == AirAlertTakeoverMode.Popup,
    "importing must carry the non-secret values through");
var portForeign = SettingsPorter.ImportJson(
    """{"AccentColor":"#ABCDEF","GitHub":{"Token":"github_pat_other"}}""", portSource);
Assert(portForeign.GitHub.Token == "github_pat_other" && portForeign.AccentColor == "#ABCDEF",
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

// ---- theme carousel ----------------------------------------------------------
var carousel = new CarouselSettings { Enabled = true, IntervalSeconds = 30, ThemeIds = ["clock", "system", "clock"] };
Assert(ThemeCarousel.ResolveThemeId(carousel, "image", DateTimeOffset.FromUnixTimeSeconds(0)) == "clock"
    && ThemeCarousel.ResolveThemeId(carousel, "image", DateTimeOffset.FromUnixTimeSeconds(29)) == "clock"
    && ThemeCarousel.ResolveThemeId(carousel, "image", DateTimeOffset.FromUnixTimeSeconds(30)) == "system"
    && ThemeCarousel.ResolveThemeId(carousel, "image", DateTimeOffset.FromUnixTimeSeconds(60)) == "clock",
    "the carousel must rotate on the wall clock with duplicates removed");
Assert(ThemeCarousel.ResolveThemeId(new CarouselSettings { Enabled = false, ThemeIds = ["clock", "system"] }, "image", DateTimeOffset.FromUnixTimeSeconds(0)) == "image",
    "a disabled carousel keeps the selected theme");
Assert(ThemeCarousel.ResolveThemeId(new CarouselSettings { Enabled = true, ThemeIds = ["clock"] }, "image", DateTimeOffset.FromUnixTimeSeconds(0)) == "image",
    "fewer than two themes keeps the selected theme");
Assert(ThemeCarousel.ResolveThemeId(new CarouselSettings { Enabled = true, IntervalSeconds = 1, ThemeIds = ["clock", "system"] }, "image", DateTimeOffset.FromUnixTimeSeconds(15)) == "system",
    "the interval must clamp to the 10-second floor");
Assert(ThemeCarousel.ResolveThemeId(null, "image", DateTimeOffset.FromUnixTimeSeconds(0)) == "image",
    "missing settings keep the selected theme");
Console.WriteLine("PASS theme carousel: rotation, dedup, clamps");

// ---- display units ----------------------------------------------------------
var unitsStamp = new DateTimeOffset(2026, 8, 21, 21, 5, 0, TimeSpan.Zero);
Assert(!DisplayUnits.Use12HourClock && !DisplayUnits.UseFahrenheit, "units must default to 24h and Celsius");
Assert(DisplayUnits.Time(unitsStamp) == "21:05" && DisplayUnits.Hours(unitsStamp) == "21",
    "24-hour formatting is wrong");
Assert(DisplayUnits.TemperatureShort(26.4) == "26°" && DisplayUnits.TemperatureWithUnit(26.4) == "26°C",
    "Celsius formatting is wrong");
try
{
    DisplayUnits.Use12HourClock = true;
    DisplayUnits.UseFahrenheit = true;
    Assert(DisplayUnits.Time(unitsStamp) == "9:05" && DisplayUnits.Hours(unitsStamp) == "9",
        "12-hour formatting is wrong");
    Assert(DisplayUnits.Time(unitsStamp.AddHours(-12)) == "9:05", "9 AM and 9 PM share digits without a marker");
    Assert(DisplayUnits.TemperatureShort(26.0) == "79°" && DisplayUnits.TemperatureWithUnit(0) == "32°F",
        "Fahrenheit conversion is wrong");
    Assert(HardwareMonitorTheme.FormatTemperature(62.0) == "144°",
        "hardware temperatures must follow the Fahrenheit preference");
    var units12Frame = renderer.Render(themes.Single(theme => theme.Id == "clock-flip"),
        SystemSnapshot.DesignSample with { Timestamp = unitsStamp });
    Assert(units12Frame.JpegBytes is [0xFF, 0xD8, ..], "the flip clock did not render in 12-hour mode");
}
finally
{
    DisplayUnits.Use12HourClock = false;
    DisplayUnits.UseFahrenheit = false;
}
Console.WriteLine("PASS display units: 12/24-hour time and Celsius/Fahrenheit");

// ---- per-theme refresh cadence ----------------------------------------------
var refreshSettings = new AppSettings();
Assert(ThemeRefreshPolicy.EffectiveSeconds(refreshSettings, "clock", 1) == 1
    && ThemeRefreshPolicy.EffectiveSeconds(refreshSettings, "clock", 5) == 5,
    "live themes must follow the global refresh setting");
Assert(ThemeRefreshPolicy.EffectiveSeconds(refreshSettings, "currency", 1) == 60
    && ThemeRefreshPolicy.EffectiveSeconds(refreshSettings, "weather-five-day", 1) == 60
    && ThemeRefreshPolicy.EffectiveSeconds(refreshSettings, "crypto", 1) == 30
    && ThemeRefreshPolicy.EffectiveSeconds(refreshSettings, "github", 1) == 300,
    "data themes must default to their built-in slower cadence");
refreshSettings.ThemeRefreshOverrides["currency"] = 10;
refreshSettings.ThemeRefreshOverrides["clock"] = 5;
Assert(ThemeRefreshPolicy.EffectiveSeconds(refreshSettings, "currency", 1) == 10
    && ThemeRefreshPolicy.EffectiveSeconds(refreshSettings, "clock", 1) == 5,
    "a per-theme override must win over the default and the global setting");
Assert(ThemeRefreshPolicy.EffectiveSeconds(refreshSettings, null, 3) == 3,
    "no theme id falls back to the global setting");

var refreshNow = new DateTimeOffset(2026, 8, 22, 10, 0, 12, TimeSpan.Zero);
Assert(ThemeRefreshPolicy.NextDelay(refreshNow, 1) == TimeSpan.FromSeconds(1),
    "a live theme sleeps exactly its interval");
TimeSpan slowDelay = ThemeRefreshPolicy.NextDelay(refreshNow, 60);
Assert(slowDelay > TimeSpan.FromSeconds(40) && slowDelay < TimeSpan.FromSeconds(50),
    "a slow theme must wake right after the next minute flip");
TimeSpan carouselDelay = ThemeRefreshPolicy.NextDelay(
    refreshNow, 600, new CarouselSettings { Enabled = true, IntervalSeconds = 30, ThemeIds = ["clock", "system"] });
Assert(carouselDelay <= TimeSpan.FromSeconds(31),
    "a running carousel must cap the sleep at its next boundary");
Assert(ThemeRefreshPolicy.NextDelay(refreshNow, 60, pollCapSeconds: 1) == TimeSpan.FromSeconds(1),
    "media polling must cap the sleep");
Console.WriteLine("PASS refresh cadence: defaults, overrides, minute alignment, carousel cap");

// ---- air alerts --------------------------------------------------------------
Assert(AirAlertSource.MatchesLocation("м. Київ", "Київ") && AirAlertSource.MatchesLocation("Київська область", "київ")
    && !AirAlertSource.MatchesLocation("Львівська область", "Київ"),
    "location matching must be case-insensitive and substring-based");
Assert(ThemeRefreshPolicy.EffectiveSeconds(new AppSettings(), "alerts", 1) == 30,
    "the alerts theme must default to a 30-second cadence");

const string alertsJson = """
    {"alerts":[
      {"id":1,"location_title":"м. Київ","location_type":"city","started_at":"2026-08-22T03:40:00.000Z","alert_type":"air_raid"},
      {"id":2,"location_title":"Чернігівська область","location_type":"oblast","started_at":"2026-08-22T03:12:00.000Z","alert_type":"air_raid"}
    ],"meta":{"last_updated_at":"2026-08-22T04:20:00.000Z"}}
    """;
var alertsHandler = new ClaudeHandler(new Queue<string>(new[] { alertsJson }));
using (var alertsClient = new HttpClient(alertsHandler))
using (var alertsSource = new AirAlertSource(alertsClient) { BaseUrl = "https://alerts.test/v1/alerts/active.json" })
{
    var alertsSettings = new AirAlertSettings { Token = "test-token", Location = "Київ" };
    var alertsSnapshot = await alertsSource.ReadAsync(alertsSettings);
    Assert(alertsHandler.Requests[0] == "https://alerts.test/v1/alerts/active.json", "the alerts request URL is wrong");
    Assert(alertsHandler.Cookies[0].Length == 0, "no cookies belong on the alerts request");
    Assert(alertsSnapshot.Available && alertsSnapshot.ActiveAlerts.Count == 2, "both alerts should parse");
    Assert(alertsSnapshot.AlertActiveAtLocation, "the Київ location must match м. Київ");
    Assert(alertsSnapshot.ActiveAlerts[0].StartedAt is not null, "started_at must parse");
    var alertsCached = await alertsSource.ReadAsync(alertsSettings);
    Assert(ReferenceEquals(alertsSnapshot, alertsCached), "alert reads inside the cache window must not call the API");
    Assert(alertsHandler.Requests.Count == 1, "exactly one alerts request expected");

    var alertsTheme = themes.Single(theme => theme.Id == "alerts");
    var alertFrame = renderer.Render(alertsTheme, SystemSnapshot.DesignSample);
    Assert(alertFrame.JpegBytes is [0xFF, 0xD8, ..], "the alert-state view did not render");
    var clearFrame = renderer.Render(alertsTheme, SystemSnapshot.DesignSample with
    {
        AirAlerts = new AirAlertSnapshot(true, false, [], "м. Київ", DateTimeOffset.Now)
    });
    Assert(clearFrame.JpegBytes is [0xFF, 0xD8, ..], "the clear-state view did not render");
    Assert(!clearFrame.JpegBytes.AsSpan().SequenceEqual(alertFrame.JpegBytes), "alert and clear states must differ");
    var countryFrame = renderer.Render(alertsTheme, SystemSnapshot.DesignSample with
    {
        AirAlerts = alertsSnapshot with { Location = string.Empty }
    });
    Assert(countryFrame.JpegBytes is [0xFF, 0xD8, ..], "the country summary did not render");
    var unconfiguredFrame = renderer.Render(alertsTheme, SystemSnapshot.DesignSample with { AirAlerts = null });
    Assert(unconfiguredFrame.JpegBytes is [0xFF, 0xD8, ..], "the unconfigured view did not render");
}

using (var badTokenClient = new HttpClient(new StatusHandler(System.Net.HttpStatusCode.Unauthorized)))
using (var badTokenSource = new AirAlertSource(badTokenClient) { BaseUrl = "https://alerts.test/v1/alerts/active.json" })
{
    var rejected = await badTokenSource.ReadAsync(new AirAlertSettings { Token = "bad" });
    Assert(!rejected.Available && rejected.ErrorMessage == Loc.T("AirAlertBadToken"),
        "a rejected token must surface the localized message");
}
Console.WriteLine("PASS air alerts: parsing, location match, cache, renders, bad token");

// ---- air alerts: oblast matching, alert duration, takeover ------------------
var raionAlert = new AirAlertInfo("Куп'янський район", "air_raid", null, "Харківська область");
Assert(AirAlertSource.Matches(raionAlert, "Харківська область") && !AirAlertSource.Matches(raionAlert, "Львівська область"),
    "a raion alert must count for its oblast and only its oblast");
Assert(AirAlertRegions.All.Count == 27 && AirAlertRegions.All[0] == "м. Київ"
    && AirAlertRegions.All.Contains("Харківська область"),
    "the region dropdown list must carry the 27 oblast-level entries");

const string alertsOblastJson = """
    {"alerts":[
      {"id":3,"location_title":"Куп'янський район","location_type":"raion","location_oblast":"Харківська область","started_at":"2026-08-22T03:10:00.000Z","alert_type":"air_raid"},
      {"id":4,"location_title":"м. Харків","location_type":"city","location_oblast":"Харківська область","started_at":"2026-08-22T02:55:00.000Z","alert_type":"air_raid"}
    ]}
    """;
const string alertsQuietJson = """{"alerts":[]}""";
var lastedHandler = new ClaudeHandler(new Queue<string>(new[] { alertsOblastJson, alertsQuietJson }));
using (var lastedClient = new HttpClient(lastedHandler))
using (var lastedSource = new AirAlertSource(lastedClient)
{
    BaseUrl = "https://alerts.test/v1/alerts/active.json",
    CacheDuration = TimeSpan.Zero
})
{
    var lastedSettings = new AirAlertSettings { Token = "test-token", Location = "Харківська область" };
    var duringAlert = await lastedSource.ReadAsync(lastedSettings);
    Assert(duringAlert.AlertActiveAtLocation, "the oblast must match its raion and city alerts");
    Assert(AirAlertSource.EarliestStart(duringAlert.ActiveAlerts, "Харківська область")
        == DateTimeOffset.Parse("2026-08-22T02:55:00.000Z"),
        "the alert start must be the earliest matching entry");
    var afterClear = await lastedSource.ReadAsync(lastedSettings);
    Assert(!afterClear.AlertActiveAtLocation
        && afterClear.LastAlertStartedAt == DateTimeOffset.Parse("2026-08-22T02:55:00.000Z")
        && afterClear.LastAlertEndedAt is not null,
        "going quiet must record how long the alert lasted");

    var alertsThemeAgain = themes.Single(theme => theme.Id == "alerts");
    var clearPlain = renderer.Render(alertsThemeAgain, SystemSnapshot.DesignSample with
    {
        AirAlerts = new AirAlertSnapshot(true, false, [], "Харківська область", DateTimeOffset.Now)
    });
    var clearWithLasted = renderer.Render(alertsThemeAgain, SystemSnapshot.DesignSample with
    {
        AirAlerts = afterClear with { UpdatedAt = DateTimeOffset.Now }
    });
    Assert(clearWithLasted.JpegBytes is [0xFF, 0xD8, ..]
        && !clearWithLasted.JpegBytes.AsSpan().SequenceEqual(clearPlain.JpegBytes),
        "the clear screen must show how long the alert lasted");
}

var takeoverSettings = new AirAlertSettings
{
    Token = "t",
    Location = "м. Київ",
    Takeover = AirAlertTakeoverMode.FullScreen,
    TakeoverUntilClear = true
};
var takeoverState = new AirAlertTakeoverState();
var takeoverT0 = new DateTimeOffset(2026, 8, 22, 4, 0, 0, TimeSpan.Zero);
var activeSnapshot = new AirAlertSnapshot(true, true,
    [new AirAlertInfo("м. Київ", "air_raid", takeoverT0.AddMinutes(-2))], "м. Київ", takeoverT0);
var quietSnapshot = new AirAlertSnapshot(true, false, [], "м. Київ", takeoverT0);

Assert(!AirAlertTakeover.IsArmed(new AirAlertSettings { Token = "t", Location = "м. Київ" }),
    "takeover must stay off by default");
Assert(!AirAlertTakeover.IsArmed(new AirAlertSettings { Token = "t", Takeover = AirAlertTakeoverMode.FullScreen }),
    "takeover without a picked region must stay unarmed");
Assert(AirAlertTakeover.IsArmed(takeoverSettings),
    "a mode plus token plus location arms the takeover");
Assert(!AirAlertTakeover.Decide(takeoverSettings, quietSnapshot, takeoverState, takeoverT0).Active,
    "quiet skies must not take over");
var engaged = AirAlertTakeover.Decide(takeoverSettings, activeSnapshot, takeoverState, takeoverT0);
Assert(engaged.ShowFullScreen && !engaged.IsAllClear, "an active alert must take over full screen");
Assert(AirAlertTakeover.Decide(takeoverSettings, null, takeoverState, takeoverT0.AddMinutes(1)).Active == false
    && takeoverState.EngagedAt is not null,
    "a transient outage must neither release nor reset the takeover");
Assert(AirAlertTakeover.Decide(takeoverSettings, activeSnapshot, takeoverState, takeoverT0.AddHours(3)).ShowFullScreen,
    "until-the-all-clear must hold for the whole alert");
var allClear = AirAlertTakeover.Decide(takeoverSettings, quietSnapshot, takeoverState, takeoverT0.AddHours(3).AddMinutes(1));
Assert(allClear.ShowFullScreen && allClear.IsAllClear, "the all-clear must linger on screen");
Assert(!AirAlertTakeover.Decide(takeoverSettings, quietSnapshot, takeoverState,
        takeoverT0.AddHours(3).AddMinutes(2).AddSeconds(5)).Active
    && takeoverState.EngagedAt is null,
    "after the linger the takeover must release and reset");

var timedSettings = new AirAlertSettings
{
    Token = "t",
    Location = "м. Київ",
    Takeover = AirAlertTakeoverMode.Popup,
    TakeoverUntilClear = false,
    TakeoverMinutes = 5
};
var timedState = new AirAlertTakeoverState();
Assert(AirAlertTakeover.Decide(timedSettings, activeSnapshot, timedState, takeoverT0).ShowPopup,
    "the popup mode must surface the banner");
Assert(!AirAlertTakeover.Decide(timedSettings, activeSnapshot, timedState, takeoverT0.AddMinutes(6)).Active,
    "a timed takeover must step aside after its minutes run out");
var timedClear = AirAlertTakeover.Decide(timedSettings, quietSnapshot, timedState, takeoverT0.AddMinutes(20));
Assert(timedClear.ShowPopup && timedClear.IsAllClear, "the all-clear banner must still appear after a timed hold");

var bannerTheme = themes.Single(theme => theme.Id == "clock-dot-matrix");
var noBanner = renderer.Render(bannerTheme, SystemSnapshot.DesignSample);
var alertBanner = renderer.Render(bannerTheme, SystemSnapshot.DesignSample,
    overlay: c => AirAlertPopupOverlay.Draw(c, false, "м. Київ"));
var clearBanner = renderer.Render(bannerTheme, SystemSnapshot.DesignSample,
    overlay: c => AirAlertPopupOverlay.Draw(c, true, "м. Київ"));
Assert(alertBanner.JpegBytes is [0xFF, 0xD8, ..] && clearBanner.JpegBytes is [0xFF, 0xD8, ..],
    "the alert banners did not render");
Assert(!alertBanner.JpegBytes.AsSpan().SequenceEqual(noBanner.JpegBytes)
    && !alertBanner.JpegBytes.AsSpan().SequenceEqual(clearBanner.JpegBytes),
    "the banner must draw over the theme and differ between states");
Console.WriteLine("PASS air alerts: oblast match, alert duration, takeover state machine, banners");

// ---- world clocks, countdown, disks ------------------------------------------
Assert(WorldClockTheme.TryGetTime("Europe/Kyiv", new DateTimeOffset(2026, 1, 15, 12, 0, 0, TimeSpan.Zero), out DateTimeOffset kyivWinter)
    && kyivWinter.Hour == 14,
    "Europe/Kyiv must resolve to UTC+2 in winter");
Assert(WorldClockTheme.TryGetTime("Asia/Tokyo", new DateTimeOffset(2026, 1, 15, 12, 0, 0, TimeSpan.Zero), out DateTimeOffset tokyo)
    && tokyo.Hour == 21,
    "Asia/Tokyo must resolve to UTC+9");
Assert(!WorldClockTheme.TryGetTime("Neverland/Nowhere", DateTimeOffset.Now, out _),
    "an unknown zone must fail gracefully");
Assert(WorldClockSnapshot.From(new WorldClockSettings
{
    Items =
    [
        new WorldClockItem { Label = "Київ", TimeZoneId = "Europe/Kyiv" },
        new WorldClockItem(),
        new WorldClockItem { Label = "", TimeZoneId = "Asia/Tokyo" }
    ]
}).Clocks.Count == 2, "empty world-clock rows must be skipped");

Assert(CountdownSettings.TryParseDate("2026-12-31", out DateTimeOffset newYearEve) && newYearEve.Month == 12,
    "ISO countdown dates must parse");
Assert(CountdownSettings.TryParseDate("31.12.2026 18:30", out DateTimeOffset withTime)
    && withTime.Hour == 18 && withTime.Minute == 30,
    "dotted countdown dates with a time must parse");
Assert(!CountdownSettings.TryParseDate("soon", out _), "junk countdown dates must be rejected");
var countdownSnapshot = CountdownSnapshot.From(new CountdownSettings
{
    Items =
    [
        new CountdownItem { Title = "B", Date = "2027-05-01" },
        new CountdownItem { Title = "A", Date = "2026-12-31" },
        new CountdownItem { Title = "", Date = "2026-11-01" }
    ]
});
Assert(countdownSnapshot.Events.Count == 2 && countdownSnapshot.Events[0].Title == "A",
    "countdown events must sort by date and skip untitled rows");

var diskSourceSnapshot = new DiskUsageSource().Read();
Assert(diskSourceSnapshot is not null, "the disk source must never throw");
Assert(new DiskVolume("C:", "", 95, 100).UsedPercent == 95, "disk percent math is wrong");

foreach (string newThemeId in new[] { "disks", "ping", "world-clock", "countdown", "calendar" })
{
    var newFrame = renderer.Render(themes.Single(theme => theme.Id == newThemeId), SystemSnapshot.DesignSample);
    Assert(newFrame.JpegBytes is [0xFF, 0xD8, ..], $"{newThemeId}: the design sample did not render");
    var emptyFrame = renderer.Render(themes.Single(theme => theme.Id == newThemeId), SystemSnapshot.DesignSample with
    {
        Disks = null, Ping = null, WorldClocks = null, Countdown = null, Calendar = null
    });
    Assert(emptyFrame.JpegBytes is [0xFF, 0xD8, ..], $"{newThemeId}: the empty state did not render");
}
Console.WriteLine("PASS world clocks, countdown, disks: zones, dates, renders");

// ---- ping monitor -------------------------------------------------------------
var emptyPing = await new PingMonitorSource().ReadAsync(new PingSettings { Hosts = ["", "  "] });
Assert(!emptyPing.Available, "no hosts must read as unavailable");
Assert(new PingHostSnapshot("x", null, [null]).IsReachable == false
    && new PingHostSnapshot("x", 12, [12.0]).IsReachable,
    "ping reachability must follow the latency");
Console.WriteLine("PASS ping monitor: empty hosts, reachability");

// ---- ICS calendar parser ------------------------------------------------------
const string icsFixture = """
    BEGIN:VCALENDAR
    VERSION:2.0
    BEGIN:VEVENT
    SUMMARY:Разова зустріч\, важлива
    DTSTART:20260825T140000
    END:VEVENT
    BEGIN:VEVENT
    SUMMARY:Щотижневий стендап
    DTSTART;TZID=Europe/Kyiv:20260824T110000
    RRULE:FREQ=WEEKLY;BYDAY=MO,WE;COUNT=6
    EXDATE;TZID=Europe/Kyiv:20260826T110000
    END:VEVENT
    BEGIN:VEVENT
    SUMMARY:День народження
    DTSTART;VALUE=DATE:20260901
    RRULE:FREQ=YEARLY
    END:VEVENT
    END:VCALENDAR
    """;
var icsWindowStart = new DateTimeOffset(2026, 8, 22, 0, 0, 0, DateTimeOffset.Now.Offset);
var icsEvents = IcsParser.Parse(icsFixture, icsWindowStart, icsWindowStart.AddDays(30));
Assert(icsEvents.Count(item => item.Title == "Разова зустріч, важлива") == 1,
    "the single event must parse once with unescaped commas");
var standups = icsEvents.Where(item => item.Title == "Щотижневий стендап").ToArray();
Assert(standups.Length == 5, $"weekly BYDAY with COUNT=6 minus one EXDATE must leave 5, got {standups.Length}");
Assert(standups.All(item => item.Start.Hour == 11 || item.Start.Hour != 0),
    "recurring times must carry the hour through");
Assert(standups.Select(item => item.Start.LocalDateTime.DayOfWeek)
        .All(day => day is DayOfWeek.Monday or DayOfWeek.Wednesday),
    "weekly BYDAY must expand onto the listed weekdays only");
Assert(icsEvents.Count(item => item.Title == "День народження" && item.IsAllDay) == 1,
    "the yearly all-day event must appear once inside the window");

var icsHandler = new ClaudeHandler(new Queue<string>(new[] { icsFixture }));
using (var icsClient = new HttpClient(icsHandler))
using (var icsSource = new IcsCalendarSource(icsClient))
{
    var calendarSnapshot = await icsSource.ReadAsync(new CalendarSettings { IcsUrl = "webcal://calendar.test/private-token/basic.ics" });
    Assert(calendarSnapshot.Available, "the calendar snapshot must be available");
    Assert(icsHandler.Requests[0].StartsWith("https://calendar.test/", StringComparison.Ordinal),
        "webcal:// must be fetched over https");
    var cachedCalendar = await icsSource.ReadAsync(new CalendarSettings { IcsUrl = "webcal://calendar.test/private-token/basic.ics" });
    Assert(ReferenceEquals(calendarSnapshot, cachedCalendar), "calendar reads must use the fifteen-minute cache");
}
Assert(!new CalendarSettings { IcsUrl = "ftp://x" }.IsConfigured
    && new CalendarSettings { IcsUrl = "https://x/cal.ics" }.IsConfigured,
    "only http(s) calendar links count as configured");
Console.WriteLine("PASS ICS calendar: unfolding, RRULE, EXDATE, webcal, cache");

// ---- stock portfolio ----------------------------------------------------------
var portfolioQuotes = new List<StockQuoteSnapshot>
{
    new("AAPL", "Apple", 100, 2.0, DateTimeOffset.Now, Quantity: 10),
    new("MSFT", "Microsoft", 50, -1.0, DateTimeOffset.Now, Quantity: 4),
    new("NOQTY", "NoQty", 77, 5.0, DateTimeOffset.Now)
};
var portfolioResult = StockPortfolio.Compute(portfolioQuotes);
Assert(portfolioResult is not null, "a portfolio with quantities must compute");
Assert(Math.Abs(portfolioResult!.Value.Value - 1200) < 0.001, "the portfolio value must sum price times quantity");
double expectedPrevious = 1000 / 1.02 + 200 / 0.99;
Assert(Math.Abs(portfolioResult.Value.DayChange - (1200 - expectedPrevious)) < 0.01,
    "the portfolio day change must derive from the change percents");
Assert(StockPortfolio.Compute([new("AAPL", "Apple", 100, 2.0, DateTimeOffset.Now)]) is null,
    "no quantities must mean no portfolio row");
var attachedStocks = StockPortfolio.Attach(
    new StockSnapshot([new("AAPL", "Apple", 100, 2.0, DateTimeOffset.Now)], DateTimeOffset.Now, true),
    new StockSettings { Items = [new() { Symbol = "aapl", Quantity = 3 }] });
Assert(attachedStocks.Quotes[0].Quantity == 3, "quantities must attach by symbol, case-insensitively");
var portfolioFrame = renderer.Render(themes.Single(theme => theme.Id == "stocks"), SystemSnapshot.DesignSample with
{
    Stocks = new StockSnapshot(portfolioQuotes, DateTimeOffset.Now, RedForGain: false)
});
Assert(portfolioFrame.JpegBytes is [0xFF, 0xD8, ..], "the portfolio strip did not render");
Console.WriteLine("PASS stock portfolio: math, attach, render");

// ---- per-theme accent and porter secrets --------------------------------------
var accentSettings = new AppSettings
{
    ThemeAccentOverrides = new Dictionary<string, string> { ["clock"] = "#3366FF" },
    Calendar = new CalendarSettings { IcsUrl = "https://calendar.test/secret-token/basic.ics" },
    AdditionalEndpoints = ["192.168.1.51"]
};
string accentExported = SettingsPorter.ExportJson(accentSettings);
Assert(!accentExported.Contains("secret-token"), "the calendar link must be stripped from exports");
Assert(accentExported.Contains("#3366FF") && accentExported.Contains("192.168.1.51"),
    "accent overrides and extra devices must survive the export");
var accentImported = SettingsPorter.ImportJson(accentExported, accentSettings);
Assert(accentImported.Calendar.IcsUrl == "https://calendar.test/secret-token/basic.ics",
    "importing a stripped file must keep the local calendar link");
Assert(accentImported.ThemeAccentOverrides["clock"] == "#3366FF",
    "accent overrides must round-trip through the porter");
Console.WriteLine("PASS per-theme accent and calendar-link stripping");

// ---- knob theme switching: the pure half --------------------------------------
string[] knobIds = ["clock", "weather-five-day", "stocks"];
Assert(KnobControl.Next(knobIds, "clock", 1) == "weather-five-day", "the knob must step to the next theme");
Assert(KnobControl.Next(knobIds, "stocks", 1) == "clock", "the knob must wrap forward past the end");
Assert(KnobControl.Next(knobIds, "clock", -1) == "stocks", "the knob must wrap backward past the start");
Assert(KnobControl.Next(knobIds, "CLOCK", 1) == "weather-five-day", "theme ids must match case-insensitively");
Assert(KnobControl.Next(knobIds, "unknown", 1) == "clock", "an unknown current theme must land on the first");
Assert(KnobControl.Next(knobIds, null, -1) == "clock", "no current theme must land on the first");
Assert(KnobControl.Next([], "clock", 1) is null, "an empty cycle must produce nothing to do");
Assert(KnobControl.Next(["solo"], "solo", 1) == "solo", "a single-entry cycle must stay put");

Assert(KnobControl.ResolveCycleList(
        new CarouselSettings { ThemeIds = ["clock", "stocks", "clock"] },
        ["a", "b", "c"]).SequenceEqual(["clock", "stocks"]),
    "a configured carousel must drive the cycle, deduplicated");
Assert(KnobControl.ResolveCycleList(
        new CarouselSettings { ThemeIds = ["clock"] },
        ["a", "b", "c"]).SequenceEqual(["a", "b", "c"]),
    "fewer than two carousel entries must fall back to the catalog");
Assert(KnobControl.ResolveCycleList(null, ["a", "b"]).SequenceEqual(["a", "b"]),
    "no carousel settings must fall back to the catalog");

Assert(KnobControl.TryParseVidPid("3151:4015", out ushort knobVid, out ushort knobPid)
    && knobVid == 0x3151 && knobPid == 0x4015,
    "colon-separated VID:PID must parse as hex");
Assert(KnobControl.TryParseVidPid("vid_046d&pid_c52b", out knobVid, out knobPid)
    && knobVid == 0x046D && knobPid == 0xC52B,
    "device-manager style VID_/PID_ ids must parse");
Assert(!KnobControl.TryParseVidPid("porridge", out _, out _)
    && !KnobControl.TryParseVidPid("", out _, out _)
    && !KnobControl.TryParseVidPid("1234", out _, out _),
    "junk VID:PID input must be rejected");

Assert(KnobControl.DevicePathMatches(@"\\?\HID#VID_3151&PID_4015&MI_02#8&2f5c8e0&0&0000#{884b96c3-56ef-11d1-bc8c-00a0c91405dd}", 0x3151, 0x4015),
    "a real raw-input device path must match its VID/PID");
Assert(!KnobControl.DevicePathMatches(@"\\?\HID#VID_046D&PID_C52B#7&1f6a&0&0000#{884b96c3}", 0x3151, 0x4015),
    "a different device must not match");
Assert(!KnobControl.DevicePathMatches(null, 0x3151, 0x4015), "a missing path must not match");

// Any combination, not a fixed F13-F24 list: the Linx68 has no such keys, so
// that mode only ever worked on a board you could remap in VIA/QMK.
var chord = KnobShortcut.Parse("Ctrl+Alt+P");
Assert(chord.VirtualKey == 'P' && chord.Modifiers == (KnobModifiers.Control | KnobModifiers.Alt),
    "a chord must round-trip from its stored form");
Assert(chord.ToStorageString() == "Ctrl+Alt+P", "storage form must be stable");
Assert(chord.Describe() == "Ctrl + Alt + P", "the settings page spaces a chord out");
Assert(chord.HasModifier && chord.IsSet, "a chord is set and carries a modifier");

// Settings written before this existed hold a bare "F13"; they must keep working.
var legacy = KnobShortcut.Parse("F13");
Assert(legacy.VirtualKey == 0x7C && legacy.Modifiers == KnobModifiers.None,
    "an old F13 binding must still parse");
Assert(!legacy.HasModifier, "a bare key is what the warning is for");
Assert(KnobShortcut.Parse("F24").VirtualKey == 0x87, "F24 must still map");
Assert(KnobShortcut.Parse("").IsSet == false && KnobShortcut.Parse(null).IsSet == false,
    "an empty binding is simply unset");
Assert(KnobShortcut.Parse("Shift+Win+Num5").ToStorageString() == "Shift+Win+Num5",
    "numpad keys and the Windows modifier must round-trip");
Assert(KnobShortcut.Parse("VK173").VirtualKey == 0xAD && KnobShortcut.Parse("Mute").VirtualKey == 0xAD,
    "a media key round-trips by name and by raw code");
Assert(KnobShortcut.IsModifierKey(0x11) && !KnobShortcut.IsModifierKey('P'),
    "modifier keys alone must not end a capture");

var knobBindings = new KnobSettings { KeyForward = "Ctrl+Alt+Right", KeyBackward = "F14", KeyToggle = "" };
Assert(KnobControl.ShortcutFor(knobBindings, KnobAction.NextTheme).Describe() == "Ctrl + Alt + Right",
    "the forward binding must be read back");
Assert(KnobControl.ShortcutFor(knobBindings, KnobAction.PreviousTheme).VirtualKey == 0x7D,
    "a legacy binding beside a new one must still work");
Assert(!KnobControl.ShortcutFor(knobBindings, KnobAction.ToggleCarousel).IsSet,
    "an unset binding must not match anything");

var knobDefaults = new KnobSettings();
Assert(!knobDefaults.Enabled && knobDefaults.SuppressVolume && knobDefaults.Mode == KnobMode.VolumeKnob,
    "the knob feature must ship disabled with volume suppression preferred");
Assert(knobDefaults.DeviceKey.Length == 0 && knobDefaults.Usages.Count == 0,
    "no knob is bound until the detector has run");

// The device key is what separates a knob from the same keyboard's Fn media
// keys, so it must keep the HID collection and drop the port-specific instance.
Assert(KnobControl.DeviceKeyFromPath(
        @"\\?\HID#VID_3151&PID_4015&MI_01&Col02#7&2f3a1b&0&0000#{4d1e55b2-f16f-11cf-88cb-001111000030}")
    == "VID_3151&PID_4015&MI_01&COL02",
    "the device key must be the collection segment, upper-cased");
Assert(KnobControl.DeviceKeyFromPath(@"\\?\HID#VID_3151&PID_4015&MI_01&Col02#8&11111&0&0000#{guid}")
    == KnobControl.DeviceKeyFromPath(@"\\?\HID#VID_3151&PID_4015&MI_01&Col02#7&2f3a1b&0&0000#{guid}"),
    "the same collection on another USB port must give the same key");
Assert(KnobControl.DeviceKeyFromPath(@"\\?\HID#VID_3151&PID_4015&MI_01&Col01#7&2f3a1b&0&0000#{guid}")
    != KnobControl.DeviceKeyFromPath(@"\\?\HID#VID_3151&PID_4015&MI_01&Col02#7&2f3a1b&0&0000#{guid}"),
    "two collections on one keyboard must give different keys");
Assert(KnobControl.DeviceKeyFromPath(null).Length == 0 && KnobControl.DeviceKeyFromPath("  ").Length == 0,
    "a missing path must give no key");

var knobCollection = new KnobObservation("VID_3151&PID_4015&MI_01&COL02", 0x00E9, 0);
var keysCollection = new KnobObservation("VID_3151&PID_4015&MI_01&COL01", 0x00E9, 0);
Assert(KnobControl.Matches(knobCollection, string.Empty, []),
    "an unbound knob must still react to every volume key, as it did before the detector");
Assert(KnobControl.Matches(knobCollection, "VID_3151&PID_4015&MI_01&COL02", []),
    "a bound collection must match its own reports whatever the usage");
Assert(!KnobControl.Matches(keysCollection, "VID_3151&PID_4015&MI_01&COL02", []),
    "the keyboard's other collection must not pass as the knob");
Assert(KnobControl.Matches(knobCollection, "vid_3151&pid_4015&mi_01&col02", []),
    "the collection key must match case-insensitively");
Assert(!KnobControl.Matches(knobCollection, "VID_3151&PID_4015&MI_01&COL02", [0x00EA]),
    "a usage-narrowed binding must reject the usages it does not name");

// The good case: the knob has a collection of its own, so the binding takes the
// whole collection and both rotation directions keep working.
KnobObservation[] turned =
[
    new("VID_3151&PID_4015&MI_01&COL02", 0x00E9, 0),
    new("VID_3151&PID_4015&MI_01&COL02", 0x00EA, 0)
];
KnobObservation[] fnKeys = [new("VID_3151&PID_4015&MI_01&COL01", 0x00E9, 0)];
KnobBinding? ownCollection = KnobControl.PickBinding(turned, fnKeys);
Assert(ownCollection is { Usages.Count: 0 } && ownCollection.DeviceKey == "VID_3151&PID_4015&MI_01&COL02",
    "a knob on its own collection must bind the collection, not one usage");
Assert(KnobControl.Matches(turned[0], ownCollection!.DeviceKey, ownCollection.Usages)
    && KnobControl.Matches(turned[1], ownCollection.DeviceKey, ownCollection.Usages)
    && !KnobControl.Matches(fnKeys[0], ownCollection.DeviceKey, ownCollection.Usages),
    "both directions must stay bound while the Fn keys stay free");

// One collection, but the knob has a usage of its own: narrow to that usage.
KnobObservation[] sharedCollection = [new("SHARED", 0x00E9, 0), new("SHARED", 0x0238, 0)];
KnobObservation[] sharedOthers = [new("SHARED", 0x00E9, 0)];
KnobBinding? narrowed = KnobControl.PickBinding(sharedCollection, sharedOthers);
Assert(narrowed is { DeviceKey: "SHARED" } && narrowed.Usages.SequenceEqual([0x0238]),
    "a shared collection must fall back to the usages the other keys never send");

// Truly indistinguishable: no binding at all, rather than one that looks right.
Assert(KnobControl.PickBinding([new("SHARED", 0x00E9, 0)], [new("SHARED", 0x00E9, 0)]) is null,
    "identical collection and usage must report no binding is possible");
Assert(KnobControl.PickBinding([], [new("SHARED", 0x00E9, 0)]) is null,
    "hearing nothing from the knob must not bind anything");
Console.WriteLine("PASS knob switching: circular cycle, carousel fallback, VID/PID, hot keys, knob detection");

// ---- screen builder (composer) --------------------------------------------
// Layout math: widgets stack with gaps, the first that does not fit is
// dropped together with everything after it, unknown kinds are skipped.
Rect composerBounds = new(0, 0, 122, 100);
List<ComposerWidgetSettings> composerList =
[
    new() { Kind = "cpu" },        // 42
    new() { Kind = "no-such" },    // skipped
    new() { Kind = "ram" },        // 42 -> 42+6+42 = 90, fits
    new() { Kind = "net" }         // would end at 90+6+46 = 142 > 100, clipped
];
var placed = ComposerWidgets.Arrange(composerList, composerBounds);
Assert(placed.Count == 2 && placed[0].Info.Kind == "cpu" && placed[1].Info.Kind == "ram",
    "arrange must skip unknown kinds and clip at the height budget");
Assert(Math.Abs(placed[1].Bounds.Top - 48) < 0.01, "the second widget must sit below the first plus the gap");
Assert(Math.Abs(ComposerWidgets.UsedHeight(composerList) - (42 + 6 + 42 + 6 + 46)) < 0.01,
    "used height must count every known widget and the gaps between them");
Assert(ComposerWidgets.Arrange([], composerBounds).Count == 0, "an empty layout must place nothing");

// Data needs: the render loop fetches exactly what the placed widgets read.
var composerNeeds = ComposerWidgets.RequiredSources(new ComposerSettings
{
    Widgets =
    [
        new() { Kind = "clock" },
        new() { Kind = "currency" },
        new() { Kind = "currency" },
        new() { Kind = "alerts" },
        new() { Kind = "claude" }
    ]
});
Assert(composerNeeds.Count == 3
    && composerNeeds.Contains("currency") && composerNeeds.Contains("alerts") && composerNeeds.Contains("claude-usage"),
    "required sources must dedupe and skip widgets that need nothing");
Assert(ComposerWidgets.RequiredSources(new ComposerSettings { Widgets = [] }).Count == 0,
    "an empty layout must need no sources");

// Every widget kind renders against the design sample, in batches that fit the
// screen, plus the empty-layout hint. This exercises all renderer branches.
var composerTheme = (ComposerTheme)themes.First(theme => theme.Id == "composer");
IReadOnlyList<ComposerWidgetSettings> savedComposerWidgets = composerTheme.Widgets;
string[][] composerBatches =
[
    ["clock", "date", "cpu", "ram", "gpu", "net", "spacer"],
    ["hardware", "weather", "currency", "crypto", "ping", "alerts"],
    ["claude", "pomodoro", "music", "calendar-next", "countdown-next", "world-clock", "github", "text"]
];
Assert(composerBatches.SelectMany(batch => batch).Distinct().Count() == ComposerWidgets.Catalog.Count,
    "the render batches must cover every widget kind in the catalog");
foreach (string[] batch in composerBatches)
{
    composerTheme.Widgets = batch
        .Select(kind => new ComposerWidgetSettings { Kind = kind, Text = kind == "text" ? "Привіт, світ" : "" })
        .ToList();
    var composedFrame = renderer.Render(composerTheme, SystemSnapshot.DesignSample);
    Assert(composedFrame.JpegBytes is [0xFF, 0xD8, ..], $"composer batch '{string.Join(",", batch)}' did not render");
}
composerTheme.Widgets = [];
Assert(renderer.Render(composerTheme, SystemSnapshot.DesignSample).JpegBytes.Length > 0,
    "the empty layout must render its hint");
// Widgets must also survive a snapshot with no optional data at all.
composerTheme.Widgets = ComposerWidgets.Catalog
    .Select(info => new ComposerWidgetSettings { Kind = info.Kind })
    .Take(8)
    .ToList();
Assert(renderer.Render(composerTheme, new SystemSnapshot(DateTimeOffset.Now, 10, 20)).JpegBytes.Length > 0,
    "composer must render placeholders when optional snapshots are missing");
// A Cloudflare challenge must read as its own state, not as "not connected".
composerTheme.Widgets = [new() { Kind = "claude" }];
Assert(renderer.Render(composerTheme, new SystemSnapshot(DateTimeOffset.Now, 5, 5,
        ClaudeUsage: ClaudeUsageSnapshot.Unavailable(Loc.T("ClaudeNoTranscripts")))).JpegBytes.Length > 0,
    "the claude widget must render its Cloudflare-challenged state");
composerTheme.Widgets = savedComposerWidgets;

// The layout persists through the settings file and the porter keeps it.
var composerRoundtrip = System.Text.Json.JsonSerializer.Deserialize<AppSettings>(
    SettingsPorter.ExportJson(new AppSettings
    {
        Composer = new ComposerSettings
        {
            Widgets = [new() { Kind = "clock" }, new() { Kind = "text", Text = "нотатка" }]
        }
    }));
Assert(composerRoundtrip?.Composer.Widgets is [{ Kind: "clock" }, { Kind: "text", Text: "нотатка" }],
    "the composer layout must survive export/import untouched");
// Per-widget font and accent. The dot face is offered only where a number is
// drawn: Doto has no Cyrillic, so a label in it would fall back to a system face.
Assert(ComposerWidgets.Find("cpu")!.HasNumber && ComposerWidgets.Find("clock")!.HasNumber,
    "numeric widgets must offer the dot font");
Assert(!ComposerWidgets.Find("text")!.HasNumber && !ComposerWidgets.Find("music")!.HasNumber
    && !ComposerWidgets.Find("spacer")!.HasNumber && !ComposerWidgets.Find("weather")!.HasNumber,
    "widgets whose value is prose must not offer it");

var styledWidgets = new List<ComposerWidgetSettings>
{
    new() { Kind = "clock", DotFont = true, Accent = "#22C55E" },
    new() { Kind = "cpu", DotFont = true },
    new() { Kind = "text", Text = "hello", DotFont = true, Accent = "#FF0000" }
};
var styledTheme = new ComposerTheme(new PomodoroTimer()) { Widgets = styledWidgets };
Assert(renderer.Render(styledTheme, SystemSnapshot.DesignSample).JpegBytes is [0xFF, 0xD8, ..],
    "a styled layout must render");

// A bad or empty colour falls back to the theme accent rather than throwing.
foreach (string accent in new[] { "", "not-a-colour", "#GGGGGG", "#12345" })
{
    var oddTheme = new ComposerTheme(new PomodoroTimer())
    {
        Widgets = [new ComposerWidgetSettings { Kind = "cpu", Accent = accent }]
    };
    Assert(renderer.Render(oddTheme, SystemSnapshot.DesignSample).JpegBytes.Length > 0,
        $"accent '{accent}' must fall back instead of failing");
}

// The two styles must survive a settings round-trip.
var composerStore = Path.Combine(Path.GetTempPath(), $"kss-composer-{Guid.NewGuid():N}.json");
try
{
    var composerSettings = new AppSettings { Composer = new ComposerSettings { Widgets = styledWidgets } };
    await new JsonSettingsStore(composerStore).SaveAsync(composerSettings);
    var reloaded = await new JsonSettingsStore(composerStore).LoadAsync();
    var first = reloaded.Composer!.Widgets[0];
    Assert(first.DotFont && first.Accent == "#22C55E", "per-widget font and accent must persist");
    Assert(!reloaded.Composer.Widgets[1].DotFont == false, "the second widget keeps its own font flag");
}
finally
{
    try { File.Delete(composerStore); } catch (IOException) { }
}
Console.WriteLine("PASS screen builder: layout math, data needs, all widget renders, per-widget font and accent, persistence");

// ---- crash-safe settings store --------------------------------------------
string storeDirectory = Path.Combine(Path.GetTempPath(), "kss-smoke-" + Guid.NewGuid().ToString("N"));
try
{
    var store = new JsonSettingsStore(Path.Combine(storeDirectory, "settings.json"));
    await store.SaveAsync(new AppSettings { AccentColor = "#112233" });
    await store.SaveAsync(new AppSettings { AccentColor = "#445566" });
    Assert((await store.LoadAsync()).AccentColor == "#445566", "the settings store must read back the latest save");
    Assert(File.Exists(store.Path + ".bak"), "a re-save must leave the previous file as .bak");
    // A crash mid-write leaves a torn main file; the previous save must win.
    await File.WriteAllTextAsync(store.Path, "{\"AccentColor\": \"#44");
    Assert((await store.LoadAsync()).AccentColor == "#112233",
        "a torn settings file must fall back to the .bak, not to blank settings");
    File.Delete(store.Path);
    Assert((await store.LoadAsync()).AccentColor == "#112233",
        "a deleted settings file must still recover from the .bak");
}
finally
{
    if (Directory.Exists(storeDirectory))
    {
        Directory.Delete(storeDirectory, recursive: true);
    }
}
Console.WriteLine("PASS settings store: atomic swap, backup recovery after torn writes");

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
    public List<string> ClientHints { get; } = [];
    public List<string> UserAgents { get; } = [];
    public List<Version> Versions { get; } = [];

    /// <summary>What each request presented on Authorization, so bearer auth can be asserted.</summary>
    public List<string> AuthorizationHeaders { get; } = [];

    /// <summary>Only the requests that carried a Cookie header at all.</summary>
    public List<string> CookieHeaders { get; } = [];

    /// <summary>What each request presented on anthropic-beta, which gates the OAuth contract.</summary>
    public List<string> BetaHeaders { get; } = [];

    /// <summary>Answer every request with this instead of 200, to exercise the refusal paths.</summary>
    public HttpStatusCode Status { get; init; } = HttpStatusCode.OK;

    /// <summary>Sent as Set-Cookie on every response when set, like Cloudflare's __cf_bm.</summary>
    public string? SetCookie { get; init; }

    public ClaudeHandler(Queue<string> responses)
    {
        _responses = responses;
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Requests.Add(request.RequestUri?.ToString() ?? string.Empty);
        Cookies.Add(request.Headers.TryGetValues("Cookie", out var values) ? string.Join("; ", values) : string.Empty);
        if (request.Headers.TryGetValues("Cookie", out var sent))
        {
            CookieHeaders.Add(string.Join("; ", sent));
        }

        AuthorizationHeaders.Add(request.Headers.Authorization?.ToString() ?? string.Empty);
        BetaHeaders.Add(request.Headers.TryGetValues("anthropic-beta", out var beta) ? string.Join("; ", beta) : string.Empty);
        ClientHints.Add(request.Headers.TryGetValues("sec-ch-ua", out var hints) ? string.Join("; ", hints) : string.Empty);
        UserAgents.Add(request.Headers.TryGetValues("User-Agent", out var agents) ? string.Join(" ", agents) : string.Empty);
        Versions.Add(request.Version);
        if (Status != HttpStatusCode.OK)
        {
            return Task.FromResult(new HttpResponseMessage(Status) { Content = new StringContent("{}") });
        }

        if (_responses.Count == 0)
        {
            throw new InvalidOperationException("No mocked Claude response remains.");
        }
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(_responses.Dequeue())
        };
        if (SetCookie is { Length: > 0 })
        {
            response.Headers.TryAddWithoutValidation("Set-Cookie", SetCookie);
        }
        return Task.FromResult(response);
    }
}

/// <summary>A fixed status with a fixed body, counting the requests.</summary>
sealed class StatusBodyHandler(System.Net.HttpStatusCode status, string body) : HttpMessageHandler
{
    public int RequestCount { get; private set; }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        RequestCount++;
        return Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(body) });
    }
}

/// <summary>Routes by URL: a body string means 200, null means 503.</summary>
sealed class RoutingHandler(Func<string, string?> route) : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
        Task.FromResult(route(request.RequestUri!.ToString()) is { } body
            ? new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body) }
            : new HttpResponseMessage(HttpStatusCode.ServiceUnavailable) { Content = new StringContent(string.Empty) });
}

sealed class StatusHandler(System.Net.HttpStatusCode status) : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
        Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent("") });
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
