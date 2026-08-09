namespace KeyboardScreen.Core;

/// <summary>
/// Ensures that one-time startup behaviors run only once per process:
/// window initialization must not repeat when the window is shown again
/// from the tray, and minimize-to-tray must not re-trigger on restore.
/// </summary>
public sealed class StartupGate
{
    private bool _initializationStarted;
    private bool _startupMinimizeConsumed;

    public bool TryBeginInitialization()
    {
        if (_initializationStarted)
        {
            return false;
        }

        _initializationStarted = true;
        return true;
    }

    public bool ConsumeStartupMinimize(bool startMinimized, bool hasStartupArgument)
    {
        if (_startupMinimizeConsumed)
        {
            return false;
        }

        _startupMinimizeConsumed = true;
        return startMinimized || hasStartupArgument;
    }
}
