using System.Globalization;

namespace KeyboardScreen.Core;

public enum KnobMode
{
    /// <summary>The volume knob itself: Volume Up/Down/Mute drive the themes.</summary>
    VolumeKnob = 0,

    /// <summary>The encoder remapped in VIA/QMK to plain keys (F13-F24); volume is untouched.</summary>
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
    /// arrive together with a Raw Input report from this device count.
    /// </summary>
    public string VidPid { get; set; } = string.Empty;

    public string KeyForward { get; set; } = "F13";

    public string KeyBackward { get; set; } = "F14";

    public string KeyToggle { get; set; } = "F15";
}

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

    /// <summary>The keys VIA/QMK encoder remaps typically use; F13 = 0x7C.</summary>
    public static readonly IReadOnlyList<string> HotKeyNames =
        Enumerable.Range(13, 12).Select(number => "F" + number).ToArray();

    public static int? HotKeyToVirtualKey(string? name)
    {
        string trimmed = (name ?? string.Empty).Trim();
        return trimmed.Length is 3 or 2
            && trimmed[0] is 'F' or 'f'
            && int.TryParse(trimmed[1..], out int number)
            && number is >= 13 and <= 24
            ? 0x7C + (number - 13)
            : null;
    }
}
