using System.Threading;

namespace KeyboardScreen.App.Avalonia;

/// <summary>
/// Ensures that only one KSS process runs per user session. A second launch
/// signals the running instance to show its main window and then exits.
/// </summary>
public static class SingleInstance
{
    internal static string MutexName { get; set; } = @"Local\KeyboardScreenStudio.SingleInstance";
    internal static string ShowWindowEventName { get; set; } = @"Local\KeyboardScreenStudio.ShowWindow";

    private static Mutex? _mutex;

    public static bool TryAcquire()
    {
        Mutex mutex = new(initiallyOwned: true, MutexName, out bool createdNew);
        if (!createdNew)
        {
            mutex.Dispose();
            return false;
        }

        _mutex?.Dispose();
        _mutex = mutex;
        return true;
    }

    public static void SignalShowWindow()
    {
        try
        {
            using EventWaitHandle signal = EventWaitHandle.OpenExisting(ShowWindowEventName);
            signal.Set();
        }
        catch (WaitHandleCannotBeOpenedException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    public static IDisposable? StartShowWindowListener(Action onShow)
    {
        EventWaitHandle handle;
        try
        {
            handle = new EventWaitHandle(false, EventResetMode.AutoReset, ShowWindowEventName);
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }

        CancellationTokenSource stop = new();
        Task task = Task.Run(() =>
        {
            while (!stop.IsCancellationRequested)
            {
                try
                {
                    if (handle.WaitOne(250))
                    {
                        onShow();
                    }
                }
                catch (ObjectDisposedException)
                {
                    break;
                }
            }
        });

        return new ListenerRegistration(stop, handle, task);
    }

    public static void Release() => _mutex?.Dispose();

    private sealed class ListenerRegistration : IDisposable
    {
        private readonly CancellationTokenSource _stop;
        private readonly EventWaitHandle _handle;
        private readonly Task _task;

        public ListenerRegistration(CancellationTokenSource stop, EventWaitHandle handle, Task task)
        {
            _stop = stop;
            _handle = handle;
            _task = task;
        }

        public void Dispose()
        {
            _stop.Cancel();
            _handle.Dispose();
            try
            {
                _task.Wait(TimeSpan.FromSeconds(1));
            }
            catch
            {
                // Process exit cleans up any straggler.
            }
            _stop.Dispose();
        }
    }
}
