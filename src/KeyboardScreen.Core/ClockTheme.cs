
namespace KeyboardScreen.Core;

public sealed class ClockTheme : IScreenTheme
{
    public string Id => "clock";
    public string DisplayName => Loc.T("ThemeClockName");
    public string Description => Loc.T("ThemeClockDescription");
    public string Details => Loc.T("ThemeClockDetails");

    public void Draw(ScreenCanvas canvas, SystemSnapshot snapshot)
    {
        var background = Color.FromRgb(7, 9, 12);
        var accent = canvas.AccentColor;
        var safe = canvas.SafeBounds;
        canvas.Fill(background);

        ThemeHeader.Draw(canvas, snapshot, Loc.T("ScreenTitleLocalTime"), showTime: false);
        canvas.Line(new Point(safe.Left, safe.Top + 38),
            new Point(safe.Left + 48, safe.Top + 38), Color.FromRgb(43, 50, 58), 2);

        canvas.AlignedText(DisplayUnits.Time(snapshot.Timestamp), 39, Colors.White,
            new Rect(safe.Left + 12, safe.Top + 64, safe.Width - 12, 58),
            FontWeights.SemiBold, TextAlignment.Right);
        canvas.AlignedText(snapshot.Timestamp.ToString("ss"), 20, accent,
            new Rect(safe.Left, safe.Top + 132, safe.Width, 28),
            FontWeights.SemiBold, TextAlignment.Right);

        var secondsTrack = new Rect(safe.Left + 12, safe.Top + 174, safe.Width - 12, 5);
        canvas.ProgressBar(secondsTrack, snapshot.Timestamp.Second / 59.0 * 100.0,
            Color.FromRgb(29, 35, 42), accent);

        canvas.Line(new Point(safe.Left, safe.Top + 230),
            new Point(safe.Left + 48, safe.Top + 230), Color.FromRgb(34, 41, 49), 2);
        canvas.AlignedText(Loc.DayName(snapshot.Timestamp), 15, accent,
            new Rect(safe.Left, safe.Top + 216, safe.Width, 27),
            FontWeights.SemiBold, TextAlignment.Right);
        canvas.AlignedText(Loc.LongDate(snapshot.Timestamp), 16, Colors.White,
            new Rect(safe.Left, safe.Top + 254, safe.Width, 27),
            FontWeights.Medium, TextAlignment.Right);


    }
}
