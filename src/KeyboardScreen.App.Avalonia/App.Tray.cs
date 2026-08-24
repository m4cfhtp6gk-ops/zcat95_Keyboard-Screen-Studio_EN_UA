using System.Windows.Input;
using Avalonia.Controls.ApplicationLifetimes;
using KeyboardScreen.App.Avalonia.Infrastructure;

namespace KeyboardScreen.App.Avalonia;

public sealed partial class App
{
    public App()
    {
        ShowWindowCommand = new RelayCommand(ShowMainWindow);
        TogglePomodoroCommand = new RelayCommand(TogglePomodoro);
        PushNowCommand = new RelayCommand(PushNow);
        ExitCommand = new RelayCommand(ExitApplication);
        DataContext = this;
    }

    public ICommand ShowWindowCommand { get; }
    public ICommand TogglePomodoroCommand { get; }
    public ICommand PushNowCommand { get; }
    public ICommand ExitCommand { get; }

    private void ShowMainWindow()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop &&
            desktop.MainWindow is MainWindow window)
        {
            window.RestoreFromTray();
        }
    }

    private void TogglePomodoro()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop &&
            desktop.MainWindow?.DataContext is ViewModels.MainWindowViewModel viewModel)
        {
            viewModel.TogglePomodoro();
        }
    }

    /// <summary>
    /// The app lives in the tray by default, so a failed push has to be
    /// retryable without opening the window first.
    /// </summary>
    private void PushNow()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop &&
            desktop.MainWindow?.DataContext is ViewModels.MainWindowViewModel viewModel &&
            viewModel.RefreshCommand.CanExecute(null))
        {
            viewModel.RefreshCommand.Execute(null);
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
