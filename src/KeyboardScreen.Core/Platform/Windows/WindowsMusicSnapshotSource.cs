using Windows.Media.Control;
using Windows.Storage.Streams;

namespace KeyboardScreen.Core;

public sealed class WindowsMusicSnapshotSource : IMusicSnapshotSource
{
	private GlobalSystemMediaTransportControlsSessionManager? _manager;
	public bool IgnoreBrowserSessions { get; set; } = true;

	public async ValueTask<MusicSnapshot> ReadAsync(CancellationToken cancellationToken = default)
	{
		cancellationToken.ThrowIfCancellationRequested();
		try
		{
			_manager ??= await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
			GlobalSystemMediaTransportControlsSession? session = SelectSession(_manager);
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
				string.IsNullOrWhiteSpace(properties.Title) ? Loc.T("MusicUnknownTrack") : properties.Title,
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

		private GlobalSystemMediaTransportControlsSession? SelectSession(GlobalSystemMediaTransportControlsSessionManager manager)
	{
		GlobalSystemMediaTransportControlsSession? current = manager.GetCurrentSession();
		if (!IgnoreBrowserSessions || current is null || !IsBrowserSessionId(current.SourceAppUserModelId)) return current;
		return manager.GetSessions().FirstOrDefault(session => !IsBrowserSessionId(session.SourceAppUserModelId) && session.GetPlaybackInfo().PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing);
	}

	public static bool IsBrowserSessionId(string? sourceAppUserModelId)
	{
		if (string.IsNullOrWhiteSpace(sourceAppUserModelId)) return false;
		string value = sourceAppUserModelId.ToLowerInvariant();
		return value.Contains("chrome", StringComparison.Ordinal) || value.Contains("msedge", StringComparison.Ordinal) || value.Contains("microsoftedge", StringComparison.Ordinal) || value.Contains("firefox", StringComparison.Ordinal) || value.Contains("brave", StringComparison.Ordinal) || value.Contains("opera", StringComparison.Ordinal) || value.Contains("vivaldi", StringComparison.Ordinal) || value.Contains("thebrowsercompany", StringComparison.Ordinal) || value.Contains("arc.exe", StringComparison.Ordinal);
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
