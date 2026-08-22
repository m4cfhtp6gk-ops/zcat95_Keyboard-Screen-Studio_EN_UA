using System.Runtime.InteropServices;
using KeyboardScreen.Core;

namespace KeyboardScreen.App.Avalonia.Platform;

/// <summary>
/// Turns the Linx68 volume knob into a theme switcher.
///
/// Two Windows mechanisms cooperate: Raw Input (usage page 0x0C, Consumer
/// Control, INPUTSINK) tells us WHICH device produced a volume event, so an
/// optional VID/PID filter can bind the feature to the keyboard alone; the
/// low-level keyboard hook sees the translated Volume Up/Down/Mute keys and
/// is the only one of the two that can swallow them, so the system volume
/// stays put while the knob drives themes. In hot-key mode (an encoder
/// remapped to F13-F24 in VIA/QMK) the hook listens for those keys instead
/// and volume is never involved.
///
/// Everything lives on one dedicated thread with its own message-only window
/// and message loop; the hook callback does nothing but compare a few
/// integers and hand the action to the view model's dispatcher. When the
/// feature is off this class is simply never constructed - no hook, no
/// registration. Dispose posts WM_QUIT and joins the thread, which unhooks
/// before it exits; leaking a low-level hook would make the whole system's
/// input lag once Windows times it out.
/// </summary>
public sealed class KnobInputListener : IDisposable
{
    private const int WmInput = 0x00FF;
    private const int WmQuit = 0x0012;
    private const int WhKeyboardLl = 13;
    private const int HcAction = 0;
    private const int WmKeyDown = 0x0100;
    private const int WmKeyUp = 0x0101;
    private const int WmSysKeyDown = 0x0104;
    private const int WmSysKeyUp = 0x0105;
    private const int VkVolumeMute = 0xAD;
    private const int VkVolumeDown = 0xAE;
    private const int VkVolumeUp = 0xAF;
    private const uint RidevInputSink = 0x00000100;
    private const uint RidInput = 0x10000003;
    private const uint RidiDeviceName = 0x20000007;
    private const int RimTypeHid = 2;

    /// <summary>How close a raw report and the hook key must be to count as one knob event.</summary>
    private const long DeviceMatchWindowMs = 150;

    private readonly Action<KnobAction> _onAction;
    private readonly KnobMode _mode;
    private readonly bool _suppressVolume;
    private readonly bool _filterByDevice;
    private readonly ushort _vid;
    private readonly ushort _pid;
    private readonly int _forwardVk;
    private readonly int _backwardVk;
    private readonly int _toggleVk;

    private readonly Thread _thread;
    private readonly WndProcDelegate _wndProc;
    private readonly HookProcDelegate _hookProc;
    private nint _hwnd;
    private nint _hook;
    private uint _threadId;
    private long _lastDeviceEventMs;
    private volatile bool _disposed;

    public KnobInputListener(KnobSettings settings, Action<KnobAction> onAction)
    {
        ArgumentNullException.ThrowIfNull(settings);
        _onAction = onAction ?? throw new ArgumentNullException(nameof(onAction));
        _mode = settings.Mode;
        _suppressVolume = settings.SuppressVolume;
        _filterByDevice = KnobControl.TryParseVidPid(settings.VidPid, out _vid, out _pid);
        _forwardVk = KnobControl.HotKeyToVirtualKey(settings.KeyForward) ?? 0x7C;
        _backwardVk = KnobControl.HotKeyToVirtualKey(settings.KeyBackward) ?? 0x7D;
        _toggleVk = KnobControl.HotKeyToVirtualKey(settings.KeyToggle) ?? 0x7E;

        // The delegates are kept in fields for the listener's whole lifetime;
        // a collected callback under a live hook crashes the process.
        _wndProc = WndProc;
        _hookProc = HookCallback;

        _thread = new Thread(RunMessageLoop)
        {
            Name = "KSS-KnobInput",
            IsBackground = true
        };
        _thread.Start();
    }

    private void RunMessageLoop()
    {
        try
        {
            _threadId = GetCurrentThreadId();

            var windowClass = new WndClassEx
            {
                cbSize = (uint)Marshal.SizeOf<WndClassEx>(),
                lpfnWndProc = Marshal.GetFunctionPointerForDelegate(_wndProc),
                hInstance = GetModuleHandle(null),
                lpszClassName = "KssKnobWindow-" + Guid.NewGuid().ToString("N")
            };
            if (RegisterClassEx(ref windowClass) == 0)
            {
                return;
            }

            const nint HwndMessage = -3;
            _hwnd = CreateWindowEx(0, windowClass.lpszClassName, string.Empty, 0,
                0, 0, 0, 0, HwndMessage, 0, windowClass.hInstance, 0);
            if (_hwnd == 0)
            {
                return;
            }

            // Raw Input only matters when a specific device is being watched;
            // without a VID/PID every volume key counts as the knob anyway.
            if (_mode == KnobMode.VolumeKnob && _filterByDevice)
            {
                var devices = new[]
                {
                    new RawInputDevice
                    {
                        UsagePage = 0x0C, // Consumer
                        Usage = 0x01,     // Consumer Control
                        Flags = RidevInputSink,
                        Target = _hwnd
                    }
                };
                RegisterRawInputDevices(devices, 1, (uint)Marshal.SizeOf<RawInputDevice>());
            }

            _hook = SetWindowsHookEx(WhKeyboardLl, _hookProc, GetModuleHandle(null), 0);

            while (GetMessage(out Msg message, 0, 0, 0) > 0)
            {
                TranslateMessage(ref message);
                DispatchMessage(ref message);
            }
        }
        catch (Exception)
        {
            // A failed listener must never take the app down; the feature just
            // stays inert until settings are touched again.
        }
        finally
        {
            if (_hook != 0)
            {
                UnhookWindowsHookEx(_hook);
                _hook = 0;
            }
            if (_hwnd != 0)
            {
                DestroyWindow(_hwnd);
                _hwnd = 0;
            }
        }
    }

    private nint WndProc(nint hwnd, uint message, nint wParam, nint lParam)
    {
        if (message == WmInput)
        {
            NoteRawInputDevice(lParam);
        }

        return DefWindowProc(hwnd, message, wParam, lParam);
    }

    /// <summary>Records when the watched device speaks; the hook correlates by time.</summary>
    private void NoteRawInputDevice(nint rawInputHandle)
    {
        try
        {
            uint size = 0;
            uint headerSize = (uint)Marshal.SizeOf<RawInputHeader>();
            GetRawInputData(rawInputHandle, RidInput, 0, ref size, headerSize);
            if (size == 0 || size > 1024)
            {
                return;
            }

            nint buffer = Marshal.AllocHGlobal((int)size);
            try
            {
                if (GetRawInputData(rawInputHandle, RidInput, buffer, ref size, headerSize) != size)
                {
                    return;
                }

                RawInputHeader header = Marshal.PtrToStructure<RawInputHeader>(buffer);
                if (header.Type != RimTypeHid || header.Device == 0)
                {
                    return;
                }

                if (DeviceMatches(header.Device))
                {
                    Interlocked.Exchange(ref _lastDeviceEventMs, Environment.TickCount64);
                }
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
        }
        catch (Exception)
        {
            // Raw input parsing failures only cost the device binding.
        }
    }

    private bool DeviceMatches(nint device)
    {
        uint length = 0;
        GetRawInputDeviceInfo(device, RidiDeviceName, 0, ref length);
        if (length == 0 || length > 1024)
        {
            return false;
        }

        nint buffer = Marshal.AllocHGlobal((int)length * sizeof(char));
        try
        {
            if (GetRawInputDeviceInfo(device, RidiDeviceName, buffer, ref length) <= 0)
            {
                return false;
            }

            string? path = Marshal.PtrToStringUni(buffer);
            return KnobControl.DevicePathMatches(path, _vid, _pid);
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    /// <summary>
    /// Runs inside every key press in the system: nothing but integer
    /// comparisons and, on a match, handing the action off. Returning 1
    /// swallows the key.
    /// </summary>
    private nint HookCallback(int code, nint wParam, nint lParam)
    {
        if (code != HcAction || _disposed)
        {
            return CallNextHookEx(_hook, code, wParam, lParam);
        }

        int vk = Marshal.ReadInt32(lParam); // KBDLLHOOKSTRUCT.vkCode is the first field
        int keyMessage = (int)wParam;
        bool isDown = keyMessage is WmKeyDown or WmSysKeyDown;
        bool isUp = keyMessage is WmKeyUp or WmSysKeyUp;
        if (!isDown && !isUp)
        {
            return CallNextHookEx(_hook, code, wParam, lParam);
        }

        if (_mode == KnobMode.HotKeys)
        {
            if (vk == _forwardVk || vk == _backwardVk || vk == _toggleVk)
            {
                if (isDown)
                {
                    _onAction(vk == _forwardVk ? KnobAction.NextTheme
                        : vk == _backwardVk ? KnobAction.PreviousTheme
                        : KnobAction.ToggleCarousel);
                }
                return 1; // a dedicated F13-F24 key has no other job
            }

            return CallNextHookEx(_hook, code, wParam, lParam);
        }

        if (vk is VkVolumeUp or VkVolumeDown or VkVolumeMute)
        {
            bool fromKnob = !_filterByDevice ||
                Environment.TickCount64 - Interlocked.Read(ref _lastDeviceEventMs) < DeviceMatchWindowMs;
            if (fromKnob)
            {
                if (isDown)
                {
                    _onAction(vk == VkVolumeUp ? KnobAction.NextTheme
                        : vk == VkVolumeDown ? KnobAction.PreviousTheme
                        : KnobAction.ToggleCarousel);
                }
                if (_suppressVolume)
                {
                    return 1;
                }
            }
        }

        return CallNextHookEx(_hook, code, wParam, lParam);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }
        _disposed = true;

        if (_threadId != 0)
        {
            PostThreadMessage(_threadId, WmQuit, 0, 0);
        }
        if (!_thread.Join(TimeSpan.FromSeconds(2)))
        {
            // A stuck loop is abandoned as a background thread; the hook is
            // released with the process either way.
        }
    }

    private delegate nint WndProcDelegate(nint hwnd, uint message, nint wParam, nint lParam);
    private delegate nint HookProcDelegate(int code, nint wParam, nint lParam);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct WndClassEx
    {
        public uint cbSize;
        public uint style;
        public nint lpfnWndProc;
        public int cbClsExtra;
        public int cbWndExtra;
        public nint hInstance;
        public nint hIcon;
        public nint hCursor;
        public nint hbrBackground;
        [MarshalAs(UnmanagedType.LPWStr)] public string? lpszMenuName;
        [MarshalAs(UnmanagedType.LPWStr)] public string lpszClassName;
        public nint hIconSm;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RawInputDevice
    {
        public ushort UsagePage;
        public ushort Usage;
        public uint Flags;
        public nint Target;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RawInputHeader
    {
        public int Type;
        public int Size;
        public nint Device;
        public nint WParam;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Msg
    {
        public nint Hwnd;
        public uint Message;
        public nint WParam;
        public nint LParam;
        public uint Time;
        public int X;
        public int Y;
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern ushort RegisterClassEx(ref WndClassEx windowClass);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern nint CreateWindowEx(uint exStyle, string className, string windowName,
        uint style, int x, int y, int width, int height, nint parent, nint menu, nint instance, nint param);

    [DllImport("user32.dll")]
    private static extern bool DestroyWindow(nint hwnd);

    [DllImport("user32.dll")]
    private static extern nint DefWindowProc(nint hwnd, uint message, nint wParam, nint lParam);

    [DllImport("user32.dll")]
    private static extern int GetMessage(out Msg message, nint hwnd, uint filterMin, uint filterMax);

    [DllImport("user32.dll")]
    private static extern bool TranslateMessage(ref Msg message);

    [DllImport("user32.dll")]
    private static extern nint DispatchMessage(ref Msg message);

    [DllImport("user32.dll")]
    private static extern bool PostThreadMessage(uint threadId, uint message, nint wParam, nint lParam);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterRawInputDevices(RawInputDevice[] devices, uint count, uint size);

    [DllImport("user32.dll")]
    private static extern uint GetRawInputData(nint rawInput, uint command, nint data, ref uint size, uint headerSize);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetRawInputDeviceInfo(nint device, uint command, nint data, ref uint size);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern nint SetWindowsHookEx(int hookId, HookProcDelegate callback, nint module, uint threadId);

    [DllImport("user32.dll")]
    private static extern bool UnhookWindowsHookEx(nint hook);

    [DllImport("user32.dll")]
    private static extern nint CallNextHookEx(nint hook, int code, nint wParam, nint lParam);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern nint GetModuleHandle(string? module);

    [DllImport("kernel32.dll")]
    private static extern uint GetCurrentThreadId();
}
