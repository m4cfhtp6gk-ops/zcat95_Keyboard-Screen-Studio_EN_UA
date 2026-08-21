using System;

namespace KeyboardScreen.Core;

public sealed class MusicTheme : IScreenTheme
{
	public string Id => "music";

	public string DisplayName => Loc.T("ThemeMusicName");

	public string Description => Loc.T("ThemeMusicDescription");

	public string Details => Loc.T("ThemeMusicDetails");

	public void Draw(ScreenCanvas canvas, SystemSnapshot snapshot)
	{
		MusicSnapshot music = snapshot.Music ?? MusicSnapshot.Unavailable;
		Rect safeBounds = canvas.SafeBounds;
		Color color = Color.FromRgb(8, 10, 14);
		Color fill = Color.FromRgb(17, 21, 27);
		Color color2 = Color.FromRgb(125, 137, 150);
		Color accentColor = canvas.AccentColor;
		canvas.Fill(color);
		bool playing = music.Available && music.IsPlaying;
		bool paused = music.Available && !music.IsPlaying;
		string status = Loc.T(playing ? "ScreenMusicPlaying" : paused ? "ScreenMusicPaused" : "ScreenMusicStopped");
		ThemeHeader.Draw(canvas, snapshot, status, playing || paused ? accentColor : color2);
		Rect rect = new Rect(safeBounds.Left, safeBounds.Top + 30.0, safeBounds.Width, 150.0);
		byte[]? artwork = music.Artwork;
		if (artwork != null && artwork.Length > 0)
		{
			canvas.Image(artwork, rect, 8.0);
		}
		else
		{
			canvas.RoundedRect(rect, 8.0, fill, Color.FromRgb(35, 42, 50));
			canvas.CenteredText("MUSIC", 14.0, accentColor, rect, FontWeights.SemiBold);
		}
		canvas.Text(music.Title, 13.5, Colors.White, new Point(safeBounds.Left, 247.0), FontWeights.SemiBold, TextAlignment.Left, safeBounds.Width, 40.0);
		if (playing && !string.IsNullOrWhiteSpace(music.Artist))
		{
			canvas.Text(music.Artist, 10.5, color2, new Point(safeBounds.Left, 294.0), FontWeights.Normal, TextAlignment.Left, safeBounds.Width, 18.0);
		}
		bool live = music.Duration.TotalSeconds <= 0.0;
		double percent = live ? (playing ? 100 : 0) : (music.Position.TotalSeconds / music.Duration.TotalSeconds * 100.0);
		canvas.ProgressBar(new Rect(safeBounds.Left, 329.0, safeBounds.Width, 6.0), percent, Color.FromRgb(36, 43, 51), accentColor);
		canvas.Text(FormatTime(music.Position), 11.5, color2, new Point(safeBounds.Left, 345.0), FontWeights.SemiBold);
		if (!live)
		{
			canvas.Text(FormatTime(music.Duration), 11.5, color2, new Point(safeBounds.Left, 345.0), FontWeights.SemiBold, TextAlignment.Right, safeBounds.Width);
		}
	}

	private static string FormatTime(TimeSpan value)
	{
		return $"{(int)value.TotalMinutes}:{value.Seconds:00}";
	}
}
