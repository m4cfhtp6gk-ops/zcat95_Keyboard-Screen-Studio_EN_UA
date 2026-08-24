using System.Text.Json;

namespace KeyboardScreen.Core;

/// <summary>Tokens counted for each limit window, all from local transcripts.</summary>
/// <param name="SessionOldest">
/// The oldest record still inside the five-hour window. A rolling window empties
/// as its oldest entry ages out, so this is what a reset countdown is measured
/// from - there is no server-issued reset time to read.
/// </param>
/// <param name="WeekOldest">The same, for the seven-day window.</param>
/// <param name="LastActivity">The newest record seen in either window.</param>
public sealed record ClaudeCodeTokenTotals(
    long Session,
    long Week,
    long ModelWeek,
    bool Available,
    DateTimeOffset? SessionOldest = null,
    DateTimeOffset? WeekOldest = null,
    DateTimeOffset? LastActivity = null)
{
    public static ClaudeCodeTokenTotals None { get; } = new(0, 0, 0, false);
}

/// <summary>
/// Sums tokens from the Claude Code transcripts under <c>~/.claude/projects</c>.
///
/// This is the whole data source for the Claude screen. Nothing on this machine
/// records the account's limit windows, so nothing can be read from a server
/// without a credential that expires or a challenge that blocks - the
/// transcripts are the one place where real, current numbers sit in the clear.
/// They only ever cover Claude Code on this machine: treat every total as a
/// floor on account usage, never the account total.
///
/// Cache reads are excluded. They are discounted heavily on the server and run
/// one to two orders of magnitude above every other field, so including them
/// would swamp the figure and make it read as alarm rather than information.
/// </summary>
public sealed class ClaudeCodeTokenReader
{
    /// <summary>Stop after this many bytes so a large history cannot stall a refresh.</summary>
    private const long ScanBudgetBytes = 256L * 1024 * 1024;

    /// <summary>
    /// How long a scan is reused. The screen refreshes far more often than a
    /// transcript changes, and re-walking every file each time would put a disk
    /// sweep on a two-minute timer for no new information.
    /// </summary>
    private static readonly TimeSpan ScanCacheDuration = TimeSpan.FromSeconds(90);

    /// <summary>One assistant record: enough to re-slice any window without rescanning.</summary>
    private readonly record struct Entry(DateTimeOffset Timestamp, string Model, long Tokens);

    private readonly string _projectsDirectory;
    private readonly object _gate = new();

    private List<Entry>? _cached;
    private DateTimeOffset _cachedAt;
    private DateTimeOffset _cachedFrom;

    public ClaudeCodeTokenReader(string? projectsDirectory = null)
    {
        _projectsDirectory = projectsDirectory ?? DefaultProjectsDirectory();
    }

    public static string DefaultProjectsDirectory()
    {
        string? configured = Environment.GetEnvironmentVariable("CLAUDE_CONFIG_DIR");
        string root = string.IsNullOrWhiteSpace(configured)
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".claude")
            : configured;
        return Path.Combine(root, "projects");
    }

    /// <summary>True when Claude Code has ever written transcripts here.</summary>
    public bool TranscriptsExist => Directory.Exists(_projectsDirectory);

    public string ProjectsDirectory => _projectsDirectory;

    public ClaudeCodeTokenTotals Read(
        DateTimeOffset sessionSince,
        DateTimeOffset weekSince,
        string? modelScope)
    {
        if (!Directory.Exists(_projectsDirectory))
        {
            return ClaudeCodeTokenTotals.None;
        }

        IReadOnlyList<Entry> entries = GetEntries(weekSince);
        if (entries.Count == 0)
        {
            // The directory exists but held nothing in the window. That is a real
            // answer - zero usage - not a failure to read, so report it as such.
            return new ClaudeCodeTokenTotals(0, 0, 0, true);
        }

        long session = 0;
        long week = 0;
        long modelWeek = 0;
        DateTimeOffset? sessionOldest = null;
        DateTimeOffset? weekOldest = null;
        DateTimeOffset? lastActivity = null;
        string scope = (modelScope ?? string.Empty).Trim().ToLowerInvariant();

        foreach (Entry entry in entries)
        {
            if (entry.Timestamp < weekSince)
            {
                continue;
            }

            week += entry.Tokens;
            if (weekOldest is null || entry.Timestamp < weekOldest)
            {
                weekOldest = entry.Timestamp;
            }

            if (lastActivity is null || entry.Timestamp > lastActivity)
            {
                lastActivity = entry.Timestamp;
            }

            if (entry.Timestamp >= sessionSince)
            {
                session += entry.Tokens;
                if (sessionOldest is null || entry.Timestamp < sessionOldest)
                {
                    sessionOldest = entry.Timestamp;
                }
            }

            if (scope.Length > 0 && entry.Model.Contains(scope, StringComparison.Ordinal))
            {
                modelWeek += entry.Tokens;
            }
        }

        return new ClaudeCodeTokenTotals(
            session,
            week,
            modelWeek,
            true,
            sessionOldest,
            weekOldest,
            lastActivity);
    }

    /// <summary>
    /// The scanned records covering <paramref name="weekSince"/> onwards. A cached
    /// scan is reused while it is young enough and still reaches far enough back;
    /// a window that starts earlier than the cache covers forces a fresh sweep.
    /// </summary>
    private IReadOnlyList<Entry> GetEntries(DateTimeOffset weekSince)
    {
        lock (_gate)
        {
            if (_cached is not null
                && _cachedFrom <= weekSince
                && DateTimeOffset.Now - _cachedAt < ScanCacheDuration)
            {
                return _cached;
            }
        }

        List<Entry> scanned = Scan(weekSince);
        lock (_gate)
        {
            _cached = scanned;
            _cachedAt = DateTimeOffset.Now;
            _cachedFrom = weekSince;
            return scanned;
        }
    }

    private List<Entry> Scan(DateTimeOffset since)
    {
        var entries = new List<Entry>();
        long scanned = 0;

        foreach (string path in EnumerateTranscripts(since))
        {
            if (scanned >= ScanBudgetBytes)
            {
                break;
            }

            try
            {
                using var reader = new StreamReader(path);
                while (reader.ReadLine() is { } line)
                {
                    scanned += line.Length;
                    // Cheap gate: only assistant records carry a usage block, and
                    // parsing every line of a multi-megabyte transcript is wasteful.
                    if (line.Length < 32 || !line.Contains("\"usage\"", StringComparison.Ordinal))
                    {
                        continue;
                    }

                    if (!TryReadRecord(line, out DateTimeOffset timestamp, out string model, out long tokens))
                    {
                        continue;
                    }

                    if (timestamp < since)
                    {
                        continue;
                    }

                    entries.Add(new Entry(timestamp, model, tokens));
                }
            }
            catch (IOException)
            {
                // A transcript being written to right now is skipped, not fatal.
            }
            catch (UnauthorizedAccessException)
            {
            }
        }

        return entries;
    }

    private IEnumerable<string> EnumerateTranscripts(DateTimeOffset weekSince)
    {
        IEnumerable<string> files;
        try
        {
            files = Directory.EnumerateFiles(_projectsDirectory, "*.jsonl", SearchOption.AllDirectories);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            yield break;
        }

        foreach (string path in files)
        {
            DateTimeOffset written;
            try
            {
                written = File.GetLastWriteTimeUtc(path);
            }
            catch (IOException)
            {
                continue;
            }

            // A file untouched since before the window cannot hold a record in it.
            if (written >= weekSince)
            {
                yield return path;
            }
        }
    }

    private static bool TryReadRecord(string line, out DateTimeOffset timestamp, out string model, out long tokens)
    {
        timestamp = default;
        model = string.Empty;
        tokens = 0;

        try
        {
            using JsonDocument document = JsonDocument.Parse(line);
            JsonElement root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                return false;
            }

            if (!root.TryGetProperty("timestamp", out JsonElement stamp)
                || stamp.ValueKind != JsonValueKind.String
                || !DateTimeOffset.TryParse(stamp.GetString(), out timestamp))
            {
                return false;
            }

            if (!root.TryGetProperty("message", out JsonElement message)
                || message.ValueKind != JsonValueKind.Object
                || !message.TryGetProperty("usage", out JsonElement usage)
                || usage.ValueKind != JsonValueKind.Object)
            {
                return false;
            }

            if (message.TryGetProperty("model", out JsonElement modelElement)
                && modelElement.ValueKind == JsonValueKind.String)
            {
                model = modelElement.GetString()?.ToLowerInvariant() ?? string.Empty;
            }

            tokens = ReadTokenCount(usage, "input_tokens")
                + ReadTokenCount(usage, "output_tokens")
                + ReadTokenCount(usage, "cache_creation_input_tokens");
            return tokens > 0;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static long ReadTokenCount(JsonElement usage, string propertyName) =>
        usage.TryGetProperty(propertyName, out JsonElement value)
            && value.ValueKind == JsonValueKind.Number
            && value.TryGetInt64(out long parsed)
                ? parsed
                : 0;
}
