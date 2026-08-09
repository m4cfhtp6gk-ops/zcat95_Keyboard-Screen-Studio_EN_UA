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

    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
