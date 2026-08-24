using System.IO;

namespace KeyboardScreen.Core;

/// <summary>
/// A restrained Nothing-inspired clock for the 142 x 428 keyboard display.
/// Doto is deliberately local to this theme so user-selected fonts cannot
/// destroy the dot-matrix numeral construction.
/// </summary>
public sealed class DotMatrixClockTheme : IScreenTheme
{
    private static readonly FontFamily Doto = new("Doto", Path.Combine(AppContext.BaseDirectory, "Assets", "Fonts", "Doto.ttf"));

    private const double HourHeight = 80;
    private const double MinuteHeight = 80;
    private const double SecondHeight = 74;

    /// <summary>Gap between a value and the rule under it.</summary>
    private const double DividerDrop = 8;

    /// <summary>Gap between a rule and the next value.</summary>
    private const double SectionGap = 22;

    /// <summary>
    /// The strip DrawSecondTicks draws into, measured from the safe bottom. Must
    /// match that method, or the column centres against a gap that is not there.
    /// </summary>
    private const double TickReserve = 13;

    public string Id => "clock-dot-matrix";
    public string DisplayName => Loc.T("ThemeClockDotMatrixName");
    public string Description => Loc.T("ThemeClockDotMatrixDescription");
    public string Details => Loc.T("ThemeClockDotMatrixDetails");

    public void Draw(ScreenCanvas canvas, SystemSnapshot snapshot)
    {
        Rect safe = canvas.SafeBounds;
        Color divider = Color.FromRgb(34, 34, 34);

        canvas.Fill(Colors.Black);

        // No "Hours / Minutes / Seconds" captions. Three numbers stacked largest
        // to smallest, the last one ticking in the accent colour, already say
        // which is which; the captions were also the only text here that needed
        // translating. Dropping them frees 54 px, so the column is centred in
        // the safe area rather than left hanging from the top.
        double blockHeight = HourHeight + DividerDrop + SectionGap
            + MinuteHeight + DividerDrop + SectionGap
            + SecondHeight;
        double top = safe.Top + Math.Max(0, (safe.Height - TickReserve - blockHeight) / 2);

        top = DrawValue(canvas, safe, top, DisplayUnits.Hours(snapshot.Timestamp),
            67, HourHeight, Colors.White, divider);
        top = DrawValue(canvas, safe, top, snapshot.Timestamp.ToString("mm"),
            67, MinuteHeight, Colors.White, divider);
        canvas.AlignedText(snapshot.Timestamp.ToString("ss"), 59, canvas.AccentColor,
            new Rect(safe.Left, top, safe.Width, SecondHeight),
            FontWeights.Medium, TextAlignment.Left, Doto);

        DrawSecondTicks(canvas, safe, snapshot.Timestamp.Second, divider);
    }

    /// <summary>Draws one value with its rule and returns where the next one starts.</summary>
    private static double DrawValue(ScreenCanvas canvas, Rect safe, double top,
        string value, double size, double height, Color color, Color dividerColor)
    {
        canvas.AlignedText(value, size, color,
            new Rect(safe.Left, top, safe.Width, height),
            FontWeights.Medium, TextAlignment.Left, Doto);
        double ruleY = top + height + DividerDrop;
        canvas.Line(new Point(safe.Left, ruleY), new Point(safe.Left + 24, ruleY), dividerColor, 2);
        return ruleY + SectionGap;
    }

    private static void DrawSecondTicks(ScreenCanvas canvas, Rect safe, int second, Color emptyColor)
    {
        const int segmentCount = 10;
        const double gap = 3;
        double width = (safe.Width - gap * (segmentCount - 1)) / segmentCount;
        int filled = (int)Math.Ceiling((second + 1) / 60.0 * segmentCount);
        double top = safe.Bottom - 13;

        for (int index = 0; index < segmentCount; index++)
        {
            Color color = index < filled ? canvas.AccentColor : emptyColor;
            canvas.RoundedRect(new Rect(safe.Left + index * (width + gap), top, width, 4), 0, color);
        }
    }
}
