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
using WpfColor = KeyboardScreen.Core.Color;
using WpfFontFamily = KeyboardScreen.Core.FontFamily;

namespace KeyboardScreen.App.Avalonia.ViewModels;

/// <summary>A theme's membership checkbox in the carousel card.</summary>
public sealed class CarouselOptionViewModel(ThemeItemViewModel theme, Action changed) : ObservableObject
{
    private bool _included;

    public ThemeItemViewModel Theme { get; } = theme;

    public bool Included
    {
        get => _included;
        set { if (SetProperty(ref _included, value)) changed(); }
    }
}

public sealed class MainWindowViewModel : ObservableObject, IAsyncDisposable
{
    private readonly ISystemSnapshotSource _systemSource = new WindowsSystemSnapshotSource();
    private readonly WindowsMusicSnapshotSource _musicSource = new();
    private readonly OpenMeteoWeatherSnapshotSource _weatherSource = new();
    private readonly YahooStockSnapshotSource _yahooStockSource = new();
    private readonly TencentStockSnapshotSource _tencentStockSource = new();
    private readonly BinanceStockSnapshotSource _binanceStockSource = new();
    private readonly TokscaleDataSource _tokscaleSource = new();
    private readonly ClaudeUsageSnapshotSource _claudeUsageSource = new();
    private readonly CurrencySnapshotSource _currencySource = new();
    private readonly PomodoroTimer _pomodoroTimer = new();
    private readonly LibreHardwareMonitorSource _hardwareSource = new();
    private readonly GitHubContributionSource _gitHubSource = new();
    private readonly AirAlertSource _airAlertSource = new();
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
    public PushDiagnostics PushDiagnostics { get; } = new();
    private CancellationTokenSource? _lifetime;
    private CancellationTokenSource? _commitDelay;
    private bool _loading = true;
    private bool _disposed;
    private string _selectedNavigation = "screen";
    private ThemeItemViewModel? _selectedTheme;
    private Bitmap? _previewImage;
    private string _deviceStatus = Loc.T("StatusDisconnected");
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
    private string _weatherLocation = WeatherSettings.DefaultLocationQuery;
    private string _stockSymbol1 = string.Empty;
    private string _stockAlias1 = string.Empty;
    private string _stockSymbol2 = string.Empty;
    private string _stockAlias2 = string.Empty;
    private string _stockSymbol3 = string.Empty;
    private string _stockAlias3 = string.Empty;
    private string _stockSymbol4 = string.Empty;
    private string _stockAlias4 = string.Empty;
    private string _stockSymbol5 = string.Empty;
    private string _stockAlias5 = string.Empty;
    private bool _stockRedForGain = true;
    private StockSourceKind _stockSource = StockSourceKind.Tencent;
    private bool _stockEnabled1 = true;
    private bool _stockEnabled2 = true;
    private bool _stockEnabled3 = true;
    private bool _stockEnabled4 = true;
    private bool _stockEnabled5 = true;
    private AiUsageModeOption? _selectedAiUsageMode;
    private TokscaleDataOption? _selectedTokscaleOption;
    private TokscaleCatalog? _tokscaleCatalog;
    private string _aiDisplayName = string.Empty;
    private decimal _aiProgressTarget;
    private string _tokscaleStatusTitle = Loc.T("TokscaleDetecting");
    private string _tokscaleStatusMessage = Loc.T("TokscaleReadingLocal");
    private TokscaleStatus _tokscaleStatus = TokscaleStatus.NoData;
    private string _currentThemeName = string.Empty;
    private string _currentThemeDescription = string.Empty;
    private string _currentThemeDetails = string.Empty;
    private string _sourceSummary = Loc.T("SourceReading");
    private string _lastUpdated = Loc.T("LastUpdatedNever");
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
    private string _updateStatusText = string.Empty;
    private AppLanguageInfo _selectedLanguage = AppLanguageInfo.For(Loc.Language);
    private string _claudeSessionKey = string.Empty;
    private string _claudeModelScope = ClaudeUsageSettings.DefaultModelScope;
    private bool _claudeCountLocalTokens = true;
    private ClaudeUsageSnapshot? _claudeUsage;
    private string _currencyBase = "USD";
    private string _currencyQuotes = "EUR, UAH, PLN";
    private CurrencySnapshot? _currencySnapshot;
    private string _cryptoSymbol1 = "BTCUSDT";
    private string _cryptoSymbol2 = "ETHUSDT";
    private string _cryptoSymbol3 = string.Empty;
    private string _cryptoSymbol4 = string.Empty;
    private bool _cryptoRedForGain;
    private CryptoSnapshot? _cryptoSnapshot;
    private int _pomodoroFocusMinutes = 25;
    private int _pomodoroBreakMinutes = 5;
    private int _pomodoroTargetCycles = 4;
    private bool _scheduleEnabled;
    private string _scheduleNightStart = "22:00";
    private string _scheduleNightEnd = "07:00";
    private bool _scheduleSwitchTheme;
    private string _scheduleNightThemeId = "clock";
    private bool _scheduleDimAtNight = true;
    private int _scheduleNightBrightness = 60;
    private int _hardwarePageSeconds = 5;
    private HardwareSnapshot? _hardwareSnapshot;
    private HardwareMonitorTheme? _hardwareTheme;
    private bool _carouselEnabled;
    private int _carouselIntervalSeconds = 30;
    private bool _use12HourClock;
    private bool _useFahrenheit;
    private string? _lastEffectiveThemeId;
    private string _gitHubUsername = string.Empty;
    private string _gitHubToken = string.Empty;
    private GitHubContributionSnapshot? _gitHubSnapshot;
    private string _alertsToken = string.Empty;
    private string _alertsLocation = string.Empty;
    private AirAlertSnapshot? _alertsSnapshot;
    private TelegramService? _telegramService;
    private string _telegramApiId = string.Empty;
    private string _telegramApiHash = string.Empty;
    private string _telegramPhone = string.Empty;
    private string _telegramCode = string.Empty;
    private string _telegramPassword = string.Empty;
    private bool _telegramPopupsEnabled = true;
    private TelegramPrivacyMode _telegramPrivacyMode = TelegramPrivacyMode.Sender;
    private int _telegramPopupSeconds = 6;
    private TelegramMessageEvent? _telegramPopup;
    private int _telegramPopupExtra;
    private DateTimeOffset _telegramPopupUntil;
    private CancellationTokenSource? _telegramPopupDelay;
    private readonly NotificationEngine _notificationEngine = new();
    private readonly WindowsToastNotifier _toastNotifier = new();
    private DateTimeOffset _lastNotificationSweep = DateTimeOffset.MinValue;
    private bool _notifyEnabled;
    private bool _notifyClaude = true;
    private bool _notifyOffline = true;
    private string _notifySymbol1 = string.Empty;
    private string _notifyAbove1 = string.Empty;
    private string _notifyBelow1 = string.Empty;
    private string _notifySymbol2 = string.Empty;
    private string _notifyAbove2 = string.Empty;
    private string _notifyBelow2 = string.Empty;
    private string _notifySymbol3 = string.Empty;
    private string _notifyAbove3 = string.Empty;
    private string _notifyBelow3 = string.Empty;
    private bool _isUpdateAvailable;
    private string? _latestReleaseUrl;
    private byte[]? _lastPushedJpeg;
    private bool _perfVisualCpuEnabled = true;
    private bool _perfVisualMemoryEnabled = true;
    private bool _perfVisualDownloadEnabled = true;
    private bool _perfVisualUploadEnabled = true;
    private bool _perfVisualGpuEnabled = true;
    private PerformanceVisualTheme? _perfVisualTheme;

    public MainWindowViewModel(IWindowsDesktopServices desktopServices)
    {
        _desktopServices = desktopServices;
        _fontCatalog = new FontFolderCatalog(Path.Combine(AppContext.BaseDirectory, "Fonts"));
        _fontCatalog.FontsChanged += FontCatalogOnFontsChanged;
        Loc.Instance.LanguageChanged += OnLanguageChanged;

        SelectNavigationCommand = new ParameterizedCommand<string>(value =>
            SelectedNavigation = string.IsNullOrWhiteSpace(value) ? "screen" : value);
        SelectThemeCommand = new ParameterizedCommand<string>(SelectTheme);
        RefreshCommand = new AsyncCommand(() => RefreshAndPushAsync(shouldPush: true, forcePush: true));
        OpenFontsFolderCommand = new RelayCommand(() => _desktopServices.OpenFolder(_fontCatalog.FolderPath));
        OpenAuthorCommand = new RelayCommand(() => _desktopServices.OpenUrl("https://github.com/zcat95"));
        OpenTokscaleDocsCommand = new RelayCommand(() => _desktopServices.OpenUrl("https://github.com/junhoyeo/tokscale"));
        OpenClaudeSiteCommand = new RelayCommand(() => _desktopServices.OpenUrl("https://claude.ai"));
        OpenAlertsSiteCommand = new RelayCommand(() => _desktopServices.OpenUrl("https://alerts.in.ua"));
        RefreshTokscaleCommand = new AsyncCommand(() => RefreshTokscaleAsync(force: true));
        StartPomodoroCommand = new RelayCommand(StartPomodoro);
        StopPomodoroCommand = new RelayCommand(StopPomodoro);
        _pomodoroTimer.Changed += OnPomodoroChanged;
        ConnectTelegramCommand = new AsyncCommand(ConnectTelegramAsync);
        SubmitTelegramCodeCommand = new RelayCommand(SubmitTelegramCode);
        SubmitTelegramPasswordCommand = new RelayCommand(SubmitTelegramPassword);
        TelegramLogoutCommand = new AsyncCommand(TelegramLogoutAsync);
        ExportSettingsCommand = new AsyncCommand(ExportSettingsAsync);
        ImportSettingsCommand = new AsyncCommand(ImportSettingsAsync);
        CheckForUpdatesCommand = new AsyncCommand(CheckForUpdatesAsync);
        OpenLatestReleaseCommand = new RelayCommand(() =>
        {
            if (!string.IsNullOrWhiteSpace(LatestReleaseUrl))
            {
                _desktopServices.OpenUrl(LatestReleaseUrl);
            }
        });
    }

    public ObservableCollection<ThemeGroupViewModel> ThemeGroups { get; } = [];
    public ObservableCollection<ThemeItemViewModel> IdleThemeOptions { get; } = [];
    public ObservableCollection<ThemeItemViewModel> MusicThemeOptions { get; } = [];
    public ObservableCollection<CarouselOptionViewModel> CarouselOptions { get; } = [];
    public ObservableCollection<TokscaleDataOption> TokscaleOptions { get; } = [];
    public IReadOnlyList<AiUsageModeOption> AiUsageModes { get; } =
    [
        new(AiUsageDataKind.SubscriptionRemaining, "AiKindSubscription", "AiKindSubscriptionHint"),
        new(AiUsageDataKind.ModelTokens, "AiKindModelTokens", "AiKindModelTokensHint"),
        new(AiUsageDataKind.ModelCost, "AiKindModelCost", "AiKindModelCostHint")
    ];

    public ICommand SelectNavigationCommand { get; }
    public ICommand SelectThemeCommand { get; }
    public ICommand RefreshCommand { get; }
    public ICommand OpenFontsFolderCommand { get; }
    public ICommand OpenAuthorCommand { get; }
    public ICommand OpenTokscaleDocsCommand { get; }
    public ICommand OpenClaudeSiteCommand { get; }
    public ICommand OpenAlertsSiteCommand { get; }
    public ICommand RefreshTokscaleCommand { get; }
    public ICommand StartPomodoroCommand { get; }
    public ICommand StopPomodoroCommand { get; }
    public ICommand ConnectTelegramCommand { get; }
    public ICommand SubmitTelegramCodeCommand { get; }
    public ICommand SubmitTelegramPasswordCommand { get; }
    public ICommand TelegramLogoutCommand { get; }
    public ICommand ExportSettingsCommand { get; }
    public ICommand ImportSettingsCommand { get; }

    /// <summary>Set by the window: shows the Telegram risk notice dialog once.</summary>
    public Func<Task>? ShowTelegramNoticeAsync { get; set; }

    /// <summary>Set by the window: file pickers for the settings backup card.</summary>
    public Func<Task<string?>>? PickExportPathAsync { get; set; }

    public Func<Task<string?>>? PickImportPathAsync { get; set; }
    public ICommand CheckForUpdatesCommand { get; }
    public ICommand OpenLatestReleaseCommand { get; }

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

    public string AppVersionLabel => Loc.T("AboutVersion", AppVersion);

    public string DiagnosticsLastPushText
    {
        get
        {
            PushDiagnosticsEntry? latest = PushDiagnostics.Latest;
            if (latest is null)
            {
                return Loc.T("DiagnosticsNoPushes");
            }

            string outcome = latest.Success
                ? Loc.T("DiagnosticsOk")
                : Loc.T("DiagnosticsFailed");
            string status = latest.StatusCode is { } code ? $" · HTTP {code}" : string.Empty;
            return $"{latest.Timestamp:HH:mm:ss} · {outcome}{status}";
        }
    }

    public string DiagnosticsDetailText
    {
        get
        {
            PushDiagnosticsEntry? latest = PushDiagnostics.Latest;
            if (latest is null)
            {
                return string.Empty;
            }

            return Loc.T("DiagnosticsLatency") + $" {latest.Latency.TotalMilliseconds:0} ms · "
                + Loc.T("DiagnosticsFrameSize") + $" {latest.FrameBytes / 1024.0:0} KB";
        }
    }

    public string DiagnosticsErrorsText
    {
        get
        {
            IReadOnlyList<PushDiagnosticsEntry> errors = PushDiagnostics.RecentErrors();
            if (errors.Count == 0)
            {
                return Loc.T("DiagnosticsNoErrors");
            }

            return string.Join(
                Environment.NewLine,
                errors.Select(entry => $"{entry.Timestamp:HH:mm:ss} — {entry.Message}"));
        }
    }

    private void RaiseDiagnosticsProperties()
    {
        OnPropertyChanged(nameof(DiagnosticsLastPushText));
        OnPropertyChanged(nameof(DiagnosticsDetailText));
        OnPropertyChanged(nameof(DiagnosticsErrorsText));
    }

    public IReadOnlyList<AppLanguageInfo> AppLanguages => AppLanguageInfo.All;

    public AppLanguageInfo SelectedLanguage
    {
        get => _selectedLanguage;
        set
        {
            if (value is null || !SetProperty(ref _selectedLanguage, value))
            {
                return;
            }

            Loc.Language = value.Language;
            ScheduleCommit();
        }
    }

    private static string ResolveAppVersion()
    {
        Version? version = typeof(MainWindowViewModel).Assembly.GetName().Version;
        return version is null ? string.Empty : $"{version.Major}.{version.Minor}.{version.Build}";
    }

    public string UpdateStatusText
    {
        get => _updateStatusText;
        set => SetProperty(ref _updateStatusText, value);
    }

    public bool IsUpdateAvailable
    {
        get => _isUpdateAvailable;
        set => SetProperty(ref _isUpdateAvailable, value);
    }

    public string? LatestReleaseUrl
    {
        get => _latestReleaseUrl;
        set => SetProperty(ref _latestReleaseUrl, value);
    }

    public bool PerfVisualCpuEnabled
    {
        get => _perfVisualCpuEnabled;
        set
        {
            if (SetProperty(ref _perfVisualCpuEnabled, value))
            {
                ApplyPerfVisualModules();
                ScheduleCommit();
            }
        }
    }

    public bool PerfVisualMemoryEnabled
    {
        get => _perfVisualMemoryEnabled;
        set
        {
            if (SetProperty(ref _perfVisualMemoryEnabled, value))
            {
                ApplyPerfVisualModules();
                ScheduleCommit();
            }
        }
    }

    public bool PerfVisualDownloadEnabled
    {
        get => _perfVisualDownloadEnabled;
        set
        {
            if (SetProperty(ref _perfVisualDownloadEnabled, value))
            {
                ApplyPerfVisualModules();
                ScheduleCommit();
            }
        }
    }

    public bool PerfVisualUploadEnabled
    {
        get => _perfVisualUploadEnabled;
        set
        {
            if (SetProperty(ref _perfVisualUploadEnabled, value))
            {
                ApplyPerfVisualModules();
                ScheduleCommit();
            }
        }
    }

    public bool PerfVisualGpuEnabled
    {
        get => _perfVisualGpuEnabled;
        set
        {
            if (SetProperty(ref _perfVisualGpuEnabled, value))
            {
                ApplyPerfVisualModules();
                ScheduleCommit();
            }
        }
    }

    public bool IsPerformanceVisualTheme => SelectedTheme?.Id == "performance-visual";

    public string RefreshSecondsInput
    {
        get => _refreshSeconds.ToString(CultureInfo.InvariantCulture);
        set
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                OnPropertyChanged(nameof(RefreshSecondsInput));
                return;
            }

            if (int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out int parsed))
            {
                RefreshSeconds = Math.Clamp(parsed, 1, 3600);
            }
            else
            {
                OnPropertyChanged(nameof(RefreshSecondsInput));
            }
        }
    }

    private void ApplyPerfVisualModules() =>
        _perfVisualTheme?.SetModulesEnabled(
            PerfVisualCpuEnabled,
            PerfVisualMemoryEnabled,
            PerfVisualDownloadEnabled,
            PerfVisualUploadEnabled,
            PerfVisualGpuEnabled);

    private bool CanDisableStockSlot(int slotIndex)
    {
        string[] symbols = [StockSymbol1, StockSymbol2, StockSymbol3, StockSymbol4, StockSymbol5];
        bool[] enabled = [StockEnabled1, StockEnabled2, StockEnabled3, StockEnabled4, StockEnabled5];
        if (string.IsNullOrWhiteSpace(symbols[slotIndex]))
        {
            return true;
        }

        int remaining = 0;
        for (int index = 0; index < symbols.Length; index++)
        {
            if (index == slotIndex)
            {
                continue;
            }

            if (enabled[index] && !string.IsNullOrWhiteSpace(symbols[index]))
            {
                remaining++;
            }
        }

        return remaining >= 2;
    }

    public bool StockSlot1CanDisable => CanDisableStockSlot(0);
    public bool StockSlot2CanDisable => CanDisableStockSlot(1);
    public bool StockSlot3CanDisable => CanDisableStockSlot(2);
    public bool StockSlot4CanDisable => CanDisableStockSlot(3);
    public bool StockSlot5CanDisable => CanDisableStockSlot(4);

    private void NotifyStockSlotCanDisable()
    {
        OnPropertyChanged(nameof(StockSlot1CanDisable));
        OnPropertyChanged(nameof(StockSlot2CanDisable));
        OnPropertyChanged(nameof(StockSlot3CanDisable));
        OnPropertyChanged(nameof(StockSlot4CanDisable));
        OnPropertyChanged(nameof(StockSlot5CanDisable));
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

            ApplySelectedThemeText(value);
            Interlocked.Increment(ref _themeSelectionVersion);
            SourceSummary = GetLoadingSummary(value.Id);
            LastUpdated = Loc.T("LastUpdatedRefreshing");
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

    public string DeviceSummary => string.IsNullOrWhiteSpace(DeviceIp) ? Loc.T("DeviceNotConfigured") : DeviceIp;

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
            value = Math.Clamp(value, 1, 3600);
            if (SetProperty(ref _refreshSeconds, value))
            {
                OnPropertyChanged(nameof(RefreshDurationText));
                OnPropertyChanged(nameof(CurrentThemeRefreshSeconds));
                ScheduleCommit();
            }
        }
    }

    public string RefreshDurationText
    {
        get
        {
            if (_refreshSeconds < 60)
            {
                return Loc.T("DurationSeconds", _refreshSeconds);
            }
            if (_refreshSeconds % 3600 == 0)
            {
                return Loc.T("DurationHours", _refreshSeconds / 3600);
            }
            if (_refreshSeconds % 60 == 0)
            {
                return Loc.T("DurationMinutes", _refreshSeconds / 60);
            }
            return Loc.T("DurationSeconds", _refreshSeconds);
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
        set
        {
            if (SetProperty(ref _stockSymbol1, value))
            {
                NotifyStockSlotCanDisable();
                ScheduleCommit();
            }
        }
    }

    public string StockAlias1
    {
        get => _stockAlias1;
        set { if (SetProperty(ref _stockAlias1, value)) ScheduleCommit(); }
    }

    public string StockSymbol2
    {
        get => _stockSymbol2;
        set
        {
            if (SetProperty(ref _stockSymbol2, value))
            {
                NotifyStockSlotCanDisable();
                ScheduleCommit();
            }
        }
    }

    public string StockAlias2
    {
        get => _stockAlias2;
        set { if (SetProperty(ref _stockAlias2, value)) ScheduleCommit(); }
    }

    public string StockSymbol3
    {
        get => _stockSymbol3;
        set
        {
            if (SetProperty(ref _stockSymbol3, value))
            {
                NotifyStockSlotCanDisable();
                ScheduleCommit();
            }
        }
    }

    public string StockAlias3
    {
        get => _stockAlias3;
        set { if (SetProperty(ref _stockAlias3, value)) ScheduleCommit(); }
    }

    /// <summary>True when a rise is drawn red (the mainland-China convention).</summary>
    public bool StockRedForGain
    {
        get => _stockRedForGain;
        set
        {
            if (SetProperty(ref _stockRedForGain, value))
            {
                OnPropertyChanged(nameof(StockColorPreference));
                ScheduleCommit();
            }
        }
    }

    /// <summary>
    /// The picker binds to display text, but the choice itself is stored as a
    /// bool, so switching language never invalidates the saved preference.
    /// </summary>
    public string StockColorPreference
    {
        get => StockColorPreferences[_stockRedForGain ? 0 : 1];
        set => StockRedForGain = string.Equals(value, StockColorPreferences[0], StringComparison.Ordinal);
    }

    public IReadOnlyList<string> StockColorPreferences =>
        [Loc.T("StockColorRedUp"), Loc.T("StockColorGreenUp")];

    /// <summary>True for the Tencent feed, false for Yahoo Finance.</summary>
    /// <summary>Backing enum so a language switch cannot invalidate the choice.</summary>
    public StockSourceKind StockSource
    {
        get => _stockSource;
        set
        {
            if (SetProperty(ref _stockSource, value))
            {
                OnPropertyChanged(nameof(StockSourcePreference));
                ScheduleCommit();
            }
        }
    }

    public string StockSourcePreference
    {
        get => StockSourceOptions[Math.Clamp((int)_stockSource, 0, StockSourceOptions.Count - 1)];
        set
        {
            IReadOnlyList<string> options = StockSourceOptions;
            for (int index = 0; index < options.Count; index++)
            {
                if (string.Equals(options[index], value, StringComparison.Ordinal))
                {
                    StockSource = (StockSourceKind)index;
                    return;
                }
            }
        }
    }

    public IReadOnlyList<string> StockSourceOptions =>
        [Loc.T("StockSourceTencent"), Loc.T("StockSourceYahoo"), Loc.T("StockSourceBinance")];

    public string StockSymbol4
    {
        get => _stockSymbol4;
        set
        {
            if (SetProperty(ref _stockSymbol4, value))
            {
                NotifyStockSlotCanDisable();
                ScheduleCommit();
            }
        }
    }

    public string StockAlias4
    {
        get => _stockAlias4;
        set { if (SetProperty(ref _stockAlias4, value)) ScheduleCommit(); }
    }

    public string StockSymbol5
    {
        get => _stockSymbol5;
        set
        {
            if (SetProperty(ref _stockSymbol5, value))
            {
                NotifyStockSlotCanDisable();
                ScheduleCommit();
            }
        }
    }

    public string StockAlias5
    {
        get => _stockAlias5;
        set { if (SetProperty(ref _stockAlias5, value)) ScheduleCommit(); }
    }

    public bool StockEnabled1
    {
        get => _stockEnabled1;
        set
        {
            if (_stockEnabled1 == value)
            {
                return;
            }

            if (!value && !CanDisableStockSlot(0))
            {
                OnPropertyChanged(nameof(StockEnabled1));
                return;
            }

            if (SetProperty(ref _stockEnabled1, value))
            {
                NotifyStockSlotCanDisable();
                ScheduleCommit();
            }
        }
    }

    public bool StockEnabled2
    {
        get => _stockEnabled2;
        set
        {
            if (_stockEnabled2 == value)
            {
                return;
            }

            if (!value && !CanDisableStockSlot(1))
            {
                OnPropertyChanged(nameof(StockEnabled2));
                return;
            }

            if (SetProperty(ref _stockEnabled2, value))
            {
                NotifyStockSlotCanDisable();
                ScheduleCommit();
            }
        }
    }

    public bool StockEnabled3
    {
        get => _stockEnabled3;
        set
        {
            if (_stockEnabled3 == value)
            {
                return;
            }

            if (!value && !CanDisableStockSlot(2))
            {
                OnPropertyChanged(nameof(StockEnabled3));
                return;
            }

            if (SetProperty(ref _stockEnabled3, value))
            {
                NotifyStockSlotCanDisable();
                ScheduleCommit();
            }
        }
    }

    public bool StockEnabled4
    {
        get => _stockEnabled4;
        set
        {
            if (_stockEnabled4 == value)
            {
                return;
            }

            if (!value && !CanDisableStockSlot(3))
            {
                OnPropertyChanged(nameof(StockEnabled4));
                return;
            }

            if (SetProperty(ref _stockEnabled4, value))
            {
                NotifyStockSlotCanDisable();
                ScheduleCommit();
            }
        }
    }

    public bool StockEnabled5
    {
        get => _stockEnabled5;
        set
        {
            if (_stockEnabled5 == value)
            {
                return;
            }

            if (!value && !CanDisableStockSlot(4))
            {
                OnPropertyChanged(nameof(StockEnabled5));
                return;
            }

            if (SetProperty(ref _stockEnabled5, value))
            {
                NotifyStockSlotCanDisable();
                ScheduleCommit();
            }
        }
    }

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

    public IReadOnlyList<ImageTimePlacement> ImageTimePlacements =>
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

    public IReadOnlyList<ImageClockStyle> ImageClockStyles =>
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

    public IReadOnlyList<ImageTextColor> ImageTextColors => Enum.GetValues<ImageTextColor>();

    public ImageTextAlignment ImageTextAlignment
    {
        get => _imageTextAlignment;
        set { if (SetProperty(ref _imageTextAlignment, value)) ScheduleCommit(); }
    }

    public IReadOnlyList<ImageTextAlignment> ImageTextAlignments => Enum.GetValues<ImageTextAlignment>();

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

    public IReadOnlyList<ImageDigitalOrder> ImageDigitalOrders => Enum.GetValues<ImageDigitalOrder>();

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

    public IReadOnlyList<ImageAnalogOrder> ImageAnalogOrders => Enum.GetValues<ImageAnalogOrder>();

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

    public IReadOnlyList<UiThemeMode> UiThemeModes => Enum.GetValues<UiThemeMode>();

    public DotMatrixProgressPeriod DotMatrixProgressPeriod
    {
        get => _dotMatrixProgressPeriod;
        set { if (SetProperty(ref _dotMatrixProgressPeriod, value)) ScheduleCommit(); }
    }

    public IReadOnlyList<DotMatrixProgressPeriod> DotMatrixProgressPeriods =>
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

    public bool IsClaudeTheme => SelectedTheme?.Id == "claude-usage";

    /// <summary>
    /// The claude.ai session cookie. It is written to this machine's settings
    /// file and sent to claude.ai only; the risk notice says so before it is asked for.
    /// </summary>
    public string ClaudeSessionKey
    {
        get => _claudeSessionKey;
        set
        {
            if (SetProperty(ref _claudeSessionKey, value))
            {
                // A different account means the cached organization is wrong.
                _settings.ClaudeUsage.OrganizationId = string.Empty;
                ScheduleCommit();
            }
        }
    }

    public string ClaudeModelScope
    {
        get => _claudeModelScope;
        set { if (SetProperty(ref _claudeModelScope, value)) ScheduleCommit(); }
    }

    public bool ClaudeCountLocalTokens
    {
        get => _claudeCountLocalTokens;
        set { if (SetProperty(ref _claudeCountLocalTokens, value)) ScheduleCommit(); }
    }

    public bool IsCurrencyTheme => SelectedTheme?.Id == "currency";
    public bool IsCryptoTheme => SelectedTheme?.Id == "crypto";
    public bool IsPomodoroTheme => SelectedTheme?.Id == "pomodoro";
    public bool IsHardwareTheme => SelectedTheme?.Id == "hardware";
    public bool IsGitHubTheme => SelectedTheme?.Id == "github";
    public bool IsAlertsTheme => SelectedTheme?.Id == "alerts";

    public string AlertsToken
    {
        get => _alertsToken;
        set { if (SetProperty(ref _alertsToken, value)) ScheduleCommit(); }
    }

    public string AlertsLocation
    {
        get => _alertsLocation;
        set { if (SetProperty(ref _alertsLocation, value)) ScheduleCommit(); }
    }

    public string GitHubUsername
    {
        get => _gitHubUsername;
        set { if (SetProperty(ref _gitHubUsername, value)) ScheduleCommit(); }
    }

    public string GitHubToken
    {
        get => _gitHubToken;
        set { if (SetProperty(ref _gitHubToken, value)) ScheduleCommit(); }
    }

    public int HardwarePageSeconds
    {
        get => _hardwarePageSeconds;
        set
        {
            if (SetProperty(ref _hardwarePageSeconds, Math.Clamp(value, 3, 10)))
            {
                if (_hardwareTheme is not null)
                {
                    _hardwareTheme.PageSeconds = _hardwarePageSeconds;
                }
                ScheduleCommit();
            }
        }
    }

    public string CurrencyBase
    {
        get => _currencyBase;
        set { if (SetProperty(ref _currencyBase, value)) ScheduleCommit(); }
    }

    public string CurrencyQuotes
    {
        get => _currencyQuotes;
        set { if (SetProperty(ref _currencyQuotes, value)) ScheduleCommit(); }
    }

    public string CryptoSymbol1
    {
        get => _cryptoSymbol1;
        set { if (SetProperty(ref _cryptoSymbol1, value)) ScheduleCommit(); }
    }

    public string CryptoSymbol2
    {
        get => _cryptoSymbol2;
        set { if (SetProperty(ref _cryptoSymbol2, value)) ScheduleCommit(); }
    }

    public string CryptoSymbol3
    {
        get => _cryptoSymbol3;
        set { if (SetProperty(ref _cryptoSymbol3, value)) ScheduleCommit(); }
    }

    public string CryptoSymbol4
    {
        get => _cryptoSymbol4;
        set { if (SetProperty(ref _cryptoSymbol4, value)) ScheduleCommit(); }
    }

    public bool CryptoRedForGain
    {
        get => _cryptoRedForGain;
        set
        {
            if (SetProperty(ref _cryptoRedForGain, value))
            {
                OnPropertyChanged(nameof(CryptoColorPreference));
                ScheduleCommit();
            }
        }
    }

    /// <summary>Display-text picker over the stored bool, like <see cref="StockColorPreference"/>.</summary>
    public string CryptoColorPreference
    {
        get => StockColorPreferences[_cryptoRedForGain ? 0 : 1];
        set => CryptoRedForGain = string.Equals(value, StockColorPreferences[0], StringComparison.Ordinal);
    }

    public int PomodoroFocusMinutes
    {
        get => _pomodoroFocusMinutes;
        set { if (SetProperty(ref _pomodoroFocusMinutes, Math.Clamp(value, 1, 180))) ScheduleCommit(); }
    }

    public int PomodoroBreakMinutes
    {
        get => _pomodoroBreakMinutes;
        set { if (SetProperty(ref _pomodoroBreakMinutes, Math.Clamp(value, 1, 60))) ScheduleCommit(); }
    }

    public int PomodoroTargetCycles
    {
        get => _pomodoroTargetCycles;
        set { if (SetProperty(ref _pomodoroTargetCycles, Math.Clamp(value, 1, 12))) ScheduleCommit(); }
    }

    public bool IsPomodoroRunning => _pomodoroTimer.IsRunning;

    public bool CarouselEnabled
    {
        get => _carouselEnabled;
        set { if (SetProperty(ref _carouselEnabled, value)) ScheduleCommit(); }
    }

    public int CarouselIntervalSeconds
    {
        get => _carouselIntervalSeconds;
        set { if (SetProperty(ref _carouselIntervalSeconds, Math.Clamp(value, 10, 600))) ScheduleCommit(); }
    }

    /// <summary>
    /// The refresh interval of the currently selected theme. Reading resolves
    /// override -> built-in default -> the global setting; writing stores a
    /// per-theme override straight into the settings.
    /// </summary>
    public int CurrentThemeRefreshSeconds
    {
        get => ThemeRefreshPolicy.EffectiveSeconds(_settings, SelectedTheme?.Id, RefreshSeconds);
        set
        {
            if (SelectedTheme?.Id is not { } themeId)
            {
                return;
            }

            int clamped = Math.Clamp(value, 1, ThemeRefreshPolicy.MaxSeconds);
            if (clamped == CurrentThemeRefreshSeconds)
            {
                return;
            }

            (_settings.ThemeRefreshOverrides ??= new Dictionary<string, int>())[themeId] = clamped;
            OnPropertyChanged(nameof(CurrentThemeRefreshSeconds));
            ScheduleCommit();
        }
    }

    public bool Use12HourClock
    {
        get => _use12HourClock;
        set
        {
            if (SetProperty(ref _use12HourClock, value))
            {
                DisplayUnits.Use12HourClock = value;
                ScheduleCommit();
            }
        }
    }

    public bool UseFahrenheit
    {
        get => _useFahrenheit;
        set
        {
            if (SetProperty(ref _useFahrenheit, value))
            {
                DisplayUnits.UseFahrenheit = value;
                ScheduleCommit();
            }
        }
    }

    private CarouselSettings BuildCarouselSettings() => new()
    {
        Enabled = CarouselEnabled,
        IntervalSeconds = CarouselIntervalSeconds,
        ThemeIds = CarouselOptions.Where(option => option.Included).Select(option => option.Theme.Id).ToList()
    };

    public void TogglePomodoro()
    {
        if (_pomodoroTimer.IsRunning)
        {
            StopPomodoro();
        }
        else
        {
            StartPomodoro();
        }
    }

    private void StartPomodoro() => _pomodoroTimer.Start(new PomodoroSettings
    {
        FocusMinutes = PomodoroFocusMinutes,
        BreakMinutes = PomodoroBreakMinutes,
        TargetCycles = PomodoroTargetCycles
    });

    private void StopPomodoro() => _pomodoroTimer.Stop();

    private void OnPomodoroChanged(object? sender, EventArgs e) =>
        Dispatcher.UIThread.Post(() =>
        {
            OnPropertyChanged(nameof(IsPomodoroRunning));
            if (!_loading && !_disposed)
            {
                _ = RefreshAndPushAsync(AutoPush, forcePush: true);
            }
        });

    public bool ScheduleEnabled
    {
        get => _scheduleEnabled;
        set { if (SetProperty(ref _scheduleEnabled, value)) ScheduleCommit(); }
    }

    public string ScheduleNightStart
    {
        get => _scheduleNightStart;
        set { if (SetProperty(ref _scheduleNightStart, value)) ScheduleCommit(); }
    }

    public string ScheduleNightEnd
    {
        get => _scheduleNightEnd;
        set { if (SetProperty(ref _scheduleNightEnd, value)) ScheduleCommit(); }
    }

    public bool ScheduleSwitchTheme
    {
        get => _scheduleSwitchTheme;
        set { if (SetProperty(ref _scheduleSwitchTheme, value)) ScheduleCommit(); }
    }

    public string ScheduleNightThemeId
    {
        get => _scheduleNightThemeId;
        set { if (SetProperty(ref _scheduleNightThemeId, value)) ScheduleCommit(); }
    }

    public bool ScheduleDimAtNight
    {
        get => _scheduleDimAtNight;
        set { if (SetProperty(ref _scheduleDimAtNight, value)) ScheduleCommit(); }
    }

    public int ScheduleNightBrightness
    {
        get => _scheduleNightBrightness;
        set { if (SetProperty(ref _scheduleNightBrightness, Math.Clamp(value, 20, 100))) ScheduleCommit(); }
    }

    public string TelegramApiId
    {
        get => _telegramApiId;
        set { if (SetProperty(ref _telegramApiId, value)) ScheduleCommit(); }
    }

    public string TelegramApiHash
    {
        get => _telegramApiHash;
        set { if (SetProperty(ref _telegramApiHash, value)) ScheduleCommit(); }
    }

    public string TelegramPhone
    {
        get => _telegramPhone;
        set { if (SetProperty(ref _telegramPhone, value)) ScheduleCommit(); }
    }

    public string TelegramCode
    {
        get => _telegramCode;
        set => SetProperty(ref _telegramCode, value);
    }

    public string TelegramPassword
    {
        get => _telegramPassword;
        set => SetProperty(ref _telegramPassword, value);
    }

    public bool TelegramPopupsEnabled
    {
        get => _telegramPopupsEnabled;
        set { if (SetProperty(ref _telegramPopupsEnabled, value)) ScheduleCommit(); }
    }

    public int TelegramPopupSeconds
    {
        get => _telegramPopupSeconds;
        set { if (SetProperty(ref _telegramPopupSeconds, Math.Clamp(value, 3, 15))) ScheduleCommit(); }
    }

    public TelegramPrivacyMode TelegramPrivacyMode
    {
        get => _telegramPrivacyMode;
        set
        {
            if (SetProperty(ref _telegramPrivacyMode, value))
            {
                OnPropertyChanged(nameof(TelegramPrivacyPreference));
                ScheduleCommit();
            }
        }
    }

    /// <summary>Display-text picker over the stored enum, like <see cref="StockSourcePreference"/>.</summary>
    public string TelegramPrivacyPreference
    {
        get => TelegramPrivacyOptions[Math.Clamp((int)_telegramPrivacyMode, 0, TelegramPrivacyOptions.Count - 1)];
        set
        {
            IReadOnlyList<string> options = TelegramPrivacyOptions;
            for (int index = 0; index < options.Count; index++)
            {
                if (string.Equals(options[index], value, StringComparison.Ordinal))
                {
                    TelegramPrivacyMode = (TelegramPrivacyMode)index;
                    return;
                }
            }
        }
    }

    public IReadOnlyList<string> TelegramPrivacyOptions =>
        [Loc.T("TelegramPrivacyCounter"), Loc.T("TelegramPrivacySender"), Loc.T("TelegramPrivacySenderPreview")];

    public bool IsTelegramCodeNeeded => _telegramService?.State == TelegramState.WaitingForCode;
    public bool IsTelegramPasswordNeeded => _telegramService?.State == TelegramState.WaitingForPassword;
    public bool IsTelegramConnected => _telegramService?.State == TelegramState.Connected;
    public bool CanTelegramConnect => _telegramService?.State
        is null or TelegramState.Disconnected or TelegramState.Failed;

    public string TelegramStatusText => _telegramService?.State switch
    {
        TelegramState.Connecting => Loc.T("TelegramStateConnecting"),
        TelegramState.WaitingForCode => Loc.T("TelegramStateWaitingCode"),
        TelegramState.WaitingForPassword => Loc.T("TelegramStateWaitingPassword"),
        TelegramState.Connected => Loc.T("TelegramStateConnected"),
        TelegramState.Failed => Loc.T("TelegramStateFailed", _telegramService?.LastError ?? "?"),
        _ => Loc.T("TelegramStateDisconnected")
    };

    private TelegramSettings BuildTelegramSettings() => new()
    {
        ApiId = TelegramApiId.Trim(),
        ApiHash = TelegramApiHash.Trim(),
        PhoneNumber = TelegramPhone.Trim(),
        PopupsEnabled = TelegramPopupsEnabled,
        PrivacyMode = TelegramPrivacyMode,
        PopupSeconds = TelegramPopupSeconds
    };

    private async Task ConnectTelegramAsync()
    {
        if (_telegramService is null)
        {
            return;
        }

        if (!_settings.HasAcknowledgedTelegramNotice)
        {
            if (ShowTelegramNoticeAsync is not null)
            {
                await ShowTelegramNoticeAsync();
            }

            _settings.HasAcknowledgedTelegramNotice = true;
        }

        _settings.Telegram = BuildTelegramSettings();
        await _settingsStore.SaveAsync(_settings);
        if (!_settings.Telegram.IsConfigured)
        {
            return;
        }

        _telegramService.Connect(_settings.Telegram);
    }

    private void SubmitTelegramCode()
    {
        if (!string.IsNullOrWhiteSpace(TelegramCode))
        {
            _telegramService?.SubmitCode(TelegramCode);
            TelegramCode = string.Empty;
        }
    }

    private void SubmitTelegramPassword()
    {
        if (!string.IsNullOrWhiteSpace(TelegramPassword))
        {
            _telegramService?.SubmitPassword(TelegramPassword);
            TelegramPassword = string.Empty;
        }
    }

    private async Task TelegramLogoutAsync()
    {
        if (_telegramService is not null)
        {
            await _telegramService.LogoutAsync();
        }
    }

    private void OnTelegramStateChanged(object? sender, EventArgs e) =>
        Dispatcher.UIThread.Post(RaiseTelegramStateProperties);

    private void RaiseTelegramStateProperties()
    {
        OnPropertyChanged(nameof(TelegramStatusText));
        OnPropertyChanged(nameof(IsTelegramCodeNeeded));
        OnPropertyChanged(nameof(IsTelegramPasswordNeeded));
        OnPropertyChanged(nameof(IsTelegramConnected));
        OnPropertyChanged(nameof(CanTelegramConnect));
    }

    private void OnTelegramMessage(object? sender, TelegramMessageEvent message) =>
        Dispatcher.UIThread.Post(() =>
        {
            if (_disposed || !TelegramPopupsEnabled)
            {
                return;
            }

            DateTimeOffset now = DateTimeOffset.Now;
            if (_telegramPopup is not null && now < _telegramPopupUntil)
            {
                _telegramPopupExtra++;
                _telegramPopup = message;
            }
            else
            {
                _telegramPopup = message;
                _telegramPopupExtra = 0;
            }

            _telegramPopupUntil = now.AddSeconds(TelegramPopupSeconds);
            _ = ShowTelegramPopupAsync();
        });

    /// <summary>Pushes the popup immediately, then reverts once its time is up.</summary>
    private async Task ShowTelegramPopupAsync()
    {
        _telegramPopupDelay?.Cancel();
        _telegramPopupDelay?.Dispose();
        _telegramPopupDelay = new CancellationTokenSource();
        CancellationToken token = _telegramPopupDelay.Token;

        await RefreshAndPushAsync(AutoPush, forcePush: true, cancellationToken: CancellationToken.None);
        try
        {
            TimeSpan remaining = _telegramPopupUntil - DateTimeOffset.Now;
            if (remaining > TimeSpan.Zero)
            {
                await Task.Delay(remaining, token);
            }
        }
        catch (OperationCanceledException)
        {
            return;
        }

        _telegramPopup = null;
        _telegramPopupExtra = 0;
        await RefreshAndPushAsync(AutoPush, forcePush: true, cancellationToken: CancellationToken.None);
    }

    public bool NotifyEnabled
    {
        get => _notifyEnabled;
        set { if (SetProperty(ref _notifyEnabled, value)) ScheduleCommit(); }
    }

    public bool NotifyClaude
    {
        get => _notifyClaude;
        set { if (SetProperty(ref _notifyClaude, value)) ScheduleCommit(); }
    }

    public bool NotifyOffline
    {
        get => _notifyOffline;
        set { if (SetProperty(ref _notifyOffline, value)) ScheduleCommit(); }
    }

    public string NotifySymbol1 { get => _notifySymbol1; set { if (SetProperty(ref _notifySymbol1, value)) ScheduleCommit(); } }
    public string NotifyAbove1 { get => _notifyAbove1; set { if (SetProperty(ref _notifyAbove1, value)) ScheduleCommit(); } }
    public string NotifyBelow1 { get => _notifyBelow1; set { if (SetProperty(ref _notifyBelow1, value)) ScheduleCommit(); } }
    public string NotifySymbol2 { get => _notifySymbol2; set { if (SetProperty(ref _notifySymbol2, value)) ScheduleCommit(); } }
    public string NotifyAbove2 { get => _notifyAbove2; set { if (SetProperty(ref _notifyAbove2, value)) ScheduleCommit(); } }
    public string NotifyBelow2 { get => _notifyBelow2; set { if (SetProperty(ref _notifyBelow2, value)) ScheduleCommit(); } }
    public string NotifySymbol3 { get => _notifySymbol3; set { if (SetProperty(ref _notifySymbol3, value)) ScheduleCommit(); } }
    public string NotifyAbove3 { get => _notifyAbove3; set { if (SetProperty(ref _notifyAbove3, value)) ScheduleCommit(); } }
    public string NotifyBelow3 { get => _notifyBelow3; set { if (SetProperty(ref _notifyBelow3, value)) ScheduleCommit(); } }

    private NotificationSettings BuildNotificationSettings() => new()
    {
        Enabled = NotifyEnabled,
        ClaudeThresholds = NotifyClaude,
        DeviceOffline = NotifyOffline,
        PriceAlerts =
        [
            BuildPriceAlert(NotifySymbol1, NotifyAbove1, NotifyBelow1),
            BuildPriceAlert(NotifySymbol2, NotifyAbove2, NotifyBelow2),
            BuildPriceAlert(NotifySymbol3, NotifyAbove3, NotifyBelow3)
        ]
    };

    private static PriceAlertSettings BuildPriceAlert(string symbol, string above, string below) => new()
    {
        Symbol = symbol.Trim(),
        Above = ParseBound(above),
        Below = ParseBound(below)
    };

    private static double? ParseBound(string value) =>
        double.TryParse(value.Trim().Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out double parsed) && parsed > 0
            ? parsed
            : null;

    private static string FormatBound(double? value) =>
        value is { } bound ? bound.ToString(CultureInfo.InvariantCulture) : string.Empty;

    /// <summary>
    /// Runs at most twice a minute on the refresh tick; the sources behind it
    /// hold their own caches, and the engine holds the cooldowns.
    /// </summary>
    private async Task SweepNotificationsAsync(CancellationToken cancellationToken)
    {
        NotificationSettings settings = _settings.Notifications ?? new NotificationSettings();
        DateTimeOffset now = DateTimeOffset.Now;
        if (!settings.Enabled || now - _lastNotificationSweep < TimeSpan.FromSeconds(30))
        {
            return;
        }

        _lastNotificationSweep = now;
        try
        {
            ClaudeUsageSnapshot? claude = null;
            if (settings.ClaudeThresholds && (_settings.ClaudeUsage?.IsConfigured ?? false))
            {
                claude = await _claudeUsageSource.ReadAsync(_settings.ClaudeUsage!, cancellationToken);
                _claudeUsage = claude;
            }

            IReadOnlyDictionary<string, double>? prices = null;
            if (settings.HasConfiguredAlerts)
            {
                CryptoSnapshot alertPrices = await _binanceStockSource.ReadCryptoAsync(
                    new CryptoSettings
                    {
                        Items = settings.PriceAlerts
                            .Where(alert => alert.IsConfigured)
                            .Select(alert => new StockItemSettings { Symbol = alert.Symbol })
                            .ToList()
                    },
                    cancellationToken);
                prices = alertPrices.Coins.ToDictionary(coin => coin.Symbol, coin => coin.Price, StringComparer.Ordinal);
            }

            bool? pushFailed = PushDiagnostics.Latest is { } lastPush ? !lastPush.Success : null;
            foreach (AppNotification notification in _notificationEngine.Evaluate(
                settings, new NotificationInputs(claude, pushFailed, prices), now))
            {
                _toastNotifier.Show(notification.Title, notification.Message);
            }
        }
        catch (Exception)
        {
            // A failed sweep tries again on the next tick; alerts must never
            // break the render loop.
        }
    }

    private string _portStatusText = string.Empty;

    public string PortStatusText
    {
        get => _portStatusText;
        set => SetProperty(ref _portStatusText, value);
    }

    private async Task ExportSettingsAsync()
    {
        if (PickExportPathAsync is null)
        {
            return;
        }

        try
        {
            string? path = await PickExportPathAsync();
            if (string.IsNullOrWhiteSpace(path))
            {
                return;
            }

            // Persist the current UI state first so the export matches what is on screen.
            await SaveSettingsAsync(CancellationToken.None);
            await File.WriteAllTextAsync(path, SettingsPorter.ExportJson(_settings));
            PortStatusText = Loc.T("PortExportedTo", path);
        }
        catch (Exception ex)
        {
            PortStatusText = Loc.T("PortFailed", ex.Message);
        }
    }

    private async Task ImportSettingsAsync()
    {
        if (PickImportPathAsync is null)
        {
            return;
        }

        try
        {
            string? path = await PickImportPathAsync();
            if (string.IsNullOrWhiteSpace(path))
            {
                return;
            }

            string json = await File.ReadAllTextAsync(path);
            _settings = SettingsPorter.ImportJson(json, _settings);
            ApplySettings();
            await _settingsStore.SaveAsync(_settings);
            PortStatusText = Loc.T("PortImportDone");
            await RefreshAndPushAsync(AutoPush, forcePush: true);
        }
        catch (Exception ex)
        {
            PortStatusText = Loc.T("PortFailed", ex.Message);
        }
    }

    private (string Title, string? Preview) FormatTelegramPopup(TelegramMessageEvent message)
    {
        int total = 1 + _telegramPopupExtra;
        return TelegramPrivacyMode switch
        {
            TelegramPrivacyMode.Counter => (Loc.T("ScreenTgNewMessages", total), null),
            TelegramPrivacyMode.Sender => (WithExtra(message.SenderName), null),
            _ => (WithExtra(message.SenderName), TelegramPopupOverlay.TruncatePreview(message.Preview))
        };

        string WithExtra(string name) => _telegramPopupExtra > 0 ? $"{name} +{_telegramPopupExtra}" : name;
    }
    public bool IsMusicTheme => SelectedTheme is not null &&
        MediaThemeAutomation.IsMusicThemeId(SelectedTheme.Id);
    public bool IsSystemTheme => SelectedTheme?.Id is
        "system" or "dashboard" or "performance" or "network" or "system-minimal";

    public bool NeedsCurrentThemeNotice =>
        SelectedTheme?.Id switch
        {
            "stocks" => !_settings.HasAcknowledgedStockNotice,
            "ai-quota" => !_settings.HasAcknowledgedAiUsageNotice,
            "claude-usage" => !_settings.HasAcknowledgedClaudeNotice,
            "github" => !_settings.HasAcknowledgedGitHubNotice,
            "alerts" => !_settings.HasAcknowledgedAlertsNotice,
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
            case "claude-usage":
                _settings.HasAcknowledgedClaudeNotice = true;
                break;
            case "github":
                _settings.HasAcknowledgedGitHubNotice = true;
                break;
            case "alerts":
                _settings.HasAcknowledgedAlertsNotice = true;
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
        _themes = BuiltInThemes.Create(_imageTheme, _pomodoroTimer);
        _perfVisualTheme = (PerformanceVisualTheme?)_themes.FirstOrDefault(theme => theme.Id == "performance-visual");
        _hardwareTheme = (HardwareMonitorTheme?)_themes.FirstOrDefault(theme => theme.Id == "hardware");
        _telegramService = new TelegramService(
            Path.GetDirectoryName(_settingsStore.Path) is { Length: > 0 } settingsDirectory
                ? settingsDirectory
                : AppContext.BaseDirectory);
        _telegramService.StateChanged += OnTelegramStateChanged;
        _telegramService.MessageReceived += OnTelegramMessage;
        BuildThemeGroups();
        ReloadFonts();
        ApplySettings();
        _loading = true;
        await RefreshTokscaleAsync(force: false);

        _loading = false;
        _lifetime = new CancellationTokenSource();
        _ = RunRefreshLoopAsync(_lifetime.Token);
        // A stored session means the user already logged in - reconnect quietly.
        if (_settings.Telegram is { IsConfigured: true } && _telegramService is { HasStoredSession: true })
        {
            _telegramService.Connect(_settings.Telegram);
        }
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

    private async Task CheckForUpdatesAsync()
    {
        UpdateStatusText = Loc.T("UpdateChecking");
        IsUpdateAvailable = false;
        if (!Version.TryParse(AppVersion, out Version? currentVersion))
        {
            currentVersion = new Version(1, 0, 0);
        }

        UpdateCheckResult result = await new ReleaseUpdateChecker().CheckAsync(currentVersion);
        if (result.Error is not null)
        {
            UpdateStatusText = Loc.T("UpdateCheckFailed", result.Error);
            return;
        }

        if (result.Available)
        {
            LatestReleaseUrl = result.HtmlUrl;
            IsUpdateAvailable = true;
            UpdateStatusText = Loc.T("UpdateAvailable", result.TagName);
        }
        else
        {
            UpdateStatusText = Loc.T("UpdateUpToDate");
        }
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
        Loc.Instance.LanguageChanged -= OnLanguageChanged;
        _fontCatalog.Dispose();
        (_systemSource as IDisposable)?.Dispose();
        _transport.Dispose();
        _weatherSource.Dispose();
        _yahooStockSource.Dispose();
        _tencentStockSource.Dispose();
        _binanceStockSource.Dispose();
        _claudeUsageSource.Dispose();
        _currencySource.Dispose();
        _hardwareSource.Dispose();
        _gitHubSource.Dispose();
        _airAlertSource.Dispose();
        _telegramPopupDelay?.Cancel();
        _telegramPopupDelay?.Dispose();
        if (_telegramService is not null)
        {
            _telegramService.StateChanged -= OnTelegramStateChanged;
            _telegramService.MessageReceived -= OnTelegramMessage;
            _telegramService.Dispose();
        }
        _pomodoroTimer.Changed -= OnPomodoroChanged;
        _desktopServices.Dispose();
        _refreshLock.Dispose();
        _lifetime?.Dispose();
        _commitDelay?.Dispose();
        PreviewImage?.Dispose();
        await Task.CompletedTask;
    }

    private void ApplySelectedThemeText(ThemeItemViewModel theme)
    {
        CurrentThemeName = theme.Name;
        CurrentThemeDescription = theme.Id == "image" && !string.IsNullOrWhiteSpace(_imageTheme.ImagePath)
            ? _imageTheme.ImagePath
            : theme.Description;
        CurrentThemeDetails = theme.Details;
    }

    /// <summary>
    /// Re-reads every piece of derived text after the language changed. Bindings
    /// on this view model refresh from the blanket notification; the item view
    /// models and the rendered preview have to be told individually.
    /// </summary>
    private void OnLanguageChanged(object? sender, EventArgs e) =>
        Dispatcher.UIThread.Post(() =>
        {
            if (_disposed)
            {
                return;
            }

            OnPropertyChanged(null);

            foreach (ThemeGroupViewModel group in ThemeGroups)
            {
                group.RaiseLocalizedTextChanged();
            }

            foreach (AiUsageModeOption mode in AiUsageModes)
            {
                mode.RaiseLocalizedTextChanged();
            }

            if (SelectedTheme is { } selected)
            {
                ApplySelectedThemeText(selected);
            }

            UpdateTokscaleStatusTitle();
            RebuildTokscaleOptions();
            ReloadFonts();
            _ = RefreshAndPushAsync(shouldPush: false, forcePush: false);
        });

    private void UpdateTokscaleStatusTitle() =>
        TokscaleStatusTitle = _tokscaleStatus switch
        {
            TokscaleStatus.Ready => Loc.T("TokscaleStatusReady"),
            TokscaleStatus.NotInstalled => Loc.T("TokscaleStatusNotInstalled"),
            TokscaleStatus.NoData => Loc.T("TokscaleStatusNoData"),
            _ => Loc.T("TokscaleStatusError")
        };

    private void BuildThemeGroups()
    {
        ThemeGroups.Clear();
        var byId = _themes.ToDictionary(theme => theme.Id, StringComparer.OrdinalIgnoreCase);
        AddGroup("ThemeGroupMonitoring", ["system", "hardware", "dashboard", "performance", "network", "system-minimal", "performance-visual"], byId);
        AddGroup("ThemeGroupTime", ["clock", "clock-neon", "clock-flip", "pomodoro", "image"], byId);
        AddGroup("ThemeGroupInfo", ["weather-five-day", "stocks", "currency", "crypto", "github", "alerts", "ai-quota", "claude-usage"], byId);
        AddGroup("ThemeGroupMusic", ["music", "music-minimal", "music-poster"], byId);
        AddGroup("ThemeGroupDotMatrix", ["clock-dot-matrix", "clock-weather-dot", "clock-dot-analog", "clock-dot-progress"], byId);

        IdleThemeOptions.Clear();
        MusicThemeOptions.Clear();
        CarouselOptions.Clear();
        foreach (ThemeItemViewModel item in ThemeGroups.SelectMany(group => group.Themes))
        {
            (MediaThemeAutomation.IsMusicThemeId(item.Id) ? MusicThemeOptions : IdleThemeOptions).Add(item);
            CarouselOptions.Add(new CarouselOptionViewModel(item, ScheduleCommit));
        }
    }

    private void AddGroup(
        string nameKey,
        IEnumerable<string> ids,
        IReadOnlyDictionary<string, IScreenTheme> byId)
    {
        var items = ids
            .Where(byId.ContainsKey)
            .Select(id => new ThemeItemViewModel(byId[id]))
            .ToArray();
        ThemeGroups.Add(new ThemeGroupViewModel(nameKey, items));
    }

    private void ApplySettings()
    {
        _loading = true;
        DeviceIp = ExtractDeviceIp(_settings.DeviceEndpoint);
        AccentColor = string.IsNullOrWhiteSpace(_settings.AccentColor) ? "#E4694C" : _settings.AccentColor;
        SelectedFontId = _settings.SelectedFontId;
        SelectedLanguage = AppLanguageInfo.For(
            string.IsNullOrWhiteSpace(_settings.Language)
                ? Loc.Language
                : AppLanguageInfo.Parse(_settings.Language));
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
            ? WeatherSettings.DefaultLocationQuery
            : _settings.Weather.LocationQuery;
        IReadOnlyList<StockItemSettings> stockItems = NormalizeStockItems(_settings.Stocks ?? new StockSettings());
        StockSymbol1 = stockItems[0].Symbol;
        StockAlias1 = stockItems[0].Alias;
        StockSymbol2 = stockItems[1].Symbol;
        StockAlias2 = stockItems[1].Alias;
        StockSymbol3 = stockItems[2].Symbol;
        StockAlias3 = stockItems[2].Alias;
        StockSymbol4 = stockItems[3].Symbol;
        StockAlias4 = stockItems[3].Alias;
        StockSymbol5 = stockItems[4].Symbol;
        StockAlias5 = stockItems[4].Alias;
        _settings.ClaudeUsage ??= new ClaudeUsageSettings();
        ClaudeSessionKey = _settings.ClaudeUsage.SessionKey;
        ClaudeModelScope = string.IsNullOrWhiteSpace(_settings.ClaudeUsage.ModelScope)
            ? ClaudeUsageSettings.DefaultModelScope
            : _settings.ClaudeUsage.ModelScope;
        ClaudeCountLocalTokens = _settings.ClaudeUsage.CountLocalTokens;
        _settings.Currency ??= new CurrencySettings();
        CurrencyBase = string.IsNullOrWhiteSpace(_settings.Currency.BaseCurrency)
            ? "USD"
            : _settings.Currency.BaseCurrency;
        CurrencyQuotes = string.Join(", ", _settings.Currency.QuoteCurrencies ?? []);
        _settings.Crypto ??= new CryptoSettings();
        IReadOnlyList<StockItemSettings> cryptoItems = NormalizeCryptoItems(_settings.Crypto);
        CryptoSymbol1 = cryptoItems[0].Symbol;
        CryptoSymbol2 = cryptoItems[1].Symbol;
        CryptoSymbol3 = cryptoItems[2].Symbol;
        CryptoSymbol4 = cryptoItems[3].Symbol;
        CryptoRedForGain = _settings.Crypto.RedForGain;
        _settings.Pomodoro ??= new PomodoroSettings();
        PomodoroFocusMinutes = _settings.Pomodoro.FocusMinutes;
        PomodoroBreakMinutes = _settings.Pomodoro.BreakMinutes;
        PomodoroTargetCycles = _settings.Pomodoro.TargetCycles;
        _settings.Schedule ??= new ThemeScheduleSettings();
        ScheduleEnabled = _settings.Schedule.Enabled;
        ScheduleNightStart = FormatScheduleTime(_settings.Schedule.NightStart, "22:00");
        ScheduleNightEnd = FormatScheduleTime(_settings.Schedule.NightEnd, "07:00");
        ScheduleSwitchTheme = !string.IsNullOrWhiteSpace(_settings.Schedule.NightThemeId);
        ScheduleNightThemeId = string.IsNullOrWhiteSpace(_settings.Schedule.NightThemeId)
            ? "clock"
            : _settings.Schedule.NightThemeId;
        ScheduleDimAtNight = _settings.Schedule.DimAtNight;
        ScheduleNightBrightness = _settings.Schedule.NightBrightnessPercent;
        _settings.HardwareMonitor ??= new HardwareMonitorSettings();
        HardwarePageSeconds = _settings.HardwareMonitor.PageSeconds;
        if (_hardwareTheme is not null)
        {
            _hardwareTheme.PageSeconds = HardwarePageSeconds;
        }
        _settings.GitHub ??= new GitHubSettings();
        GitHubUsername = _settings.GitHub.Username;
        GitHubToken = _settings.GitHub.Token;
        _settings.AirAlerts ??= new AirAlertSettings();
        AlertsToken = _settings.AirAlerts.Token;
        AlertsLocation = _settings.AirAlerts.Location;
        _settings.Telegram ??= new TelegramSettings();
        TelegramApiId = _settings.Telegram.ApiId;
        TelegramApiHash = _settings.Telegram.ApiHash;
        TelegramPhone = _settings.Telegram.PhoneNumber;
        TelegramPopupsEnabled = _settings.Telegram.PopupsEnabled;
        TelegramPrivacyMode = _settings.Telegram.PrivacyMode;
        TelegramPopupSeconds = _settings.Telegram.PopupSeconds;
        Use12HourClock = _settings.Use12HourClock;
        UseFahrenheit = _settings.UseFahrenheit;
        _settings.Carousel ??= new CarouselSettings();
        CarouselEnabled = _settings.Carousel.Enabled;
        CarouselIntervalSeconds = _settings.Carousel.IntervalSeconds;
        var carouselIds = new HashSet<string>(_settings.Carousel.ThemeIds ?? [], StringComparer.OrdinalIgnoreCase);
        foreach (CarouselOptionViewModel option in CarouselOptions)
        {
            option.Included = carouselIds.Contains(option.Theme.Id);
        }
        _settings.Notifications ??= new NotificationSettings();
        NotifyEnabled = _settings.Notifications.Enabled;
        NotifyClaude = _settings.Notifications.ClaudeThresholds;
        NotifyOffline = _settings.Notifications.DeviceOffline;
        var alerts = (_settings.Notifications.PriceAlerts ?? []).Take(3).ToList();
        while (alerts.Count < 3)
        {
            alerts.Add(new PriceAlertSettings());
        }
        NotifySymbol1 = alerts[0].Symbol;
        NotifyAbove1 = FormatBound(alerts[0].Above);
        NotifyBelow1 = FormatBound(alerts[0].Below);
        NotifySymbol2 = alerts[1].Symbol;
        NotifyAbove2 = FormatBound(alerts[1].Above);
        NotifyBelow2 = FormatBound(alerts[1].Below);
        NotifySymbol3 = alerts[2].Symbol;
        NotifyAbove3 = FormatBound(alerts[2].Above);
        NotifyBelow3 = FormatBound(alerts[2].Below);
        StockRedForGain = _settings.Stocks?.RedForGain ?? true;
        StockSource = _settings.Stocks?.SourceKind ?? StockSourceKind.Tencent;
        StockEnabled1 = stockItems[0].Enabled;
        StockEnabled2 = stockItems[1].Enabled;
        StockEnabled3 = stockItems[2].Enabled;
        StockEnabled4 = stockItems[3].Enabled;
        StockEnabled5 = stockItems[4].Enabled;
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
        PerfVisualCpuEnabled = _settings.PerfVisualCpuEnabled;
        PerfVisualMemoryEnabled = _settings.PerfVisualMemoryEnabled;
        PerfVisualDownloadEnabled = _settings.PerfVisualDownloadEnabled;
        PerfVisualUploadEnabled = _settings.PerfVisualUploadEnabled;
        PerfVisualGpuEnabled = _settings.PerfVisualGpuEnabled;
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
        OnPropertyChanged(nameof(IsClaudeTheme));
        OnPropertyChanged(nameof(IsCurrencyTheme));
        OnPropertyChanged(nameof(IsCryptoTheme));
        OnPropertyChanged(nameof(IsPomodoroTheme));
        OnPropertyChanged(nameof(IsHardwareTheme));
        OnPropertyChanged(nameof(IsGitHubTheme));
        OnPropertyChanged(nameof(IsAlertsTheme));
        OnPropertyChanged(nameof(CurrentThemeRefreshSeconds));
        OnPropertyChanged(nameof(IsPerformanceVisualTheme));
        OnPropertyChanged(nameof(IsMusicTheme));
        OnPropertyChanged(nameof(IsSystemTheme));
    }

    private async Task RefreshTokscaleAsync(bool force)
    {
        TokscaleCatalog catalog = await _tokscaleSource.ReadCatalogAsync(force);
        _tokscaleCatalog = catalog;
        _tokscaleStatus = catalog.Status;
        UpdateTokscaleStatusTitle();
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
                ? Loc.T("TokscaleNoSubscriptionData")
                : Loc.T("TokscaleNoModelData");
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
            await RefreshAndPushAsync(shouldPush: true, forcePush: true, cancellationToken: cancellationToken);
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
        _settings.Language = SelectedLanguage.Id;
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
        _settings.PerfVisualCpuEnabled = PerfVisualCpuEnabled;
        _settings.PerfVisualMemoryEnabled = PerfVisualMemoryEnabled;
        _settings.PerfVisualDownloadEnabled = PerfVisualDownloadEnabled;
        _settings.PerfVisualUploadEnabled = PerfVisualUploadEnabled;
        _settings.PerfVisualGpuEnabled = PerfVisualGpuEnabled;
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
        _settings.Weather.LocationQuery = string.IsNullOrWhiteSpace(WeatherLocation) ? WeatherSettings.DefaultLocationQuery : WeatherLocation.Trim();
        _settings.ClaudeUsage ??= new ClaudeUsageSettings();
        _settings.ClaudeUsage.SessionKey = ClaudeSessionKey.Trim();
        _settings.ClaudeUsage.ModelScope = string.IsNullOrWhiteSpace(ClaudeModelScope)
            ? ClaudeUsageSettings.DefaultModelScope
            : ClaudeModelScope.Trim();
        _settings.ClaudeUsage.CountLocalTokens = ClaudeCountLocalTokens;
        _settings.Stocks = new StockSettings
        {
            SourceKind = StockSource,
            RedForGain = StockRedForGain,
            Items =
            [
                new StockItemSettings { Enabled = StockEnabled1, Symbol = StockSymbol1.Trim(), Alias = StockAlias1.Trim() },
                new StockItemSettings { Enabled = StockEnabled2, Symbol = StockSymbol2.Trim(), Alias = StockAlias2.Trim() },
                new StockItemSettings { Enabled = StockEnabled3, Symbol = StockSymbol3.Trim(), Alias = StockAlias3.Trim() },
                new StockItemSettings { Enabled = StockEnabled4, Symbol = StockSymbol4.Trim(), Alias = StockAlias4.Trim() },
                new StockItemSettings { Enabled = StockEnabled5, Symbol = StockSymbol5.Trim(), Alias = StockAlias5.Trim() }
            ]
        };
        _settings.Currency = new CurrencySettings
        {
            BaseCurrency = string.IsNullOrWhiteSpace(CurrencyBase) ? "USD" : CurrencyBase.Trim().ToUpperInvariant(),
            QuoteCurrencies = SplitCurrencyCodes(CurrencyQuotes)
        };
        _settings.Crypto = new CryptoSettings
        {
            RedForGain = CryptoRedForGain,
            Items =
            [
                new StockItemSettings { Symbol = CryptoSymbol1.Trim() },
                new StockItemSettings { Symbol = CryptoSymbol2.Trim() },
                new StockItemSettings { Symbol = CryptoSymbol3.Trim() },
                new StockItemSettings { Symbol = CryptoSymbol4.Trim() }
            ]
        };
        _settings.Pomodoro = new PomodoroSettings
        {
            FocusMinutes = PomodoroFocusMinutes,
            BreakMinutes = PomodoroBreakMinutes,
            TargetCycles = PomodoroTargetCycles
        };
        _settings.Schedule = new ThemeScheduleSettings
        {
            Enabled = ScheduleEnabled,
            NightStart = FormatScheduleTime(ScheduleNightStart, "22:00"),
            NightEnd = FormatScheduleTime(ScheduleNightEnd, "07:00"),
            NightThemeId = ScheduleSwitchTheme ? ScheduleNightThemeId : string.Empty,
            DimAtNight = ScheduleDimAtNight,
            NightBrightnessPercent = ScheduleNightBrightness
        };
        _settings.HardwareMonitor = new HardwareMonitorSettings { PageSeconds = HardwarePageSeconds };
        _settings.GitHub = new GitHubSettings
        {
            Username = GitHubUsername.Trim().TrimStart('@'),
            Token = GitHubToken.Trim()
        };
        _settings.AirAlerts = new AirAlertSettings
        {
            Token = AlertsToken.Trim(),
            Location = AlertsLocation.Trim()
        };
        _settings.Telegram = BuildTelegramSettings();
        _settings.Notifications = BuildNotificationSettings();
        _settings.Carousel = BuildCarouselSettings();
        _settings.Use12HourClock = Use12HourClock;
        _settings.UseFahrenheit = UseFahrenheit;
        UpdateRenderer();
        await _settingsStore.SaveAsync(_settings, cancellationToken);
    }

    private async Task RunRefreshLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                int themeSeconds = ThemeRefreshPolicy.EffectiveSeconds(
                    _settings,
                    _lastEffectiveThemeId ?? SelectedTheme?.Id,
                    RefreshSeconds);
                // Media automation watches for playback outside the theme, so
                // it keeps the poll fast even under a slow data theme.
                int? pollCap = AutoMediaThemeSwitch || AutoSwitchToMusic ? RefreshSeconds : null;
                await Task.Delay(
                    ThemeRefreshPolicy.NextDelay(DateTimeOffset.Now, themeSeconds, _settings.Carousel, pollCap),
                    cancellationToken);
                bool staticImage = SelectedTheme?.Id == "image" && !ImageWeatherVisible;
                if (!staticImage || AutoMediaThemeSwitch || _settings.Schedule?.Enabled == true ||
                    _settings.Carousel?.Enabled == true)
                {
                    await RefreshAndPushAsync(AutoPush, forcePush: false, cancellationToken: cancellationToken);
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private async Task RefreshAndPushAsync(
        bool shouldPush,
        bool forcePush = false,
        CancellationToken cancellationToken = default)
    {
        await RefreshPreviewAsync(cancellationToken);
        if (shouldPush)
        {
            await PushLatestAsync(cancellationToken, forcePush);
        }

        await SweepNotificationsAsync(cancellationToken);
    }

    private IStockSnapshotSource GetStockSource() =>
        (_settings.Stocks?.SourceKind ?? StockSourceKind.Tencent) switch
        {
            StockSourceKind.Yahoo => _yahooStockSource,
            StockSourceKind.Binance => _binanceStockSource,
            _ => _tencentStockSource
        };

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
            SystemSnapshot system = await Task.Run(async () => await _systemSource.ReadAsync(cancellationToken));
            _musicSource.IgnoreBrowserSessions = IgnoreBrowserMediaSessions;
            MusicSnapshot music = await _musicSource.ReadAsync(cancellationToken);
            bool mediaPlaying = music.Available && music.IsPlaying;
            DateTimeOffset scheduleNow = DateTimeOffset.Now;
            // Layering, lowest first: carousel substitutes the selected theme,
            // media automation may override it, the night theme wins at idle.
            string carouselId = ThemeCarousel.ResolveThemeId(_settings.Carousel, requestedTheme.Id, scheduleNow);
            string effectiveId = MediaThemeAutomation.ResolveThemeId(
                _settings,
                mediaPlaying,
                carouselId);
            // Media automation outranks the schedule, but only while it is
            // actually showing the playing theme; the night theme applies otherwise.
            if (!(mediaPlaying && (_settings.AutoMediaThemeSwitch || _settings.AutoSwitchToMusic)))
            {
                effectiveId = ThemeSchedule.ResolveThemeId(_settings.Schedule, effectiveId, scheduleNow);
            }
            IScreenTheme theme = _themes.FirstOrDefault(candidate =>
                string.Equals(candidate.Id, effectiveId, StringComparison.OrdinalIgnoreCase))
                ?? requestedTheme;
            _lastEffectiveThemeId = theme.Id;

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
                stocks = await GetStockSource().ReadAsync(
                    _settings.Stocks ?? new StockSettings(),
                    cancellationToken);
            }

            ClaudeUsageSnapshot? claudeUsage = null;
            if (requestedTheme.Id == "claude-usage" || theme.Id == "claude-usage")
            {
                claudeUsage = await _claudeUsageSource.ReadAsync(
                    _settings.ClaudeUsage ?? new ClaudeUsageSettings(),
                    cancellationToken);
                _claudeUsage = claudeUsage;
            }

            CurrencySnapshot? currency = null;
            if (requestedTheme.Id == "currency" || theme.Id == "currency")
            {
                currency = await _currencySource.ReadAsync(
                    _settings.Currency ?? new CurrencySettings(),
                    cancellationToken);
                _currencySnapshot = currency;
            }

            CryptoSnapshot? crypto = null;
            if (requestedTheme.Id == "crypto" || theme.Id == "crypto")
            {
                crypto = await _binanceStockSource.ReadCryptoAsync(
                    _settings.Crypto ?? new CryptoSettings(),
                    cancellationToken);
                _cryptoSnapshot = crypto;
            }

            HardwareSnapshot? hardware = null;
            if (requestedTheme.Id == "hardware" || theme.Id == "hardware")
            {
                hardware = await Task.Run(_hardwareSource.Read, cancellationToken);
                _hardwareSnapshot = hardware;
            }

            GitHubContributionSnapshot? gitHub = null;
            if (requestedTheme.Id == "github" || theme.Id == "github")
            {
                gitHub = await _gitHubSource.ReadAsync(
                    _settings.GitHub ?? new GitHubSettings(),
                    cancellationToken);
                _gitHubSnapshot = gitHub;
            }

            AirAlertSnapshot? airAlerts = null;
            if (requestedTheme.Id == "alerts" || theme.Id == "alerts")
            {
                airAlerts = await _airAlertSource.ReadAsync(
                    _settings.AirAlerts ?? new AirAlertSettings(),
                    cancellationToken);
                _alertsSnapshot = airAlerts;
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
                AiQuota = aiQuota,
                ClaudeUsage = claudeUsage,
                Currency = currency,
                Crypto = crypto,
                Hardware = hardware,
                GitHub = gitHub,
                AirAlerts = airAlerts
            };

            Action<ScreenCanvas>? popupOverlay = null;
            if (_telegramPopup is { } telegramPopup && DateTimeOffset.Now < _telegramPopupUntil)
            {
                (string popupTitle, string? popupPreview) = FormatTelegramPopup(telegramPopup);
                popupOverlay = overlayCanvas =>
                    TelegramPopupOverlay.Draw(overlayCanvas, popupTitle, popupPreview);
            }
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
                    DotMatrixProgressHeaderFontSize),
                ThemeSchedule.BrightnessPercent(_settings.Schedule, scheduleNow),
                popupOverlay);

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
                LastUpdated = Loc.T("LastUpdatedAt", DateTime.Now.ToString("HH:mm:ss"));
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
                    SourceSummary = Loc.T("RefreshFailed", ex.Message);
                }
            });
        }
        finally
        {
            _refreshLock.Release();
        }
    }

    private async Task PushLatestAsync(CancellationToken cancellationToken, bool forcePush = false)
    {
        if (_latestFrame is null || !TryCreateEndpoint(DeviceIp, out Uri? endpoint))
        {
            SetDeviceStatus(false);
            return;
        }

        if (!forcePush &&
            _lastPushedJpeg is not null &&
            _latestFrame.JpegBytes.AsSpan().SequenceEqual(_lastPushedJpeg))
        {
            return;
        }

        DevicePushResult result = await _transport.PushAsync(endpoint, _latestFrame, cancellationToken);
        PushDiagnostics.Record(result, _latestFrame.JpegBytes.Length);
        Dispatcher.UIThread.Post(RaiseDiagnosticsProperties);
        _lastPushedJpeg = _latestFrame.JpegBytes;
        SetDeviceStatus(result.Success);
    }

    private void SetDeviceStatus(bool connected) =>
        Dispatcher.UIThread.Post(() =>
        {
            DeviceConnected = connected;
            DeviceStatus = Loc.T(connected ? "StatusOnline" : "StatusDisconnected");
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
        if (!string.IsNullOrWhiteSpace(value))
        {
            string hex = value.Trim().TrimStart('#');
            if (hex.Length == 6
                && int.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int rgb))
            {
                color = WpfColor.FromRgb((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb);
                return true;
            }
            if (hex.Length == 8
                && int.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int argb))
            {
                color = WpfColor.FromArgb((byte)(argb >> 24), (byte)(argb >> 16), (byte)(argb >> 8), (byte)argb);
                return true;
            }
        }

        color = WpfColor.FromRgb(228, 105, 76);
        return false;
    }

    private void UpdateRenderer() =>
        _renderer = new ScreenRenderer(new ScreenProfile(142, 428, 524288, ReadSafeArea()));

    private static IReadOnlyList<StockItemSettings> NormalizeStockItems(StockSettings settings)
    {
        var items = (settings.Items ?? []).Take(5).ToList();
        while (items.Count < 5)
        {
            items.Add(new StockItemSettings());
        }
        return items;
    }

    private static IReadOnlyList<StockItemSettings> NormalizeCryptoItems(CryptoSettings settings)
    {
        var items = (settings.Items ?? []).Take(4).ToList();
        while (items.Count < 4)
        {
            items.Add(new StockItemSettings { Symbol = string.Empty });
        }
        return items;
    }

    private static string FormatScheduleTime(string? value, string fallback) =>
        ThemeSchedule.TryParseTime(value, out TimeSpan time)
            ? $"{(int)time.TotalHours:00}:{time.Minutes:00}"
            : fallback;

    private static List<string> SplitCurrencyCodes(string value) =>
        value.Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(code => code.ToUpperInvariant())
            .Distinct()
            .ToList();

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
                Loc.T("SummarySystem",
                    system.CpuPercent.ToString("0"),
                    system.MemoryPercent.ToString("0"),
                    system.DownloadMbps.ToString("0.0"),
                    system.UploadMbps.ToString("0.0")),
            "music" or "music-minimal" or "music-poster" =>
                music.Available
                    ? Loc.T("SummaryMusic",
                        Loc.T(music.IsPlaying ? "ScreenMusicPlaying" : "ScreenMusicPaused"),
                        music.Title,
                        music.Artist)
                    : Loc.T("SummaryNoMediaSession"),
            "clock-weather-dot" or "weather-five-day" or "image" when weather is { Available: true } =>
                weather is { Available: true }
                    ? Loc.T("SummaryWeather", weather.LocationName, weather.TemperatureC.ToString("0"), weather.ConditionText)
                    : weather?.ErrorMessage ?? Loc.T("SummaryNoWeather"),
            "stocks" =>
                stocks is { Quotes.Count: > 0 }
                    ? Loc.T("SummaryStocks", stocks.Quotes.Count, stocks.UpdatedAt.ToString("HH:mm"))
                    : stocks?.ErrorMessage ?? Loc.T("SummaryConfigureStocks"),
            "claude-usage" => _claudeUsage is { Available: true, Session: { } claudeSession, Week: { } claudeWeek }
                ? Loc.T("SummaryClaude",
                    claudeSession.EffectivePercent.ToString("0"),
                    claudeWeek.EffectivePercent.ToString("0"))
                : _claudeUsage?.ErrorMessage ?? Loc.T("SummaryClaudeNotConfigured"),
            "ai-quota" => aiQuota.Available
                ? $"{aiQuota.PlatformName} · {aiQuota.PrimaryDisplay}"
                : _tokscaleCatalog?.Message ?? Loc.T("SummaryNoTokscaleData"),
            "currency" => _currencySnapshot is { Available: true, Rates.Count: > 0 } currencySnapshot
                ? Loc.T("SummaryCurrency",
                    currencySnapshot.Rates.Count,
                    currencySnapshot.BaseCurrency,
                    currencySnapshot.UpdatedAt.ToString("HH:mm"))
                : _currencySnapshot?.ErrorMessage ?? Loc.T("SummaryConfigureCurrency"),
            "crypto" => _cryptoSnapshot is { Coins.Count: > 0 } cryptoSnapshot
                ? Loc.T("SummaryCrypto", cryptoSnapshot.Coins.Count, cryptoSnapshot.UpdatedAt.ToString("HH:mm"))
                : _cryptoSnapshot?.ErrorMessage ?? Loc.T("SummaryConfigureCrypto"),
            "pomodoro" => BuildPomodoroSummary(),
            "github" => _gitHubSnapshot is { Available: true, Days.Count: > 0 } gitHubSnapshot
                ? Loc.T("SummaryGitHub",
                    gitHubSnapshot.Username,
                    CountThisWeek(gitHubSnapshot),
                    gitHubSnapshot.UpdatedAt.ToString("HH:mm"))
                : _gitHubSnapshot?.ErrorMessage ?? Loc.T("SummaryConfigureGitHub"),
            "alerts" => _alertsSnapshot is { Available: true } alertsSnapshot
                ? Loc.T("SummaryAlerts",
                    alertsSnapshot.ActiveAlerts.Count,
                    alertsSnapshot.UpdatedAt.ToString("HH:mm"))
                : _alertsSnapshot?.ErrorMessage ?? Loc.T("SummaryConfigureAlerts"),
            "hardware" => _hardwareSnapshot is { Available: true } hardwareSnapshot
                ? Loc.T("SummaryHardware",
                    HardwareMonitorTheme.FormatTemperature(hardwareSnapshot.Cpu?.TemperatureC),
                    HardwareMonitorTheme.FormatTemperature(hardwareSnapshot.Gpu?.TemperatureC),
                    hardwareSnapshot.UpdatedAt.ToString("HH:mm"))
                : _hardwareSnapshot?.ErrorMessage ?? Loc.T("SummaryLocalOnly"),
            _ => Loc.T("SummaryLocalOnly")
        };

    private static int CountThisWeek(GitHubContributionSnapshot snapshot)
    {
        DateOnly newest = snapshot.Days[^1].Date;
        int total = 0;
        foreach (GitHubContributionDay day in snapshot.Days)
        {
            if (day.Count > 0 && GitHubContributionTheme.WeeksBack(newest, day.Date) == 0)
            {
                total += day.Count;
            }
        }

        return total;
    }

    private string BuildPomodoroSummary()
    {
        PomodoroSnapshot state = _pomodoroTimer.Read();
        string remaining = $"{(int)state.Remaining.TotalMinutes:00}:{state.Remaining.Seconds:00}";
        return state.Phase switch
        {
            PomodoroPhase.Focus => Loc.T("SummaryPomodoroPhase", Loc.T("ScreenPomodoroFocus"), remaining),
            PomodoroPhase.Break => Loc.T("SummaryPomodoroPhase", Loc.T("ScreenPomodoroBreak"), remaining),
            _ => Loc.T("SummaryPomodoroIdle")
        };
    }

    private static string GetLoadingSummary(string themeId) =>
        themeId switch
        {
            "system" or "dashboard" or "performance" or "network" or "system-minimal" => Loc.T("LoadingSystem"),
            "music" or "music-minimal" or "music-poster" => Loc.T("LoadingMusic"),
            "clock-weather-dot" or "weather-five-day" => Loc.T("LoadingWeather"),
            "stocks" => Loc.T("LoadingStocks"),
            "currency" => Loc.T("LoadingCurrency"),
            "crypto" => Loc.T("LoadingCrypto"),
            "github" => Loc.T("LoadingGitHub"),
            "alerts" => Loc.T("LoadingAlerts"),
            "ai-quota" => Loc.T("LoadingAiUsage"),
            "claude-usage" => Loc.T("LoadingClaude"),
            _ => Loc.T("LoadingTheme")
        };
}
