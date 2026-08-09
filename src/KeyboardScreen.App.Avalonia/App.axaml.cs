using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using KeyboardScreen.Core;

namespace KeyboardScreen.App.Avalonia;

public sealed partial class App : Application
{
    private IDisposable? _singleInstanceListener;

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var window = new MainWindow();
            _singleInstanceListener = SingleInstance.StartShowWindowListener(
                () => Dispatcher.UIThread.Post(window.RestoreFromTray));
            _ = StartMainWindowAsync(desktop, window);
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static async Task StartMainWindowAsync(
        IClassicDesktopStyleApplicationLifetime desktop,
        MainWindow window)
    {
        AppSettings settings;
        try
        {
            settings = await new JsonSettingsStore().LoadAsync();
        }
        catch
        {
            settings = new AppSettings();
        }

        bool hasStartupArgument = Environment.GetCommandLineArgs().Any(argument =>
            string.Equals(argument, "--startup", StringComparison.OrdinalIgnoreCase));
        bool startHidden = StartupVisibility.ShouldStartHidden(settings, hasStartupArgument);

        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            desktop.MainWindow = window;
            if (startHidden)
            {
                _ = window.InitializeHiddenStartupAsync();
            }
            else
            {
                window.Show();
            }
        });
    }
}
