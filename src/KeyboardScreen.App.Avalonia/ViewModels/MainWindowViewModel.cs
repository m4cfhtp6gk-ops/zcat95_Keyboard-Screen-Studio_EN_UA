using System.Collections.ObjectModel;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Windows.Input;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using KeyboardScreen.App.Avalonia.Infrastructure;
using KeyboardScreen.App.Avalonia.Platform;
using KeyboardScreen.Core;
using WpfColor = System.Windows.Media.Color;
using WpfColorConverter = System.Windows.Media.ColorConverter;
using WpfFontFamily = System.Windows.Media.FontFamily;

namespace KeyboardScreen.App.Avalonia.ViewModels;

public sealed class MainWindowViewModel : ObservableObject, IAsyncDisposable
{
    private readonly ISystemSnapshotSource _systemSource = new WindowsSystemSnapshotSource();
    private readonly WindowsMusicSnapshotSource _musicSource = new();
    private readonly OpenMeteoWeatherSnapshotSource _weatherSource = new();
    private readonly YahooStockSnapshotSource _stockSource = new();
    private readonly TokscaleDataSource _tokscaleSource = new();
    private readonly HttpImageDeviceTransport _transport = new();
    private readonly JsonSettingsStore _settingsStore = new();
    private readonly IWindowsDesktopServices _desktopServices;
    private readonly ImageTheme _imageTheme = new();
    private readonly FontFolderCatalog _fontCatalog;
    private readonly SemaphoreSlim _refreshLock = new(1, 1);
    private readonly object _scheduleGate = new();
    private int _themeSelectionVersion;

    private AppSettings _settings = new();
    private IReadOnlyList<IScreenTheme> _themes = [];
    private IReadOnlyList<ScreenFontOption> _fonts = [];
    private ScreenRenderer _renderer = new();
    private RenderedFrame? _latestFrame;
    private CancellationTokenSource? _lifetime;
    private CancellationTokenSource? _commitDelay;
    private bool _loading = true;
    private bool _disposed;
    private string _selectedNavigation = "screen";
    private ThemeItemViewModel? _selectedTheme;
    private Bitmap? _previewImage;
    private string _deviceStatus = "断开连接";
    private bool _deviceConnected;
    private string _deviceIp = string.Empty;
    private string _accentColor = "#E4694C";
    private string _selectedFontId = ScreenFontOption.DefaultId;
    private int _refreshSeconds = 1;
    private bool _autoPush = true;
    private bool _autoMediaThemeSwitch;
    private bool _ignoreBrowserMediaSessions = true;
    private string _mediaIdleThemeId = "system";
    private string _mediaPlayingThemeId = "music";
    private bool _autoSwitchToMusic;
    private bool _minimizeToTray = true;
    private bool _closeToTray = true;
    private bool _startMinimized;
    private bool _launchAtStartup;
    private bool _weatherAutomaticLocation = true;
    private string _weatherLocation = "北京";
    private string _stockSymbol1 = string.Empty;
    private string _stockAlias1 = string.Empty;
    private string _stockSymbol2 = string.Empty;
    private string _stockAlias2 = string.Empty;
    private string _stockSymbol3 = string.Empty;
    private string _stockAlias3 = string.Empty;
    private string _stockColorPreference = "红涨绿跌";
    private AiUsageModeOption? _selectedAiUsageMode;
    private TokscaleDataOption? _selectedTokscaleOption;
    private TokscaleCatalog? _tokscaleCatalog;
    private string _aiDisplayName = string.Empty;
    private decimal _aiProgressTarget;
    private string _tokscaleStatusTitle = "正在检测 Tokscale";
    private string _tokscaleStatusMessage = "正在读取本机用量数据…";
    private TokscaleStatus _tokscaleStatus = TokscaleStatus.NoData;
    private string _currentThemeName = string.Empty;
    private string _currentThemeDescription = string.Empty;
    private string _currentThemeDetails = string.Empty;
    private string _sourceSummary = "正在读取数据…";
    private string _lastUpdated = "尚未刷新";
    private string _safeLeft = "10";
    private string _safeTop = "52";
    private string _safeRight = "10";
    private string _safeBottom = "12";
    private ImageTimePlacement _imageTimePlacement = ImageTimePlacement.Bottom;
    private DotMatrixProgressPeriod _dotMatrixProgressPeriod = DotMatrixProgressPeriod.Today;
    private int _dotMatrixProgressHeaderFontSize = 15;
    private ImageClockStyle _imageClockStyle = ImageClockStyle.Digital;
    private bool _imageTimeBackground = true;
    private ImageTextColor _imageTextColor = ImageTextColor.White;
    private ImageTextAlignment _imageTextAlignment = ImageTextAlignment.Center;
    private bool _imageWeatherVisible;
    private int _imageTimeFontSize = 29;
    private int _imageDateFontSize = 11;
    private int _imageWeatherFontSize = 11;
    private ImageDigitalOrder _imageDigitalOrder = ImageDigitalOrder.TimeDateWeather;
    private int _imageLargeTimeFontSize = 40;
    private int _imageAnalogClockSize = 76;
    private ImageAnalogOrder _imageAnalogOrder = ImageAnalogOrder.ClockDateWeather;
    private int _imageFlipTimeFontSize = 29;
    private UiThemeMode _uiThemeMode = UiThemeMode.System;

    public MainWindowViewModel(IWindowsDesktopServices desktopServices)
    {
        _desktopServices = desktopServices;
        _fontCatalog = new FontFolderCatalog(Path.Combine(AppContext.BaseDirectory, "Fonts"));
        _fontCatalog.FontsChanged += FontCatalogOnFontsChanged;

        SelectNavigationCommand = new ParameterizedCommand<string>(value =>
            SelectedNavigation = string.IsNullOrWhiteSpace(value) ? "screen" : value);
        SelectThemeCommand = new ParameterizedCommand<string>(SelectTheme);
        RefreshCommand = new AsyncCommand(() => RefreshAndPushAsync(forcePush: true));
        OpenFontsFolderCommand = new RelayCommand(() => _desktopServices.OpenFolder(_fontCatalog.FolderPath));
        OpenAuthorCommand = new RelayCommand(() => _desktopServices.OpenUrl("https://github.com/zcat95"));
        OpenTokscaleDocsCommand = new RelayCommand(() => _desktopServices.OpenUrl("https://github.com/junhoyeo/tokscale"));
        RefreshTokscaleCommand = new AsyncCommand(() => RefreshTokscaleAsync(force: true));
    }

    public ObservableCollection<ThemeGroupViewModel> ThemeGroups { get; } = [];
    public ObservableCollection<ThemeItemViewModel> IdleThemeOptions { get; } = [];
    public ObservableCollection<ThemeItemViewModel> MusicThemeOptions { get; } = [];
    public ObservableCollection<TokscaleDataOption> TokscaleOptions { get; } = [];
    public IReadOnlyList<AiUsageModeOption> AiUsageModes { get; } =
    [
        new(AiUsageDataKind.SubscriptionRemaining, "订阅剩余额度", "读取 Tokscale Usage 中的周期额度与重置时间"),
        new(AiUsageDataKind.ModelTokens, "模型 Token", "按客户端、供应商和模型显示累计 Token"),
        new(AiUsageDataKind.ModelCost, "模型费用", "按客户端、供应商和模型显示估算费用")
    ];

    public ICommand SelectNavigationCommand { get; }
    public ICommand SelectThemeCommand { get; }
    public ICommand RefreshCommand { get; }
    public ICommand OpenFontsFolderCommand { get; }
    public ICommand OpenAuthorCommand { get; }
    public ICommand OpenTokscaleDocsCommand { get; }
    public ICommand RefreshTokscaleCommand { get; }

    public Func<Task<string?>>? PickImageAsync { get; set; }

    public string SelectedNavigation
    {
        get => _selectedNavigation;
        set
        {
            if (SetProperty(ref _selectedNavigation, value))
            {
                RaiseNavigationProperties();
            }
        }
    }

    public bool IsScreenPage => SelectedNavigation == "screen";
    public bool IsThemePage => SelectedNavigation == "theme";
    public bool IsAutomationPage => SelectedNavigation == "automation";
    public bool IsSettingsPage => SelectedNavigation == "settings";
    public bool IsAboutPage => SelectedNavigation == "about";

    public string AppVersion { get; } = ResolveAppVersion();

    private static string ResolveAppVersion()
    {
        Version? version = typeof(MainWindowViewModel).Assembly.GetName().Version;
        return version is null ? string.Empty : $"{version.Major}.{version.Minor}.{version.Build}";
    }

    public ThemeItemViewModel? SelectedTheme
    {
        get => _selectedTheme;
        private set
        {
            if (!SetProperty(ref _selectedTheme, value) || value is null)
            {
                return;
            }

            foreach (ThemeItemViewModel item in ThemeGroups.SelectMany(group => group.Themes))
            {
                item.IsSelected = ReferenceEquals(item, value);
            }

            CurrentThemeName = value.Name;
            CurrentThemeDescription = value.Id == "image" && !string.IsNullOrWhiteSpace(_imageTheme.ImagePath)
                ? _imageTheme.ImagePath
                : value.Description;
            CurrentThemeDetails = value.Details;
            Interlocked.Increment(ref _themeSelectionVersion);
            SourceSummary = GetLoadingSummary(value.Id);
            LastUpdated = "正在刷新";
            RaiseThemeContextProperties();
            ScheduleCommit();
        }
    }

    public Bitmap? PreviewImage
    {
        get => _previewImage;
        private set
        {
            Bitmap? previous = _previewImage;
            if (SetProperty(ref _previewImage, value))
            {
                previous?.Dispose();
            }
        }
    }

    public string DeviceStatus
    {
        get => _deviceStatus;
        private set => SetProperty(ref _deviceStatus, value);
    }

    public bool DeviceConnected
    {
        get => _deviceConnected;
        private set => SetProperty(ref _deviceConnected, value);
    }

    public string DeviceIp
    {
        get => _deviceIp;
        set
        {
            if (SetProperty(ref _deviceIp, value))
            {
                OnPropertyChanged(nameof(DeviceSummary));
                ScheduleCommit();
            }
        }
    }

    public string DeviceSummary => string.IsNullOrWhiteSpace(DeviceIp) ? "地址未配置" : DeviceIp;

    public string AccentColor
    {
        get => _accentColor;
        set { if (SetProperty(ref _accentColor, value)) ScheduleCommit(); }
    }

    public IReadOnlyList<ScreenFontOption> Fonts
    {
        get => _fonts;
        private set => SetProperty(ref _fonts, value);
    }

    public string SelectedFontId
    {
        get => _selectedFontId;
        set { if (SetProperty(ref _selectedFontId, value)) ScheduleCommit(); }
    }

    public int RefreshSeconds
    {
        get => _refreshSeconds;
        set
        {
            value = Math.Clamp(value, 1, 30);
            if (SetProperty(ref _refreshSeconds, value)) ScheduleCommit();
        }
    }

    public bool AutoPush
    {
        get => _autoPush;
        set { if (SetProperty(ref _autoPush, value)) ScheduleCommit(); }
    }

    public bool AutoMediaThemeSwitch
    {
        get => _autoMediaThemeSwitch;
        set { if (SetProperty(ref _autoMediaThemeSwitch, value)) ScheduleCommit(); }
    }

    public bool IgnoreBrowserMediaSessions
    {
        get => _ignoreBrowserMediaSessions;
        set { if (SetProperty(ref _ignoreBrowserMediaSessions, value)) ScheduleCommit(); }
    }
    public string MediaIdleThemeId
    {
        get => _mediaIdleThemeId;
        set { if (SetProperty(ref _mediaIdleThemeId, value)) ScheduleCommit(); }
    }

    public string MediaPlayingThemeId
    {
        get => _mediaPlayingThemeId;
        set { if (SetProperty(ref _mediaPlayingThemeId, value)) ScheduleCommit(); }
    }

    public bool AutoSwitchToMusic
    {
        get => _autoSwitchToMusic;
        set { if (SetProperty(ref _autoSwitchToMusic, value)) ScheduleCommit(); }
    }

    public bool MinimizeToTray
    {
        get => _minimizeToTray;
        set { if (SetProperty(ref _minimizeToTray, value)) ScheduleCommit(); }
    }

    public bool CloseToTray
    {
        get => _closeToTray;
        set { if (SetProperty(ref _closeToTray, value)) ScheduleCommit(); }
    }

    public bool StartMinimized
    {
        get => _startMinimized;
        set { if (SetProperty(ref _startMinimized, value)) ScheduleCommit(); }
    }

    public bool LaunchAtStartup
    {
        get => _launchAtStartup;
        set
        {
            if (SetProperty(ref _launchAtStartup, value))
            {
                _desktopServices.TrySetLaunchAtStartup(value);
                ScheduleCommit();
            }
        }
    }

    public bool WeatherAutomaticLocation
    {
        get => _weatherAutomaticLocation;
        set
        {
            if (SetProperty(ref _weatherAutomaticLocation, value))
            {
                OnPropertyChanged(nameof(IsManualWeatherLocationEnabled));
                ScheduleCommit();
            }
        }
    }

    public bool IsManualWeatherLocationEnabled => !WeatherAutomaticLocation;

    public string WeatherLocation
    {
        get => _weatherLocation;
        set { if (SetProperty(ref _weatherLocation, value)) ScheduleCommit(); }
    }

    public string StockSymbol1
    {
        get => _stockSymbol1;
        set { if (SetProperty(ref _stockSymbol1, value)) ScheduleCommit(); }
    }

    public string StockAlias1
    {
        get => _stockAlias1;
        set { if (SetProperty(ref _stockAlias1, value)) ScheduleCommit(); }
    }

    public string StockSymbol2
    {
        get => _stockSymbol2;
        set { if (SetProperty(ref _stockSymbol2, value)) ScheduleCommit(); }
    }

    public string StockAlias2
    {
        get => _stockAlias2;
        set { if (SetProperty(ref _stockAlias2, value)) ScheduleCommit(); }
    }

    public string StockSymbol3
    {
        get => _stockSymbol3;
        set { if (SetProperty(ref _stockSymbol3, value)) ScheduleCommit(); }
    }

    public string StockAlias3
    {
        get => _stockAlias3;
        set { if (SetProperty(ref _stockAlias3, value)) ScheduleCommit(); }
    }

    public string StockColorPreference
    {
        get => _stockColorPreference;
        set { if (SetProperty(ref _stockColorPreference, value)) ScheduleCommit(); }
    }

    public IReadOnlyList<string> StockColorPreferences { get; } = ["红涨绿跌", "绿涨红跌"];

    public AiUsageModeOption? SelectedAiUsageMode
    {
        get => _selectedAiUsageMode;
        set
        {
            if (SetProperty(ref _selectedAiUsageMode, value) && value is not null)
            {
                RebuildTokscaleOptions();
                OnPropertyChanged(nameof(IsAiProgressTargetVisible));
                ScheduleCommit();
            }
        }
    }

    public TokscaleDataOption? SelectedTokscaleOption
    {
        get => _selectedTokscaleOption;
        set
        {
            if (SetProperty(ref _selectedTokscaleOption, value))
            {
                ScheduleCommit();
            }
        }
    }

    public string AiDisplayName
    {
        get => _aiDisplayName;
        set { if (SetProperty(ref _aiDisplayName, value)) ScheduleCommit(); }
    }

    public decimal AiProgressTarget
    {
        get => _aiProgressTarget;
        set { if (SetProperty(ref _aiProgressTarget, Math.Max(0, value))) ScheduleCommit(); }
    }

    public string TokscaleStatusTitle
    {
        get => _tokscaleStatusTitle;
        private set => SetProperty(ref _tokscaleStatusTitle, value);
    }

    public string TokscaleStatusMessage
    {
        get => _tokscaleStatusMessage;
        private set => SetProperty(ref _tokscaleStatusMessage, value);
    }

    public bool IsTokscaleReady => _tokscaleStatus == TokscaleStatus.Ready;
    public bool IsTokscaleUnavailable => _tokscaleStatus != TokscaleStatus.Ready;
    public bool IsAiProgressTargetVisible => SelectedAiUsageMode?.Kind is AiUsageDataKind.ModelTokens or AiUsageDataKind.ModelCost;

    public string CurrentThemeName
    {
        get => _currentThemeName;
        private set => SetProperty(ref _currentThemeName, value);
    }

    public string CurrentThemeDescription
    {
        get => _currentThemeDescription;
        private set => SetProperty(ref _currentThemeDescription, value);
    }

    public string CurrentThemeDetails
    {
        get => _currentThemeDetails;
        private set => SetProperty(ref _currentThemeDetails, value);
    }

    public string SourceSummary
    {
        get => _sourceSummary;
        private set => SetProperty(ref _sourceSummary, value);
    }

    public string LastUpdated
    {
        get => _lastUpdated;
        private set => SetProperty(ref _lastUpdated, value);
    }

    public string SafeLeft
    {
        get => _safeLeft;
        set { if (SetProperty(ref _safeLeft, value)) ScheduleCommit(); }
    }

    public string SafeTop
    {
        get => _safeTop;
        set { if (SetProperty(ref _safeTop, value)) ScheduleCommit(); }
    }

    public string SafeRight
    {
        get => _safeRight;
        set { if (SetProperty(ref _safeRight, value)) ScheduleCommit(); }
    }

    public string SafeBottom
    {
        get => _safeBottom;
        set { if (SetProperty(ref _safeBottom, value)) ScheduleCommit(); }
    }

    public ImageTimePlacement ImageTimePlacement
    {
        get => _imageTimePlacement;
        set { if (SetProperty(ref _imageTimePlacement, value)) ScheduleCommit(); }
    }

    public IReadOnlyList<ImageTimePlacement> ImageTimePlacements { get; } =
        Enum.GetValues<ImageTimePlacement>();

    public ImageClockStyle ImageClockStyle
    {
        get => _imageClockStyle;
        set
        {
            if (SetProperty(ref _imageClockStyle, value))
            {
                OnPropertyChanged(nameof(IsImageDigitalClockStyle));
                OnPropertyChanged(nameof(IsImageLargeDigitalClockStyle));
                OnPropertyChanged(nameof(IsImageAnalogClockStyle));
                OnPropertyChanged(nameof(IsImageFlipClockStyle));
                OnPropertyChanged(nameof(IsImageTextAlignmentEnabled));
                ScheduleCommit();
            }
        }
    }

    public IReadOnlyList<ImageClockStyle> ImageClockStyles { get; } =
        [ImageClockStyle.Digital, ImageClockStyle.Analog, ImageClockStyle.Flip];

    public bool ImageTimeBackground
    {
        get => _imageTimeBackground;
        set { if (SetProperty(ref _imageTimeBackground, value)) ScheduleCommit(); }
    }

    public ImageTextColor ImageTextColor
    {
        get => _imageTextColor;
        set { if (SetProperty(ref _imageTextColor, value)) ScheduleCommit(); }
    }

    public IReadOnlyList<ImageTextColor> ImageTextColors { get; } = Enum.GetValues<ImageTextColor>();

    public ImageTextAlignment ImageTextAlignment
    {
        get => _imageTextAlignment;
        set { if (SetProperty(ref _imageTextAlignment, value)) ScheduleCommit(); }
    }

    public IReadOnlyList<ImageTextAlignment> ImageTextAlignments { get; } = Enum.GetValues<ImageTextAlignment>();

    public int ImageTimeFontSize
    {
        get => _imageTimeFontSize;
        set { if (SetProperty(ref _imageTimeFontSize, Math.Clamp(value, 20, 40))) ScheduleCommit(); }
    }

    public int ImageDateFontSize
    {
        get => _imageDateFontSize;
        set { if (SetProperty(ref _imageDateFontSize, Math.Clamp(value, 9, 18))) ScheduleCommit(); }
    }

    public int ImageWeatherFontSize
    {
        get => _imageWeatherFontSize;
        set { if (SetProperty(ref _imageWeatherFontSize, Math.Clamp(value, 9, 18))) ScheduleCommit(); }
    }

    public ImageDigitalOrder ImageDigitalOrder
    {
        get => _imageDigitalOrder;
        set { if (SetProperty(ref _imageDigitalOrder, value)) ScheduleCommit(); }
    }

    public IReadOnlyList<ImageDigitalOrder> ImageDigitalOrders { get; } = Enum.GetValues<ImageDigitalOrder>();

    public int ImageLargeTimeFontSize
    {
        get => _imageLargeTimeFontSize;
        set { if (SetProperty(ref _imageLargeTimeFontSize, Math.Clamp(value, 32, 44))) ScheduleCommit(); }
    }

    public int ImageAnalogClockSize
    {
        get => _imageAnalogClockSize;
        set { if (SetProperty(ref _imageAnalogClockSize, Math.Clamp(value, 58, 104))) ScheduleCommit(); }
    }

    public ImageAnalogOrder ImageAnalogOrder
    {
        get => _imageAnalogOrder;
        set { if (SetProperty(ref _imageAnalogOrder, value)) ScheduleCommit(); }
    }

    public IReadOnlyList<ImageAnalogOrder> ImageAnalogOrders { get; } = Enum.GetValues<ImageAnalogOrder>();

    public int ImageFlipTimeFontSize
    {
        get => _imageFlipTimeFontSize;
        set { if (SetProperty(ref _imageFlipTimeFontSize, Math.Clamp(value, 20, 38))) ScheduleCommit(); }
    }

    public bool IsImageDigitalClockStyle => ImageClockStyle == ImageClockStyle.Digital;
    public bool IsImageLargeDigitalClockStyle => ImageClockStyle == ImageClockStyle.LargeDigital;
    public bool IsImageAnalogClockStyle => ImageClockStyle == ImageClockStyle.Analog;
    public bool IsImageFlipClockStyle => ImageClockStyle == ImageClockStyle.Flip;
    public bool IsImageTextAlignmentEnabled => ImageClockStyle != ImageClockStyle.Flip;

    public bool ImageWeatherVisible
    {
        get => _imageWeatherVisible;
        set
        {
            if (SetProperty(ref _imageWeatherVisible, value))
            {
                OnPropertyChanged(nameof(IsWeatherSettingsVisible));
                ScheduleCommit();
            }
        }
    }

    public UiThemeMode UiThemeMode
    {
        get => _uiThemeMode;
        set { if (SetProperty(ref _uiThemeMode, value)) ScheduleCommit(); }
    }

    public IReadOnlyList<UiThemeMode> UiThemeModes { get; } = Enum.GetValues<UiThemeMode>();

    public DotMatrixProgressPeriod DotMatrixProgressPeriod
    {
        get => _dotMatrixProgressPeriod;
        set { if (SetProperty(ref _dotMatrixProgressPeriod, value)) ScheduleCommit(); }
    }

    public IReadOnlyList<DotMatrixProgressPeriod> DotMatrixProgressPeriods { get; } =
        Enum.GetValues<DotMatrixProgressPeriod>();

    public int DotMatrixProgressHeaderFontSize
    {
        get => _dotMatrixProgressHeaderFontSize;
        set { if (SetProperty(ref _dotMatrixProgressHeaderFontSize, Math.Clamp(value, 12, 20))) ScheduleCommit(); }
    }

    public bool IsImageTheme => SelectedTheme?.Id == "image";
    public bool IsDotMatrixProgressTheme => SelectedTheme?.Id == "clock-dot-progress";
    public bool IsWeatherTheme => SelectedTheme?.Id is "clock-weather-dot" or "weather-five-day";
    public bool IsWeatherSettingsVisible => IsWeatherTheme || (IsImageTheme && ImageWeatherVisible);
    public int ThemeAccentColumnSpan => IsImageTheme || IsDotMatrixProgressTheme ? 1 : 3;
    public bool IsStockTheme => SelectedTheme?.Id == "stocks";
    public bool IsAiTheme => SelectedTheme?.Id == "ai-quota";
    public bool IsMusicTheme => SelectedTheme is not null &&
        MediaThemeAutomation.IsMusicThemeId(SelectedTheme.Id);
    public bool IsSystemTheme => SelectedTheme?.Id is
        "system" or "dashboard" or "performance" or "network" or "system-minimal";

    public bool NeedsCurrentThemeNotice =>
        SelectedTheme?.Id switch
        {
            "stocks" => !_settings.HasAcknowledgedStockNotice,
            "ai-quota" => !_settings.HasAcknowledgedAiUsageNotice,
            _ => false
        };

    public async Task AcknowledgeCurrentThemeNoticeAsync()
    {
        switch (SelectedTheme?.Id)
        {
            case "stocks":
                _settings.HasAcknowledgedStockNotice = true;
                break;
            case "ai-quota":
                _settings.HasAcknowledgedAiUsageNotice = true;
                break;
            default:
                return;
        }

        await _settingsStore.SaveAsync(_settings);
    }
    public async Task InitializeAsync()
    {
        _settings = await _settingsStore.LoadAsync();
        _imageTheme.ImagePath = _settings.ImagePath;
        _themes = BuiltInThemes.Create(_imageTheme);
        BuildThemeGroups();
        ReloadFonts();
        ApplySettings();
        _loading = true;
        await RefreshTokscaleAsync(force: false);

        _loading = false;
        _lifetime = new CancellationTokenSource();
        _ = RunRefreshLoopAsync(_lifetime.Token);
        await RefreshPreviewAsync();
    }

    public async Task ChooseImageAsync()
    {
        if (PickImageAsync is null)
        {
            return;
        }

        string? path = await PickImageAsync();
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        _imageTheme.ImagePath = path;
        CurrentThemeDescription = path;
        ScheduleCommit();
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _lifetime?.Cancel();
        _commitDelay?.Cancel();
        _fontCatalog.FontsChanged -= FontCatalogOnFontsChanged;
        _fontCatalog.Dispose();
        _transport.Dispose();
        _weatherSource.Dispose();
        _stockSource.Dispose();
        _desktopServices.Dispose();
        _refreshLock.Dispose();
        _lifetime?.Dispose();
        _commitDelay?.Dispose();
        PreviewImage?.Dispose();
        await Task.CompletedTask;
    }

    private void BuildThemeGroups()
    {
        ThemeGroups.Clear();
        var byId = _themes.ToDictionary(theme => theme.Id, StringComparer.OrdinalIgnoreCase);
        AddGroup("监控", ["system", "dashboard", "performance", "network", "system-minimal"], byId);
        AddGroup("时间", ["clock", "clock-neon", "clock-flip", "image"], byId);
        AddGroup("资讯", ["weather-five-day", "stocks", "ai-quota"], byId);
        AddGroup("音乐", ["music", "music-minimal", "music-poster"], byId);
        AddGroup("点阵", ["clock-dot-matrix", "clock-weather-dot", "clock-dot-analog", "clock-dot-progress"], byId);

        IdleThemeOptions.Clear();
        MusicThemeOptions.Clear();
        foreach (ThemeItemViewModel item in ThemeGroups.SelectMany(group => group.Themes))
        {
            (MediaThemeAutomation.IsMusicThemeId(item.Id) ? MusicThemeOptions : IdleThemeOptions).Add(item);
        }
    }

    private void AddGroup(
        string name,
        IEnumerable<string> ids,
        IReadOnlyDictionary<string, IScreenTheme> byId)
    {
        var items = ids
            .Where(byId.ContainsKey)
            .Select(id => new ThemeItemViewModel(byId[id]))
            .ToArray();
        ThemeGroups.Add(new ThemeGroupViewModel(name, items));
    }

    private void ApplySettings()
    {
        _loading = true;
        DeviceIp = ExtractDeviceIp(_settings.DeviceEndpoint);
        AccentColor = string.IsNullOrWhiteSpace(_settings.AccentColor) ? "#E4694C" : _settings.AccentColor;
        SelectedFontId = _settings.SelectedFontId;
        RefreshSeconds = _settings.RefreshSeconds;
        AutoPush = _settings.AutoPush;
        AutoMediaThemeSwitch = _settings.AutoMediaThemeSwitch;
        IgnoreBrowserMediaSessions = _settings.IgnoreBrowserMediaSessions;
        MediaIdleThemeId = _settings.MediaIdleThemeId;
        MediaPlayingThemeId = _settings.MediaPlayingThemeId;
        AutoSwitchToMusic = _settings.AutoSwitchToMusic;
        MinimizeToTray = _settings.MinimizeToTray;
        CloseToTray = _settings.CloseToTray;
        StartMinimized = _settings.StartMinimized;
        LaunchAtStartup = _settings.LaunchAtStartup;
        WeatherAutomaticLocation = _settings.Weather?.UseAutomaticLocation ?? true;
        WeatherLocation = string.IsNullOrWhiteSpace(_settings.Weather?.LocationQuery)
            ? "北京"
            : _settings.Weather.LocationQuery;
        IReadOnlyList<StockItemSettings> stockItems = NormalizeStockItems(_settings.Stocks ?? new StockSettings());
        StockSymbol1 = stockItems[0].Symbol;
        StockAlias1 = stockItems[0].Alias;
        StockSymbol2 = stockItems[1].Symbol;
        StockAlias2 = stockItems[1].Alias;
        StockSymbol3 = stockItems[2].Symbol;
        StockAlias3 = stockItems[2].Alias;
        StockColorPreference = (_settings.Stocks?.RedForGain ?? true) ? "红涨绿跌" : "绿涨红跌";
        SafeLeft = _settings.SafeArea.Left.ToString(CultureInfo.InvariantCulture);
        SafeTop = _settings.SafeArea.Top.ToString(CultureInfo.InvariantCulture);
        SafeRight = _settings.SafeArea.Right.ToString(CultureInfo.InvariantCulture);
        SafeBottom = _settings.SafeArea.Bottom.ToString(CultureInfo.InvariantCulture);
        ImageTimePlacement = _settings.ImageTimePlacement;
        ImageClockStyle = _settings.ImageClockStyle == ImageClockStyle.LargeDigital ? ImageClockStyle.Digital : _settings.ImageClockStyle;
        ImageTimeBackground = _settings.ImageTimeBackground;
        ImageTextColor = _settings.ImageTextColor;
        ImageTextAlignment = _settings.ImageTextAlignment;
        ImageWeatherVisible = _settings.ImageWeatherVisible;
        ImageTimeFontSize = _settings.ImageTimeFontSize;
        ImageDateFontSize = _settings.ImageDateFontSize;
        ImageWeatherFontSize = _settings.ImageWeatherFontSize;
        ImageDigitalOrder = _settings.ImageDigitalOrder;
        ImageLargeTimeFontSize = _settings.ImageLargeTimeFontSize;
        ImageAnalogClockSize = _settings.ImageAnalogClockSize;
        ImageAnalogOrder = _settings.ImageAnalogOrder;
        ImageFlipTimeFontSize = _settings.ImageFlipTimeFontSize;
        UiThemeMode = _settings.UiThemeMode;
        DotMatrixProgressPeriod = _settings.DotMatrixProgressPeriod;
        DotMatrixProgressHeaderFontSize = _settings.DotMatrixProgressHeaderFontSize;
        _settings.AiQuota ??= new AiQuotaSettings();
        SelectedAiUsageMode = AiUsageModes.First(option => option.Kind == _settings.AiQuota.DataKind);
        AiDisplayName = _settings.AiQuota.DisplayName ?? string.Empty;
        AiProgressTarget = _settings.AiQuota.ProgressTarget;
        UpdateRenderer();
        _loading = false;
        SelectTheme(_settings.SelectedThemeId);
    }

    private void SelectTheme(string? id)
    {
        ThemeItemViewModel? item = ThemeGroups
            .SelectMany(group => group.Themes)
            .FirstOrDefault(candidate => string.Equals(candidate.Id, id, StringComparison.OrdinalIgnoreCase))
            ?? ThemeGroups.SelectMany(group => group.Themes).FirstOrDefault();
        SelectedTheme = item;
    }

    private void RaiseNavigationProperties()
    {
        OnPropertyChanged(nameof(IsScreenPage));
        OnPropertyChanged(nameof(IsThemePage));
        OnPropertyChanged(nameof(IsAutomationPage));
        OnPropertyChanged(nameof(IsSettingsPage));
        OnPropertyChanged(nameof(IsAboutPage));
    }

    private void RaiseThemeContextProperties()
    {
        OnPropertyChanged(nameof(IsImageTheme));
        OnPropertyChanged(nameof(IsDotMatrixProgressTheme));
        OnPropertyChanged(nameof(IsWeatherTheme));
        OnPropertyChanged(nameof(IsWeatherSettingsVisible));
        OnPropertyChanged(nameof(ThemeAccentColumnSpan));
        OnPropertyChanged(nameof(IsStockTheme));
        OnPropertyChanged(nameof(IsAiTheme));
        OnPropertyChanged(nameof(IsMusicTheme));
        OnPropertyChanged(nameof(IsSystemTheme));
    }

    private async Task RefreshTokscaleAsync(bool force)
    {
        TokscaleCatalog catalog = await _tokscaleSource.ReadCatalogAsync(force);
        _tokscaleCatalog = catalog;
        _tokscaleStatus = catalog.Status;
        TokscaleStatusTitle = catalog.Status switch
        {
            TokscaleStatus.Ready => "Tokscale 已连接",
            TokscaleStatus.NotInstalled => "尚未检测到 Tokscale",
            TokscaleStatus.NoData => "Tokscale 暂无数据",
            _ => "Tokscale 读取失败"
        };
        TokscaleStatusMessage = catalog.Message;
        OnPropertyChanged(nameof(IsTokscaleReady));
        OnPropertyChanged(nameof(IsTokscaleUnavailable));
        RebuildTokscaleOptions();
    }

    private void RebuildTokscaleOptions()
    {
        string preferredKey = SelectedTokscaleOption?.Key
            ?? _settings.AiQuota?.SelectedItemKey
            ?? string.Empty;
        TokscaleOptions.Clear();
        if (_tokscaleCatalog is not null && SelectedAiUsageMode is not null)
        {
            foreach (TokscaleDataOption option in TokscaleDataSource.Filter(_tokscaleCatalog, SelectedAiUsageMode.Kind))
            {
                TokscaleOptions.Add(option);
            }
        }
        SelectedTokscaleOption = TokscaleOptions.FirstOrDefault(option => option.Key == preferredKey)
            ?? TokscaleOptions.FirstOrDefault();
        if (_tokscaleStatus == TokscaleStatus.Ready && TokscaleOptions.Count == 0)
        {
            TokscaleStatusMessage = SelectedAiUsageMode?.Kind == AiUsageDataKind.SubscriptionRemaining
                ? "Tokscale 已连接，但没有订阅额度数据。可切换到模型 Token 或模型费用。"
                : "Tokscale 已连接，但当前没有模型统计数据。";
        }
    }
    private void ScheduleCommit()
    {
        if (_loading || _disposed)
        {
            return;
        }

        CancellationTokenSource delay;
        lock (_scheduleGate)
        {
            _commitDelay?.Cancel();
            _commitDelay?.Dispose();
            _commitDelay = new CancellationTokenSource();
            delay = _commitDelay;
        }

        _ = CommitAfterDelayAsync(delay.Token);
    }

    private async Task CommitAfterDelayAsync(CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(450, cancellationToken);
            await SaveSettingsAsync(cancellationToken);
            await RefreshAndPushAsync(forcePush: true, cancellationToken);
        }
        catch (OperationCanceledException)
        {
        }
    }

    private async Task SaveSettingsAsync(CancellationToken cancellationToken)
    {
        _settings.DeviceEndpoint = TryCreateEndpoint(DeviceIp, out Uri? endpoint)
            ? endpoint.AbsoluteUri
            : string.Empty;
        _settings.SelectedThemeId = SelectedTheme?.Id ?? "clock-dot-matrix";
        _settings.AccentColor = TryParseAccentColor(AccentColor, out _)
            ? AccentColor.Trim().ToUpperInvariant()
            : "#E4694C";
        _settings.SelectedFontId = SelectedFontId;
        _settings.RefreshSeconds = RefreshSeconds;
        _settings.AutoPush = AutoPush;
        _settings.AutoMediaThemeSwitch = AutoMediaThemeSwitch;
        _settings.IgnoreBrowserMediaSessions = IgnoreBrowserMediaSessions;
        _settings.MediaIdleThemeId = MediaIdleThemeId;
        _settings.MediaPlayingThemeId = MediaPlayingThemeId;
        _settings.AutoSwitchToMusic = AutoSwitchToMusic;
        _settings.MinimizeToTray = MinimizeToTray;
        _settings.CloseToTray = CloseToTray;
        _settings.StartMinimized = StartMinimized;
        _settings.LaunchAtStartup = LaunchAtStartup;
        _settings.SafeArea = ReadSafeArea();
        _settings.ImagePath = _imageTheme.ImagePath;
        _settings.ImageTimePlacement = ImageTimePlacement;
        _settings.ImageClockStyle = ImageClockStyle;
        _settings.ImageTimeBackground = ImageTimeBackground;
        _settings.ImageTextColor = ImageTextColor;
        _settings.ImageTextAlignment = ImageTextAlignment;
        _settings.ImageWeatherVisible = ImageWeatherVisible;
        _settings.ImageTimeFontSize = ImageTimeFontSize;
        _settings.ImageDateFontSize = ImageDateFontSize;
        _settings.ImageWeatherFontSize = ImageWeatherFontSize;
        _settings.ImageDigitalOrder = ImageDigitalOrder;
        _settings.ImageLargeTimeFontSize = ImageLargeTimeFontSize;
        _settings.ImageAnalogClockSize = ImageAnalogClockSize;
        _settings.ImageAnalogOrder = ImageAnalogOrder;
        _settings.ImageFlipTimeFontSize = ImageFlipTimeFontSize;
        _settings.UiThemeMode = UiThemeMode;
        _settings.DotMatrixProgressPeriod = DotMatrixProgressPeriod;
        _settings.DotMatrixProgressHeaderFontSize = DotMatrixProgressHeaderFontSize;
        _settings.AiQuota = new AiQuotaSettings
        {
            SourceKind = AiQuotaSourceKind.Tokscale,
            DataKind = SelectedAiUsageMode?.Kind ?? AiUsageDataKind.SubscriptionRemaining,
            SelectedItemKey = SelectedTokscaleOption?.Key ?? string.Empty,
            DisplayName = AiDisplayName.Trim(),
            ProgressTarget = AiProgressTarget
        };
        _settings.Weather ??= new WeatherSettings();
        _settings.Weather.UseAutomaticLocation = WeatherAutomaticLocation;
        _settings.Weather.LocationQuery = string.IsNullOrWhiteSpace(WeatherLocation) ? "北京" : WeatherLocation.Trim();
        _settings.Stocks = new StockSettings
        {
            RedForGain = StockColorPreference == "红涨绿跌",
            Items =
            [
                new StockItemSettings { Symbol = StockSymbol1.Trim(), Alias = StockAlias1.Trim() },
                new StockItemSettings { Symbol = StockSymbol2.Trim(), Alias = StockAlias2.Trim() },
                new StockItemSettings { Symbol = StockSymbol3.Trim(), Alias = StockAlias3.Trim() }
            ]
        };
        UpdateRenderer();
        await _settingsStore.SaveAsync(_settings, cancellationToken);
    }

    private async Task RunRefreshLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(RefreshSeconds), cancellationToken);
                bool staticImage = SelectedTheme?.Id == "image" && !ImageWeatherVisible;
                if (!staticImage || AutoMediaThemeSwitch)
                {
                    await RefreshAndPushAsync(AutoPush, cancellationToken);
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private async Task RefreshAndPushAsync(
        bool forcePush,
        CancellationToken cancellationToken = default)
    {
        await RefreshPreviewAsync(cancellationToken);
        if (forcePush)
        {
            await PushLatestAsync(cancellationToken);
        }
    }

    private async Task RefreshPreviewAsync(CancellationToken cancellationToken = default)
    {
        if (!await _refreshLock.WaitAsync(0, cancellationToken))
        {
            return;
        }

        int selectionVersion = Volatile.Read(ref _themeSelectionVersion);

        try
        {
            IScreenTheme requestedTheme = SelectedTheme?.Theme ?? _themes[0];
            SystemSnapshot system = await _systemSource.ReadAsync(cancellationToken);
            _musicSource.IgnoreBrowserSessions = IgnoreBrowserMediaSessions;
            MusicSnapshot music = await _musicSource.ReadAsync(cancellationToken);
            string effectiveId = MediaThemeAutomation.ResolveThemeId(
                _settings,
                music.Available && music.IsPlaying,
                requestedTheme.Id);
            IScreenTheme theme = _themes.FirstOrDefault(candidate =>
                string.Equals(candidate.Id, effectiveId, StringComparison.OrdinalIgnoreCase))
                ?? requestedTheme;

            WeatherSnapshot? weather = null;
            StockSnapshot? stocks = null;
            if (requestedTheme.Id is "clock-weather-dot" or "weather-five-day" ||
                theme.Id is "clock-weather-dot" or "weather-five-day" ||
                ImageWeatherVisible && (requestedTheme.Id == "image" || theme.Id == "image"))
            {
                WeatherSettings weatherSettings = await _desktopServices.ResolveWeatherSettingsAsync(
                    _settings.Weather ?? new WeatherSettings(),
                    cancellationToken);
                weather = await _weatherSource.ReadAsync(weatherSettings, cancellationToken);
            }
            if (requestedTheme.Id == "stocks" || theme.Id == "stocks")
            {
                stocks = await _stockSource.ReadAsync(
                    _settings.Stocks ?? new StockSettings(),
                    cancellationToken);
            }

            AiQuotaSnapshot aiQuota = AiQuotaSnapshot.Empty;
            if (requestedTheme.Id == "ai-quota" || theme.Id == "ai-quota")
            {
                TokscaleCatalog catalog = await _tokscaleSource.ReadCatalogAsync(cancellationToken: cancellationToken);
                _tokscaleCatalog = catalog;
                aiQuota = TokscaleDataSource.CreateSnapshot(
                    new AiQuotaSettings
                    {
                        SourceKind = AiQuotaSourceKind.Tokscale,
                        DataKind = SelectedAiUsageMode?.Kind ?? AiUsageDataKind.SubscriptionRemaining,
                        SelectedItemKey = SelectedTokscaleOption?.Key ?? string.Empty,
                        DisplayName = AiDisplayName,
                        ProgressTarget = AiProgressTarget
                    },
                    catalog);
            }

            SystemSnapshot snapshot = system with
            {
                Music = music,
                Weather = weather,
                Stocks = stocks,
                AiQuota = aiQuota
            };
            _latestFrame = _renderer.Render(
                theme,
                snapshot,
                100,
                GetAccentColor(),
                GetSelectedFontFamily(),
                new ScreenDisplayOptions(
                    ImageTimePlacement,
                    DotMatrixProgressPeriod,
                    ImageClockStyle,
                    ImageTimeBackground,
                    ImageTextColor,
                    ImageTextAlignment,
                    ImageWeatherVisible,
                    ImageTimeFontSize,
                    ImageDateFontSize,
                    ImageWeatherFontSize,
                    ImageDigitalOrder,
                    ImageLargeTimeFontSize,
                    ImageAnalogClockSize,
                    ImageAnalogOrder,
                    ImageFlipTimeFontSize,
                    DotMatrixProgressHeaderFontSize));

            using var stream = new MemoryStream(_latestFrame.JpegBytes, writable: false);
            var bitmap = new Bitmap(stream);
            bool accepted = false;
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (selectionVersion != Volatile.Read(ref _themeSelectionVersion) ||
                    !string.Equals(SelectedTheme?.Id, requestedTheme.Id, StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }

                PreviewImage = bitmap;
                accepted = true;
                LastUpdated = $"更新于 {DateTime.Now:HH:mm:ss}";
                SourceSummary = BuildSourceSummary(requestedTheme.Id, system, music, weather, stocks, aiQuota);
            });
            if (!accepted)
            {
                bitmap.Dispose();
            }
        }
        catch (Exception ex)
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (selectionVersion == Volatile.Read(ref _themeSelectionVersion))
                {
                    SourceSummary = $"刷新失败：{ex.Message}";
                }
            });
        }
        finally
        {
            _refreshLock.Release();
        }
    }

    private async Task PushLatestAsync(CancellationToken cancellationToken)
    {
        if (_latestFrame is null || !TryCreateEndpoint(DeviceIp, out Uri? endpoint))
        {
            SetDeviceStatus(false);
            return;
        }

        DevicePushResult result = await _transport.PushAsync(endpoint, _latestFrame, cancellationToken);
        SetDeviceStatus(result.Success);
    }

    private void SetDeviceStatus(bool connected) =>
        Dispatcher.UIThread.Post(() =>
        {
            DeviceConnected = connected;
            DeviceStatus = connected ? "设备在线" : "断开连接";
        });

    private void ReloadFonts()
    {
        string preferred = SelectedFontId;
        Fonts = _fontCatalog.Scan();
        SelectedFontId = Fonts.Any(item => item.Id == preferred)
            ? preferred
            : ScreenFontOption.DefaultId;
    }

    private void FontCatalogOnFontsChanged(object? sender, EventArgs e) =>
        Dispatcher.UIThread.Post(() =>
        {
            ReloadFonts();
            ScheduleCommit();
        });

    private WpfFontFamily GetSelectedFontFamily() =>
        Fonts.FirstOrDefault(item => item.Id == SelectedFontId)?.FontFamily
        ?? ScreenFontOption.Default.FontFamily;

    private WpfColor GetAccentColor() =>
        TryParseAccentColor(AccentColor, out WpfColor color)
            ? color
            : WpfColor.FromRgb(228, 105, 76);

    private static bool TryParseAccentColor(string? value, out WpfColor color)
    {
        try
        {
            object? parsed = WpfColorConverter.ConvertFromString(value?.Trim());
            if (parsed is WpfColor result)
            {
                color = result;
                return true;
            }
        }
        catch
        {
        }

        color = default;
        return false;
    }

    private void UpdateRenderer() =>
        _renderer = new ScreenRenderer(new ScreenProfile(142, 428, 524288, ReadSafeArea()));

    private static IReadOnlyList<StockItemSettings> NormalizeStockItems(StockSettings settings)
    {
        var items = (settings.Items ?? []).Take(3).ToList();
        while (items.Count < 3)
        {
            items.Add(new StockItemSettings());
        }
        return items;
    }

    private ScreenInsets ReadSafeArea()
    {
        int left = ReadClamped(SafeLeft, 10, 0, 60);
        int top = ReadClamped(SafeTop, 52, 0, 160);
        int right = ReadClamped(SafeRight, 10, 0, 60);
        int bottom = ReadClamped(SafeBottom, 12, 0, 100);
        return left + right >= 132 || top + bottom >= 418
            ? new ScreenInsets(10, 52, 10, 12)
            : new ScreenInsets(left, top, right, bottom);
    }

    private static int ReadClamped(string value, int fallback, int minimum, int maximum) =>
        int.TryParse(value, out int parsed)
            ? Math.Clamp(parsed, minimum, maximum)
            : fallback;

    private static bool TryCreateEndpoint(string? value, out Uri endpoint)
    {
        endpoint = null!;
        if (!IPAddress.TryParse(ExtractDeviceIp(value), out IPAddress? address) ||
            address.AddressFamily != AddressFamily.InterNetwork)
        {
            return false;
        }

        endpoint = new Uri($"http://{address}/image/upload", UriKind.Absolute);
        return true;
    }

    private static string ExtractDeviceIp(string? value)
    {
        string candidate = value?.Trim() ?? string.Empty;
        return Uri.TryCreate(candidate, UriKind.Absolute, out Uri? uri) &&
               uri.Scheme is "http" or "https"
            ? uri.Host
            : candidate;
    }

    private string BuildSourceSummary(
        string themeId,
        SystemSnapshot system,
        MusicSnapshot music,
        WeatherSnapshot? weather,
        StockSnapshot? stocks,
        AiQuotaSnapshot aiQuota) =>
        themeId switch
        {
            "system" or "dashboard" or "performance" or "network" or "system-minimal" =>
                $"CPU {system.CpuPercent:0}% · 内存 {system.MemoryPercent:0}% · 下载 {system.DownloadMbps:0.0}M · 上传 {system.UploadMbps:0.0}M",
            "music" or "music-minimal" or "music-poster" =>
                music.Available ? $"{(music.IsPlaying ? "正在播放" : "已暂停")} · {music.Title} — {music.Artist}" : "当前没有可用的 Windows 媒体会话",
            "clock-weather-dot" or "weather-five-day" or "image" when weather is { Available: true } =>
                weather is { Available: true } ? $"{weather.LocationName} · {weather.TemperatureC:0}° · {weather.ConditionText}" : weather?.ErrorMessage ?? "暂无天气数据",
            "stocks" =>
                stocks is { Quotes.Count: > 0 } ? $"{stocks.Quotes.Count} 项行情 · 更新 {stocks.UpdatedAt:HH:mm}" : stocks?.ErrorMessage ?? "请先配置股票代码",
            "ai-quota" => aiQuota.Available
                ? $"{aiQuota.PlatformName} · {aiQuota.PrimaryDisplay}"
                : _tokscaleCatalog?.Message ?? "尚未读取 Tokscale 数据",
            _ => "此主题仅使用本地时间与设置"
        };

    private static string GetLoadingSummary(string themeId) =>
        themeId switch
        {
            "system" or "dashboard" or "performance" or "network" or "system-minimal" => "正在读取电脑状态…",
            "music" or "music-minimal" or "music-poster" => "正在读取媒体状态…",
            "clock-weather-dot" or "weather-five-day" => "正在读取天气数据…",
            "stocks" => "正在读取股票行情…",
            "ai-quota" => "正在读取 AI 用量…",
            _ => "正在刷新主题数据…"
        };
}
