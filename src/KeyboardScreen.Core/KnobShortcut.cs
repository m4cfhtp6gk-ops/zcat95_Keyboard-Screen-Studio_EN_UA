using System.Globalization;

namespace KeyboardScreen.Core;

[Flags]
public enum KnobModifiers
{
    None = 0,
    Control = 1,
    Alt = 2,
    Shift = 4,
    Windows = 8
}

/// <summary>
/// One key combination the knob can send.
///
/// This replaces a fixed F13-F24 list. That list assumed the encoder had been
/// remapped in VIA/QMK to emit those keys - the Linx68 has no such keys of its
/// own - so on a keyboard that cannot be remapped the whole mode was dead
/// weight. Capturing whatever the knob actually sends covers that case and the
/// VIA one both: F13 is simply one combination among all of them.
///
/// A bare key is allowed but not encouraged. The listener swallows whatever it
/// binds, which is right for a deliberate chord and destructive for a letter -
/// bind "M" and M stops working everywhere until the app quits.
/// </summary>
public sealed record KnobShortcut(int VirtualKey, KnobModifiers Modifiers = KnobModifiers.None)
{
    public static KnobShortcut None { get; } = new(0);

    public bool IsSet => VirtualKey != 0;

    public bool HasModifier => Modifiers != KnobModifiers.None;

    /// <summary>Round-trips through settings; see <see cref="Parse"/> for what is accepted.</summary>
    public string ToStorageString()
    {
        if (!IsSet)
        {
            return string.Empty;
        }

        var parts = new List<string>();
        if (Modifiers.HasFlag(KnobModifiers.Control)) parts.Add("Ctrl");
        if (Modifiers.HasFlag(KnobModifiers.Alt)) parts.Add("Alt");
        if (Modifiers.HasFlag(KnobModifiers.Shift)) parts.Add("Shift");
        if (Modifiers.HasFlag(KnobModifiers.Windows)) parts.Add("Win");
        parts.Add(KeyName(VirtualKey));
        return string.Join("+", parts);
    }

    /// <summary>What the settings page shows, spaced out to read as a chord.</summary>
    public string Describe() => IsSet ? ToStorageString().Replace("+", " + ") : string.Empty;

    /// <summary>
    /// Accepts what <see cref="ToStorageString"/> writes, and also a bare
    /// "F13".."F24" so a configuration written before this existed keeps working
    /// rather than silently losing the user's knob.
    /// </summary>
    public static KnobShortcut Parse(string? text)
    {
        string trimmed = (text ?? string.Empty).Trim();
        if (trimmed.Length == 0)
        {
            return None;
        }

        var modifiers = KnobModifiers.None;
        string key = trimmed;
        foreach (string part in trimmed.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            switch (part.ToLowerInvariant())
            {
                case "ctrl" or "control": modifiers |= KnobModifiers.Control; break;
                case "alt": modifiers |= KnobModifiers.Alt; break;
                case "shift": modifiers |= KnobModifiers.Shift; break;
                case "win" or "windows": modifiers |= KnobModifiers.Windows; break;
                default: key = part; break;
            }
        }

        int virtualKey = KeyCode(key);
        return virtualKey == 0 ? None : new KnobShortcut(virtualKey, modifiers);
    }

    /// <summary>
    /// A readable name where one exists, so the settings page shows "Ctrl + Alt + P"
    /// rather than a number. Anything else round-trips as VK&lt;code&gt;.
    /// </summary>
    public static string KeyName(int virtualKey) => virtualKey switch
    {
        >= 0x70 and <= 0x87 => "F" + (virtualKey - 0x70 + 1),
        >= 0x41 and <= 0x5A => ((char)virtualKey).ToString(),
        >= 0x30 and <= 0x39 => ((char)virtualKey).ToString(),
        >= 0x60 and <= 0x69 => "Num" + (virtualKey - 0x60),
        0x20 => "Space",
        0x0D => "Enter",
        0x09 => "Tab",
        0x1B => "Esc",
        0x21 => "PageUp",
        0x22 => "PageDown",
        0x23 => "End",
        0x24 => "Home",
        0x25 => "Left",
        0x26 => "Up",
        0x27 => "Right",
        0x28 => "Down",
        0x2D => "Insert",
        0x2E => "Delete",
        0xAD => "Mute",
        0xAE => "VolumeDown",
        0xAF => "VolumeUp",
        0xB0 => "MediaNext",
        0xB1 => "MediaPrev",
        0xB3 => "MediaPlay",
        _ => "VK" + virtualKey.ToString(CultureInfo.InvariantCulture)
    };

    private static int KeyCode(string name)
    {
        string key = name.Trim();
        if (key.Length == 0)
        {
            return 0;
        }

        if (key.StartsWith("VK", StringComparison.OrdinalIgnoreCase)
            && int.TryParse(key[2..], NumberStyles.Integer, CultureInfo.InvariantCulture, out int raw)
            && raw is > 0 and <= 0xFF)
        {
            return raw;
        }

        if (key.StartsWith("Num", StringComparison.OrdinalIgnoreCase)
            && key.Length == 4 && char.IsDigit(key[3]))
        {
            return 0x60 + (key[3] - '0');
        }

        if ((key[0] is 'F' or 'f') && key.Length is 2 or 3
            && int.TryParse(key[1..], NumberStyles.Integer, CultureInfo.InvariantCulture, out int number)
            && number is >= 1 and <= 24)
        {
            return 0x70 + (number - 1);
        }

        if (key.Length == 1)
        {
            char c = char.ToUpperInvariant(key[0]);
            if (c is >= 'A' and <= 'Z' or >= '0' and <= '9')
            {
                return c;
            }
        }

        foreach (int candidate in NamedKeys)
        {
            if (string.Equals(KeyName(candidate), key, StringComparison.OrdinalIgnoreCase))
            {
                return candidate;
            }
        }

        return 0;
    }

    private static readonly int[] NamedKeys =
    [
        0x20, 0x0D, 0x09, 0x1B, 0x21, 0x22, 0x23, 0x24,
        0x25, 0x26, 0x27, 0x28, 0x2D, 0x2E,
        0xAD, 0xAE, 0xAF, 0xB0, 0xB1, 0xB3
    ];

    /// <summary>True when a modifier is a virtual key in its own right, not a chord.</summary>
    public static bool IsModifierKey(int virtualKey) => virtualKey is
        0x10 or 0x11 or 0x12 or 0x5B or 0x5C          // Shift, Ctrl, Alt, Win
        or 0xA0 or 0xA1 or 0xA2 or 0xA3 or 0xA4 or 0xA5;
}
