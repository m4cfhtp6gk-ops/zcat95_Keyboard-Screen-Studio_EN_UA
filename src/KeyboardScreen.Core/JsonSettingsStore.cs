using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace KeyboardScreen.Core;

public sealed class JsonSettingsStore
{
	private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
	{
		WriteIndented = true
	};
	private readonly SemaphoreSlim _ioGate = new(1, 1);

	public string Path { get; }

	public JsonSettingsStore(string? path = null)
	{
		Path = path ?? ResolveDefaultPath();
	}

	private static string ResolveDefaultPath()
	{
		if (!File.Exists(System.IO.Path.Combine(AppContext.BaseDirectory, "portable.flag")))
		{
			return System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "KeyboardScreenStudio", "settings.json");
		}
		return System.IO.Path.Combine(AppContext.BaseDirectory, "Data", "settings.json");
	}

	public async Task<AppSettings> LoadAsync(CancellationToken cancellationToken = default(CancellationToken))
	{
		await _ioGate.WaitAsync(cancellationToken);
		try
		{
			// The last good save survives as .bak: a file torn by a crash or
			// power loss mid-write must fall back to it, not to blank settings.
			return await ReadFileAsync(Path, cancellationToken)
				?? await ReadFileAsync(BackupPath, cancellationToken)
				?? new AppSettings();
		}
		finally
		{
			_ioGate.Release();
		}
	}

	private static async Task<AppSettings?> ReadFileAsync(string path, CancellationToken cancellationToken)
	{
		try
		{
			if (!File.Exists(path))
			{
				return null;
			}
			FileStream stream = File.OpenRead(path);
			try
			{
				return await JsonSerializer.DeserializeAsync<AppSettings>(stream, JsonOptions, cancellationToken);
			}
			finally
			{
				await stream.DisposeAsync();
			}
		}
		catch (OperationCanceledException)
		{
			throw;
		}
		catch
		{
			return null;
		}
	}

	private string BackupPath => Path + ".bak";

	public async Task SaveAsync(AppSettings settings, CancellationToken cancellationToken = default(CancellationToken))
	{
		await _ioGate.WaitAsync(cancellationToken);
		try
		{
			string? directoryName = System.IO.Path.GetDirectoryName(Path);
			if (!string.IsNullOrWhiteSpace(directoryName))
			{
				Directory.CreateDirectory(directoryName);
			}
			// Write to the side, then swap in atomically: the live file is
			// never open for writing, so no crash can leave it half-written.
			string tempPath = Path + ".tmp";
			FileStream stream = File.Create(tempPath);
			try
			{
				await JsonSerializer.SerializeAsync(stream, settings, JsonOptions, cancellationToken);
				await stream.FlushAsync(cancellationToken);
			}
			finally
			{
				await stream.DisposeAsync();
			}
			if (File.Exists(Path))
			{
				File.Replace(tempPath, Path, BackupPath, ignoreMetadataErrors: true);
			}
			else
			{
				File.Move(tempPath, Path);
			}
		}
		finally
		{
			_ioGate.Release();
		}
	}
}
