using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;

namespace KeyboardScreen.Core;

/// <summary>
/// Reads the real Claude subscription windows from
/// <c>claude.ai/api/organizations/{id}/usage</c>, authenticated with the OAuth
/// token Claude Code already holds on this machine.
///
/// Three designs have stood here. The endpoint was never the problem - the
/// first one called this exact URL. What failed was the credential: a browser
/// <c>sessionKey</c> cookie, which Cloudflare binds to the browser that solved
/// its challenge, TLS fingerprint included, so a desktop client could not
/// present it convincingly. The second design gave up on the server and summed
/// tokens from local transcripts, which is honest arithmetic but answers a
/// different question - it can only report a floor on one machine's usage
/// against a budget the user invented.
///
/// The token Claude Code stores is an OAuth credential issued for a
/// non-browser client, which is what the earlier attempts were missing. It is
/// read fresh on every refresh, never copied into settings, never exported and
/// never logged; this class sends it to claude.ai and nowhere else. Refreshing
/// it is deliberately left to Claude Code: holding the refresh token here would
/// put the user's login one bug away from being invalidated.
/// </summary>
public sealed class ClaudeUsageSnapshotSource : IDisposable
{
    /// <summary>Usage moves slowly and every call spends a request; twice a minute is plenty.</summary>
    private static readonly TimeSpan CacheDuration = TimeSpan.FromSeconds(30);

    private readonly HttpClient _client;
    private readonly bool _ownsClient;
    private readonly Func<ClaudeCodeCredential?> _credentialReader;

    private ClaudeUsageSnapshot? _cached;
    private string _cachedKey = string.Empty;
    private DateTimeOffset _cachedAt;

    public ClaudeUsageSnapshotSource(
        HttpClient? client = null,
        Func<ClaudeCodeCredential?>? credentialReader = null)
    {
        _ownsClient = client is null;
        _client = client ?? new HttpClient(new SocketsHttpHandler { UseCookies = false })
        {
            Timeout = TimeSpan.FromSeconds(15)
        };
        _credentialReader = credentialReader ?? (() => ClaudeCodeCredentials.Read());
    }

    public string BaseUrl { get; init; } = "https://claude.ai/api";

    public async Task<ClaudeUsageSnapshot> ReadAsync(
        ClaudeUsageSettings settings,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);

        ClaudeCodeCredential? credential = _credentialReader();
        if (credential is null)
        {
            return ClaudeUsageSnapshot.Unavailable(Loc.T("ClaudeNoCredentials"));
        }

        if (credential.IsExpired)
        {
            return ClaudeUsageSnapshot.Unavailable(Loc.T("ClaudeCredentialExpired"));
        }

        DateTimeOffset now = DateTimeOffset.Now;
        string key = credential.AccessToken.Length + "|" + settings.ModelScope;
        if (_cached is not null && _cachedKey == key && now - _cachedAt < CacheDuration)
        {
            return _cached with { IsStale = true };
        }

        try
        {
            string organizationId = settings.OrganizationId;
            if (string.IsNullOrWhiteSpace(organizationId))
            {
                organizationId = await ResolveOrganizationIdAsync(credential, cancellationToken);
                if (organizationId.Length == 0)
                {
                    return ClaudeUsageSnapshot.Unavailable(Loc.T("ClaudeNoOrganization"));
                }

                settings.OrganizationId = organizationId;
            }

            using JsonDocument usage = await GetJsonAsync(
                $"/organizations/{organizationId}/usage", credential, cancellationToken);
            ClaudeUsageSnapshot snapshot = Parse(usage.RootElement, settings.ModelScope, now);
            if (snapshot.Available)
            {
                _cached = snapshot;
                _cachedKey = key;
                _cachedAt = now;
            }

            return snapshot;
        }
        catch (ClaudeRequestException ex)
        {
            // A stale organization id is the one failure worth retrying blind:
            // it is cached in settings and only the server knows it went bad.
            if (ex.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Forbidden
                && settings.OrganizationId.Length > 0)
            {
                settings.OrganizationId = string.Empty;
            }

            return ClaudeUsageSnapshot.Unavailable(ex.Message);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            return ClaudeUsageSnapshot.Unavailable(ex.Message);
        }
    }

    public async Task<string> ResolveOrganizationIdAsync(
        ClaudeCodeCredential credential,
        CancellationToken cancellationToken = default)
    {
        using JsonDocument document = await GetJsonAsync("/organizations", credential, cancellationToken);
        return FirstOrganizationId(document.RootElement);
    }

    internal static string FirstOrganizationId(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Array)
        {
            return string.Empty;
        }

        foreach (JsonElement item in root.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.Object
                && item.TryGetProperty("uuid", out JsonElement uuid)
                && uuid.ValueKind == JsonValueKind.String
                && uuid.GetString() is { Length: > 0 } value)
            {
                return value;
            }
        }

        return string.Empty;
    }

    private async Task<JsonDocument> GetJsonAsync(
        string path,
        ClaudeCodeCredential credential,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, BaseUrl + path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", credential.AccessToken);
        request.Headers.Accept.ParseAdd("application/json");
        // Identify honestly. The previous design claimed to be Chrome while
        // presenting a .NET connection fingerprint, which is a contradiction bot
        // detection scores against - and it never helped.
        request.Headers.UserAgent.ParseAdd("KeyboardScreenStudio");

        using HttpResponseMessage response = await _client.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new ClaudeRequestException(
                response.StatusCode,
                response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden
                    ? Loc.T("ClaudeTokenRejected", (int)response.StatusCode)
                    : Loc.T("ClaudeRequestFailed", (int)response.StatusCode));
        }

        string body = await response.Content.ReadAsStringAsync(cancellationToken);
        return JsonDocument.Parse(body);
    }

    /// <summary>
    /// Older payloads carry the per-model weekly window as its own
    /// <c>seven_day_&lt;model&gt;</c> object. Newer ones null those out and report
    /// per-model limits in a <c>limits</c> array instead, so an entry there wins
    /// whenever it is present.
    /// </summary>
    internal static ClaudeUsageSnapshot Parse(JsonElement root, string? modelScope, DateTimeOffset now)
    {
        if (root.ValueKind != JsonValueKind.Object)
        {
            return ClaudeUsageSnapshot.Unavailable(Loc.T("ClaudeRequestFailed", 0));
        }

        string scope = string.IsNullOrWhiteSpace(modelScope)
            ? ClaudeUsageSettings.DefaultModelScope
            : modelScope.Trim();

        ClaudeUsageWindow? session = ReadWindow(root, "five_hour", ClaudeUsageWindowKind.Session, null);
        ClaudeUsageWindow? week = ReadWindow(root, "seven_day", ClaudeUsageWindowKind.Week, null);
        ClaudeUsageWindow? modelWeek = ReadWindow(
            root, "seven_day_" + scope.ToLowerInvariant(), ClaudeUsageWindowKind.ModelWeek, scope);

        if (root.TryGetProperty("limits", out JsonElement limits) && limits.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement limit in limits.EnumerateArray())
            {
                if (limit.ValueKind != JsonValueKind.Object
                    || !limit.TryGetProperty("kind", out JsonElement kind)
                    || kind.ValueKind != JsonValueKind.String
                    || kind.GetString() != "weekly_scoped"
                    || !limit.TryGetProperty("scope", out JsonElement scopeElement)
                    || !scopeElement.TryGetProperty("model", out JsonElement model))
                {
                    continue;
                }

                // Match the stable id first; display_name is a human label that
                // can be renamed out from under us.
                string id = ReadString(model, "id").ToLowerInvariant();
                string displayName = ReadString(model, "display_name");
                string needle = scope.ToLowerInvariant();
                if (!id.Contains(needle, StringComparison.Ordinal)
                    && !displayName.Equals(scope, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                modelWeek = new ClaudeUsageWindow(
                    ClaudeUsageWindowKind.ModelWeek,
                    ReadPercent(limit, "percent"),
                    ReadResetsAt(limit),
                    displayName.Length > 0 ? displayName : scope);
                break;
            }
        }

        bool available = session is not null || week is not null || modelWeek is not null;
        return available
            ? new ClaudeUsageSnapshot(true, session, week, modelWeek, now)
            : ClaudeUsageSnapshot.Unavailable(Loc.T("ClaudeNoWindows"));
    }

    private static ClaudeUsageWindow? ReadWindow(
        JsonElement root,
        string propertyName,
        ClaudeUsageWindowKind kind,
        string? scopeName)
    {
        if (!root.TryGetProperty(propertyName, out JsonElement window) || window.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        return new ClaudeUsageWindow(kind, ReadPercent(window, "utilization"), ReadResetsAt(window), scopeName);
    }

    /// <summary>
    /// Both spellings are accepted for each field. The two descriptions of this
    /// payload in the wild disagree - <c>utilization</c> against
    /// <c>utilization_pct</c>, <c>resets_at</c> against <c>reset_at</c> - and
    /// guessing wrong here costs a release to find out.
    /// </summary>
    private static DateTimeOffset? ReadResetsAt(JsonElement element)
    {
        foreach (string name in new[] { "resets_at", "reset_at" })
        {
            if (element.TryGetProperty(name, out JsonElement resets)
                && resets.ValueKind == JsonValueKind.String
                && DateTimeOffset.TryParse(resets.GetString(), out DateTimeOffset parsed))
            {
                return parsed;
            }
        }

        return null;
    }

    /// <summary>Utilization arrives as a number or as a string that may carry a percent sign.</summary>
    internal static double ReadPercent(JsonElement element, string propertyName)
    {
        foreach (string name in new[] { propertyName, propertyName + "_pct" })
        {
            if (!element.TryGetProperty(name, out JsonElement value))
            {
                continue;
            }

            if (value.ValueKind == JsonValueKind.Number && value.TryGetDouble(out double number))
            {
                return number;
            }

            if (value.ValueKind == JsonValueKind.String)
            {
                string text = (value.GetString() ?? string.Empty).Trim().TrimEnd('%').Trim();
                if (double.TryParse(text, System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out double parsed))
                {
                    return parsed;
                }
            }
        }

        return 0;
    }

    private static string ReadString(JsonElement element, string propertyName) =>
        element.TryGetProperty(propertyName, out JsonElement value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? string.Empty
            : string.Empty;

    /// <summary>
    /// The diagnostic button. Reports which credential was found, where it came
    /// from and what the server said - never the token itself.
    /// </summary>
    public async Task<ClaudeConnectionReport> CheckAsync(ClaudeUsageSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        ClaudeCodeCredential? credential = _credentialReader();
        if (credential is null)
        {
            return new ClaudeConnectionReport(false, Loc.T("ClaudeCheckNoCredentials",
                ClaudeCodeCredentials.DefaultCredentialsPath()));
        }

        if (credential.IsExpired)
        {
            return new ClaudeConnectionReport(false, Loc.T("ClaudeCredentialExpired"));
        }

        // The cache must not answer a diagnostic: the whole point is a live call.
        _cached = null;
        settings.OrganizationId = string.Empty;

        ClaudeUsageSnapshot snapshot = await ReadAsync(settings);
        return snapshot.Available
            ? new ClaudeConnectionReport(true, credential.Source)
            : new ClaudeConnectionReport(false,
                Loc.T("ClaudeCheckFrom", credential.Source, snapshot.ErrorMessage ?? string.Empty));
    }

    public void Dispose()
    {
        if (_ownsClient)
        {
            _client.Dispose();
        }
    }
}

/// <summary>Carries the status code so a stale organization id can be told from a dead token.</summary>
internal sealed class ClaudeRequestException(HttpStatusCode statusCode, string message) : Exception(message)
{
    public HttpStatusCode StatusCode { get; } = statusCode;
}
