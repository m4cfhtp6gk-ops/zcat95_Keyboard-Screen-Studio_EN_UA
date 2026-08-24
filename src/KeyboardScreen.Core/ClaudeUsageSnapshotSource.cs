using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;

namespace KeyboardScreen.Core;

/// <summary>
/// Reads the real Claude subscription windows from
/// <c>api.anthropic.com/api/oauth/usage</c>, authenticated with the OAuth token
/// Claude Code already holds on this machine.
///
/// Four designs have stood here, and each got one half right. The first sent a
/// browser <c>sessionKey</c> cookie to <c>claude.ai/api</c>: right host for that
/// credential, but Cloudflare binds the cookie to the browser that solved its
/// challenge, so a desktop client cannot present it. The second gave up on the
/// server and summed tokens from local transcripts - honest arithmetic about the
/// wrong question, a floor on one machine's usage against a budget the user
/// invented. The third found the credential that is actually meant for a
/// non-browser client - the Claude Code OAuth token - and then sent it to the
/// cookie's host, where it means nothing.
///
/// The token and the host belong together: this endpoint is the one that takes
/// a bearer token, and it is scoped by the token, so there is no organization to
/// resolve. Two headers are not optional. <c>anthropic-beta: oauth-2025-04-20</c>
/// selects the OAuth contract, and the user agent must be <c>claude-code/</c>;
/// any other one lands in a bucket that rate-limits hard enough to look like a
/// broken feature. That is also why this polls slowly and, on a 429, stops
/// asking for a while instead of hammering its way into a longer ban.
///
/// The token is read fresh on every refresh, sent to api.anthropic.com and
/// nowhere else, and never copied into settings, an export or a log. Refreshing
/// it is deliberately left to Claude Code: holding the refresh token here would
/// put the user's login one bug away from being invalidated.
/// </summary>
public sealed class ClaudeUsageSnapshotSource : IDisposable
{
    /// <summary>
    /// This endpoint rate-limits by how often you ask, not by how much you use.
    /// Three minutes is the interval it is documented to tolerate, and usage does
    /// not move fast enough for a tighter one to show the user anything new.
    /// </summary>
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(3);

    /// <summary>
    /// How long to stay quiet after a refusal. Retrying a 429 on the next tick is
    /// what turns a minute of throttling into an hour of it.
    /// </summary>
    private static readonly TimeSpan FailureBackoff = TimeSpan.FromMinutes(5);

    /// <summary>Selects the OAuth contract; without it the token is not honoured.</summary>
    private const string OAuthBetaHeader = "oauth-2025-04-20";

    /// <summary>
    /// Required. Any other user agent is served by a bucket that rate-limits so
    /// aggressively the screen never fills. We are a client of the user's own
    /// Claude Code login, reading only that user's own numbers, so this says what
    /// the request is on behalf of rather than pretending to be a browser.
    /// </summary>
    private const string ClaudeCodeUserAgent = "claude-code/2.1.69";

    private readonly HttpClient _client;
    private readonly bool _ownsClient;
    private readonly Func<ClaudeCodeCredential?> _credentialReader;

    private ClaudeUsageSnapshot? _cached;
    private string _cachedKey = string.Empty;
    private DateTimeOffset _cachedAt;
    private ClaudeUsageSnapshot? _lastFailure;
    private DateTimeOffset _failedAt;

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

    public string BaseUrl { get; init; } = "https://api.anthropic.com";

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

        // Repeat the last refusal rather than earn a longer one. The screen keeps
        // saying exactly what went wrong; it just stops asking for a few minutes.
        if (_lastFailure is not null && now - _failedAt < FailureBackoff)
        {
            return _lastFailure;
        }

        try
        {
            using JsonDocument usage = await GetJsonAsync("/api/oauth/usage", credential, cancellationToken);
            ClaudeUsageSnapshot snapshot = Parse(usage.RootElement, settings.ModelScope, now);
            if (snapshot.Available)
            {
                _cached = snapshot;
                _cachedKey = key;
                _cachedAt = now;
                _lastFailure = null;
            }

            return snapshot;
        }
        catch (Exception ex) when (ex is ClaudeRequestException or HttpRequestException
                                      or TaskCanceledException or JsonException)
        {
            ClaudeUsageSnapshot failure = ClaudeUsageSnapshot.Unavailable(ex.Message);
            _lastFailure = failure;
            _failedAt = now;
            return failure;
        }
    }

    private async Task<JsonDocument> GetJsonAsync(
        string path,
        ClaudeCodeCredential credential,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, BaseUrl + path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", credential.AccessToken);
        request.Headers.Accept.ParseAdd("application/json");
        request.Headers.TryAddWithoutValidation("anthropic-beta", OAuthBetaHeader);
        request.Headers.UserAgent.ParseAdd(ClaudeCodeUserAgent);

        using HttpResponseMessage response = await _client.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new ClaudeRequestException(
                response.StatusCode,
                response.StatusCode switch
                {
                    HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden =>
                        Loc.T("ClaudeTokenRejected", (int)response.StatusCode),
                    HttpStatusCode.TooManyRequests => Loc.T("ClaudeRateLimited"),
                    _ => Loc.T("ClaudeRequestFailed", (int)response.StatusCode)
                });
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

        // Neither cache may answer a diagnostic: the whole point is a live call.
        _cached = null;
        _lastFailure = null;

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

/// <summary>Carries the status code so a dead token can be told from a throttled one.</summary>
internal sealed class ClaudeRequestException(HttpStatusCode statusCode, string message) : Exception(message)
{
    public HttpStatusCode StatusCode { get; } = statusCode;
}
