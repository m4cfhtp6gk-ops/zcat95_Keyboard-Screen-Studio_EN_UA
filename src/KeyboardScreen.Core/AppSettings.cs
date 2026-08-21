namespace KeyboardScreen.Core;

public sealed class AppSettings
{
	public string DeviceEndpoint { get; set; } = string.Empty;


	public string SelectedThemeId { get; set; } = "clock-dot-matrix";

	public UiThemeMode UiThemeMode { get; set; } = UiThemeMode.System;

	/// <summary>Language identifier ("en", "uk", "zh-Hans"); empty means "ask the OS".</summary>
	public string Language { get; set; } = string.Empty;


	public bool AutoPush { get; set; } = true;

	public int RefreshSeconds { get; set; } = 1;




	public string AccentColor { get; set; } = "#E4694C";


	public string SelectedFontId { get; set; } = "builtin:segoe-variable-display";


	public bool MinimizeToTray { get; set; } = true;


	public bool CloseToTray { get; set; } = true;


	public bool StartMinimized { get; set; }

	public bool LaunchAtStartup { get; set; }

	public bool HasCompletedOnboarding { get; set; }

	public bool HasAcknowledgedStockNotice { get; set; }

	public bool HasAcknowledgedAiUsageNotice { get; set; }

	public bool HasAcknowledgedClaudeNotice { get; set; }

	public bool AutoSwitchToMusic { get; set; }

	public bool AutoMediaThemeSwitch { get; set; }

	public bool IgnoreBrowserMediaSessions { get; set; } = true;

	public string MediaPlayingThemeId { get; set; } = "music";

	public string MediaIdleThemeId { get; set; } = "system";

	public string? ImagePath { get; set; }

	public ScreenInsets SafeArea { get; set; } = new ScreenInsets(10, 52, 10, 12);

	public AiQuotaSettings AiQuota { get; set; } = new();

	public WeatherSettings Weather { get; set; } = new();

	public StockSettings Stocks { get; set; } = new();

	public ClaudeUsageSettings ClaudeUsage { get; set; } = new();





	public ImageTimePlacement ImageTimePlacement { get; set; } = ImageTimePlacement.Bottom;

	public ImageClockStyle ImageClockStyle { get; set; } = ImageClockStyle.Digital;

	public bool ImageTimeBackground { get; set; } = true;

	public ImageTextColor ImageTextColor { get; set; } = ImageTextColor.White;

	public ImageTextAlignment ImageTextAlignment { get; set; } = ImageTextAlignment.Center;

	public bool ImageWeatherVisible { get; set; }

	public int ImageTimeFontSize { get; set; } = 29;

	public int ImageDateFontSize { get; set; } = 11;

	public int ImageWeatherFontSize { get; set; } = 11;

	public ImageDigitalOrder ImageDigitalOrder { get; set; } = ImageDigitalOrder.TimeDateWeather;

	public int ImageLargeTimeFontSize { get; set; } = 40;

	public int ImageAnalogClockSize { get; set; } = 76;

	public ImageAnalogOrder ImageAnalogOrder { get; set; } = ImageAnalogOrder.ClockDateWeather;

	public int ImageFlipTimeFontSize { get; set; } = 29;

	public DotMatrixProgressPeriod DotMatrixProgressPeriod { get; set; } = DotMatrixProgressPeriod.Today;

	public int DotMatrixProgressHeaderFontSize { get; set; } = 15;

	public bool PerfVisualCpuEnabled { get; set; } = true;

	public bool PerfVisualMemoryEnabled { get; set; } = true;

	public bool PerfVisualDownloadEnabled { get; set; } = true;

	public bool PerfVisualUploadEnabled { get; set; } = true;

	public bool PerfVisualGpuEnabled { get; set; } = true;
}
