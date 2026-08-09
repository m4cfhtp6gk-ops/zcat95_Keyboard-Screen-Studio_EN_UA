namespace KeyboardScreen.Core;

/// <summary>
/// Decides whether the application should start with its main window hidden,
/// for example when auto-starting minimized or from the --startup argument.
/// </summary>
public static class StartupVisibility
{
    public static bool ShouldStartHidden(AppSettings settings, bool hasStartupArgument) =>
        settings.HasCompletedOnboarding && (settings.StartMinimized || hasStartupArgument);
}
