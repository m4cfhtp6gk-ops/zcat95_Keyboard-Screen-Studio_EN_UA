using System.IO;
using System.Net;
using System.Net.Http;
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
Assert(themes.Count == 19, "built-in theme catalog should contain the 19 supported schemes");
Assert(themes.All(theme => theme.Id is not "calendar" and not "ambient"), "removed calendar/ambient themes must not be registered");
Assert(themes.All(theme => theme.Id != "clock-seconds"), "removed seconds progress theme must not be registered");
Assert(themes.All(theme => theme.Id != "week"), "removed week calendar theme must not be registered");
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
    """{"chart":{"result":[{"meta":{"symbol":"AAPL","regularMarketPrice":105.0,"chartPreviousClose":100.0,"regularMarketTime":1785200000}}],"error":null}}"""
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
    Assert(!stockSnapshot.RedForGain, "stock color preference was not preserved");
    var cachedStocks = await stockSource.ReadAsync(stockSettings);
    Assert(ReferenceEquals(stockSnapshot, cachedStocks), "stock snapshot should use the fifteen-minute cache");
    Assert(stockHandler.RequestCount == 1, "cached stock read must not call the API again");
    var stockFrame = renderer.Render(themes.Single(theme => theme.Id == "stocks"), SystemSnapshot.DesignSample with { Stocks = stockSnapshot });
    Assert(stockFrame.JpegBytes is [0xFF, 0xD8, ..], "stock data view did not render");
}
Console.WriteLine("PASS Yahoo chart parser, aliases, color preference and cache");

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

var settingsPath = Path.Combine(Path.GetTempPath(), $"keyboard-screen-settings-{Guid.NewGuid():N}.json");
try
{
    var settingsStore = new JsonSettingsStore(settingsPath);
    var settings = new AppSettings { SelectedThemeId = "music", RefreshSeconds = 17, AccentColor = "#A23BFF", SelectedFontId = "file:test.ttf|test", SafeArea = new ScreenInsets(11, 53, 9, 13), AiQuota = new AiQuotaSettings { DataKind = AiUsageDataKind.ModelCost, SelectedItemKey = "model:test", DisplayName = "My AI", ProgressTarget = 25 }, Weather = new WeatherSettings { LocationQuery = "上海", UseAutomaticLocation = true }, Stocks = new StockSettings { RedForGain = false, Items = [new StockItemSettings { Symbol = "0700.HK", Alias = "腾讯" }] }, ImageTimePlacement = ImageTimePlacement.Top, ImageClockStyle = ImageClockStyle.Flip, ImageTimeBackground = false, ImageTextColor = ImageTextColor.Black, ImageTextAlignment = ImageTextAlignment.Right, ImageWeatherVisible = true, ImageTimeFontSize = 34, ImageDateFontSize = 15, ImageWeatherFontSize = 13, ImageDigitalOrder = ImageDigitalOrder.WeatherTimeDate, ImageLargeTimeFontSize = 42, ImageAnalogClockSize = 94, ImageAnalogOrder = ImageAnalogOrder.DateWeatherClock, ImageFlipTimeFontSize = 35, IgnoreBrowserMediaSessions = false, UiThemeMode = UiThemeMode.Dark, DotMatrixProgressPeriod = DotMatrixProgressPeriod.Quarter, DotMatrixProgressHeaderFontSize = 18, LaunchAtStartup = true, AutoMediaThemeSwitch = true, MediaPlayingThemeId = "music-poster", MediaIdleThemeId = "clock-neon" , HasCompletedOnboarding = true, HasAcknowledgedStockNotice = true, HasAcknowledgedAiUsageNotice = true };
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
    Assert(!loadedSettings.Stocks.RedForGain && loadedSettings.Stocks.Items[0].Alias == "腾讯", "stock settings did not persist");
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
