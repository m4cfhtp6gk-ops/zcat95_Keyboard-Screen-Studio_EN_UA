using Microsoft.Toolkit.Uwp.Notifications;

namespace KeyboardScreen.App.Avalonia.Platform;

/// <summary>
/// Windows toast notifications for unpackaged Win32 apps via the UWP toolkit.
/// Toasts can be blocked by policy or fail on stripped-down editions, so a
/// failed Show never takes the app down - the notification is simply skipped.
/// </summary>
public sealed class WindowsToastNotifier
{
    private bool _broken;

    public void Show(string title, string message)
    {
        if (_broken || !OperatingSystem.IsWindowsVersionAtLeast(10, 0, 17763))
        {
            return;
        }

        try
        {
            new ToastContentBuilder()
                .AddText(title)
                .AddText(message)
                .Show();
        }
        catch
        {
            // One failure means the notification stack is unusable here;
            // stop trying instead of throwing once a minute.
            _broken = true;
        }
    }
}
