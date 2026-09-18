using System.Globalization;

namespace KeyboardScreen.Core;

public enum KnobMode
{
    /// <summary>The volume knob itself: Volume Up/Down/Mute drive the themes.</summary>
    VolumeKnob = 0,

    /// <summary>A key combination you record yourself; the volume keys are untouched.</summary>
    HotKeys = 1
}

public sealed class KnobSettings
{
    /// <summary>Off by default: no hook, no raw-input registration.</summary>
    public bool Enabled { get; set; }

    public KnobMode Mode { get; set; } = KnobMode.VolumeKnob;

    /// <summary>Swallow the volume keys while the knob switches themes.</summary>
    public bool SuppressVolume { get; set; } = true;

    /// <summary>
    /// Optional "VID:PID" of the keyboard; when set, only volume events that
    /// arrive together with a Raw Input report from this device count. Kept for
    /// settings written before the detector existed - a whole keyboard is a
    /// blunt filter, because its knob and its Fn media keys share these ids.
    /// </summary>
    public string VidPid { get; set; } = string.Empty;

    /// <summary>
    /// The HID collection the knob speaks through, as learned by the detector -
    /// this is what separates the knob from the same keyboard's other media
    /// keys. Takes precedence over <see cref="VidPid"/> when set.
    /// </summary>
    public string DeviceKey { get; set; } = string.Empty;

    /// <summary>
    /// The consumer usages the knob sends (0x00E9 Volume Up and friends). Empty
    /// - the good case - accepts every usage from the bound collection, so both
    /// rotation directions keep working; it is filled in only when the knob and
    /// the other media keys share one collection and the usage is the only
    /// thing left to separate them.
    /// </summary>
    public List<int> Usages { get; set; } = [];

    /// <summary>
    /// The combinations the knob sends, in <see cref="KnobShortcut"/> storage
    /// form. The F13-F15 defaults are what a VIA/QMK encoder remap typically
    /// emits, and stay as a sensible starting point - but any combination can
    /// be recorded now, which is the only option on a keyboard that cannot be
    /// remapped at all.
    /// </summary>
    public string KeyForward { get; set; } = "F13";

    public string KeyBackward { get; set; } = "F14";

    public string KeyToggle { get; set; } = "F15";
}

/// <summary>
/// Where one Raw Input report came from. A knob and the keyboard's own media
/// keys share a VID and PID, so the HID collection inside the device path and
/// the usage the report carries are the only two things that can tell them
/// apart - and on some firmware they are identical, which is a real answer the
/// detector has to be able to give.
/// </summary>
/// <param name="DeviceKey">Stable collection key, e.g. VID_3151&amp;PID_4015&amp;MI_01&amp;COL02.</param>
/// <param name="Usage">Consumer usage id, or 0 for a report that carried none.</param>
/// <param name="VirtualKey">Virtual-key code for keyboard reports, or 0.</param>
public readonly record struct KnobObservation(string DeviceKey, ushort Usage, int VirtualKey);

/// <summary>What the detector settled on: a HID collection, optionally narrowed to usages.</summary>
public sealed record KnobBinding(string DeviceKey, IReadOnlyList<int> Usages);

public enum KnobAction
{
    NextTheme,
    PreviousTheme,
    ToggleCarousel
}

/// <summary>
/// The pure half of the knob feature: circular theme navigation, the cycle
/// list, VID/PID parsing and device-path matching. Everything here runs in
/// the smoke tests; the P/Invoke listener stays in the Windows app project.
/// </summary>
public static class KnobControl
{
    /// <summary>
    /// The next theme id in the cycle. Wraps in both directions; an unknown
    /// or missing current id lands on the first entry; an empty list yields
    /// nothing to do.
    /// </summary>
    public static string? Next(IReadOnlyList<string> ids, string? currentId, int direction)
    {
        if (ids.Count == 0)
        {
            return null;
        }

        int index = -1;
        for (int candidate = 0; candidate < ids.Count; candidate++)
        {
            if (string.Equals(ids[candidate], currentId, StringComparison.OrdinalIgnoreCase))
            {
                index = candidate;
                break;
            }
        }

        if (index < 0)
        {
            return ids[0];
        }

        int shifted = (index + Math.Sign(direction) + ids.Count) % ids.Count;
        return ids[shifted];
    }

    /// <summary>The carousel's set when one is configured (two or more entries), otherwise the whole catalog.</summary>
    public static IReadOnlyList<string> ResolveCycleList(CarouselSettings? carousel, IReadOnlyList<string> catalogIds)
    {
        string[] configured = (carousel?.ThemeIds ?? [])
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        return configured.Length >= 2 ? configured : catalogIds;
    }

    /// <summary>Accepts "046D:C52B", "046d c52b", "VID_046D&amp;PID_C52B" and friends.</summary>
    public static bool TryParseVidPid(string? text, out ushort vid, out ushort pid)
    {
        vid = 0;
        pid = 0;
        string cleaned = (text ?? string.Empty)
            .Replace("VID_", "", StringComparison.OrdinalIgnoreCase)
            .Replace("PID_", "", StringComparison.OrdinalIgnoreCase);
        string[] parts = cleaned.Split(new[] { ':', ';', ',', ' ', '&', '/' },
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return parts.Length == 2
            && ushort.TryParse(parts[0], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out vid)
            && ushort.TryParse(parts[1], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out pid);
    }

    /// <summary>
    /// Raw Input device paths look like <c>\\?\HID#VID_3151&amp;PID_4015&amp;MI_02#...</c>;
    /// the ids are embedded as VID_xxxx / PID_xxxx tokens.
    /// </summary>
    public static bool DevicePathMatches(string? devicePath, ushort vid, ushort pid)
    {
        if (string.IsNullOrEmpty(devicePath))
        {
            return false;
        }

        string vidToken = "VID_" + vid.ToString("X4", CultureInfo.InvariantCulture);
        string pidToken = "PID_" + pid.ToString("X4", CultureInfo.InvariantCulture);
        return devicePath.Contains(vidToken, StringComparison.OrdinalIgnoreCase)
            && devicePath.Contains(pidToken, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// The part of a Raw Input device path that names the device and its HID
    /// collection. Paths look like
    /// <c>\\?\HID#VID_3151&amp;PID_4015&amp;MI_01&amp;Col02#7&amp;2f3a1b&amp;0&amp;0000#{guid}</c>:
    /// the second segment survives being replugged into another USB port, while
    /// the third is the port-specific instance and must not be part of the key.
    /// </summary>
    public static string DeviceKeyFromPath(string? devicePath)
    {
        if (string.IsNullOrWhiteSpace(devicePath))
        {
            return string.Empty;
        }

        string[] segments = devicePath.Split('#', StringSplitOptions.RemoveEmptyEntries);
        return (segments.Length >= 2 ? segments[1] : devicePath).ToUpperInvariant();
    }

    /// <summary>
    /// Whether an observed report is the bound knob. An empty device key accepts
    /// everything (the feature's behaviour before the detector existed), and an
    /// empty usage list accepts every usage from the bound collection.
    /// </summary>
    public static bool Matches(KnobObservation observed, string? boundDeviceKey, IReadOnlyList<int>? boundUsages)
    {
        if (string.IsNullOrWhiteSpace(boundDeviceKey))
        {
            return true;
        }

        if (!string.Equals(observed.DeviceKey, boundDeviceKey.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return boundUsages is not { Count: > 0 } || boundUsages.Contains(observed.Usage);
    }

    /// <summary>
    /// What to bind so the knob drives the themes and the keyboard's other media
    /// keys are left alone.
    ///
    /// The collection is tried first and on its own: binding a whole collection
    /// keeps both rotation directions working, and a knob usually sits on its
    /// own HID collection even when it shares a VID and PID with the keys. Only
    /// when the two share a collection does the usage come into it, and then
    /// every usage the other keys never send is bound, not just one.
    ///
    /// Null means the firmware sends both through one collection with the same
    /// usages. Nothing in user mode can separate them then, and the honest
    /// answer is to remap the encoder in VIA/QMK and use hot-key mode.
    /// </summary>
    public static KnobBinding? PickBinding(
        IReadOnlyCollection<KnobObservation> knob,
        IReadOnlyCollection<KnobObservation> others)
    {
        // Busiest collection first: a stray report from another device caught
        // mid-capture should not outrank the one the knob actually speaks on.
        var byCollection = knob
            .GroupBy(observation => observation.DeviceKey, StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(group => group.Count())
            .ToArray();

        foreach (var group in byCollection)
        {
            bool collectionShared = others.Any(other =>
                string.Equals(other.DeviceKey, group.Key, StringComparison.OrdinalIgnoreCase));
            if (!collectionShared)
            {
                return new KnobBinding(group.Key, []);
            }
        }

        foreach (var group in byCollection)
        {
            int[] ownUsages = group
                .Select(observation => (int)observation.Usage)
                .Distinct()
                .Where(usage => !others.Any(other =>
                    string.Equals(other.DeviceKey, group.Key, StringComparison.OrdinalIgnoreCase)
                    && other.Usage == usage))
                .OrderBy(usage => usage)
                .ToArray();
            if (ownUsages.Length > 0)
            {
                return new KnobBinding(group.Key, ownUsages);
            }
        }

        return null;
    }

    /// <summary>Consumer usages the detector is likely to see, for the settings text.</summary>
    public static string DescribeUsage(ushort usage) => usage switch
    {
        0x00E9 => Loc.T("KnobUsageVolumeUp"),
        0x00EA => Loc.T("KnobUsageVolumeDown"),
        0x00E2 => Loc.T("KnobUsageMute"),
        0 => Loc.T("KnobUsageNone"),
        _ => Loc.T("KnobUsageOther", "0x" + usage.ToString("X4", CultureInfo.InvariantCulture))
    };

    /// <summary>
    /// The stored combination for one action, or <see cref="KnobShortcut.None"/>
    /// when it has never been recorded. Old settings holding a bare "F13" parse
    /// unchanged, so upgrading does not silently unbind anyone's knob.
    /// </summary>
    public static KnobShortcut ShortcutFor(KnobSettings? settings, KnobAction action) =>
        KnobShortcut.Parse(action switch
        {
            KnobAction.NextTheme => settings?.KeyForward,
            KnobAction.PreviousTheme => settings?.KeyBackward,
            _ => settings?.KeyToggle
        });
}
