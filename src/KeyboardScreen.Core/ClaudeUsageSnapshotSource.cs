using System.Net;
using System.Net.Http;
using System.Text.Json;

namespace KeyboardScreen.Core;

/// <summary>
/// Reads Claude subscription limits from claude.ai using the session cookie the
/// user pastes into settings.
///
/// This is an unofficial, undocumented endpoint — the same one the account page
/// itself calls. It can change without notice, so every failure degrades to the
/// last good snapshot rather than blanking the screen.
/// </summary>
public sealed class ClaudeUsageSnapshotSource : IDisposable
{
    private static readonly TimeSpan CacheWindow = TimeSpan.FromSeconds(120);

    /// <summary>
    /// First hold-off after a Cloudflare challenge; each consecutive challenge
    /// doubles it up to <see cref="MaxChallengeBackoff"/>. Hammering a challenge
    /// only prolongs the block, and backing off is what lets it clear.
    /// </summary>
    private static readonly TimeSpan ChallengeBackoff = TimeSpan.FromMinutes(5);

    private static readonly TimeSpan MaxChallengeBackoff = TimeSpan.FromMinutes(60);

    // Deliberately NOT pretending to be Chrome. Claiming a browser user-agent
    // and browser client hints while presenting a .NET TLS and HTTP/2 fingerprint
    // is a self-contradiction bot heuristics score against, and pinning a Chrome
    // major means chasing a release every few weeks. An honest product token is
    // what an API client is supposed to send.
    private const string ProductUserAgent = "KeyboardScreenStudio/1.8 (+https://github.com/zcat95)";

    private readonly HttpClient _client;
    private readonly bool _ownsClient;
    private readonly ClaudeCodeTokenReader _tokenReader;

    private ClaudeUsageSnapshot? _cached;
    private DateTimeOffset _lastFetch = DateTimeOffset.MinValue;
    private string _cachedFor = string.Empty;
    private DateTimeOffset _challengedUntil = DateTimeOffset.MinValue;
    private int _challengeStrikes;

    public ClaudeUsageSnapshotSource(HttpClient? client = null, ClaudeCodeTokenReader? tokenReader = null)
    {
        _ownsClient = client is null;
        // Cookies are handled here rather than by the handler: the Cookie header
        // is built explicitly from what the user pasted plus what the server has
        // issued, so a handler-managed jar cannot quietly drop or duplicate it.
        _client = client ?? new HttpClient(new SocketsHttpHandler { UseCookies = false })
        {
            Timeout = TimeSpan.FromSeconds(15)
        };
        _tokenReader = tokenReader ?? new ClaudeCodeTokenReader();
    }

    /// <summary>
    /// Cookies claude.ai and Cloudflare have issued this run, newest value
    /// winning. Concurrent: the refresh loop and the "test connection" button
    /// can be inside a request at the same moment.
    /// </summary>
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, string> _serverCookies =
        new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Overridable for tests; production always talks to claude.ai.</summary>
    public string BaseUrl { get; init; } = "https://claude.ai/api";

    /// <summary>Overridable for tests; production reads what the status-line shim writes.</summary>
    public string? StatuslinePath { get; init; }

    public async Task<ClaudeUsageSnapshot> ReadAsync(
        ClaudeUsageSettings settings,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);

        if (settings.SourceKind == ClaudeUsageSourceKind.StatusLine)
        {
            // Nothing to cache, throttle or back off: this is a local file that
            // Claude Code rewrites on its own schedule.
            return ClaudeStatuslineUsage.Read(StatuslinePath);
        }

        if (!settings.IsConfigured)
        {
            return ClaudeUsageSnapshot.Unavailable();
        }

        // Hash the value, not its length: a freshly pasted cookie is usually the
        // same length as the stale one, and a length-keyed cache would serve the
        // old failure right through the window in which the new cookie works.
        string fingerprint = Convert.ToHexString(
            System.Security.Cryptography.SHA256.HashData(
                System.Text.Encoding.UTF8.GetBytes(settings.SessionKey)))
            + ":" + settings.ModelScope;
        DateTimeOffset now = DateTimeOffset.Now;
        if (_cached is not null
            && string.Equals(_cachedFor, fingerprint, StringComparison.Ordinal)
            && now - _lastFetch < CacheWindow)
        {
            return _cached;
        }

        if (now < _challengedUntil)
        {
            // Mid-backoff after a Cloudflare challenge: stay off the wire.
            return _cached is null
                ? ClaudeUsageSnapshot.Unavailable(Loc.T("ClaudeChallenged"))
                : _cached with { IsStale = true, ErrorMessage = Loc.T("ClaudeChallenged") };
        }

        try
        {
            string organizationId = string.IsNullOrWhiteSpace(settings.OrganizationId)
                ? await ResolveOrganizationIdAsync(settings.SessionKey, cancellationToken)
                : settings.OrganizationId;
            settings.OrganizationId = organizationId;

            using JsonDocument document = await GetJsonAsync(
                $"/organizations/{organizationId}/usage", settings.SessionKey, cancellationToken);

            ClaudeUsageSnapshot snapshot = Parse(document.RootElement, settings.ModelScope, now);
            snapshot = WithLocalTokens(snapshot, settings, now);

            _challengeStrikes = 0;
            _cached = snapshot;
            _cachedFor = fingerprint;
            _lastFetch = now;
            return snapshot;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException or InvalidOperationException)
        {
            // Keep showing the last good reading rather than blanking the screen.
            return _cached is null
                ? ClaudeUsageSnapshot.Unavailable(ex.Message)
                : _cached with { IsStale = true, ErrorMessage = ex.Message };
        }
    }

    /// <summary>Resolves the first organization the session key can see.</summary>
    public async Task<string> ResolveOrganizationIdAsync(string sessionKey, CancellationToken cancellationToken = default)
    {
        using JsonDocument document = await GetJsonAsync("/organizations", sessionKey, cancellationToken);
        return FirstOrganizationId(document.RootElement);
    }

    private static string FirstOrganizationId(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Array || root.GetArrayLength() == 0)
        {
            throw new InvalidOperationException(Loc.T("ClaudeNoOrganization"));
        }

        foreach (JsonElement organization in root.EnumerateArray())
        {
            if (organization.TryGetProperty("uuid", out JsonElement uuid)
                && uuid.ValueKind == JsonValueKind.String
                && uuid.GetString() is { Length: > 0 } value)
            {
                return value;
            }
        }

        throw new InvalidOperationException(Loc.T("ClaudeNoOrganization"));
    }

    private async Task<JsonDocument> GetJsonAsync(
        string path,
        string sessionKey,
        CancellationToken cancellationToken,
        bool armBackoff = true)
    {
        // "No response yet": a transport failure must not report the previous
        // call's status code.
        _lastStatusCode = 0;
        using var request = new HttpRequestMessage(HttpMethod.Get, BaseUrl + path)
        {
            Version = HttpVersion.Version20,
            VersionPolicy = HttpVersionPolicy.RequestVersionOrLower
        };
        // Everything the user pasted (a bare key, or a whole browser Cookie
        // header carrying cf_clearance) plus what Cloudflare has issued since.
        string cookieHeader = ClaudeCookies.Merge(ClaudeCookies.Normalize(sessionKey), _serverCookies);
        request.Headers.TryAddWithoutValidation("Cookie", cookieHeader);
        request.Headers.TryAddWithoutValidation("Accept", "application/json");
        request.Headers.TryAddWithoutValidation("Accept-Language", "en-US,en;q=0.9");
        request.Headers.TryAddWithoutValidation("User-Agent", ProductUserAgent);
        request.Headers.TryAddWithoutValidation("Referer", "https://claude.ai/settings/usage");
        request.Headers.TryAddWithoutValidation("Origin", "https://claude.ai");

        using HttpResponseMessage response = await _client.SendAsync(request, cancellationToken);
        RememberCookies(response);
        _lastStatusCode = (int)response.StatusCode;
        if (!response.IsSuccessStatusCode)
        {
            string failure = await DescribeFailureAsync(response, cancellationToken);
            // Pressing "test connection" during a block must not extend it.
            if (armBackoff && failure == Loc.T("ClaudeChallenged"))
            {
                double factor = Math.Pow(2, Math.Min(4, _challengeStrikes));
                TimeSpan backoff = ChallengeBackoff * factor;
                _challengedUntil = DateTimeOffset.Now +
                    (backoff < MaxChallengeBackoff ? backoff : MaxChallengeBackoff);
                _challengeStrikes++;
            }
            throw new InvalidOperationException(failure);
        }

        await using Stream stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        return await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Keeps the cookies the server sets - <c>__cf_bm</c> arrives on nearly every
    /// response, and carrying it back is a large part of continuing to look like
    /// the browser the session came from.
    /// </summary>
    private void RememberCookies(HttpResponseMessage response)
    {
        if (!response.Headers.TryGetValues("Set-Cookie", out IEnumerable<string>? setCookies))
        {
            return;
        }

        foreach (string line in setCookies)
        {
            if (ClaudeCookies.ReadSetCookie(line) is { } cookie)
            {
                _serverCookies[cookie.Name] = cookie.Value;
            }
        }
    }

    /// <summary>
    /// One live round-trip, reported in full: which call failed, its status, and
    /// whether the body is a Cloudflare challenge. Nothing is cached and no
    /// backoff is consulted, because the user pressed the button to find out
    /// what is happening right now.
    /// </summary>
    public async Task<ClaudeConnectionReport> CheckAsync(
        ClaudeUsageSettings settings,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);
        if (settings.SourceKind == ClaudeUsageSourceKind.StatusLine)
        {
            ClaudeUsageSnapshot local = ClaudeStatuslineUsage.Read(StatuslinePath);
            return new ClaudeConnectionReport(
                local.Available,
                StatuslinePath ?? ClaudeStatuslineUsage.DefaultPath(),
                0,
                local.Available ? Loc.T("ClaudeCheckOk") : local.ErrorMessage ?? Loc.T("ClaudeStatuslineNotSetUp"),
                "-");
        }

        if (!settings.IsConfigured)
        {
            return new ClaudeConnectionReport(false, "-", 0, Loc.T("ClaudeCheckNoKey"), "-");
        }

        string stage = "/organizations";
        _lastStatusCode = 0;
        try
        {
            using (JsonDocument organizations = await GetJsonAsync(
                "/organizations", settings.SessionKey, cancellationToken, armBackoff: false))
            {
                settings.OrganizationId = FirstOrganizationId(organizations.RootElement);
            }

            stage = "/usage";
            using JsonDocument document = await GetJsonAsync(
                $"/organizations/{settings.OrganizationId}/usage",
                settings.SessionKey,
                cancellationToken,
                armBackoff: false);
            ClaudeUsageSnapshot snapshot = Parse(document.RootElement, settings.ModelScope, DateTimeOffset.Now);
            return new ClaudeConnectionReport(
                snapshot.Available,
                stage,
                200,
                snapshot.Available ? Loc.T("ClaudeCheckOk") : Loc.T("ClaudeNoWindows"),
                ClaudeCookies.Describe(ClaudeCookies.Merge(
                    ClaudeCookies.Normalize(settings.SessionKey), _serverCookies)));
        }
        catch (Exception ex)
        {
            return new ClaudeConnectionReport(
                false,
                stage,
                _lastStatusCode,
                ex.Message,
                ClaudeCookies.Describe(ClaudeCookies.Merge(
                    ClaudeCookies.Normalize(settings.SessionKey), _serverCookies)));
        }
    }

    private int _lastStatusCode;

    private static async Task<string> DescribeFailureAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
        {
            // Cloudflare marks a challenge with the cf-mitigated HEADER; looking
            // for it in the body never matched, so every challenge without the
            // English interstitial was misreported as a dead session key and sent
            // the user off to regenerate a key that was fine.
            if (response.Headers.Contains("cf-mitigated"))
            {
                return Loc.T("ClaudeChallenged");
            }

            string body = await ReadPreviewAsync(response, cancellationToken);
            return body.Contains("Just a moment", StringComparison.OrdinalIgnoreCase)
                || body.Contains("cf-mitigated", StringComparison.OrdinalIgnoreCase)
                    ? Loc.T("ClaudeChallenged")
                    : Loc.T("ClaudeKeyRejected");
        }

        return response.StatusCode == HttpStatusCode.TooManyRequests
            ? Loc.T("ClaudeRateLimited")
            : Loc.T("ClaudeRequestFailed", (int)response.StatusCode);
    }

    private static async Task<string> ReadPreviewAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        try
        {
            string body = await response.Content.ReadAsStringAsync(cancellationToken);
            return body.Length > 400 ? body[..400] : body;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            return string.Empty;
        }
    }

    /// <summary>
    /// Reads the three windows out of the usage payload.
    ///
    /// Older responses carry the per-model weekly window as its own
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
                    null,
                    displayName.Length > 0 ? displayName : scope);
                break;
            }
        }

        bool available = session is not null || week is not null || modelWeek is not null;
        return available
            ? new ClaudeUsageSnapshot(true, session, week, modelWeek, now)
            : ClaudeUsageSnapshot.Unavailable(Loc.T("ClaudeNoWindows"));
    }

    private ClaudeUsageSnapshot WithLocalTokens(
        ClaudeUsageSnapshot snapshot,
        ClaudeUsageSettings settings,
        DateTimeOffset now)
    {
        if (!settings.CountLocalTokens)
        {
            return snapshot;
        }

        ClaudeCodeTokenTotals totals = _tokenReader.Read(
            now.AddHours(-5),
            now.AddDays(-7),
            settings.ModelScope);
        if (!totals.Available)
        {
            return snapshot;
        }

        return snapshot with
        {
            Session = snapshot.Session is { } session ? session with { TokensUsed = totals.Session } : null,
            Week = snapshot.Week is { } week ? week with { TokensUsed = totals.Week } : null,
            ModelWeek = snapshot.ModelWeek is { } modelWeek ? modelWeek with { TokensUsed = totals.ModelWeek } : null
        };
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

        return new ClaudeUsageWindow(kind, ReadPercent(window, "utilization"), ReadResetsAt(window), null, scopeName);
    }

    private static DateTimeOffset? ReadResetsAt(JsonElement element) =>
        element.TryGetProperty("resets_at", out JsonElement resets)
            && resets.ValueKind == JsonValueKind.String
            && DateTimeOffset.TryParse(resets.GetString(), out DateTimeOffset parsed)
                ? parsed
                : null;

    /// <summary>Utilization arrives as a number or as a string that may carry a percent sign.</summary>
    internal static double ReadPercent(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out JsonElement value))
        {
            return 0;
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

        return 0;
    }

    private static string ReadString(JsonElement element, string propertyName) =>
        element.TryGetProperty(propertyName, out JsonElement value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? string.Empty
            : string.Empty;

    public void Dispose()
    {
        if (_ownsClient)
        {
            _client.Dispose();
        }
    }
}
