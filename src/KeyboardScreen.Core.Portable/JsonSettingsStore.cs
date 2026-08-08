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
		_ = 1;
		try
		{
			if (!File.Exists(Path))
			{
				return new AppSettings();
			}
			FileStream stream = File.OpenRead(Path);
			AppSettings result;
			try
			{
				result = (await JsonSerializer.DeserializeAsync<AppSettings>(stream, JsonOptions, cancellationToken)) ?? new AppSettings();
			}
			finally
			{
				if (stream != null)
				{
					await stream.DisposeAsync();
				}
			}
			return result;
		}
		catch
		{
			return new AppSettings();
		}
	}

	public async Task SaveAsync(AppSettings settings, CancellationToken cancellationToken = default(CancellationToken))
	{
		string? directoryName = System.IO.Path.GetDirectoryName(Path);
		if (!string.IsNullOrWhiteSpace(directoryName))
		{
			Directory.CreateDirectory(directoryName);
		}
		FileStream stream = File.Create(Path);
		try
		{
			await JsonSerializer.SerializeAsync(stream, settings, JsonOptions, cancellationToken);
		}
		finally
		{
			if (stream != null)
			{
				await stream.DisposeAsync();
			}
		}
	}
}
