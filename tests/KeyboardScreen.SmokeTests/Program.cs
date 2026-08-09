using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using KeyboardScreen.Core;

var defaults = new AppSettings();
Assert(defaults.SelectedThemeId == "clock-dot-matrix", "first-run theme must default to dot-matrix clock");
Assert(defaults.AccentColor == "#E4694C", "first-run accent color is incorrect");
Assert(defaults.AutoPush && defaults.RefreshSeconds == 1, "first-run automation defaults are incorrect");
Assert(defaults.MinimizeToTray && defaults.CloseToTray, "first-run tray defaults are incorrect");
Assert(defaults.Weather.UseAutomaticLocation, "first-run weather must use automatic location");
Assert(defaults.SafeArea == new ScreenInsets(10, 52, 10, 12), "first-run safe area is incorrect");

var profile = ScreenProfile.KeyboardDisplay;
Assert(profile.SafeArea.Top == 52, "keyboard firmware safe area must reserve the top status pills");
Assert(profile.SafeArea.Left + profile.SafeArea.Right < profile.Width, "safe area horizontal insets are invalid");
Assert(profile.SafeArea.Top + profile.SafeArea.Bottom < profile.Height, "safe area vertical insets are invalid");
var renderer = new ScreenRenderer(profile);
var themes = BuiltInThemes.Create(new ImageTheme());
Assert(themes.Count == 20, "built-in theme catalog should contain the 20 supported schemes");
Assert(themes.All(theme => theme.Id is not "calendar" and not "ambient"), "removed calendar/ambient themes must not be registered");
Assert(themes.All(theme => theme.Id != "clock-seconds"), "removed seconds progress theme must not be registered");
Assert(themes.All(theme => theme.Id != "week"), "removed week calendar theme must not be registered");
Assert(themes.Any(theme => theme.Id == "performance-visual"), "performance-visual theme must be registered");
Assert(themes.Single(theme => theme.Id == "clock-dot-matrix").DisplayName == "点阵时钟", "dot-matrix clock theme must be registered");
Assert(themes.Single(theme => theme.Id == "clock-weather-dot").DisplayName == "点阵时钟天气", "dot-matrix weather clock theme must be registered");
Assert(themes.Single(theme => theme.Id == "clock-dot-analog").DisplayName == "点阵模拟时钟", "dot-matrix analog clock theme must be registered");
Assert(themes.Single(theme => theme.Id == "clock-dot-progress").DisplayName == "点阵进度", "dot-matrix progress theme must be registered");
Assert(themes.Single(theme => theme.Id == "image").DisplayName == "图片时间", "image theme must be named 图片时间");
Assert(themes.Single(theme => theme.Id == "ai-quota").DisplayName == "AI用量（开发中）", "AI quota theme must carry the development label");
Assert(themes.Any(theme => theme.Id == "weather-five-day"), "five-day weather theme must be registered");
Assert(themes.Any(theme => theme.Id == "stocks"), "stock theme must be registered");
Assert(themes.Select(theme => theme.Id).Distinct(StringComparer.OrdinalIgnoreCase).Count() == themes.Count, "theme ids should be unique");

var aiQuotaTheme = themes.Single(theme => theme.Id == "ai-quota");
var subscriptionQuota = AiQuotaSnapshot.ForSubscription(
    "ChatGPT",
    56,
    remainingCount: 1,
    resetPeriod: AiResetPeriod.Weekly);
Assert(subscriptionQuota.RemainingDisplay == "56% / 1次", "subscription quota display is incorrect");
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
    Assert(weatherSnapshot.ConditionText == "多云", "WMO weather condition mapping failed");
    Assert(weatherSnapshot.DailyForecast?.Count == 5, "five-day weather forecast was not parsed");
    Assert(weatherSnapshot.DailyForecast![2].ConditionText == "雨", "daily WMO weather condition mapping failed");
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
        && Math.Abs(closes[^1] - 105.0) < 0.001, "Yahoo 5日收盘价未解析");
    Assert(!stockSnapshot.RedForGain, "stock color preference was not preserved");
    var cachedStocks = await stockSource.ReadAsync(stockSettings);
    Assert(ReferenceEquals(stockSnapshot, cachedStocks), "stock snapshot should use the fifteen-minute cache");
    Assert(stockHandler.RequestCount == 1, "cached stock read must not call the API again");
    var stockFrame = renderer.Render(themes.Single(theme => theme.Id == "stocks"), SystemSnapshot.DesignSample with { Stocks = stockSnapshot });
    Assert(stockFrame.JpegBytes is [0xFF, 0xD8, ..], "stock data view did not render");
}
Console.WriteLine("PASS Yahoo chart parser, aliases, color preference and cache");

Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
Assert(TencentStockSnapshotSource.MapSymbol("600519.SS") == "sh600519", "A股沪市代码映射错误");
Assert(TencentStockSnapshotSource.MapSymbol("000001.SZ") == "sz000001", "A股深市代码映射错误");
Assert(TencentStockSnapshotSource.MapSymbol("0700.HK") == "hk00700", "港股代码映射错误");
Assert(TencentStockSnapshotSource.MapSymbol("AAPL") == "usAAPL", "美股代码映射错误");
Assert(TencentStockSnapshotSource.MapSymbol("BTC-USD") is null, "加密货币应判为不支持");

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
    Assert(tencentHandler.LastRequestUri?.AbsoluteUri == "https://qt.gtimg.cn/q=sh600519,hk00700,usAAPL", "腾讯请求应使用映射后的代码");
    Assert(tencentSnapshot.Quotes.Count == 3, "腾讯行情应解析出三项");
    Assert(tencentSnapshot.Quotes[0].DisplayName == "茅台", "A股别名未生效");
    Assert(Math.Abs(tencentSnapshot.Quotes[0].CurrentPrice - 1420.00) < 0.001, "A股现价解析错误");
    Assert(Math.Abs(tencentSnapshot.Quotes[0].ChangePercent - 0.71) < 0.001, "A股涨跌幅（字段32）解析错误");
    Assert(tencentSnapshot.Quotes[0].UpdatedAt.ToString("yyyyMMddHHmmss") == "20260807161431", "A股时间戳解析错误");
    Assert(Math.Abs(tencentSnapshot.Quotes[1].ChangePercent - (-0.08)) < 0.001, "港股涨跌幅解析错误");
    Assert(tencentSnapshot.Quotes[1].UpdatedAt.ToString("yyyy-MM-dd HH:mm:ss") == "2026-08-07 16:08:23", "港股时间戳解析错误");
    Assert(tencentSnapshot.Quotes[2].DisplayName == "苹果", "美股别名未生效");
    Assert(Math.Abs(tencentSnapshot.Quotes[2].ChangePercent - 0.29) < 0.001, "美股涨跌幅解析错误");
    Assert(!tencentSnapshot.RedForGain, "腾讯行情颜色偏好未保留");
    var tencentCached = await tencentSource.ReadAsync(tencentSettings);
    Assert(ReferenceEquals(tencentSnapshot, tencentCached), "腾讯行情应使用十五分钟缓存");
    Assert(tencentHandler.RequestCount == 1, "缓存的腾讯行情读取不应再次请求");
    var tencentFrame = renderer.Render(themes.Single(theme => theme.Id == "stocks"), SystemSnapshot.DesignSample with { Stocks = tencentSnapshot });
    Assert(tencentFrame.JpegBytes is [0xFF, 0xD8, ..], "腾讯行情数据视图未渲染");
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
    Assert(trendHandler.KlineRequests == 2, "恰好两只 A/港股时应请求两次日K");
    Assert(trendHandler.KlineQueries.All(uri => uri.Contains("fqkline") && uri.Contains("param=")), "日K请求参数错误");
    Assert(trendSnapshot.Quotes.Count == 2, "双股行情应解析");
    var aCloses = trendSnapshot.Quotes[0].FiveDayCloses;
    var hkCloses = trendSnapshot.Quotes[1].FiveDayCloses;
    Assert(aCloses is { Count: 5 } && Math.Abs(aCloses[^1] - 1309.22) < 0.001, "A股 qfqday 走势未解析");
    Assert(hkCloses is { Count: 5 } && Math.Abs(hkCloses[^1] - 478.8) < 0.001, "港股 day 走势未解析");
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
    Assert(usTrendHandler.KlineRequests == 2, "含美股的两只股票应请求两次日K");
    Assert(usTrendHandler.KlineQueries.Any(uri => uri.Contains("usAAPL.OQ")), "美股日K应使用 .OQ 格式");
    Assert(usTrendSnapshot.Quotes[1].FiveDayCloses is { Count: 5 } usCloses && Math.Abs(usCloses[^1] - 313.33) < 0.001, "美股 .OQ 走势未解析");
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
    Assert(newStockHandler.KlineRequests == 2, "双 A 股应请求两次日K");
    Assert(newStockSnapshot.Quotes.Count == 2, "双 A 股行情应解析");
    Assert(newStockSnapshot.Quotes[0].FiveDayCloses is { Count: 5 }, "老股 5 日走势未解析");
    Assert(newStockSnapshot.Quotes[1].FiveDayCloses is { Count: 1 }, "首日新股应保留 1 个收盘价样本");
    var newStockFrame = renderer.Render(themes.Single(theme => theme.Id == "stocks"),
        SystemSnapshot.DesignSample with { Stocks = newStockSnapshot });
    Assert(newStockFrame.JpegBytes is [0xFF, 0xD8, ..], "含首日新股的双股视图未渲染");
}

var twoTrendStocks = new StockSnapshot(
    [
        new StockQuoteSnapshot("600519.SS", "贵州茅台", 1309.22, 0.05, DateTimeOffset.Now, [1358.98, 1328.36, 1306.45, 1308.55, 1309.22]),
        new StockQuoteSnapshot("0700.HK", "腾讯控股", 478.80, -0.08, DateTimeOffset.Now, [490.4, 487.6, 492.2, 479.2, 478.8])
    ],
    DateTimeOffset.Now,
    RedForGain: true);
var twoTrendFrame = renderer.Render(themes.Single(theme => theme.Id == "stocks"), SystemSnapshot.DesignSample with { Stocks = twoTrendStocks });
Assert(twoTrendFrame.JpegBytes is [0xFF, 0xD8, ..], "双股走势可视化视图未渲染");

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
Assert(fiveStocksFrame.JpegBytes is [0xFF, 0xD8, ..], "五只股票视图未渲染");

var extremeStocks = new StockSnapshot(
    [new StockQuoteSnapshot("X", "极端涨幅", 49.69, 396.89, DateTimeOffset.Now)],
    DateTimeOffset.Now,
    RedForGain: true);
var extremeFrame = renderer.Render(themes.Single(theme => theme.Id == "stocks"), SystemSnapshot.DesignSample with { Stocks = extremeStocks });
Assert(extremeFrame.JpegBytes is [0xFF, 0xD8, ..] && extremeFrame.JpegBytes.Length <= profile.MaxJpegBytes, "极端涨幅 396.89% 视图未渲染或超限");

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
    Assert(partialSnapshot.Quotes.Count == 1 && partialSnapshot.Quotes[0].Symbol == "AAPL", "不支持的代码应被跳过，其余正常解析");
}

using (var emptyClient = new HttpClient(new GbkHandler("v_sz000001=\"51~平安银行~000001~11.19~11.27~11.23\";")))
using (var emptySource = new TencentStockSnapshotSource(emptyClient))
{
    var failedSnapshot = await emptySource.ReadAsync(new StockSettings
    {
        Items = [new StockItemSettings { Symbol = "600519.SS" }]
    });
    Assert(failedSnapshot.Quotes.Count == 0 && !string.IsNullOrWhiteSpace(failedSnapshot.ErrorMessage), "全部失败时应返回错误快照");
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
    var settings = new AppSettings { SelectedThemeId = "music", RefreshSeconds = 17, AccentColor = "#A23BFF", SelectedFontId = "file:test.ttf|test", SafeArea = new ScreenInsets(11, 53, 9, 13), AiQuota = new AiQuotaSettings { DataKind = AiUsageDataKind.ModelCost, SelectedItemKey = "model:test", DisplayName = "My AI", ProgressTarget = 25 }, Weather = new WeatherSettings { LocationQuery = "上海", UseAutomaticLocation = true }, Stocks = new StockSettings { SourceKind = StockSourceKind.Yahoo, RedForGain = false, Items = [new StockItemSettings { Symbol = "0700.HK", Alias = "腾讯", Enabled = false }] }, ImageTimePlacement = ImageTimePlacement.Top, ImageClockStyle = ImageClockStyle.Flip, ImageTimeBackground = false, ImageTextColor = ImageTextColor.Black, ImageTextAlignment = ImageTextAlignment.Right, ImageWeatherVisible = true, ImageTimeFontSize = 34, ImageDateFontSize = 15, ImageWeatherFontSize = 13, ImageDigitalOrder = ImageDigitalOrder.WeatherTimeDate, ImageLargeTimeFontSize = 42, ImageAnalogClockSize = 94, ImageAnalogOrder = ImageAnalogOrder.DateWeatherClock, ImageFlipTimeFontSize = 35, IgnoreBrowserMediaSessions = false, UiThemeMode = UiThemeMode.Dark, DotMatrixProgressPeriod = DotMatrixProgressPeriod.Quarter, DotMatrixProgressHeaderFontSize = 18, LaunchAtStartup = true, AutoMediaThemeSwitch = true, MediaPlayingThemeId = "music-poster", MediaIdleThemeId = "clock-neon" , HasCompletedOnboarding = true, HasAcknowledgedStockNotice = true, HasAcknowledgedAiUsageNotice = true };
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

Assert(ReleaseUpdateChecker.TryParseTagVersion("v1.1.0") == new Version(1, 1, 0), "tag version parsing failed");
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

Console.WriteLine("All smoke tests passed.");
return;

static void Assert(bool condition, string message)
{
    if (!condition)
    {
        throw new InvalidOperationException(message);
    }
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
