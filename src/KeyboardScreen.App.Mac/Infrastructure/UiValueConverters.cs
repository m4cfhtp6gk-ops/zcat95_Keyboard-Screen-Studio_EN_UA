using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;
using KeyboardScreen.Core;

namespace KeyboardScreen.App.Avalonia.Infrastructure;

public sealed class ConnectionBrushConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is true
            ? new SolidColorBrush(global::Avalonia.Media.Color.Parse("#27BE7B"))
            : new SolidColorBrush(global::Avalonia.Media.Color.Parse("#A2A8B1"));

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

public sealed class ImageTimePlacementLabelConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        Loc.T(value is ImageTimePlacement.Top ? "ImagePlacementTop" : "ImagePlacementBottom");

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
public sealed class DotMatrixProgressPeriodLabelConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        Loc.T(value switch
        {
            DotMatrixProgressPeriod.Week => "PeriodWeek",
            DotMatrixProgressPeriod.Month => "PeriodMonth",
            DotMatrixProgressPeriod.Quarter => "PeriodQuarter",
            DotMatrixProgressPeriod.Year => "PeriodYear",
            _ => "PeriodToday"
        });

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
public sealed class ImageClockStyleLabelConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        Loc.T(value switch
        {
            ImageClockStyle.LargeDigital => "ClockStyleLargeDigital",
            ImageClockStyle.Analog => "ClockStyleAnalog",
            ImageClockStyle.Flip => "ClockStyleFlip",
            _ => "ClockStyleDigital"
        });

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotSupportedException();
}

public sealed class ImageTextColorLabelConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        Loc.T(value is ImageTextColor.Black ? "ImageTextColorLight" : "ImageTextColorDark");

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotSupportedException();
}

public sealed class ImageTextAlignmentLabelConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        Loc.T(value switch
        {
            ImageTextAlignment.Left => "AlignLeft",
            ImageTextAlignment.Right => "AlignRight",
            _ => "AlignCenter"
        });

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotSupportedException();
}

public sealed class ImageDigitalOrderLabelConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => value switch
    {
        ImageDigitalOrder.TimeWeatherDate => RowOrder.Format("OrderTime", "OrderWeather", "OrderDate"),
        ImageDigitalOrder.DateTimeWeather => RowOrder.Format("OrderDate", "OrderTime", "OrderWeather"),
        ImageDigitalOrder.DateWeatherTime => RowOrder.Format("OrderDate", "OrderWeather", "OrderTime"),
        ImageDigitalOrder.WeatherTimeDate => RowOrder.Format("OrderWeather", "OrderTime", "OrderDate"),
        ImageDigitalOrder.WeatherDateTime => RowOrder.Format("OrderWeather", "OrderDate", "OrderTime"),
        _ => RowOrder.Format("OrderTime", "OrderDate", "OrderWeather")
    };

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
public sealed class ImageAnalogOrderLabelConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => value switch
    {
        ImageAnalogOrder.ClockWeatherDate => RowOrder.Format("OrderClock", "OrderWeather", "OrderDate"),
        ImageAnalogOrder.DateClockWeather => RowOrder.Format("OrderDate", "OrderClock", "OrderWeather"),
        ImageAnalogOrder.DateWeatherClock => RowOrder.Format("OrderDate", "OrderWeather", "OrderClock"),
        ImageAnalogOrder.WeatherClockDate => RowOrder.Format("OrderWeather", "OrderClock", "OrderDate"),
        ImageAnalogOrder.WeatherDateClock => RowOrder.Format("OrderWeather", "OrderDate", "OrderClock"),
        _ => RowOrder.Format("OrderClock", "OrderDate", "OrderWeather")
    };

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>Joins the three row names of an image-clock order into one label.</summary>
internal static class RowOrder
{
    public static string Format(string firstKey, string secondKey, string thirdKey) =>
        $"{Loc.T(firstKey)} \u00b7 {Loc.T(secondKey)} \u00b7 {Loc.T(thirdKey)}";
}

public sealed class UiThemeModeLabelConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        Loc.T(value switch
        {
            UiThemeMode.Light => "UiThemeLight",
            UiThemeMode.Dark => "UiThemeDark",
            _ => "UiThemeSystem"
        });

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotSupportedException();
}
