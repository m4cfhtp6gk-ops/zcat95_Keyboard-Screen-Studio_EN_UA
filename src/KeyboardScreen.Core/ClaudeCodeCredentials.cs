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
/// <summary>Why no credential came back, so the screen can say the true thing.</summary>
public enum ClaudeCredentialProblem
{
    None,

    /// <summary>Claude Code has never run for this user, or runs somewhere else entirely.</summary>
    NoDirectory,

    /// <summary>The folder is there but holds no credentials file - most often a login that never happened.</summary>
    NoFile,

    /// <summary>The file exists and could not be opened: permissions, or another process holding it.</summary>
    Unreadable,

    /// <summary>Opened, but the JSON is torn - usually a write caught halfway.</summary>
    Unparseable,

    /// <summary>Valid JSON with no access token anywhere in it: this is not the file we want.</summary>
    NoTokenInside
}

/// <summary>The outcome of a credential search, including the near misses.</summary>
/// <param name="Detail">
/// Context for <see cref="Problem"/>: the folder for NoDirectory, the names it
/// holds for NoFile, the exception type for Unreadable. Never file contents,
/// and never the token.
/// </param>
public sealed record ClaudeCredentialLookup(
    ClaudeCodeCredential? Credential,
    ClaudeCredentialProblem Problem,
    string Path,
    string Detail)
{
    public static ClaudeCredentialLookup From(ClaudeCodeCredential credential) =>
        new(credential, ClaudeCredentialProblem.None, credential.Source, string.Empty);
}

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
    /// Returns null when the machine has no Claude Code login to borrow; use
    /// <see cref="Locate"/> when you need to say why.
    /// </summary>
    public static ClaudeCodeCredential? Read(string? credentialsPath = null) =>
        Locate(credentialsPath).Credential;

    /// <summary>
    /// The same search, but it reports what it saw.
    ///
    /// The first version of this collapsed a missing folder, a missing file, a
    /// file it was not allowed to open, a file held open by Claude Code, torn
    /// JSON and a file with no token in it into one null - and the settings page
    /// then told the user, in all six cases, that there was no login "at" a path.
    /// Being told the wrong reason is worse than being told nothing: it sends
    /// you off to reinstall something that was never missing.
    /// </summary>
    public static ClaudeCredentialLookup Locate(string? credentialsPath = null)
    {
        string? fromEnvironment = Environment.GetEnvironmentVariable(TokenEnvironmentVariable);
        if (!string.IsNullOrWhiteSpace(fromEnvironment))
        {
            return ClaudeCredentialLookup.From(
                new ClaudeCodeCredential(fromEnvironment.Trim(), null, TokenEnvironmentVariable));
        }

        string path = credentialsPath ?? DefaultCredentialsPath();
        string directory = Path.GetDirectoryName(path) ?? string.Empty;
        string json;
        try
        {
            if (!File.Exists(path))
            {
                return directory.Length > 0 && !Directory.Exists(directory)
                    ? new ClaudeCredentialLookup(null, ClaudeCredentialProblem.NoDirectory, path, directory)
                    : new ClaudeCredentialLookup(
                        null, ClaudeCredentialProblem.NoFile, path, DescribeDirectory(directory));
            }

            // Claude Code refreshes this file roughly hourly and may hold it open
            // while it does. Sharing the read means a refresh in flight looks like
            // a moment's bad timing, not a missing login.
            using var stream = new FileStream(
                path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using var reader = new StreamReader(stream);
            json = reader.ReadToEnd();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            return new ClaudeCredentialLookup(
                null, ClaudeCredentialProblem.Unreadable, path, ex.GetType().Name);
        }

        if (Parse(json, path) is { } credential)
        {
            return ClaudeCredentialLookup.From(credential);
        }

        // Torn JSON and well-formed JSON without a token are different mistakes:
        // the first is a bad moment, the second means this is not the file.
        bool wellFormed;
        try
        {
            using JsonDocument _ = JsonDocument.Parse(json);
            wellFormed = true;
        }
        catch (JsonException)
        {
            wellFormed = false;
        }

        return new ClaudeCredentialLookup(
            null,
            wellFormed ? ClaudeCredentialProblem.NoTokenInside : ClaudeCredentialProblem.Unparseable,
            path,
            string.Empty);
    }

    /// <summary>
    /// The names in the folder, so "no login file" can be told apart from "wrong
    /// folder entirely". Names only - nothing in here is opened or reported.
    /// </summary>
    private static string DescribeDirectory(string directory)
    {
        try
        {
            string[] names = Directory
                .EnumerateFileSystemEntries(directory)
                .Select(Path.GetFileName)
                .Where(name => !string.IsNullOrEmpty(name))
                .Order(StringComparer.OrdinalIgnoreCase)
                .Take(8)
                .ToArray()!;
            return names.Length == 0 ? string.Empty : string.Join(", ", names);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return string.Empty;
        }
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
