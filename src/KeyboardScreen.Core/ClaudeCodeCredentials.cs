using System.Text.Json;

namespace KeyboardScreen.Core;

/// <summary>
/// One OAuth credential belonging to Claude Code on this machine.
/// </summary>
/// <param name="Source">
/// Where it came from, for the diagnostic line. A path or an environment
/// variable name - never the token, which must not reach a log, the settings
/// file, or an exported backup.
/// </param>
public sealed record ClaudeCodeCredential(string AccessToken, DateTimeOffset? ExpiresAt, string Source)
{
    /// <summary>
    /// A credential without an expiry is treated as usable: the file's shape is
    /// not a documented contract, so a missing field must not be read as "dead".
    /// </summary>
    public bool IsExpired => ExpiresAt is { } expiry && expiry <= DateTimeOffset.Now;
}

/// <summary>
/// Finds the token Claude Code stores when you sign in.
///
/// The documented locations are <c>%USERPROFILE%\.claude\.credentials.json</c>
/// on Windows and <c>~/.claude/.credentials.json</c> on Linux, both overridden
/// by <c>CLAUDE_CONFIG_DIR</c>; on macOS the credential lives in the Keychain
/// and is not reachable from here.
///
/// The file's internal shape is not documented, so nothing here depends on a
/// particular nesting: the token is found by walking the JSON for an access
/// token property. Guessing one exact path is how the previous two designs of
/// this screen died, and a search costs nothing on a file this size.
/// </summary>
public static class ClaudeCodeCredentials
{
    /// <summary>Documented override for a long-lived token, used by CI and scripts.</summary>
    public const string TokenEnvironmentVariable = "CLAUDE_CODE_OAUTH_TOKEN";

    private static readonly string[] TokenNames = ["accesstoken", "access_token"];
    private static readonly string[] ExpiryNames = ["expiresat", "expires_at", "expiry"];

    public static string ConfigDirectory()
    {
        string? configured = Environment.GetEnvironmentVariable("CLAUDE_CONFIG_DIR");
        return string.IsNullOrWhiteSpace(configured)
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".claude")
            : configured;
    }

    public static string DefaultCredentialsPath() =>
        Path.Combine(ConfigDirectory(), ".credentials.json");

    /// <summary>
    /// The environment variable wins, exactly as it does for Claude Code itself.
    /// Returns null when the machine has no Claude Code login to borrow.
    /// </summary>
    public static ClaudeCodeCredential? Read(string? credentialsPath = null)
    {
        string? fromEnvironment = Environment.GetEnvironmentVariable(TokenEnvironmentVariable);
        if (!string.IsNullOrWhiteSpace(fromEnvironment))
        {
            return new ClaudeCodeCredential(fromEnvironment.Trim(), null, TokenEnvironmentVariable);
        }

        string path = credentialsPath ?? DefaultCredentialsPath();
        string json;
        try
        {
            if (!File.Exists(path))
            {
                return null;
            }

            json = File.ReadAllText(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }

        return Parse(json, path);
    }

    /// <summary>Split out so the shape handling is testable without touching the real file.</summary>
    internal static ClaudeCodeCredential? Parse(string json, string source)
    {
        try
        {
            using JsonDocument document = JsonDocument.Parse(json);
            return Find(document.RootElement, source, depth: 0);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static ClaudeCodeCredential? Find(JsonElement element, string source, int depth)
    {
        if (depth > 6)
        {
            return null;
        }

        if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement item in element.EnumerateArray())
            {
                if (Find(item, source, depth + 1) is { } nested)
                {
                    return nested;
                }
            }

            return null;
        }

        if (element.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        foreach (JsonProperty property in element.EnumerateObject())
        {
            if (property.Value.ValueKind == JsonValueKind.String
                && TokenNames.Contains(Normalize(property.Name))
                && property.Value.GetString() is { Length: > 0 } token)
            {
                // The expiry, when there is one, sits beside the token.
                return new ClaudeCodeCredential(token.Trim(), ReadExpiry(element), source);
            }
        }

        foreach (JsonProperty property in element.EnumerateObject())
        {
            if (Find(property.Value, source, depth + 1) is { } nested)
            {
                return nested;
            }
        }

        return null;
    }

    private static DateTimeOffset? ReadExpiry(JsonElement owner)
    {
        foreach (JsonProperty property in owner.EnumerateObject())
        {
            if (!ExpiryNames.Contains(Normalize(property.Name)))
            {
                continue;
            }

            // Milliseconds since the epoch is the common form; seconds and an
            // ISO string are both cheap to accept rather than misread.
            if (property.Value.ValueKind == JsonValueKind.Number
                && property.Value.TryGetInt64(out long stamp))
            {
                return stamp > 100_000_000_000L
                    ? DateTimeOffset.FromUnixTimeMilliseconds(stamp)
                    : DateTimeOffset.FromUnixTimeSeconds(stamp);
            }

            if (property.Value.ValueKind == JsonValueKind.String
                && DateTimeOffset.TryParse(property.Value.GetString(), out DateTimeOffset parsed))
            {
                return parsed;
            }
        }

        return null;
    }

    private static string Normalize(string name) => name.Replace("-", string.Empty).ToLowerInvariant();
}
