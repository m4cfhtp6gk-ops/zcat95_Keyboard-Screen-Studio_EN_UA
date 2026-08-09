using System.Runtime.InteropServices;
using System;
using System.Runtime.InteropServices;
using Avalonia.Controls;

namespace KeyboardScreen.App.Avalonia.Platform;

/// <summary>
/// Lets Avalonia draw the application shell corners instead of allowing
/// Windows to clip the HWND to its smaller system-defined radius, and keeps
/// the borderless window resizable through native edge hit-testing.
/// </summary>
internal static class WindowsRoundedWindow
{
    private const int DwmWindowCornerPreference = 33;
    private const int DwmDoNotRound = 1;
    private const int WmNcHitTest = 0x0084;
    private const int HtClient = 1;
    private const int HtLeft = 10;
    private const int HtRight = 11;
    private const int HtTop = 12;
    private const int HtTopLeft = 13;
    private const int HtTopRight = 14;
    private const int HtBottom = 15;
    private const int HtBottomLeft = 16;
    private const int HtBottomRight = 17;
    private const int ResizeBorderPixels = 6;

    private static readonly SubclassProc WindowProcDelegate = WindowProc;

    public static void UseCustomCorners(Window window)
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        window.Opened += (_, _) =>
        {
            IntPtr hwnd = window.TryGetPlatformHandle()?.Handle ?? IntPtr.Zero;
            if (hwnd == IntPtr.Zero)
            {
                return;
            }

            ApplyCustomCornerPolicy(hwnd);
            EnableBorderlessResize(hwnd);
        };
    }

    private static void ApplyCustomCornerPolicy(IntPtr hwnd)
    {
        int preference = DwmDoNotRound;
        _ = DwmSetWindowAttribute(
            hwnd,
            DwmWindowCornerPreference,
            ref preference,
            Marshal.SizeOf<int>());
    }

    private static void EnableBorderlessResize(IntPtr hwnd) =>
        _ = SetWindowSubclass(hwnd, WindowProcDelegate, (UIntPtr)1, IntPtr.Zero);

    private static IntPtr WindowProc(
        IntPtr hwnd,
        uint msg,
        IntPtr wParam,
        IntPtr lParam,
        UIntPtr uIdSubclass,
        IntPtr dwRefData)
    {
        if (msg == WmNcHitTest)
        {
            IntPtr result = DefSubclassProc(hwnd, msg, wParam, lParam);
            if (result == (IntPtr)HtClient)
            {
                int screenX = (short)((uint)lParam & 0xFFFF);
                int screenY = (short)(((uint)lParam >> 16) & 0xFFFF);
                if (GetWindowRect(hwnd, out RECT rect))
                {
                    double left = screenX - rect.Left;
                    double right = rect.Right - screenX;
                    double top = screenY - rect.Top;
                    double bottom = rect.Bottom - screenY;

                    bool hitLeft = left >= 0 && left < ResizeBorderPixels;
                    bool hitRight = right >= 0 && right < ResizeBorderPixels;
                    bool hitTop = top >= 0 && top < ResizeBorderPixels;
                    bool hitBottom = bottom >= 0 && bottom < ResizeBorderPixels;

                    if (hitLeft && hitTop)
                    {
                        return (IntPtr)HtTopLeft;
                    }
                    if (hitRight && hitTop)
                    {
                        return (IntPtr)HtTopRight;
                    }
                    if (hitLeft && hitBottom)
                    {
                        return (IntPtr)HtBottomLeft;
                    }
                    if (hitRight && hitBottom)
                    {
                        return (IntPtr)HtBottomRight;
                    }
                    if (hitLeft)
                    {
                        return (IntPtr)HtLeft;
                    }
                    if (hitRight)
                    {
                        return (IntPtr)HtRight;
                    }
                    if (hitTop)
                    {
                        return (IntPtr)HtTop;
                    }
                    if (hitBottom)
                    {
                        return (IntPtr)HtBottom;
                    }
                }
            }

            return result;
        }

        return DefSubclassProc(hwnd, msg, wParam, lParam);
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(
        IntPtr hwnd,
        int attribute,
        ref int value,
        int valueSize);

    [DllImport("comctl32.dll")]
    private static extern bool SetWindowSubclass(
        IntPtr hWnd,
        SubclassProc pfnSubclass,
        UIntPtr uIdSubclass,
        IntPtr dwRefData);

    [DllImport("comctl32.dll")]
    private static extern IntPtr DefSubclassProc(
        IntPtr hWnd,
        uint uMsg,
        IntPtr wParam,
        IntPtr lParam);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    private delegate IntPtr SubclassProc(
        IntPtr hWnd,
        uint uMsg,
        IntPtr wParam,
        IntPtr lParam,
        UIntPtr uIdSubclass,
        IntPtr dwRefData);

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }
}
