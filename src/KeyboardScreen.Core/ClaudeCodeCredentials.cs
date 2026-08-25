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
    /// <summary>
    /// Every path that was tried. A user whose login is somewhere this does not
    /// look can only tell us so if we say where we looked.
    /// </summary>
    public IReadOnlyList<string> Searched { get; init; } = [];

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

    /// <summary>
    /// Names that carry the bearer token itself. Matched on a normalized name so
    /// accessToken, access_token and access-token are one case.
    /// </summary>
    private static readonly string[] TokenNames = ["accesstoken", "oauthaccesstoken"];

    /// <summary>
    /// Weaker names, tried only after the whole document has been searched for a
    /// real access token, so a file that has both does not answer with this one.
    /// </summary>
    private static readonly string[] FallbackTokenNames = ["token", "oauthtoken", "bearertoken", "bearer"];
    private static readonly string[] ExpiryNames = ["expiresat", "expires_at", "expiry"];

    private const string ConfigDirectoryVariable = "CLAUDE_CONFIG_DIR";
    private const string CredentialsFileName = ".credentials.json";

    public static string ConfigDirectory()
    {
        string? configured = ReadEnvironment(ConfigDirectoryVariable);
        return string.IsNullOrWhiteSpace(configured)
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".claude")
            : configured;
    }

    public static string DefaultCredentialsPath() =>
        Path.Combine(ConfigDirectory(), CredentialsFileName);

    /// <summary>
    /// An environment variable as this process sees it, and - on Windows - as the
    /// user and machine have it set.
    ///
    /// A process inherits its environment when it starts. Someone who sets
    /// CLAUDE_CODE_OAUTH_TOKEN or CLAUDE_CONFIG_DIR in System Properties, or in a
    /// terminal, and then looks at an app that was already running (or was
    /// started from an Explorer session older than the change) will not see it
    /// there. Reading the stored value as well means the setting takes effect
    /// when it is made rather than after the next sign-out.
    /// </summary>
    private static string? ReadEnvironment(string name)
    {
        string? value = Environment.GetEnvironmentVariable(name);
        if (!string.IsNullOrWhiteSpace(value) || !OperatingSystem.IsWindows())
        {
            return value;
        }

        foreach (EnvironmentVariableTarget target in
                 new[] { EnvironmentVariableTarget.User, EnvironmentVariableTarget.Machine })
        {
            try
            {
                value = Environment.GetEnvironmentVariable(name, target);
                if (!string.IsNullOrWhiteSpace(value))
                {
                    return value;
                }
            }
            catch (Exception ex) when (ex is System.Security.SecurityException or IOException)
            {
                // A locked-down machine may refuse the registry read; the process
                // block was already checked, so there is simply nothing more here.
            }
        }

        return null;
    }

    /// <summary>
    /// Every place this machine might be keeping a Claude Code login, best first.
    ///
    /// The previous version knew exactly one path and, when the file was not
    /// there, could only repeat that path back at the user. That is fine when the
    /// guess is right and useless when it is not - and the documented path is
    /// only right for a login made by the Windows CLI under this account. A
    /// developer whose Claude Code lives in WSL, or who moved CLAUDE_CONFIG_DIR,
    /// or who signed in with `ant auth login`, has the file somewhere this never
    /// looked.
    /// </summary>
    public static IReadOnlyList<string> SearchPaths(bool includeSlowPaths = true)
    {
        var paths = new List<string>();
        void Add(string? directory)
        {
            if (!string.IsNullOrWhiteSpace(directory))
            {
                paths.Add(Path.Combine(directory, CredentialsFileName));
            }
        }

        Add(ReadEnvironment(ConfigDirectoryVariable));

        string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (home.Length > 0)
        {
            Add(Path.Combine(home, ".claude"));
            Add(Path.Combine(home, ".config", "claude"));
        }

        if (OperatingSystem.IsWindows())
        {
            Add(Environment.GetEnvironmentVariable("LOCALAPPDATA") is { Length: > 0 } local
                ? Path.Combine(local, ".claude") : null);
            Add(Environment.GetEnvironmentVariable("APPDATA") is { Length: > 0 } roaming
                ? Path.Combine(roaming, ".claude") : null);

            if (includeSlowPaths)
            {
                paths.AddRange(WslCredentialPaths());
            }
        }

        return paths.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    }

    /// <summary>
    /// Claude Code installed under WSL keeps its login in the Linux home, which
    /// Windows reaches over a UNC share. Worth looking at, and worth looking at
    /// last: resolving these paths goes through the WSL service, which is slow
    /// when it is starting and slower when it is not installed at all.
    /// </summary>
    private static IEnumerable<string> WslCredentialPaths()
    {
        foreach (string root in new[] { @"\\wsl.localhost\", @"\\wsl$\" })
        {
            string[] distributions;
            try
            {
                distributions = Directory.GetDirectories(root);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
            {
                continue;
            }

            foreach (string distribution in distributions)
            {
                yield return Path.Combine(distribution, "root", ".claude", CredentialsFileName);

                string[] users;
                try
                {
                    users = Directory.GetDirectories(Path.Combine(distribution, "home"));
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
                {
                    continue;
                }

                foreach (string user in users)
                {
                    yield return Path.Combine(user, ".claude", CredentialsFileName);
                }
            }
        }
    }

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
    /// <summary>
    /// Just the environment-variable override, so a caller can give it priority
    /// without also triggering the file search.
    /// </summary>
    public static ClaudeCodeCredential? FromEnvironment()
    {
        string? value = ReadEnvironment(TokenEnvironmentVariable);
        return string.IsNullOrWhiteSpace(value)
            ? null
            : new ClaudeCodeCredential(value.Trim(), null, TokenEnvironmentVariable);
    }

    public static ClaudeCredentialLookup Locate(string? credentialsPath = null)
    {
        if (FromEnvironment() is { } fromEnvironment)
        {
            return ClaudeCredentialLookup.From(fromEnvironment);
        }

        if (credentialsPath is not null)
        {
            return Inspect(credentialsPath);
        }

        // Every candidate is tried before giving up, and the failure reported is
        // the most informative one seen - a file that exists and cannot be read
        // says far more than four folders that do not exist.
        ClaudeCredentialLookup? best = null;
        var searched = new List<string>();
        foreach (string candidate in SearchPaths())
        {
            searched.Add(candidate);
            ClaudeCredentialLookup attempt = Inspect(candidate);
            if (attempt.Credential is not null)
            {
                return attempt;
            }

            if (best is null || Rank(attempt.Problem) > Rank(best.Problem))
            {
                best = attempt;
            }
        }

        best ??= new ClaudeCredentialLookup(
            null, ClaudeCredentialProblem.NoDirectory, DefaultCredentialsPath(), string.Empty);
        return best with { Searched = searched };
    }

    /// <summary>
    /// How much a failure tells the user. A file that is there but unreadable is
    /// worth reporting over a folder that never existed.
    /// </summary>
    private static int Rank(ClaudeCredentialProblem problem) => problem switch
    {
        ClaudeCredentialProblem.Unreadable => 5,
        ClaudeCredentialProblem.NoTokenInside => 4,
        ClaudeCredentialProblem.Unparseable => 3,
        ClaudeCredentialProblem.NoFile => 2,
        _ => 1
    };

    private static ClaudeCredentialLookup Inspect(string path)
    {
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
            wellFormed ? DescribeShape(json) : string.Empty);
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

    private static ClaudeCodeCredential? Find(JsonElement element, string source, int depth) =>
        Find(element, source, depth, TokenNames, false)
        ?? Find(element, source, depth, FallbackTokenNames, false);

    /// <summary>
    /// Sections that hold somebody else's credential. This same file stores the
    /// OAuth state for every MCP server a plugin has connected - Linear, Notion,
    /// whatever else - each with its own accessToken. Those belong to those
    /// services, and sending one to api.anthropic.com would both fail and hand a
    /// third party's token to a fourth. Loosening the name match in v1.10.3
    /// opened that door; this closes it.
    /// </summary>
    private static readonly string[] ForeignSections = ["mcp", "plugin"];

    private static ClaudeCodeCredential? Find(
        JsonElement element,
        string source,
        int depth,
        string[] wanted,
        bool foreign)
    {
        if (depth > 8)
        {
            return null;
        }

        if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement item in element.EnumerateArray())
            {
                if (Find(item, source, depth + 1, wanted, foreign) is { } nested)
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

        if (!foreign)
        {
            foreach (JsonProperty property in element.EnumerateObject())
            {
                if (property.Value.ValueKind == JsonValueKind.String
                    && Wants(property.Name, wanted)
                    && property.Value.GetString() is { Length: > 0 } token)
                {
                    // The expiry, when there is one, sits beside the token.
                    return new ClaudeCodeCredential(token.Trim(), ReadExpiry(element), source);
                }
            }
        }

        foreach (JsonProperty property in element.EnumerateObject())
        {
            bool nowForeign = foreign || IsForeignSection(property.Name);
            if (Find(property.Value, source, depth + 1, wanted, nowForeign) is { } nested)
            {
                return nested;
            }

            // A value that is itself a JSON document is worth stepping into: some
            // stores keep the credential as one serialized blob.
            if (property.Value.ValueKind == JsonValueKind.String
                && property.Value.GetString() is { Length: > 1 } text
                && text.TrimStart().StartsWith('{'))
            {
                try
                {
                    using JsonDocument inner = JsonDocument.Parse(text);
                    if (Find(inner.RootElement, source, depth + 1, wanted, nowForeign) is { } embedded)
                    {
                        return embedded;
                    }
                }
                catch (JsonException)
                {
                    // Not JSON after all; it was only ever a guess.
                }
            }
        }

        return null;
    }

    /// <summary>
    /// A refresh token is never the answer: presenting one as a bearer fails, and
    /// it is the more sensitive of the pair. Everything else is matched on a
    /// contains so a prefixed name still counts.
    /// </summary>
    /// <summary>True for a key that opens somebody else's credential store.</summary>
    private static bool IsForeignSection(string name)
    {
        string normalized = Normalize(name);
        return ForeignSections.Any(marker => normalized.Contains(marker, StringComparison.Ordinal));
    }

    private static bool Wants(string name, string[] wanted)
    {
        string normalized = Normalize(name);
        return !normalized.Contains("refresh", StringComparison.Ordinal)
            && wanted.Any(candidate => normalized.Contains(candidate, StringComparison.Ordinal));
    }

    /// <summary>
    /// The property names in the document, so a file that holds no token can say
    /// what it does hold. Names only - no value is ever read here.
    ///
    /// Breadth first, deliberately. The first version walked depth first and hit
    /// its cap inside the first branch it entered, which on a real file meant it
    /// reported twelve names from one MCP server's OAuth state and never reached
    /// the top-level keys - and whether a key like claudeAiOauth is present is
    /// the entire question.
    /// </summary>
    internal static string DescribeShape(string json)
    {
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(json);
        }
        catch (JsonException)
        {
            return string.Empty;
        }

        using (document)
        {
            var names = new List<string>();
            var queue = new Queue<(JsonElement Element, int Depth)>();
            queue.Enqueue((document.RootElement, 0));

            while (queue.Count > 0 && names.Count < 18)
            {
                (JsonElement element, int depth) = queue.Dequeue();
                if (depth > 3)
                {
                    continue;
                }

                if (element.ValueKind == JsonValueKind.Array)
                {
                    foreach (JsonElement item in element.EnumerateArray())
                    {
                        queue.Enqueue((item, depth + 1));
                    }

                    continue;
                }

                if (element.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                foreach (JsonProperty property in element.EnumerateObject())
                {
                    if (!names.Contains(property.Name, StringComparer.Ordinal))
                    {
                        names.Add(property.Name);
                    }

                    queue.Enqueue((property.Value, depth + 1));
                }
            }

            return string.Join(", ", names);
        }
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

    private static string Normalize(string name) =>
        name.Replace("-", string.Empty).Replace("_", string.Empty).ToLowerInvariant();
}
