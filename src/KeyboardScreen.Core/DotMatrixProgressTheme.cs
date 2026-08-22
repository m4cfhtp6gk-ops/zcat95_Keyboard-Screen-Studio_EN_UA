using System.IO;

namespace KeyboardScreen.Core;

public sealed class DotMatrixProgressTheme : IScreenTheme
{
    private static readonly FontFamily Doto = new("Doto", Path.Combine(AppContext.BaseDirectory, "Assets", "Fonts", "Doto.ttf"));

    public string Id => "clock-dot-progress";
    public string DisplayName => Loc.T("ThemeClockDotProgressName");
    public string Description => Loc.T("ThemeClockDotProgressDescription");
    public string Details => Loc.T("ThemeClockDotProgressDetails");

    public void Draw(ScreenCanvas canvas, SystemSnapshot snapshot)
    {
        Rect safe = canvas.SafeBounds;
        Color background = Color.FromRgb(5, 7, 10);
        Color idle = Color.FromRgb(28, 33, 40);
        Color secondary = Color.FromRgb(139, 149, 160);
        canvas.Fill(background);

        DotMatrixProgressPeriod period = canvas.DisplayOptions.DotMatrixProgressPeriod;
        double progress = CalculateProgress(snapshot.Timestamp, period);
        DrawHeader(canvas, safe, snapshot.Timestamp, secondary, canvas.DisplayOptions.DotMatrixProgressHeaderFontSize);
        DrawProgressDots(canvas, new Rect(safe.Left, safe.Top + 38, safe.Width, safe.Height - 96), progress, idle);

        var footer = new Rect(safe.Left, safe.Bottom - 46, safe.Width, 38);
        canvas.AlignedText($"{progress * 100:0}%", 27, Colors.White,
            new Rect(footer.Left, footer.Top, footer.Width * 0.68, footer.Height),
            FontWeights.Medium, TextAlignment.Left, Doto);
        canvas.AlignedText(GetLabel(period), 10.5, secondary,
            new Rect(footer.Left + footer.Width * 0.68, footer.Top, footer.Width * 0.32, footer.Height),
            FontWeights.SemiBold, TextAlignment.Right);
    }

    private static void DrawHeader(ScreenCanvas canvas, Rect safe, DateTimeOffset timestamp, Color secondary, int requestedFontSize)
    {
        double fontSize = Math.Clamp(requestedFontSize, 12, 20);
        var header = new Rect(safe.Left, safe.Top + 4, safe.Width, 30);
        canvas.AlignedText($"{timestamp.Month}/{timestamp.Day}", fontSize, secondary,
            new Rect(header.Left, header.Top, header.Width / 2, header.Height),
            FontWeights.Medium, TextAlignment.Left, Doto);
        canvas.AlignedText(DisplayUnits.Time(timestamp), fontSize, Colors.White,
            new Rect(header.Left + header.Width / 2, header.Top, header.Width / 2, header.Height),
            FontWeights.Medium, TextAlignment.Right, Doto);
    }
    private static void DrawProgressDots(ScreenCanvas canvas, Rect bounds, double progress, Color idle)
    {
        const int columns = 10;
        const int rows = 20;
        const double step = 12;
        int activeDots = (int)Math.Round(Math.Clamp(progress, 0, 1) * columns * rows);
        double width = (columns - 1) * step;
        double height = (rows - 1) * step;
        double left = bounds.Left + (bounds.Width - width) / 2;
        double top = bounds.Top + (bounds.Height - height) / 2;

        for (int row = 0; row < rows; row++)
        {
            for (int column = 0; column < columns; column++)
            {
                int fillIndex = (rows - 1 - row) * columns + column;
                bool active = fillIndex < activeDots;
                double diameter = active ? 6.4 : 4.4;
                double x = left + column * step;
                double y = top + row * step;
                canvas.Ellipse(new Rect(x - diameter / 2, y - diameter / 2, diameter, diameter),
                    active ? Colors.White : idle);
            }
        }
    }

    private static double CalculateProgress(DateTimeOffset timestamp, DotMatrixProgressPeriod period)
    {
        DateTimeOffset dayStart = new(timestamp.Year, timestamp.Month, timestamp.Day, 0, 0, 0, timestamp.Offset);
        DateTimeOffset start;
        DateTimeOffset end;
        switch (period)
        {
            case DotMatrixProgressPeriod.Week:
                int daysFromMonday = ((int)timestamp.DayOfWeek + 6) % 7;
                start = dayStart.AddDays(-daysFromMonday);
                end = start.AddDays(7);
                break;
            case DotMatrixProgressPeriod.Month:
                start = new DateTimeOffset(timestamp.Year, timestamp.Month, 1, 0, 0, 0, timestamp.Offset);
                end = start.AddMonths(1);
                break;
            case DotMatrixProgressPeriod.Quarter:
                int quarterMonth = ((timestamp.Month - 1) / 3) * 3 + 1;
                start = new DateTimeOffset(timestamp.Year, quarterMonth, 1, 0, 0, 0, timestamp.Offset);
                end = start.AddMonths(3);
                break;
            case DotMatrixProgressPeriod.Year:
                start = new DateTimeOffset(timestamp.Year, 1, 1, 0, 0, 0, timestamp.Offset);
                end = start.AddYears(1);
                break;
            default:
                start = dayStart;
                end = start.AddDays(1);
                break;
        }

        return Math.Clamp((timestamp - start).TotalSeconds / (end - start).TotalSeconds, 0, 1);
    }

    private static string GetLabel(DotMatrixProgressPeriod period) => period switch
    {
        DotMatrixProgressPeriod.Week => Loc.T("ScreenPeriodWeek"),
        DotMatrixProgressPeriod.Month => Loc.T("ScreenPeriodMonth"),
        DotMatrixProgressPeriod.Quarter => Loc.T("ScreenPeriodQuarter"),
        DotMatrixProgressPeriod.Year => Loc.T("ScreenPeriodYear"),
        _ => Loc.T("ScreenPeriodToday")
    };
}
