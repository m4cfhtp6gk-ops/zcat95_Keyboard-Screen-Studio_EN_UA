using KeyboardScreen.Core;

namespace KeyboardScreen.App.Avalonia.Platform;

public interface IWindowsDesktopServices : IDisposable
{
    ValueTask<WeatherSettings> ResolveWeatherSettingsAsync(
        WeatherSettings settings,
        CancellationToken cancellationToken = default);

    bool TrySetLaunchAtStartup(bool enabled);
    void OpenFolder(string path);
    void OpenUrl(string url);
}
