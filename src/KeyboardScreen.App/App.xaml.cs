using System.Windows;
using System.Threading;

namespace KeyboardScreen.App;

public partial class App : System.Windows.Application
{
    private const string InstanceMutexName = "Local\\KeyboardScreenStudio.Instance";
    private const string ActivationEventName = "Local\\KeyboardScreenStudio.Activate";
    private Mutex? _instanceMutex;
    private EventWaitHandle? _activationEvent;
    private CancellationTokenSource? _activationCancellation;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        _instanceMutex = new Mutex(true, InstanceMutexName, out var isFirstInstance);
        _activationEvent = new EventWaitHandle(false, EventResetMode.AutoReset, ActivationEventName);
        if (!isFirstInstance)
        {
            _activationEvent.Set();
            _activationEvent.Dispose();
            _activationEvent = null;
            _instanceMutex.Dispose();
            _instanceMutex = null;
            Shutdown();
            return;
        }

        var window = new MainWindow();
        MainWindow = window;
        window.Title = "Keyboard Screen Studio";
        window.ShowInTaskbar = true;
        window.WindowStartupLocation = WindowStartupLocation.CenterScreen;
        window.WindowState = WindowState.Normal;
        window.Topmost = true;
        window.Show();
        window.Activate();
        window.ContentRendered += (_, _) =>
        {
            window.Show();
            window.WindowState = WindowState.Normal;
            window.Activate();
            window.Topmost = false;
        };

        _activationCancellation = new CancellationTokenSource();
        _ = Task.Run(() => WaitForActivation(_activationCancellation.Token));
    }

    private void WaitForActivation(CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested && _activationEvent is not null)
            {
                _activationEvent.WaitOne();
                if (cancellationToken.IsCancellationRequested)
                {
                    break;
                }

                Dispatcher.BeginInvoke(() => (MainWindow as KeyboardScreen.App.MainWindow)?.RestoreFromExternalActivation());
            }
        }
        catch (ObjectDisposedException)
        {
            // The activation event is disposed during shutdown; the listener exits quietly.
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _activationCancellation?.Cancel();
        try
        {
            _activationEvent?.Set();
        }
        catch (ObjectDisposedException)
        {
            // Already disposed by the non-first-instance path.
        }
        _activationEvent?.Dispose();
        _activationCancellation?.Dispose();
        try
        {
            _instanceMutex?.ReleaseMutex();
        }
        catch (ApplicationException)
        {
            // The mutex is not owned by this thread (e.g. abandoned); nothing to release.
        }
        _instanceMutex?.Dispose();
        base.OnExit(e);
    }
}
