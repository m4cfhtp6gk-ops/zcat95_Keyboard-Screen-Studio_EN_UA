using System.Diagnostics;
using KeyboardScreen.Core;
using Microsoft.Win32;
using Windows.Devices.Geolocation;

namespace KeyboardScreen.App.Avalonia.Platform;

public sealed class WindowsDesktopServices : IWindowsDesktopServices
{
    private const string StartupValueName = "KeyboardScreenStudio";
    private const string StartupRegistryPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private readonly BigDataCloudReverseGeocoder _reverseGeocoder = new();
    private AutomaticWeatherLocation? _cachedLocation;
    private DateTimeOffset _cachedAt;

    public async ValueTask<WeatherSettings> ResolveWeatherSettingsAsync(
        WeatherSettings settings,
        CancellationToken cancellationToken = default)
    {
        if (!settings.UseAutomaticLocation)
        {
            return settings;
        }

        AutomaticWeatherLocation? location = await TryGetLocationAsync(cancellationToken);
        if (location is null)
        {
            return new WeatherSettings
            {
                UseAutomaticLocation = false,
                LocationQuery = settings.LocationQuery
            };
        }

        return new WeatherSettings
        {
            UseAutomaticLocation = true,
            LocationQuery = settings.LocationQuery,
            Latitude = location.Latitude,
            Longitude = location.Longitude,
            AutomaticLocationName = location.DisplayName
        };
    }

    public bool TrySetLaunchAtStartup(bool enabled)
    {
        try
        {
            using RegistryKey key = Registry.CurrentUser.CreateSubKey(StartupRegistryPath);
            if (enabled)
            {
                string executable = Environment.ProcessPath
                    ?? throw new InvalidOperationException("无法确定程序路径。");
                key.SetValue(StartupValueName, $"\"{executable}\" --startup");
            }
            else
            {
                key.DeleteValue(StartupValueName, throwOnMissingValue: false);
            }

            return true;
        }
        catch
        {
            return false;
        }
    }

    public void OpenFolder(string path)
    {
        Directory.CreateDirectory(path);
        Process.Start(new ProcessStartInfo
        {
            FileName = path,
            UseShellExecute = true
        });
    }

    public void OpenUrl(string url) =>
        Process.Start(new ProcessStartInfo
        {
            FileName = url,
            UseShellExecute = true
        });

    public void Dispose() => _reverseGeocoder.Dispose();

    private async Task<AutomaticWeatherLocation?> TryGetLocationAsync(
        CancellationToken cancellationToken)
    {
        if (_cachedLocation is not null &&
            DateTimeOffset.Now - _cachedAt < TimeSpan.FromMinutes(30))
        {
            return _cachedLocation;
        }

        try
        {
            GeolocationAccessStatus access = await Geolocator.RequestAccessAsync();
            if (access != GeolocationAccessStatus.Allowed)
            {
                return null;
            }

            var locator = new Geolocator
            {
                DesiredAccuracy = PositionAccuracy.Default,
                ReportInterval = 0
            };
            Geoposition position = await locator.GetGeopositionAsync(
                TimeSpan.FromMinutes(10),
                TimeSpan.FromSeconds(8));
            cancellationToken.ThrowIfCancellationRequested();
            BasicGeoposition point = position.Coordinate.Point.Position;
            string displayName = await _reverseGeocoder.ResolveCityAsync(
                point.Latitude,
                point.Longitude,
                cancellationToken) ?? "当前位置";
            _cachedLocation = new AutomaticWeatherLocation(
                point.Latitude,
                point.Longitude,
                displayName);
            _cachedAt = DateTimeOffset.Now;
            return _cachedLocation;
        }
        catch
        {
            return null;
        }
    }

    private sealed record AutomaticWeatherLocation(
        double Latitude,
        double Longitude,
        string DisplayName);
}
