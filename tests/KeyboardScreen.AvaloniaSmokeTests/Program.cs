using Avalonia;
using Avalonia.Headless;
using KeyboardScreen.App.Avalonia;
using KeyboardScreen.Core;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        AppBuilder.Configure<App>()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions())
            .WithInterFont()
            .SetupWithoutStarting();

        VerifyGuideWindowConstructs();
        VerifyAccentColorWindowConstructs();
        VerifyRiskNoticeWindowConstructs();
        VerifyLocalizedWindowTitleFollowsLanguage();

        VerifyStartupGateBehaviors();
        VerifyStartupVisibilityBehavior();
        VerifySingleInstanceBehavior();
        VerifyShowWindowSignal();

        Console.WriteLine("All Avalonia UI smoke tests passed.");
    }

    private static void VerifyGuideWindowConstructs()
    {
        var window = new FirstRunGuideWindow(string.Empty);
        window.Show();
        window.Close();
    }

    private static void VerifyAccentColorWindowConstructs()
    {
        var window = new AccentColorWindow("#E4694C");
        window.Show();
        window.Close();
    }

    private static void VerifyRiskNoticeWindowConstructs()
    {
        var window = new RiskNoticeWindow("title", "subtitle", "body");
        window.Show();
        window.Close();
    }

    /// <summary>
    /// Exercises the {infra:Localize} markup extension end to end: the title must
    /// come from the catalogue at load time and must follow a language switch on
    /// a window that is already open.
    /// </summary>
    private static void VerifyLocalizedWindowTitleFollowsLanguage()
    {
        Loc.Instance.Initialize(AppLanguage.English);
        var window = new AccentColorWindow("#E4694C");
        window.Show();
        try
        {
            Assert(
                window.Title == Loc.T("AccentTitle"),
                "the Localize markup extension did not resolve the window title");

            Loc.Language = AppLanguage.Ukrainian;
            Assert(
                window.Title == Loc.T("AccentTitle"),
                "an open window did not pick up the language change");

            Loc.Language = AppLanguage.ChineseSimplified;
            Assert(
                window.Title == Loc.T("AccentTitle"),
                "an open window did not pick up a second language change");
        }
        finally
        {
            Loc.Language = AppLanguage.English;
            window.Close();
        }
    }

    private static void VerifyStartupGateBehaviors()
    {
        var initialization = new StartupGate();
        Assert(initialization.TryBeginInitialization(), "first open must run initialization");
        Assert(!initialization.TryBeginInitialization(), "second open must not re-run initialization");

        var startMinimized = new StartupGate();
        Assert(
            startMinimized.ConsumeStartupMinimize(startMinimized: true, hasStartupArgument: false),
            "start-minimized setting must minimize on the first open");
        Assert(
            !startMinimized.ConsumeStartupMinimize(startMinimized: true, hasStartupArgument: false),
            "restore from tray must not re-minimize a start-minimized launch");

        var startupArgument = new StartupGate();
        Assert(
            startupArgument.ConsumeStartupMinimize(startMinimized: false, hasStartupArgument: true),
            "--startup argument must minimize on the first open");
        Assert(
            !startupArgument.ConsumeStartupMinimize(startMinimized: false, hasStartupArgument: true),
            "restore from tray must not re-minimize a --startup launch");

        var normalLaunch = new StartupGate();
        Assert(
            !normalLaunch.ConsumeStartupMinimize(startMinimized: false, hasStartupArgument: false),
            "normal launch must not minimize");
        Assert(
            !normalLaunch.ConsumeStartupMinimize(startMinimized: false, hasStartupArgument: false),
            "normal launch must stay non-minimized on later opens");
    }

    private static void VerifyStartupVisibilityBehavior()
    {
        var configured = new AppSettings
        {
            HasCompletedOnboarding = true,
            StartMinimized = true
        };
        Assert(
            StartupVisibility.ShouldStartHidden(configured, hasStartupArgument: false),
            "start-minimized setting must start hidden after onboarding");

        var autoStart = new AppSettings { HasCompletedOnboarding = true };
        Assert(
            StartupVisibility.ShouldStartHidden(autoStart, hasStartupArgument: true),
            "--startup must start hidden after onboarding");

        var normal = new AppSettings { HasCompletedOnboarding = true };
        Assert(
            !StartupVisibility.ShouldStartHidden(normal, hasStartupArgument: false),
            "normal launch must not start hidden");

        var freshInstall = new AppSettings();
        Assert(
            !StartupVisibility.ShouldStartHidden(freshInstall, hasStartupArgument: true),
            "a fresh install with --startup must show the onboarding window");
    }

    private static void VerifySingleInstanceBehavior()
    {
        SingleInstance.MutexName = @"Local\KssAvaloniaSmoke.SingleInstance";
        SingleInstance.ShowWindowEventName = @"Local\KssAvaloniaSmoke.ShowWindow";

        Assert(SingleInstance.TryAcquire(), "first launch must acquire the instance lock");

        bool secondAcquired = false;
        var thread = new Thread(() => secondAcquired = SingleInstance.TryAcquire());
        thread.Start();
        thread.Join();
        Assert(!secondAcquired, "a second launch must not acquire the instance lock");

        SingleInstance.Release();
        Assert(SingleInstance.TryAcquire(), "a later launch may acquire after release");
        SingleInstance.Release();
    }

    private static void VerifyShowWindowSignal()
    {
        using var gate = new ManualResetEventSlim();
        using IDisposable? listener = SingleInstance.StartShowWindowListener(() => gate.Set());
        SingleInstance.SignalShowWindow();
        Assert(gate.Wait(TimeSpan.FromSeconds(5)), "a second launch must signal the running instance");
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
