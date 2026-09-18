namespace KeyboardScreen.Core;

/// <summary>
/// The colour of a usage bar, as a ramp from green to red rather than three
/// steps.
///
/// The old rule was accent below 75, amber to 90, red above. That reads as
/// "fine, fine, fine, suddenly bad": the bar looked identical at 4% and at 74%,
/// so the only thing carrying the change was the number beside it. A ramp makes
/// the colour say the same thing as the length, which is what a meter is for.
///
/// The stops are picked so the mid-range is unmistakably amber rather than a
/// muddy olive - interpolating green straight to red passes through exactly the
/// colour nobody can read a value from.
/// </summary>
public static class ClaudeUsagePalette
{
    private static readonly Color Low = Color.FromRgb(63, 185, 80);
    private static readonly Color Middle = Color.FromRgb(227, 179, 65);
    private static readonly Color High = Color.FromRgb(238, 78, 88);

    /// <summary>Where the ramp is fully amber, before it turns towards red.</summary>
    private const double MiddleAt = 55;

    public static Color ForPercent(double percent)
    {
        double value = Math.Clamp(percent, 0, 100);
        return value <= MiddleAt
            ? Mix(Low, Middle, value / MiddleAt)
            : Mix(Middle, High, (value - MiddleAt) / (100 - MiddleAt));
    }

    private static Color Mix(Color from, Color to, double amount)
    {
        double t = Math.Clamp(amount, 0, 1);
        return Color.FromRgb(
            (byte)Math.Round(from.R + (to.R - from.R) * t),
            (byte)Math.Round(from.G + (to.G - from.G) * t),
            (byte)Math.Round(from.B + (to.B - from.B) * t));
    }
}
