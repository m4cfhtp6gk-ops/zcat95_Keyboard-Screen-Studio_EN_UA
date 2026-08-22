namespace KeyboardScreen.Core;

/// <summary>
/// Global display units for everything rendered onto the keyboard screen, set
/// by the app from settings before rendering (the same pattern as Loc). The
/// 12-hour clock deliberately draws no AM/PM marker - the screen is 142 px
/// wide and every layout keeps its geometry, only the digits change.
/// </summary>
public static class DisplayUnits
{
    public static bool Use12HourClock { get; set; }

    public static bool UseFahrenheit { get; set; }

    /// <summary>"21:47" or "9:47".</summary>
    public static string Time(DateTimeOffset timestamp) =>
        timestamp.ToString(Use12HourClock ? "h:mm" : "HH:mm");

    /// <summary>The hour tier of tile and dot-matrix clocks: "21" or "9".</summary>
    public static string Hours(DateTimeOffset timestamp) =>
        timestamp.ToString(Use12HourClock ? "%h" : "HH");

    public static double Temperature(double celsius) =>
        UseFahrenheit ? celsius * 9.0 / 5.0 + 32.0 : celsius;

    /// <summary>"26°" - the value converted, the unit implied.</summary>
    public static string TemperatureShort(double celsius) => $"{Temperature(celsius):0}°";

    /// <summary>"26°C" / "79°F".</summary>
    public static string TemperatureWithUnit(double celsius) =>
        $"{Temperature(celsius):0}°{(UseFahrenheit ? "F" : "C")}";
}
