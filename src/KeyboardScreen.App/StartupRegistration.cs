using Microsoft.Win32;

namespace KeyboardScreen.App;

internal static class StartupRegistration
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "KeyboardScreenStudio";

    public static bool TrySetEnabled(bool enabled)
    {
        try
        {
            using RegistryKey? runKey = Registry.CurrentUser.CreateSubKey(RunKeyPath, writable: true);
            if (runKey is null)
            {
                return false;
            }

            if (!enabled)
            {
                runKey.DeleteValue(ValueName, throwOnMissingValue: false);
                return true;
            }

            string executablePath = Environment.ProcessPath
                ?? System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName
                ?? string.Empty;
            if (string.IsNullOrWhiteSpace(executablePath))
            {
                return false;
            }

            runKey.SetValue(ValueName, $"\"{executablePath}\" --startup", RegistryValueKind.String);
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }
}