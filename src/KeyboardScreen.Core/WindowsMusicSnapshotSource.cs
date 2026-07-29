using Windows.Media.Control;
using Windows.Storage.Streams;

namespace KeyboardScreen.Core;

public sealed class WindowsMusicSnapshotSource : IMusicSnapshotSource
{
	private GlobalSystemMediaTransportControlsSessionManager? _manager;

	public async ValueTask<MusicSnapshot> ReadAsync(CancellationToken cancellationToken = default)
	{
		cancellationToken.ThrowIfCancellationRequested();
		try
		{
			_manager ??= await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
			GlobalSystemMediaTransportControlsSession? session = _manager.GetCurrentSession();
			if (session is null)
			{
				return MusicSnapshot.Unavailable;
			}
			GlobalSystemMediaTransportControlsSessionMediaProperties properties = await session.TryGetMediaPropertiesAsync();
			GlobalSystemMediaTransportControlsSessionTimelineProperties timeline = session.GetTimelineProperties();
			GlobalSystemMediaTransportControlsSessionPlaybackInfo playback = session.GetPlaybackInfo();
			byte[]? artwork = properties.Thumbnail is null
				? null
				: await ReadArtworkAsync(properties.Thumbnail, cancellationToken);
			TimeSpan duration = timeline.EndTime > timeline.StartTime
				? timeline.EndTime - timeline.StartTime
				: TimeSpan.Zero;
			return new MusicSnapshot(
				Available: true,
				string.IsNullOrWhiteSpace(properties.Title) ? "未知曲目" : properties.Title,
				properties.Artist ?? string.Empty,
				timeline.Position,
				duration,
				playback.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing,
				artwork);
		}
		catch
		{
			return MusicSnapshot.Unavailable;
		}
	}

	private static async Task<byte[]?> ReadArtworkAsync(IRandomAccessStreamReference reference, CancellationToken cancellationToken)
	{
		using IRandomAccessStreamWithContentType stream = await reference.OpenReadAsync();
		ulong size = stream.Size;
		if (size is 0 or > 5242880)
		{
			return null;
		}
		using DataReader reader = new DataReader(stream.GetInputStreamAt(0uL));
		uint length = (uint)stream.Size;
		await reader.LoadAsync(length);
		cancellationToken.ThrowIfCancellationRequested();
		byte[] array = new byte[length];
		reader.ReadBytes(array);
		return array;
	}
}
