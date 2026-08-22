using KeyboardScreen.Core;

namespace KeyboardScreen.App.Avalonia.Platform;

public interface IWindowsDesktopServices : IDisposable
{
    ValueTask<WeatherSettings> ResolveWeatherSettingsAsync(
        WeatherSettings settings,
        CancellationToken cancellationToken = default);

    bool TrySetLaunchAtStartup(bool enabled);

    /// <summary>
    /// Relaunches this executable elevated so the sensor driver can load.
    /// False means the elevation prompt was dismissed - an answer, not a failure.
    /// </summary>
    bool TryRestartElevated();

    void OpenFolder(string path);
    void OpenUrl(string url);
}
