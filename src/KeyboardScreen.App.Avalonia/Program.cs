using Avalonia;
using KeyboardScreen.Core;

namespace KeyboardScreen.App.Avalonia;

internal static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        AppDomain.CurrentDomain.UnhandledException += (_, eventArgs) =>
            CrashLog.Write(eventArgs.ExceptionObject as Exception ?? new Exception("Unknown unhandled exception"));

        Loc.Instance.Initialize(ResolveStartupLanguage());

        if (!SingleInstance.TryAcquire())
        {
            SingleInstance.SignalShowWindow();
            return;
        }

        try
        {
            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }
        finally
        {
            SingleInstance.Release();
        }
    }

    /// <summary>
    /// Resolves the language before any XAML loads, so the first frame is already
    /// translated. On first run it follows the machine's own UI culture.
    /// </summary>
    private static AppLanguage ResolveStartupLanguage()
    {
        try
        {
            AppSettings settings = new JsonSettingsStore().LoadAsync().GetAwaiter().GetResult();
            return string.IsNullOrWhiteSpace(settings.Language)
                ? AppLanguageInfo.FromSystemCulture()
                : AppLanguageInfo.Parse(settings.Language);
        }
        catch
        {
            return AppLanguageInfo.FromSystemCulture();
        }
    }

    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
