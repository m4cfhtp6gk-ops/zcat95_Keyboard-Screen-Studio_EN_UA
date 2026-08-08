using System.IO;
using System.Windows;
using System.Windows.Media;

namespace KeyboardScreen.Core;

public sealed class ImageTheme : IScreenTheme
{
    public string Id => "image";
    public string DisplayName => "图片时间";
    public string Description => "图片背景叠加可定制的时间、日期与天气";
    public string Details => "支持数字、大数字、方形模拟与翻页时钟，可调整位置、字号、顺序和天气信息。";
    public string? ImagePath { get; set; }

    public void Draw(ScreenCanvas canvas, SystemSnapshot snapshot)
    {
        canvas.Fill(Color.FromRgb(8, 10, 14));
        var hasImage = false;
        if (!string.IsNullOrWhiteSpace(ImagePath) && File.Exists(ImagePath))
        {
            try
            {
                canvas.Image(File.ReadAllBytes(ImagePath), new Rect(0, 0, canvas.Profile.Width, canvas.Profile.Height));
                hasImage = true;
            }
            catch
            {
                hasImage = false;
            }
        }

        if (!hasImage)
        {
            DrawPlaceholder(canvas);
            return;
        }

        switch (canvas.DisplayOptions.ImageClockStyle)
        {
            case ImageClockStyle.LargeDigital:
                DrawLargeDigitalOverlay(canvas, snapshot);
                break;
            case ImageClockStyle.Analog:
                DrawAnalogOverlay(canvas, snapshot);
                break;
            case ImageClockStyle.Flip:
                DrawFlipOverlay(canvas, snapshot);
                break;
            default:
                DrawDigitalOverlay(canvas, snapshot);
                break;
        }
    }

    private static void DrawPlaceholder(ScreenCanvas canvas)
    {
        Rect safe = canvas.SafeBounds;
        var card = new Rect(safe.Left, 126, safe.Width, 162);
        canvas.RoundedRect(card, 12, Color.FromRgb(17, 21, 27), Color.FromRgb(48, 57, 67));
        canvas.Text("图片时间", 15, canvas.AccentColor, new Point(card.Left, card.Top + 48),
            FontWeights.SemiBold, TextAlignment.Center, card.Width);
        canvas.Text("请选择一张图片", 14, Colors.White, new Point(card.Left, card.Top + 88),
            FontWeights.SemiBold, TextAlignment.Center, card.Width);
    }

    private static void DrawDigitalOverlay(ScreenCanvas canvas, SystemSnapshot snapshot)
    {
        bool weatherVisible = HasWeather(canvas, snapshot);
        int timeSize = Math.Clamp(canvas.DisplayOptions.ImageTimeFontSize, 20, 40);
        int dateSize = Math.Clamp(canvas.DisplayOptions.ImageDateFontSize, 9, 18);
        int weatherSize = Math.Clamp(canvas.DisplayOptions.ImageWeatherFontSize, 9, 18);
        DigitalRow[] rows = DigitalRows(canvas.DisplayOptions.ImageDigitalOrder)
            .Where(row => row != DigitalRow.Weather || weatherVisible)
            .ToArray();
        DrawTextRows(canvas, snapshot, rows, timeSize, dateSize, weatherSize);
    }

    private static void DrawLargeDigitalOverlay(ScreenCanvas canvas, SystemSnapshot snapshot)
    {
        bool weatherVisible = HasWeather(canvas, snapshot);
        int timeSize = Math.Clamp(canvas.DisplayOptions.ImageLargeTimeFontSize, 32, 44);
        int dateSize = Math.Clamp(canvas.DisplayOptions.ImageDateFontSize, 9, 18);
        int weatherSize = Math.Clamp(canvas.DisplayOptions.ImageWeatherFontSize, 9, 18);
        DigitalRow[] rows = weatherVisible
            ? [DigitalRow.Time, DigitalRow.Date, DigitalRow.Weather]
            : [DigitalRow.Time, DigitalRow.Date];
        DrawTextRows(canvas, snapshot, rows, timeSize, dateSize, weatherSize);
    }

    private static void DrawTextRows(
        ScreenCanvas canvas,
        SystemSnapshot snapshot,
        IReadOnlyList<DigitalRow> rows,
        int timeSize,
        int dateSize,
        int weatherSize)
    {
        double contentHeight = rows.Sum(row => RowHeight(row, timeSize, dateSize, weatherSize)) + Math.Max(0, rows.Count - 1) * 3;
        Rect card = OverlayBounds(canvas, contentHeight + 16);
        DrawOverlayBackground(canvas, card);
        Color text = PrimaryText(canvas);
        TextAlignment alignment = Alignment(canvas);
        double y = card.Top + 8;
        foreach (DigitalRow row in rows)
        {
            int fontSize = row switch
            {
                DigitalRow.Time => timeSize,
                DigitalRow.Date => dateSize,
                _ => weatherSize
            };
            double lineHeight = RowHeight(row, timeSize, dateSize, weatherSize);
            string value = row switch
            {
                DigitalRow.Time => snapshot.Timestamp.ToString("HH:mm"),
                DigitalRow.Date => DateLabel(snapshot.Timestamp),
                _ => WeatherLabel(snapshot.Weather!)
            };
            canvas.AlignedText(value, fontSize, text,
                new Rect(card.Left + 10, y, card.Width - 20, lineHeight),
                row == DigitalRow.Time ? FontWeights.SemiBold : FontWeights.Medium,
                alignment);
            y += lineHeight + 3;
        }
    }

    private static void DrawAnalogOverlay(ScreenCanvas canvas, SystemSnapshot snapshot)
    {
        bool weatherVisible = HasWeather(canvas, snapshot);
        int clockSize = Math.Clamp(canvas.DisplayOptions.ImageAnalogClockSize, 58, 104);
        clockSize = Math.Min(clockSize, (int)Math.Floor(canvas.SafeBounds.Width - 16));
        int dateSize = Math.Clamp(canvas.DisplayOptions.ImageDateFontSize, 9, 18);
        int weatherSize = Math.Clamp(canvas.DisplayOptions.ImageWeatherFontSize, 9, 18);
        AnalogRow[] rows = AnalogRows(canvas.DisplayOptions.ImageAnalogOrder)
            .Where(row => row != AnalogRow.Weather || weatherVisible)
            .ToArray();
        double contentHeight = rows.Sum(row => row switch
        {
            AnalogRow.Clock => clockSize,
            AnalogRow.Date => dateSize + 6,
            _ => weatherSize + 6
        }) + Math.Max(0, rows.Length - 1) * 5;
        Rect card = OverlayBounds(canvas, contentHeight + 16);
        DrawOverlayBackground(canvas, card);
        Color text = PrimaryText(canvas);
        TextAlignment alignment = Alignment(canvas);
        double y = card.Top + 8;
        foreach (AnalogRow row in rows)
        {
            switch (row)
            {
                case AnalogRow.Clock:
                    DrawSquareAnalogFace(canvas, snapshot.Timestamp, new Rect(card.Left + (card.Width - clockSize) / 2, y, clockSize, clockSize));
                    y += clockSize;
                    break;
                case AnalogRow.Date:
                    canvas.AlignedText(DateLabel(snapshot.Timestamp), dateSize, text,
                        new Rect(card.Left + 10, y, card.Width - 20, dateSize + 6), FontWeights.SemiBold, alignment);
                    y += dateSize + 6;
                    break;
                default:
                    canvas.AlignedText(WeatherLabel(snapshot.Weather!), weatherSize, text,
                        new Rect(card.Left + 10, y, card.Width - 20, weatherSize + 6), FontWeights.Medium, alignment);
                    y += weatherSize + 6;
                    break;
            }
            y += 5;
        }
    }

    private static void DrawSquareAnalogFace(ScreenCanvas canvas, DateTimeOffset timestamp, Rect face)
    {
        bool light = canvas.DisplayOptions.ImageTextColor == ImageTextColor.Black;
        Color faceFill = light ? Color.FromArgb(225, 246, 247, 249) : Color.FromArgb(225, 14, 15, 18);
        Color tick = light ? Colors.Black : Colors.White;
        Color stroke = light ? Color.FromArgb(110, 20, 24, 29) : Color.FromArgb(90, 255, 255, 255);
        canvas.RoundedRect(face, Math.Max(10, face.Width * 0.13), faceFill, stroke);
        var center = new Point(face.Left + face.Width / 2, face.Top + face.Height / 2);
        double half = face.Width / 2 - 8;
        for (int i = 0; i < 60; i++)
        {
            double angle = (i * 6 - 90) * Math.PI / 180;
            double dx = Math.Cos(angle);
            double dy = Math.Sin(angle);
            double scale = half / Math.Max(Math.Abs(dx), Math.Abs(dy));
            var outer = new Point(center.X + dx * scale, center.Y + dy * scale);
            double length = i % 5 == 0 ? 9 : 4;
            var inner = new Point(outer.X - dx * length, outer.Y - dy * length);
            Color mark = i % 5 == 0 ? tick : Color.FromArgb(145, tick.R, tick.G, tick.B);
            canvas.Line(inner, outer, mark, i % 5 == 0 ? 1.8 : 0.8);
        }
        double minuteAngle = (timestamp.Minute * 6 - 90) * Math.PI / 180;
        double hourAngle = (((timestamp.Hour % 12) + timestamp.Minute / 60d) * 30 - 90) * Math.PI / 180;
        canvas.Line(center, new Point(center.X + Math.Cos(hourAngle) * face.Width * 0.22, center.Y + Math.Sin(hourAngle) * face.Height * 0.22), tick, 3.4);
        canvas.Line(center, new Point(center.X + Math.Cos(minuteAngle) * face.Width * 0.32, center.Y + Math.Sin(minuteAngle) * face.Height * 0.32), canvas.AccentColor, 2.2);
        canvas.Ellipse(new Rect(center.X - 3, center.Y - 3, 6, 6), canvas.AccentColor);
    }

    private static void DrawFlipOverlay(ScreenCanvas canvas, SystemSnapshot snapshot)
    {
        bool weatherVisible = HasWeather(canvas, snapshot);
        int timeSize = Math.Clamp(canvas.DisplayOptions.ImageFlipTimeFontSize, 20, 38);
        int dateSize = Math.Clamp(canvas.DisplayOptions.ImageDateFontSize, 9, 16);
        int weatherSize = Math.Clamp(canvas.DisplayOptions.ImageWeatherFontSize, 9, 16);
        const double inset = 8;
        const double gap = 6;
        double tileHeight = Math.Max(58, timeSize + 22);
        double barHeight = Math.Max(24, Math.Max(dateSize, weatherSize) + 10);
        double height = inset + tileHeight + gap + barHeight + (weatherVisible ? gap + barHeight : 0) + inset;
        Rect area = OverlayBounds(canvas, height);
        DrawOverlayBackground(canvas, area);
        Color primary = PrimaryText(canvas);
        double tileWidth = (area.Width - inset * 2 - gap) / 2;
        var hour = new Rect(area.Left + inset, area.Top + inset, tileWidth, tileHeight);
        var minute = new Rect(hour.Right + gap, hour.Top, tileWidth, tileHeight);
        DrawFlipTile(canvas, hour, snapshot.Timestamp.ToString("HH"), primary, timeSize);
        DrawFlipTile(canvas, minute, snapshot.Timestamp.ToString("mm"), primary, timeSize);
        var dateBar = new Rect(area.Left + inset, hour.Bottom + gap, area.Width - inset * 2, barHeight);
        DrawInfoBar(canvas, dateBar, DateOnlyLabel(snapshot.Timestamp), WeekdayLabel(snapshot.Timestamp), primary, dateSize);
        if (weatherVisible)
        {
            var weatherBar = new Rect(area.Left + inset, dateBar.Bottom + gap, area.Width - inset * 2, barHeight);
            DrawInfoBar(canvas, weatherBar, WeatherLabel(snapshot.Weather!), snapshot.Weather!.LocationName, primary, weatherSize);
        }
    }

    private static void DrawFlipTile(ScreenCanvas canvas, Rect tile, string value, Color primary, int fontSize)
    {
        bool light = canvas.DisplayOptions.ImageTextColor == ImageTextColor.Black;
        Color fill = light ? Color.FromArgb(220, 244, 245, 247) : Color.FromArgb(220, 12, 16, 21);
        Color stroke = light ? Color.FromArgb(100, 30, 35, 42) : Color.FromArgb(80, 255, 255, 255);
        canvas.RoundedRect(tile, 9, fill, stroke);
        canvas.CenteredText(value, fontSize, primary, new Rect(tile.Left + 3, tile.Top, tile.Width - 6, tile.Height), FontWeights.SemiBold);
        double middle = tile.Top + tile.Height / 2;
        canvas.Line(new Point(tile.Left + 4, middle), new Point(tile.Right - 4, middle), stroke, 1);
    }

    private static void DrawInfoBar(ScreenCanvas canvas, Rect bar, string left, string right, Color text, int fontSize)
    {
        bool light = canvas.DisplayOptions.ImageTextColor == ImageTextColor.Black;
        Color fill = light ? Color.FromArgb(220, 244, 245, 247) : Color.FromArgb(220, 12, 16, 21);
        Color stroke = light ? Color.FromArgb(100, 30, 35, 42) : Color.FromArgb(80, 255, 255, 255);
        canvas.RoundedRect(bar, 7, fill, stroke);
        canvas.AlignedText(left, fontSize, text,
            new Rect(bar.Left + 7, bar.Top, bar.Width * 0.58, bar.Height), FontWeights.Medium, TextAlignment.Left);
        canvas.AlignedText(right, fontSize, text,
            new Rect(bar.Left + bar.Width * 0.58, bar.Top, bar.Width * 0.42 - 7, bar.Height), FontWeights.Medium, TextAlignment.Right);
    }

    private static Rect OverlayBounds(ScreenCanvas canvas, double height)
    {
        Rect safe = canvas.SafeBounds;
        double y = canvas.DisplayOptions.ImageTimePlacement == ImageTimePlacement.Top
            ? safe.Top + 10
            : safe.Bottom - height - 10;
        return new Rect(safe.Left, y, safe.Width, height);
    }

    private static void DrawOverlayBackground(ScreenCanvas canvas, Rect card)
    {
        if (!canvas.DisplayOptions.ImageTimeBackground)
        {
            return;
        }
        bool light = canvas.DisplayOptions.ImageTextColor == ImageTextColor.Black;
        Color fill = light ? Color.FromArgb(205, 250, 250, 250) : Color.FromArgb(190, 5, 8, 12);
        Color stroke = light ? Color.FromArgb(90, 25, 29, 35) : Color.FromArgb(80, 255, 255, 255);
        canvas.RoundedRect(card, 11, fill, stroke);
    }

    private static bool HasWeather(ScreenCanvas canvas, SystemSnapshot snapshot) =>
        canvas.DisplayOptions.ImageWeatherVisible && snapshot.Weather is { Available: true };

    private static Color PrimaryText(ScreenCanvas canvas) =>
        canvas.DisplayOptions.ImageTextColor == ImageTextColor.Black ? Colors.Black : Colors.White;

    private static TextAlignment Alignment(ScreenCanvas canvas) => canvas.DisplayOptions.ImageTextAlignment switch
    {
        ImageTextAlignment.Left => TextAlignment.Left,
        ImageTextAlignment.Right => TextAlignment.Right,
        _ => TextAlignment.Center
    };

    private static DigitalRow[] DigitalRows(ImageDigitalOrder order) => order switch
    {
        ImageDigitalOrder.TimeWeatherDate => [DigitalRow.Time, DigitalRow.Weather, DigitalRow.Date],
        ImageDigitalOrder.DateTimeWeather => [DigitalRow.Date, DigitalRow.Time, DigitalRow.Weather],
        ImageDigitalOrder.DateWeatherTime => [DigitalRow.Date, DigitalRow.Weather, DigitalRow.Time],
        ImageDigitalOrder.WeatherTimeDate => [DigitalRow.Weather, DigitalRow.Time, DigitalRow.Date],
        ImageDigitalOrder.WeatherDateTime => [DigitalRow.Weather, DigitalRow.Date, DigitalRow.Time],
        _ => [DigitalRow.Time, DigitalRow.Date, DigitalRow.Weather]
    };

    private static AnalogRow[] AnalogRows(ImageAnalogOrder order) => order switch
    {
        ImageAnalogOrder.ClockWeatherDate => [AnalogRow.Clock, AnalogRow.Weather, AnalogRow.Date],
        ImageAnalogOrder.DateClockWeather => [AnalogRow.Date, AnalogRow.Clock, AnalogRow.Weather],
        ImageAnalogOrder.DateWeatherClock => [AnalogRow.Date, AnalogRow.Weather, AnalogRow.Clock],
        ImageAnalogOrder.WeatherClockDate => [AnalogRow.Weather, AnalogRow.Clock, AnalogRow.Date],
        ImageAnalogOrder.WeatherDateClock => [AnalogRow.Weather, AnalogRow.Date, AnalogRow.Clock],
        _ => [AnalogRow.Clock, AnalogRow.Date, AnalogRow.Weather]
    };

    private static double RowHeight(DigitalRow row, int timeSize, int dateSize, int weatherSize) =>
        (row switch
        {
            DigitalRow.Time => timeSize,
            DigitalRow.Date => dateSize,
            _ => weatherSize
        }) + 6;

    private static string DateLabel(DateTimeOffset timestamp) => $"{timestamp.Month}月{timestamp.Day}日  {timestamp:dddd}";
    private static string DateOnlyLabel(DateTimeOffset timestamp) => $"{timestamp.Month}月{timestamp.Day}日";
    private static string WeekdayLabel(DateTimeOffset timestamp) => timestamp.ToString("dddd");
    private static string WeatherLabel(WeatherSnapshot weather) => $"{weather.ConditionText} {weather.TemperatureC:0}°C";

    private enum DigitalRow { Time, Date, Weather }
    private enum AnalogRow { Clock, Date, Weather }
}