using System.Runtime.InteropServices;
using Avalonia.Controls;

namespace KeyboardScreen.App.Avalonia.Platform;

/// <summary>
/// Lets Avalonia draw the application shell corners instead of allowing
/// Windows to clip the HWND to its smaller system-defined radius.
/// </summary>
internal static class WindowsRoundedWindow
{
    private const int DwmWindowCornerPreference = 33;
    private const int DwmDoNotRound = 1;

    public static void UseCustomCorners(Window window)
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        window.Opened += (_, _) => ApplyCustomCornerPolicy(window);
    }

    private static void ApplyCustomCornerPolicy(Window window)
    {
        IntPtr hwnd = window.TryGetPlatformHandle()?.Handle ?? IntPtr.Zero;
        if (hwnd == IntPtr.Zero)
        {
            return;
        }

        int preference = DwmDoNotRound;
        _ = DwmSetWindowAttribute(
            hwnd,
            DwmWindowCornerPreference,
            ref preference,
            Marshal.SizeOf<int>());
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(
        IntPtr hwnd,
        int attribute,
        ref int value,
        int valueSize);
}