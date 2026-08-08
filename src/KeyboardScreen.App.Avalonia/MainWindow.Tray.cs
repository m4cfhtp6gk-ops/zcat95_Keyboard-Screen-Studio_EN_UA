using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;

namespace KeyboardScreen.App.Avalonia;

public sealed partial class MainWindow
{
    private bool _allowExit;

    internal void RestoreFromTray()
    {
        Show();
        WindowState = WindowState.Normal;
        Activate();
    }

    internal void RequestExit()
    {
        _allowExit = true;
        Close();
    }

    protected override void OnClosing(WindowClosingEventArgs e)
    {
        if (!_allowExit && _viewModel.CloseToTray)
        {
            e.Cancel = true;
            Hide();
            return;
        }

        base.OnClosing(e);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == WindowStateProperty &&
            WindowState == WindowState.Minimized &&
            _viewModel.MinimizeToTray)
        {
            Dispatcher.UIThread.Post(Hide, DispatcherPriority.Background);
        }
    }
}
