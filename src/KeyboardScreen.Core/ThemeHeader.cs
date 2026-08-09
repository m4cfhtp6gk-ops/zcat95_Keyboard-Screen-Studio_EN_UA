
namespace KeyboardScreen.Core;

/// <summary>
/// 统一主题头部设计规范：标题 12pt SemiBold 位于 (Left, Top+7)，
/// 时间 14pt White 右上对齐位于 Top+5，与监控类主题完全一致。
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
        canvas.Text(
            title,
            12,
            titleColor ?? TitleColor,
            new Point(safe.Left, safe.Top + 7),
            FontWeights.SemiBold);
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
