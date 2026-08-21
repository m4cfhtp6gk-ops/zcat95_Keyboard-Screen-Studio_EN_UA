
namespace KeyboardScreen.Core;

/// <summary>
/// The shared theme header: a 12pt SemiBold title at (Left, Top+7) and a 14pt
/// white clock right-aligned at Top+5, matching the monitoring themes exactly.
/// </summary>
public static class ThemeHeader
{
    public static readonly Color TitleColor = Color.FromRgb(190, 201, 213);

    public static void Draw(
        ScreenCanvas canvas,
        SystemSnapshot snapshot,
        string title,
        Color? titleColor = null,
        bool showTime = true)
    {
        Rect safe = canvas.SafeBounds;

        // Reserve the clock's corner so a long title truncates instead of
        // running into it; translations are wider than the original Chinese.
        const double clockReserve = 46;
        canvas.Text(
            title,
            12,
            titleColor ?? TitleColor,
            new Point(safe.Left, safe.Top + 7),
            FontWeights.SemiBold,
            TextAlignment.Left,
            showTime ? safe.Width - clockReserve : safe.Width);
        if (showTime)
        {
            canvas.Text(
                snapshot.Timestamp.ToString("HH:mm"),
                14,
                Colors.White,
                new Point(safe.Left, safe.Top + 5),
                FontWeights.SemiBold,
                TextAlignment.Right,
                safe.Width);
        }
    }
}
