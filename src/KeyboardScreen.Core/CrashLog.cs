using System;
using System.IO;

namespace KeyboardScreen.Core;

/// <summary>
/// Appends unhandled exception details to a local log file for diagnosis.
/// Portable builds write under Data/crash.log; installed builds write under
/// the per-user local application-data directory.
/// </summary>
public static class CrashLog
{
    public static string ResolveLogDirectory()
    {
        if (File.Exists(Path.Combine(AppContext.BaseDirectory, "portable.flag")))
        {
            return Path.Combine(AppContext.BaseDirectory, "Data");
        }

        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "KeyboardScreenStudio");
    }

    public static void Write(Exception exception, string? directory = null)
    {
        try
        {
            string logDirectory = directory ?? ResolveLogDirectory();
            Directory.CreateDirectory(logDirectory);
            string entry = string.Concat(
                "[",
                DateTimeOffset.Now.ToString("yyyy-MM-dd HH:mm:ss zzz"),
                "]",
                Environment.NewLine,
                exception,
                Environment.NewLine,
                new string('-', 80),
                Environment.NewLine);
            File.AppendAllText(Path.Combine(logDirectory, "crash.log"), entry);
        }
        catch
        {
            // Logging must never mask the original failure.
        }
    }
}
