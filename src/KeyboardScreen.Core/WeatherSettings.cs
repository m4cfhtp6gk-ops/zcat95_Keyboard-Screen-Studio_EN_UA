namespace KeyboardScreen.Core;

public sealed class WeatherSettings
{
    public const string DefaultLocationQuery = "Kyiv";

    public string LocationQuery { get; set; } = DefaultLocationQuery;

    public bool UseAutomaticLocation { get; set; } = true;

    [System.Text.Json.Serialization.JsonIgnore]
    public double? Latitude { get; set; }

    [System.Text.Json.Serialization.JsonIgnore]
    public double? Longitude { get; set; }

    [System.Text.Json.Serialization.JsonIgnore]
    public string? AutomaticLocationName { get; set; }
}