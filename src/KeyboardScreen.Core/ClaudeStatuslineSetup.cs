using System.Text.Json;
using System.Text.Json.Nodes;

namespace KeyboardScreen.Core;

/// <summary>
/// Turns the status-line feed on by writing one setting into the user's Claude
/// Code configuration.
///
/// This edits a file the user owns, so it is deliberately conservative: an
/// existing status line is never silently replaced (the caller is told, and can
/// pass <c>replace</c> once the user has agreed), the rest of the file is
/// preserved verbatim through a JSON node graph rather than a typed model, and
/// the write goes through a temp file so a crash cannot corrupt the settings of
/// another program.
/// </summary>
public static class ClaudeStatuslineSetup
{
    public enum Outcome
    {
        /// <summary>The shim is now installed.</summary>
        Installed,

        /// <summary>It was already installed; nothing changed.</summary>
        AlreadyInstalled,

        /// <summary>A different status line is configured and was left alone.</summary>
        ForeignStatusLine,

        /// <summary>The settings file could not be read or written.</summary>
        Failed
    }

    public sealed record Result(Outcome Outcome, string Path, string? ExistingCommand = null)
    {
        public string Describe() => Outcome switch
        {
            Outcome.Installed => Loc.T("ClaudeStatuslineInstalled"),
            Outcome.AlreadyInstalled => Loc.T("ClaudeStatuslineAlready"),
            Outcome.ForeignStatusLine => Loc.T("ClaudeStatuslineForeign", ExistingCommand ?? "-"),
            _ => Loc.T("ClaudeStatuslineFailed")
        };
    }

    /// <summary>Marks the command as ours, so a rerun can recognise its own work.</summary>
    public const string Marker = "keyboard-screen-studio-usage";

    public static string DefaultSettingsPath()
    {
        string? configured = Environment.GetEnvironmentVariable("CLAUDE_CONFIG_DIR");
        string root = string.IsNullOrWhiteSpace(configured)
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".claude")
            : configured;
        return Path.Combine(root, "settings.json");
    }

    /// <summary>
    /// Adds (or reports on) the status-line command. The document is round-tripped
    /// as nodes so every unrelated setting - hooks, permissions, model - survives.
    /// </summary>
    public static Result Install(
        string? settingsPath = null,
        string? usagePath = null,
        bool replace = false)
    {
        string path = settingsPath ?? DefaultSettingsPath();
        string command = ClaudeStatuslineUsage.ShimCommand(usagePath);

        try
        {
            JsonObject root = ReadObject(path);
            if (root["statusLine"] is JsonObject existing)
            {
                string current = existing["command"]?.GetValue<string>() ?? string.Empty;
                if (current.Contains(Marker, StringComparison.OrdinalIgnoreCase))
                {
                    return new Result(Outcome.AlreadyInstalled, path, current);
                }

                if (current.Length > 0 && !replace)
                {
                    // Somebody else's status line: overwriting it would break a
                    // tool the user set up on purpose.
                    return new Result(Outcome.ForeignStatusLine, path, current);
                }
            }

            root["statusLine"] = new JsonObject
            {
                ["type"] = "command",
                ["command"] = command
            };

            string directory = Path.GetDirectoryName(path) ?? string.Empty;
            if (directory.Length > 0)
            {
                Directory.CreateDirectory(directory);
            }

            // Temp file plus swap: this is another program's configuration and
            // must never be left half-written.
            string temp = path + ".kss.tmp";
            File.WriteAllText(temp, root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
            if (File.Exists(path))
            {
                File.Replace(temp, path, path + ".kss.bak", ignoreMetadataErrors: true);
            }
            else
            {
                File.Move(temp, path);
            }

            return new Result(Outcome.Installed, path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            return new Result(Outcome.Failed, path);
        }
    }

    /// <summary>Whether our shim is the configured status line right now.</summary>
    public static bool IsInstalled(string? settingsPath = null)
    {
        try
        {
            JsonObject root = ReadObject(settingsPath ?? DefaultSettingsPath());
            return root["statusLine"] is JsonObject line
                && (line["command"]?.GetValue<string>() ?? string.Empty)
                    .Contains(Marker, StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            return false;
        }
    }

    private static JsonObject ReadObject(string path)
    {
        if (!File.Exists(path))
        {
            return new JsonObject();
        }

        string text = File.ReadAllText(path);
        return string.IsNullOrWhiteSpace(text)
            ? new JsonObject()
            : JsonNode.Parse(text) as JsonObject ?? new JsonObject();
    }
}
