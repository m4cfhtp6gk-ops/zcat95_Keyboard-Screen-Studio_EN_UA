using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;
using KeyboardScreen.Core;

namespace KeyboardScreen.App.Avalonia.Infrastructure;

public sealed class ConnectionBrushConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is true ? new SolidColorBrush(global::Avalonia.Media.Color.Parse("#27BE7B")) : new SolidColorBrush(global::Avalonia.Media.Color.Parse("#A2A8B1"));

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

public sealed class ImageTimePlacementLabelConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is ImageTimePlacement.Top ? "显示在上方" : "显示在下方";

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
public sealed class DotMatrixProgressPeriodLabelConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value switch
        {
            DotMatrixProgressPeriod.Week => "本周",
            DotMatrixProgressPeriod.Month => "本月",
            DotMatrixProgressPeriod.Quarter => "本季度",
            DotMatrixProgressPeriod.Year => "本年",
            _ => "今天"
        };

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
public sealed class ImageClockStyleLabelConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => value switch
    {
        ImageClockStyle.LargeDigital => "大数字时间",
        ImageClockStyle.Analog => "模拟时钟",
        ImageClockStyle.Flip => "翻页时钟",
        _ => "数字时钟"
    };
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotSupportedException();
}

public sealed class ImageTextColorLabelConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is ImageTextColor.Black ? "浅色模式" : "暗色模式";
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotSupportedException();
}

public sealed class ImageTextAlignmentLabelConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => value switch
    {
        ImageTextAlignment.Left => "左对齐",
        ImageTextAlignment.Right => "右对齐",
        _ => "居中对齐"
    };
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotSupportedException();
}

public sealed class ImageDigitalOrderLabelConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => value switch
    {
        ImageDigitalOrder.TimeWeatherDate => "时间 · 天气 · 日期",
        ImageDigitalOrder.DateTimeWeather => "日期 · 时间 · 天气",
        ImageDigitalOrder.DateWeatherTime => "日期 · 天气 · 时间",
        ImageDigitalOrder.WeatherTimeDate => "天气 · 时间 · 日期",
        ImageDigitalOrder.WeatherDateTime => "天气 · 日期 · 时间",
        _ => "时间 · 日期 · 天气"
    };

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
public sealed class ImageAnalogOrderLabelConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => value switch
    {
        ImageAnalogOrder.ClockWeatherDate => "时钟 · 天气 · 日期",
        ImageAnalogOrder.DateClockWeather => "日期 · 时钟 · 天气",
        ImageAnalogOrder.DateWeatherClock => "日期 · 天气 · 时钟",
        ImageAnalogOrder.WeatherClockDate => "天气 · 时钟 · 日期",
        ImageAnalogOrder.WeatherDateClock => "天气 · 日期 · 时钟",
        _ => "时钟 · 日期 · 天气"
    };

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
public sealed class UiThemeModeLabelConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => value switch
    {
        UiThemeMode.Light => "亮色模式",
        UiThemeMode.Dark => "暗色模式",
        _ => "跟随系统"
    };
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotSupportedException();
}
