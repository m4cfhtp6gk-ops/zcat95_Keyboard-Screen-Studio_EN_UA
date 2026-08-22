using Avalonia;
using KeyboardScreen.Core;

namespace KeyboardScreen.App.Avalonia;

internal static class Program
{
    /// <summary>Marks the process that a "restart as administrator" started.</summary>
    internal const string ElevatedRestartArgument = "--restart-elevated";

    [STAThread]
    public static void Main(string[] args)
    {
        AppDomain.CurrentDomain.UnhandledException += (_, eventArgs) =>
            CrashLog.Write(eventArgs.ExceptionObject as Exception ?? new Exception("Unknown unhandled exception"));

        Loc.Instance.Initialize(ResolveStartupLanguage());

        // Started by "restart as administrator": the instance that asked for the
        // elevation is still shutting down, so wait for it instead of bouncing off
        // its mutex and handing the window straight back to the unelevated process.
        bool elevatedRestart = args.Any(argument =>
            string.Equals(argument, ElevatedRestartArgument, StringComparison.OrdinalIgnoreCase));
        bool acquired = elevatedRestart
            ? SingleInstance.TryAcquire(TimeSpan.FromSeconds(20))
            : SingleInstance.TryAcquire();
        if (!acquired)
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
