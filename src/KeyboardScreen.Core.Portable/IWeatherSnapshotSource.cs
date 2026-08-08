namespace KeyboardScreen.Core;

public interface IWeatherSnapshotSource
{
    Task<WeatherSnapshot> ReadAsync(WeatherSettings settings, CancellationToken cancellationToken = default);
}