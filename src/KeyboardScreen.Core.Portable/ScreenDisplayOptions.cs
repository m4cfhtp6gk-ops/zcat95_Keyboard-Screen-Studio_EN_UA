namespace KeyboardScreen.Core;

public enum UiThemeMode
{
    System,
    Light,
    Dark
}

public enum ImageTimePlacement
{
    Top,
    Bottom
}

public enum ImageClockStyle
{
    Digital,
    LargeDigital,
    Analog,
    Flip
}

public enum ImageTextColor
{
    White,
    Black
}

public enum ImageTextAlignment
{
    Left,
    Center,
    Right
}

public enum ImageDigitalOrder
{
    TimeDateWeather,
    TimeWeatherDate,
    DateTimeWeather,
    DateWeatherTime,
    WeatherTimeDate,
    WeatherDateTime
}


public enum ImageAnalogOrder
{
    ClockDateWeather,
    ClockWeatherDate,
    DateClockWeather,
    DateWeatherClock,
    WeatherClockDate,
    WeatherDateClock
}
public enum DotMatrixProgressPeriod
{
    Today,
    Week,
    Month,
    Quarter,
    Year
}

public sealed record ScreenDisplayOptions(
    ImageTimePlacement ImageTimePlacement = ImageTimePlacement.Bottom,
    DotMatrixProgressPeriod DotMatrixProgressPeriod = DotMatrixProgressPeriod.Today,
    ImageClockStyle ImageClockStyle = ImageClockStyle.Digital,
    bool ImageTimeBackground = true,
    ImageTextColor ImageTextColor = ImageTextColor.White,
    ImageTextAlignment ImageTextAlignment = ImageTextAlignment.Center,
    bool ImageWeatherVisible = false,
    int ImageTimeFontSize = 29,
    int ImageDateFontSize = 11,
    int ImageWeatherFontSize = 11,
    ImageDigitalOrder ImageDigitalOrder = ImageDigitalOrder.TimeDateWeather,
    int ImageLargeTimeFontSize = 40,
    int ImageAnalogClockSize = 76,
    ImageAnalogOrder ImageAnalogOrder = ImageAnalogOrder.ClockDateWeather,
    int ImageFlipTimeFontSize = 29,
    int DotMatrixProgressHeaderFontSize = 15)
{
    public static ScreenDisplayOptions Default { get; } = new();
}