using System.Text.Json;

namespace KeyboardScreen.Core;

/// <summary>Tokens counted for each limit window, all from local transcripts.</summary>
public sealed record ClaudeCodeTokenTotals(long Session, long Week, long ModelWeek, bool Available)
{
    public static ClaudeCodeTokenTotals None { get; } = new(0, 0, 0, false);
}

/// <summary>
/// Sums tokens from the Claude Code transcripts under <c>~/.claude/projects</c>.
///
/// No limit endpoint reports token counts, so this is the only place real
/// numbers can come from — and it only ever sees Claude Code on this machine.
/// Treat every total as a floor on account usage, never the account total.
///
/// Cache reads are excluded. They are discounted heavily on the server and run
/// one to two orders of magnitude above every other field, so including them
/// would swamp the figure and make it read as alarm rather than information.
/// </summary>
public sealed class ClaudeCodeTokenReader
{
    /// <summary>Stop after this many bytes so a large history cannot stall a refresh.</summary>
    private const long ScanBudgetBytes = 256L * 1024 * 1024;

    private readonly string _projectsDirectory;

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

    public ClaudeCodeTokenTotals Read(
        DateTimeOffset sessionSince,
        DateTimeOffset weekSince,
        string? modelScope)
    {
        if (!Directory.Exists(_projectsDirectory))
        {
            return ClaudeCodeTokenTotals.None;
        }

        long session = 0;
        long week = 0;
        long modelWeek = 0;
        long scanned = 0;
        bool sawAny = false;
        string scope = (modelScope ?? string.Empty).Trim().ToLowerInvariant();

        foreach (string path in EnumerateTranscripts(weekSince))
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

                    if (timestamp < weekSince)
                    {
                        continue;
                    }

                    sawAny = true;
                    week += tokens;
                    if (timestamp >= sessionSince)
                    {
                        session += tokens;
                    }

                    if (scope.Length > 0 && model.Contains(scope, StringComparison.Ordinal))
                    {
                        modelWeek += tokens;
                    }
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

        return new ClaudeCodeTokenTotals(session, week, modelWeek, sawAny);
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
