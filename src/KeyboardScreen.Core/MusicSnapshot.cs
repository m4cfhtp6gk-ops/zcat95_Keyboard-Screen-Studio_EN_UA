using System;

namespace KeyboardScreen.Core;

public sealed record MusicSnapshot(bool Available, string Title, string Artist, TimeSpan Position, TimeSpan Duration, bool IsPlaying, byte[]? Artwork)
{
	public static MusicSnapshot Unavailable { get; } = new MusicSnapshot(Available: false, "没有正在播放的音乐", "", TimeSpan.Zero, TimeSpan.Zero, IsPlaying: false, null);

}
