using System.Windows.Input;
using Avalonia.Controls.ApplicationLifetimes;
using KeyboardScreen.App.Avalonia.Infrastructure;

namespace KeyboardScreen.App.Avalonia;

public sealed partial class App
{
    public App()
    {
        ShowWindowCommand = new RelayCommand(ShowMainWindow);
        ExitCommand = new RelayCommand(ExitApplication);
        DataContext = this;
    }

    public ICommand ShowWindowCommand { get; }
    public ICommand ExitCommand { get; }

    private void ShowMainWindow()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop &&
            desktop.MainWindow is MainWindow window)
        {
            window.RestoreFromTray();
        }
    }

    private void ExitApplication()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop &&
            desktop.MainWindow is MainWindow window)
        {
            window.RequestExit();
        }
    }
}
