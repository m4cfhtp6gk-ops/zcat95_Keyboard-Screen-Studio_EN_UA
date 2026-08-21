using System.Text;

namespace KeyboardScreen.Core;

/// <summary>
/// The incoming-message popup drawn over whatever theme is on the screen. The
/// caller decides what the title and preview contain (the privacy mode lives
/// upstream); this type only knows how to draw the card.
/// </summary>
public static class TelegramPopupOverlay
{
    private static readonly Color CardBackground = Color.FromArgb(246, 13, 19, 27);
    private static readonly Color CardStroke = Color.FromRgb(56, 132, 169);
    private static readonly Color Plane = Color.FromRgb(64, 165, 217);
    private static readonly Color Secondary = Color.FromRgb(170, 180, 192);

    /// <summary>
    /// Flattens line breaks, strips runes outside the Basic Multilingual Plane
    /// (the screen font has no emoji, and Skia drops the whole run when it
    /// meets one), and caps the length.
    /// </summary>
    public static string TruncatePreview(string? text, int maxLength = 96)
    {
        var builder = new StringBuilder((text ?? string.Empty).Length);
        foreach (Rune rune in (text ?? string.Empty).EnumerateRunes())
        {
            if (rune.Value > 0xFFFF)
            {
                continue;
            }

            builder.Append(rune.Value is '\n' or '\r' or '\t' ? ' ' : (char)rune.Value);
        }

        string flattened = string.Join(' ', builder.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries));
        return flattened.Length <= maxLength ? flattened : flattened[..(maxLength - 1)] + "…";
    }

    public static void Draw(ScreenCanvas canvas, string title, string? preview)
    {
        Rect safe = canvas.SafeBounds;
        bool hasPreview = !string.IsNullOrWhiteSpace(preview);
        double height = hasPreview ? 60 : 42;
        var card = new Rect(safe.Left - 4, safe.Top - 4, safe.Width + 8, height);

        canvas.RoundedRect(card, 12, CardBackground, CardStroke);
        canvas.RoundedRect(new Rect(card.Left, card.Top + 9, 3.5, card.Height - 18), 1.75, Plane);

        // A little paper plane built from strokes - no brand asset involved.
        double planeX = card.Left + 11;
        double planeY = card.Top + 10;
        canvas.Ellipse(new Rect(planeX, planeY, 22, 22), Plane);
        var glyph = new StreamGeometry();
        using (StreamGeometryContext context = glyph.Open())
        {
            context.BeginFigure(new Point(planeX + 5.5, planeY + 11.5), false, false);
            context.LineTo(new Point(planeX + 16.5, planeY + 6.5), true, true);
            context.LineTo(new Point(planeX + 10, planeY + 16), true, true);
            context.LineTo(new Point(planeX + 9, planeY + 12.5), true, true);
            context.LineTo(new Point(planeX + 5.5, planeY + 11.5), true, true);
        }
        canvas.Path(glyph, Colors.White, 1.5);

        double textLeft = planeX + 29;
        double textWidth = card.Right - 10 - textLeft;
        canvas.Text(TruncatePreview(title, 40), 12, Colors.White,
            new Point(textLeft, card.Top + (hasPreview ? 10 : 13)), FontWeights.SemiBold,
            TextAlignment.Left, textWidth, 18);

        if (hasPreview)
        {
            canvas.Text(preview!, 10, Secondary,
                new Point(card.Left + 12, card.Top + 36), FontWeights.Normal,
                TextAlignment.Left, card.Width - 24, 16);
        }
    }
}
