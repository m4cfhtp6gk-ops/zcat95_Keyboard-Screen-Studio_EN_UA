namespace KeyboardScreen.Core;

public sealed record WeatherSnapshot(
    bool Available,
    string LocationName,
    double TemperatureC,
    double ApparentTemperatureC,
    int RelativeHumidityPercent,
    int WeatherCode,
    bool IsDay,
    DateTimeOffset UpdatedAt,
    bool IsStale = false,
    string? ErrorMessage = null,
    IReadOnlyList<DailyWeatherForecast>? DailyForecast = null)
{
    public static WeatherSnapshot Unavailable(string? message = null) =>
        new(false, Loc.T("WeatherUnknown"), 0, 0, 0, 0, true, DateTimeOffset.MinValue, false, message);

    public string ConditionText => WeatherCondition.FromCode(WeatherCode);
}

public static class WeatherCondition
{
    public static string FromCode(int code) => code switch
    {
        0 => Loc.T("WeatherClear"),
        1 => Loc.T("WeatherMostlyClear"),
        2 => Loc.T("WeatherPartlyCloudy"),
        3 => Loc.T("WeatherOvercast"),
        45 or 48 => Loc.T("WeatherFog"),
        51 or 53 or 55 => Loc.T("WeatherDrizzle"),
        56 or 57 => Loc.T("WeatherFreezingDrizzle"),
        61 or 63 or 65 => Loc.T("WeatherRain"),
        66 or 67 => Loc.T("WeatherFreezingRain"),
        71 or 73 or 75 or 77 => Loc.T("WeatherSnow"),
        80 or 81 or 82 => Loc.T("WeatherShowers"),
        85 or 86 => Loc.T("WeatherSnowShowers"),
        95 or 96 or 99 => Loc.T("WeatherThunderstorm"),
        _ => Loc.T("WeatherUnknown")
    };

    public static WeatherIconKind IconFromCode(int code) => code switch
    {
        0 => WeatherIconKind.Clear,
        1 or 2 => WeatherIconKind.PartlyCloudy,
        3 => WeatherIconKind.Cloudy,
        45 or 48 => WeatherIconKind.Fog,
        51 or 53 or 55 or 56 or 57 or 61 or 63 or 65 or 66 or 67 or 80 or 81 or 82 => WeatherIconKind.Rain,
        71 or 73 or 75 or 77 or 85 or 86 => WeatherIconKind.Snow,
        95 or 96 or 99 => WeatherIconKind.Thunderstorm,
        _ => WeatherIconKind.Cloudy
    };
}

public enum WeatherIconKind
{
    Clear,
    PartlyCloudy,
    Cloudy,
    Fog,
    Rain,
    Snow,
    Thunderstorm
}