using System.Runtime.InteropServices;
using KeyboardScreen.Core;

namespace KeyboardScreen.App.Avalonia.Platform;

/// <summary>
/// Turns the Linx68 volume knob into a theme switcher.
///
/// Two Windows mechanisms cooperate. Raw Input (usage page 0x0C, Consumer
/// Control, INPUTSINK) says WHICH HID collection produced a volume event and
/// which usage it carried; the low-level keyboard hook sees the translated
/// Volume Up/Down/Mute keys and is the only one of the two that can swallow
/// them. Raw Input therefore decides whether a key belongs to the knob, and
/// the hook acts on that decision.
///
/// The distinction matters because a knob and the same keyboard's Fn media
/// keys share a VID and PID: filtering on those alone binds the whole
/// keyboard and silences its media keys too. A knob almost always sits on its
/// own HID collection, which the detector learns and this class then matches,
/// so the other keys keep controlling the volume. In hot-key mode (an encoder
/// remapped to F13-F24 in VIA/QMK) the hook listens for those keys instead and
/// volume is never involved.
///
/// Everything lives on one dedicated thread with its own message-only window
/// and message loop; the hook callback does nothing but compare a few integers
/// and hand the action to the view model's dispatcher. When the feature is off
/// this class is simply never constructed - no hook, no registration. Dispose
/// posts WM_QUIT and joins the thread, which unhooks before it exits; leaking a
/// low-level hook would make the whole system's input lag once Windows times it
/// out.
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
    private const int VkShift = 0x10;
    private const int VkControl = 0x11;
    private const int VkMenu = 0x12;
    private const int VkLeftWin = 0x5B;
    private const int VkRightWin = 0x5C;
    private const int VkVolumeMute = 0xAD;
    private const int VkVolumeDown = 0xAE;
    private const int VkVolumeUp = 0xAF;
    private const uint RidevInputSink = 0x00000100;
    private const uint RidInput = 0x10000003;
    private const uint RidiPreparsedData = 0x20000005;
    private const uint RidiDeviceName = 0x20000007;
    private const int RimTypeKeyboard = 1;
    private const int RimTypeHid = 2;
    private const ushort ConsumerUsagePage = 0x0C;
    private const ushort ConsumerControlUsage = 0x01;
    private const ushort GenericDesktopUsagePage = 0x01;
    private const ushort KeyboardUsage = 0x06;
    private const int HidPInput = 0;
    private const int HidPStatusSuccess = 0x00110000;
    private const ushort RawKeyboardBreakFlag = 0x01;

    /// <summary>How close a raw report and the hook key must be to count as one event.</summary>
    private const long DeviceMatchWindowMs = 150;

    private readonly Action<KnobAction>? _onAction;
    private readonly Action<KnobObservation>? _onObserved;
    private readonly bool _learnMode;
    private readonly KnobMode _mode;
    private readonly bool _suppressVolume;
    private readonly string _boundDeviceKey;
    private readonly IReadOnlyList<int> _boundUsages;
    private readonly bool _filterByVidPid;
    private readonly ushort _vid;
    private readonly ushort _pid;
    private readonly KnobShortcut _forward;
    private readonly KnobShortcut _backward;
    private readonly KnobShortcut _toggle;

    private readonly Thread _thread;
    private readonly WndProcDelegate _wndProc;
    private readonly HookProcDelegate _hookProc;
    private readonly object _observationGate = new();
    private readonly Dictionary<nint, string> _deviceKeys = new();
    private readonly Dictionary<nint, nint> _preparsed = new();
    private nint _hwnd;
    private nint _hook;
    private uint _threadId;
    private KnobObservation _lastObservation;
    private long _lastObservationMs;
    private volatile bool _rawInputActive;
    private volatile bool _disposed;

    public KnobInputListener(KnobSettings settings, Action<KnobAction> onAction)
        : this(settings, onAction ?? throw new ArgumentNullException(nameof(onAction)), null, learnMode: false)
    {
    }

    /// <summary>
    /// The detector's listener: it reports every consumer and keyboard report it
    /// sees and never acts on one, so learning which collection the knob speaks
    /// on can never swallow a volume key or switch a theme by accident.
    /// </summary>
    public KnobInputListener(Action<KnobObservation> onObserved)
        : this(new KnobSettings(), null, onObserved ?? throw new ArgumentNullException(nameof(onObserved)), learnMode: true)
    {
    }

    private KnobInputListener(
        KnobSettings settings,
        Action<KnobAction>? onAction,
        Action<KnobObservation>? onObserved,
        bool learnMode)
    {
        ArgumentNullException.ThrowIfNull(settings);
        _onAction = onAction;
        _onObserved = onObserved;
        _learnMode = learnMode;
        _mode = settings.Mode;
        _suppressVolume = settings.SuppressVolume;
        _boundDeviceKey = (settings.DeviceKey ?? string.Empty).Trim();
        _boundUsages = settings.Usages is { Count: > 0 } usages ? usages.ToArray() : Array.Empty<int>();
        // The old whole-keyboard filter still applies to settings written before
        // the detector existed, but a learned collection always outranks it.
        _filterByVidPid = _boundDeviceKey.Length == 0
            && KnobControl.TryParseVidPid(settings.VidPid, out _vid, out _pid);
        _forward = KnobControl.ShortcutFor(settings, KnobAction.NextTheme);
        _backward = KnobControl.ShortcutFor(settings, KnobAction.PreviousTheme);
        _toggle = KnobControl.ShortcutFor(settings, KnobAction.ToggleCarousel);

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

    private bool NeedsRawInput => _learnMode || _mode == KnobMode.VolumeKnob;

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

            if (NeedsRawInput)
            {
                RegisterForRawInput();
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

            ReleasePreparsedData();
        }
    }

    private void RegisterForRawInput()
    {
        // Consumer Control carries the knob and the media keys. The keyboard
        // page is only added while learning, so the detector can also show an
        // encoder already remapped to F13-F24; subscribing to every keystroke
        // in the system is not worth it for the steady state.
        var devices = _learnMode
            ? new[]
            {
                new RawInputDevice
                {
                    UsagePage = ConsumerUsagePage,
                    Usage = ConsumerControlUsage,
                    Flags = RidevInputSink,
                    Target = _hwnd
                },
                new RawInputDevice
                {
                    UsagePage = GenericDesktopUsagePage,
                    Usage = KeyboardUsage,
                    Flags = RidevInputSink,
                    Target = _hwnd
                }
            }
            : new[]
            {
                new RawInputDevice
                {
                    UsagePage = ConsumerUsagePage,
                    Usage = ConsumerControlUsage,
                    Flags = RidevInputSink,
                    Target = _hwnd
                }
            };
        _rawInputActive = RegisterRawInputDevices(devices, (uint)devices.Length, (uint)Marshal.SizeOf<RawInputDevice>());
    }

    private nint WndProc(nint hwnd, uint message, nint wParam, nint lParam)
    {
        if (message == WmInput)
        {
            HandleRawInput(lParam);
        }

        return DefWindowProc(hwnd, message, wParam, lParam);
    }

    /// <summary>
    /// Reads one raw report, works out which collection and usage it carries and
    /// remembers it. The hook, microseconds later, asks whether that was the
    /// bound knob.
    /// </summary>
    private void HandleRawInput(nint rawInputHandle)
    {
        try
        {
            uint headerSize = (uint)Marshal.SizeOf<RawInputHeader>();
            uint size = 0;
            GetRawInputData(rawInputHandle, RidInput, 0, ref size, headerSize);
            if (size == 0 || size > 8192)
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
                if (header.Device == 0)
                {
                    return;
                }

                string deviceKey = ResolveDeviceKey(header.Device);
                if (deviceKey.Length == 0)
                {
                    return;
                }

                nint payload = buffer + (int)headerSize;
                if (header.Type == RimTypeHid)
                {
                    ReadHidUsages(header.Device, deviceKey, payload);
                }
                else if (header.Type == RimTypeKeyboard)
                {
                    ReadKeyboardReport(deviceKey, payload);
                }
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
        }
        catch (Exception)
        {
            // Raw input parsing failures only cost the device binding; the knob
            // then behaves as it did before one was learned.
        }
    }

    private void ReadHidUsages(nint device, string deviceKey, nint payload)
    {
        int reportSize = Marshal.ReadInt32(payload);
        int reportCount = Marshal.ReadInt32(payload, 4);
        nint reports = payload + 8;
        if (reportSize <= 0 || reportCount <= 0 || reportCount > 16)
        {
            Observe(new KnobObservation(deviceKey, 0, 0));
            return;
        }

        nint preparsed = ResolvePreparsedData(device);
        bool sawUsage = false;
        for (int index = 0; index < reportCount; index++)
        {
            nint report = reports + index * reportSize;
            foreach (ushort usage in ReadUsages(preparsed, report, (uint)reportSize))
            {
                sawUsage = true;
                Observe(new KnobObservation(deviceKey, usage, 0));
            }
        }

        if (!sawUsage)
        {
            // A release report carries no usage. It still names the collection,
            // which is the part the binding is built on.
            Observe(new KnobObservation(deviceKey, 0, 0));
        }
    }

    private static ushort[] ReadUsages(nint preparsed, nint report, uint reportSize)
    {
        if (preparsed == 0)
        {
            return [];
        }

        uint capacity = HidP_MaxUsageListLength(HidPInput, ConsumerUsagePage, preparsed);
        if (capacity == 0 || capacity > 64)
        {
            return [];
        }

        ushort[] usages = new ushort[capacity];
        uint length = capacity;
        int status = HidP_GetUsages(HidPInput, ConsumerUsagePage, 0, usages, ref length, preparsed, report, reportSize);
        if (status != HidPStatusSuccess || length == 0)
        {
            return [];
        }

        return usages[..(int)Math.Min(length, capacity)];
    }

    private void ReadKeyboardReport(string deviceKey, nint payload)
    {
        // RAWKEYBOARD: MakeCode, Flags, Reserved, VKey, Message, ExtraInformation.
        ushort flags = (ushort)Marshal.ReadInt16(payload, 2);
        ushort virtualKey = (ushort)Marshal.ReadInt16(payload, 6);
        if ((flags & RawKeyboardBreakFlag) != 0 || virtualKey == 0)
        {
            return;
        }

        Observe(new KnobObservation(deviceKey, 0, virtualKey));
    }

    private void Observe(KnobObservation observation)
    {
        lock (_observationGate)
        {
            _lastObservation = observation;
            _lastObservationMs = Environment.TickCount64;
        }

        if (_learnMode)
        {
            _onObserved?.Invoke(observation);
        }
    }

    private string ResolveDeviceKey(nint device)
    {
        if (_deviceKeys.TryGetValue(device, out string? cached))
        {
            return cached;
        }

        string key = KnobControl.DeviceKeyFromPath(ReadDevicePath(device));
        // Device handles stay valid for as long as the device is attached, so a
        // handful of entries is the whole cache; it is only read on this thread.
        _deviceKeys[device] = key;
        return key;
    }

    private static string? ReadDevicePath(nint device)
    {
        uint length = 0;
        GetRawInputDeviceInfo(device, RidiDeviceName, 0, ref length);
        if (length == 0 || length > 2048)
        {
            return null;
        }

        nint buffer = Marshal.AllocHGlobal((int)length * sizeof(char));
        try
        {
            return GetRawInputDeviceInfo(device, RidiDeviceName, buffer, ref length) > 0
                ? Marshal.PtrToStringUni(buffer)
                : null;
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    private nint ResolvePreparsedData(nint device)
    {
        if (_preparsed.TryGetValue(device, out nint cached))
        {
            return cached;
        }

        nint blob = 0;
        uint size = 0;
        GetRawInputDeviceInfo(device, RidiPreparsedData, 0, ref size);
        if (size > 0 && size <= 64 * 1024)
        {
            nint buffer = Marshal.AllocHGlobal((int)size);
            if (GetRawInputDeviceInfo(device, RidiPreparsedData, buffer, ref size) > 0)
            {
                blob = buffer;
            }
            else
            {
                Marshal.FreeHGlobal(buffer);
            }
        }

        _preparsed[device] = blob;
        return blob;
    }

    private void ReleasePreparsedData()
    {
        foreach (nint blob in _preparsed.Values)
        {
            if (blob != 0)
            {
                Marshal.FreeHGlobal(blob);
            }
        }

        _preparsed.Clear();
        _deviceKeys.Clear();
    }

    /// <summary>
    /// Whether the volume key the hook is looking at belongs to the bound knob.
    ///
    /// Raw Input for a report reaches this thread's queue before the hook runs
    /// for the key it produced, so the most recent observation is the right one
    /// to ask. When it is stale the answer is no: at worst the knob changes the
    /// volume once, whereas guessing yes would swallow another device's volume
    /// key - the exact thing binding to the knob is meant to prevent.
    /// </summary>
    /// <summary>Exact match: a chord must not fire when extra modifiers are down.</summary>
    private static bool Matches(KnobShortcut shortcut, int vk, KnobModifiers held) =>
        shortcut.IsSet && shortcut.VirtualKey == vk && shortcut.Modifiers == held;

    private static KnobModifiers CurrentModifiers()
    {
        var held = KnobModifiers.None;
        if (IsDown(VkControl)) held |= KnobModifiers.Control;
        if (IsDown(VkMenu)) held |= KnobModifiers.Alt;
        if (IsDown(VkShift)) held |= KnobModifiers.Shift;
        if (IsDown(VkLeftWin) || IsDown(VkRightWin)) held |= KnobModifiers.Windows;
        return held;
    }

    private static bool IsDown(int virtualKey) => (GetAsyncKeyState(virtualKey) & 0x8000) != 0;

    private bool IsFromBoundSource()
    {
        if (_boundDeviceKey.Length == 0 && !_filterByVidPid)
        {
            return true;
        }

        // No Raw Input means no way to attribute a key to a device. Falling back
        // to the unbound behaviour keeps the knob working; refusing everything
        // would make the feature look broken with nothing on screen to say why.
        if (!_rawInputActive)
        {
            return true;
        }

        KnobObservation observation;
        lock (_observationGate)
        {
            if (Environment.TickCount64 - _lastObservationMs >= DeviceMatchWindowMs)
            {
                return false;
            }

            observation = _lastObservation;
        }

        return _boundDeviceKey.Length > 0
            ? KnobControl.Matches(observation, _boundDeviceKey, _boundUsages)
            : KnobControl.DevicePathMatches(observation.DeviceKey, _vid, _pid);
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

        // Learning watches; it never acts and never swallows, so a detector left
        // open cannot take the volume keys away.
        if (_learnMode)
        {
            return CallNextHookEx(_hook, code, wParam, lParam);
        }

        if (_mode == KnobMode.HotKeys)
        {
            // Modifiers are separate key events, so the chord is resolved by
            // asking for their live state at the moment the real key arrives.
            KnobModifiers held = CurrentModifiers();
            KnobAction? action =
                Matches(_forward, vk, held) ? KnobAction.NextTheme
                : Matches(_backward, vk, held) ? KnobAction.PreviousTheme
                : Matches(_toggle, vk, held) ? KnobAction.ToggleCarousel
                : null;

            if (action is { } bound)
            {
                if (isDown)
                {
                    _onAction?.Invoke(bound);
                }

                // Swallowed so the chord does not also reach whatever has focus.
                // Safe for a deliberate combination; the settings page warns
                // before letting a bare key be bound, because binding one takes
                // that key away from the whole system while the app runs.
                return 1;
            }

            return CallNextHookEx(_hook, code, wParam, lParam);
        }

        if ((vk is VkVolumeUp or VkVolumeDown or VkVolumeMute) && IsFromBoundSource())
        {
            if (isDown)
            {
                _onAction?.Invoke(vk == VkVolumeUp ? KnobAction.NextTheme
                    : vk == VkVolumeDown ? KnobAction.PreviousTheme
                    : KnobAction.ToggleCarousel);
            }
            if (_suppressVolume)
            {
                return 1;
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
    private static extern short GetAsyncKeyState(int vKey);

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

    [DllImport("hid.dll")]
    private static extern uint HidP_MaxUsageListLength(int reportType, ushort usagePage, nint preparsedData);

    [DllImport("hid.dll")]
    private static extern int HidP_GetUsages(int reportType, ushort usagePage, ushort linkCollection,
        [In, Out] ushort[] usageList, ref uint usageLength, nint preparsedData, nint report, uint reportLength);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern nint GetModuleHandle(string? module);

    [DllImport("kernel32.dll")]
    private static extern uint GetCurrentThreadId();
}
