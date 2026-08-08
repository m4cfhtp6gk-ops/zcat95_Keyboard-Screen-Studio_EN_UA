using System.IO;

namespace KeyboardScreen.Core;

public sealed class DotMatrixAnalogClockTheme : IScreenTheme
{
    private static readonly FontFamily Doto = new("Doto", Path.Combine(AppContext.BaseDirectory, "Assets", "Fonts", "Doto.ttf"));


    public string Id => "clock-dot-analog";
    public string DisplayName => "点阵模拟时钟";
    public string Description => "点阵指针表盘与高密度状态信息";
    public string Details => "上方使用正方形点阵模拟指针时钟，下方集中显示数字时间、日期和电脑状态摘要。";

    public void Draw(ScreenCanvas canvas, SystemSnapshot snapshot)
    {
        Rect safe = canvas.SafeBounds;
        Color background = Color.FromRgb(5, 7, 10);
        Color idleDot = Color.FromRgb(28, 33, 40);
        Color secondary = Color.FromRgb(139, 149, 160);
        Color primary = Colors.White;

        canvas.Fill(background);

        double dialSize = Math.Min(safe.Width, 122);
        var dial = new Rect(safe.Left + (safe.Width - dialSize) / 2, safe.Top + 4, dialSize, dialSize);
        DrawDial(canvas, dial, snapshot.Timestamp, idleDot, primary);

        double timeTop = dial.Bottom + 12;
        DrawDotMatrixTime(canvas,
            new Rect(safe.Left, timeTop, safe.Width, 48),
            snapshot.Timestamp,
            primary);

        double dateTop = timeTop + 53;
        canvas.AlignedText(snapshot.Timestamp.ToString("dddd"), 11, canvas.AccentColor,
            new Rect(safe.Left, dateTop, safe.Width * 0.42, 18), FontWeights.SemiBold, TextAlignment.Left);
        canvas.AlignedText($"{snapshot.Timestamp.Month}月{snapshot.Timestamp.Day}日", 11, primary,
            new Rect(safe.Left + safe.Width * 0.42, dateTop, safe.Width * 0.58, 18), FontWeights.SemiBold, TextAlignment.Right);

        DrawDottedRule(canvas, safe.Left, safe.Right, dateTop + 28, idleDot);

        double metricTop = dateTop + 43;
        DrawMetric(canvas, new Rect(safe.Left, metricTop, safe.Width / 2 - 4, 48), "CPU", snapshot.CpuPercent, secondary, primary);
        DrawMetric(canvas, new Rect(safe.Left + safe.Width / 2 + 4, metricTop, safe.Width / 2 - 4, 48), "MEM", snapshot.MemoryPercent, secondary, primary);

        double networkTop = metricTop + 58;
        canvas.AlignedText("DOWN", 9, secondary,
            new Rect(safe.Left, networkTop, safe.Width * 0.5, 15), FontWeights.SemiBold, TextAlignment.Left);
        canvas.AlignedText($"{snapshot.DownloadMbps:0.0}M", 12, primary,
            new Rect(safe.Left, networkTop + 16, safe.Width * 0.5, 19), FontWeights.Medium, TextAlignment.Left, Doto);
        canvas.AlignedText("UP", 9, secondary,
            new Rect(safe.Left + safe.Width * 0.5, networkTop, safe.Width * 0.5, 15), FontWeights.SemiBold, TextAlignment.Right);
        canvas.AlignedText($"{snapshot.UploadMbps:0.0}M", 12, primary,
            new Rect(safe.Left + safe.Width * 0.5, networkTop + 16, safe.Width * 0.5, 19), FontWeights.Medium, TextAlignment.Right, Doto);

        DrawSecondProgress(canvas, new Rect(safe.Left, safe.Bottom - 7, safe.Width, 4), snapshot.Timestamp.Second, idleDot);
    }

    private static void DrawDial(ScreenCanvas canvas, Rect bounds, DateTimeOffset timestamp, Color idleDot, Color primary)
    {
        const int gridSize = 13;
        double spacing = bounds.Width / (gridSize - 1);

        for (int row = 0; row < gridSize; row++)
        {
            for (int column = 0; column < gridSize; column++)
            {
                double x = bounds.Left + column * spacing;
                double y = bounds.Top + row * spacing;
                double diameter = column is 0 or gridSize - 1 || row is 0 or gridSize - 1 ? 2.2 : 1.6;
                canvas.Ellipse(new Rect(x - diameter / 2, y - diameter / 2, diameter, diameter), idleDot);
            }
        }

        Point center = new(bounds.Left + bounds.Width / 2, bounds.Top + bounds.Height / 2);
        double hourAngle = ((timestamp.Hour % 12) + timestamp.Minute / 60.0) * 30 - 90;
        double minuteAngle = (timestamp.Minute + timestamp.Second / 60.0) * 6 - 90;
        double secondAngle = timestamp.Second * 6 - 90;

        DrawDotHand(canvas, center, hourAngle, bounds.Width * 0.27, 7, 4.2, primary);
        DrawDotHand(canvas, center, minuteAngle, bounds.Width * 0.39, 7, 3.5, canvas.AccentColor);
        DrawDotHand(canvas, center, secondAngle, bounds.Width * 0.43, 6, 2.2,
            Color.FromArgb(190, canvas.AccentColor.R, canvas.AccentColor.G, canvas.AccentColor.B));

        foreach (double angle in new[] { -90d, 0d, 90d, 180d })
        {
            Point point = PointOnCircle(center, bounds.Width * 0.46, angle);
            canvas.Ellipse(new Rect(point.X - 2.6, point.Y - 2.6, 5.2, 5.2), canvas.AccentColor);
        }

        canvas.Ellipse(new Rect(center.X - 4, center.Y - 4, 8, 8), primary);
        canvas.Ellipse(new Rect(center.X - 1.8, center.Y - 1.8, 3.6, 3.6), canvas.AccentColor);
    }

    private static void DrawDotMatrixTime(ScreenCanvas canvas, Rect bounds, DateTimeOffset timestamp, Color color)
    {
        double centerX = bounds.Left + bounds.Width / 2;
        const double colonHalfGap = 7;

        canvas.AlignedText(timestamp.ToString("HH"), 40, color,
            new Rect(bounds.Left, bounds.Top, centerX - bounds.Left - colonHalfGap, bounds.Height),
            FontWeights.Medium, TextAlignment.Right, Doto);
        canvas.AlignedText(timestamp.ToString("mm"), 40, color,
            new Rect(centerX + colonHalfGap, bounds.Top, bounds.Right - centerX - colonHalfGap, bounds.Height),
            FontWeights.Medium, TextAlignment.Left, Doto);

        double centerY = bounds.Top + bounds.Height / 2;
        const double diameter = 3.6;
        foreach (double offset in new[] { -7.2, 7.2 })
        {
            canvas.Ellipse(new Rect(
                centerX - diameter / 2,
                centerY + offset - diameter / 2,
                diameter,
                diameter), color);
        }
    }
    private static void DrawDotHand(ScreenCanvas canvas, Point center, double angle, double length, double step, double diameter, Color color)
    {
        double radians = angle * Math.PI / 180.0;
        for (double distance = step; distance <= length; distance += step)
        {
            double x = center.X + Math.Cos(radians) * distance;
            double y = center.Y + Math.Sin(radians) * distance;
            canvas.Ellipse(new Rect(x - diameter / 2, y - diameter / 2, diameter, diameter), color);
        }
    }

    private static Point PointOnCircle(Point center, double radius, double angle)
    {
        double radians = angle * Math.PI / 180.0;
        return new Point(center.X + Math.Cos(radians) * radius, center.Y + Math.Sin(radians) * radius);
    }

    private static void DrawDottedRule(ScreenCanvas canvas, double left, double right, double y, Color color)
    {
        const double diameter = 2;
        const double gap = 6;
        for (double x = left; x <= right - diameter; x += gap)
        {
            canvas.Ellipse(new Rect(x, y, diameter, diameter), color);
        }
    }

    private static void DrawMetric(ScreenCanvas canvas, Rect bounds, string label, double value, Color secondary, Color primary)
    {
        canvas.AlignedText(label, 9, secondary,
            new Rect(bounds.Left, bounds.Top, bounds.Width, 14), FontWeights.SemiBold, TextAlignment.Left);
        canvas.AlignedText($"{value:0}%", 17, primary,
            new Rect(bounds.Left, bounds.Top + 15, bounds.Width, 24), FontWeights.Medium, TextAlignment.Left, Doto);

        int lit = (int)Math.Round(Math.Clamp(value, 0, 100) / 20.0);
        double y = bounds.Bottom - 3;
        for (int index = 0; index < 5; index++)
        {
            Color color = index < lit ? canvas.AccentColor : Color.FromRgb(28, 33, 40);
            canvas.Ellipse(new Rect(bounds.Left + index * 8, y, 3, 3), color);
        }
    }

    private static void DrawSecondProgress(ScreenCanvas canvas, Rect bounds, int second, Color idle)
    {
        const int dotCount = 15;
        double step = bounds.Width / (dotCount - 1);
        int active = (int)Math.Ceiling((second + 1) / 60.0 * dotCount);
        for (int index = 0; index < dotCount; index++)
        {
            double diameter = index < active ? 3.6 : 2.4;
            Color color = index < active ? canvas.AccentColor : idle;
            double x = bounds.Left + index * step;
            canvas.Ellipse(new Rect(x - diameter / 2, bounds.Top, diameter, diameter), color);
        }
    }
}