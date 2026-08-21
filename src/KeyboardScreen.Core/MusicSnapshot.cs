using System;

namespace KeyboardScreen.Core;

public sealed record MusicSnapshot(bool Available, string Title, string Artist, TimeSpan Position, TimeSpan Duration, bool IsPlaying, byte[]? Artwork)
{
	public static MusicSnapshot Unavailable =>
		new(Available: false, Loc.T("MusicNothingPlaying"), "", TimeSpan.Zero, TimeSpan.Zero, IsPlaying: false, null);

}
