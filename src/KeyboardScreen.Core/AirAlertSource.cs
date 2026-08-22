using System.Net;
using System.Net.Http;
using System.Text.Json;

namespace KeyboardScreen.Core;

public sealed class AirAlertSettings
{
    /// <summary>API token from alerts.in.ua; the card links to where to get one.</summary>
    public string Token { get; set; } = string.Empty;

    /// <summary>
    /// Location to watch, matched case-insensitively against the feed's
    /// location titles (e.g. "Київ" matches both "м. Київ" and
    /// "Київська область"). Empty shows the whole-country summary.
    /// </summary>
    public string Location { get; set; } = string.Empty;

    public bool IsConfigured => !string.IsNullOrWhiteSpace(Token);
}

public sealed record AirAlertInfo(string LocationTitle, string AlertType, DateTimeOffset? StartedAt);

public sealed record AirAlertSnapshot(
    bool Available,
    bool AlertActiveAtLocation,
    IReadOnlyList<AirAlertInfo> ActiveAlerts,
    string Location,
    DateTimeOffset UpdatedAt,
    bool IsStale = false,
    string? ErrorMessage = null)
{
    public static AirAlertSnapshot Unavailable(string? message = null) =>
        new(false, false, [], string.Empty, DateTimeOffset.MinValue, false, message);
}

/// <summary>
/// Active air-raid alerts from the alerts.in.ua community API. Thirty-second
/// cache (their guidance asks for gentle polling), stale fallback on failures.
/// The screen carries an honest note: this is an informational display with
/// inherent delays, never the only warning source anyone should rely on.
/// </summary>
public sealed class AirAlertSource : IDisposable
{
    private static readonly TimeSpan CacheDuration = TimeSpan.FromSeconds(30);

    private readonly HttpClient _client;
    private readonly bool _ownsClient;
    private AirAlertSnapshot? _cached;
    private string _cachedKey = string.Empty;
    private DateTimeOffset _lastFetch;

    public AirAlertSource(HttpClient? client = null)
    {
        _ownsClient = client is null;
        _client = client ?? new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
        _client.DefaultRequestHeaders.UserAgent.ParseAdd("KeyboardScreenStudio/1.0");
    }

    /// <summary>Overridable for tests.</summary>
    public string BaseUrl { get; init; } = "https://api.alerts.in.ua/v1/alerts/active.json";

    public async Task<AirAlertSnapshot> ReadAsync(AirAlertSettings settings, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);
        string token = (settings.Token ?? string.Empty).Trim();
        string location = (settings.Location ?? string.Empty).Trim();
        if (token.Length == 0)
        {
            return AirAlertSnapshot.Unavailable();
        }

        string key = token + "|" + location;
        DateTimeOffset now = DateTimeOffset.Now;
        if (_cached is not null && _cachedKey == key && now - _lastFetch < CacheDuration)
        {
            return _cached;
        }

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, BaseUrl);
            request.Headers.TryAddWithoutValidation("Authorization", "Bearer " + token);
            using HttpResponseMessage response = await _client.SendAsync(request, cancellationToken);
            if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
            {
                throw new InvalidOperationException(Loc.T("AirAlertBadToken"));
            }

            if (!response.IsSuccessStatusCode)
            {
                throw new InvalidOperationException(Loc.T("AirAlertRequestFailed", (int)response.StatusCode));
            }

            await using Stream stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using JsonDocument document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            List<AirAlertInfo> alerts = ParseAlerts(document.RootElement);

            _cached = new AirAlertSnapshot(
                true,
                location.Length > 0 && alerts.Any(alert => MatchesLocation(alert.LocationTitle, location)),
                alerts,
                location,
                now);
            _cachedKey = key;
            _lastFetch = now;
            return _cached;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException or InvalidOperationException)
        {
            return _cached is not null && _cachedKey == key
                ? _cached with { IsStale = true, ErrorMessage = ex.Message }
                : AirAlertSnapshot.Unavailable(ex.Message) with { Location = location, UpdatedAt = now };
        }
    }

    private static List<AirAlertInfo> ParseAlerts(JsonElement root)
    {
        var alerts = new List<AirAlertInfo>();
        if (!root.TryGetProperty("alerts", out JsonElement list) || list.ValueKind != JsonValueKind.Array)
        {
            return alerts;
        }

        foreach (JsonElement alert in list.EnumerateArray())
        {
            string title = alert.TryGetProperty("location_title", out JsonElement titleValue)
                ? titleValue.GetString() ?? string.Empty
                : string.Empty;
            if (title.Length == 0)
            {
                continue;
            }

            string type = alert.TryGetProperty("alert_type", out JsonElement typeValue)
                ? typeValue.GetString() ?? "air_raid"
                : "air_raid";
            DateTimeOffset? startedAt = alert.TryGetProperty("started_at", out JsonElement startedValue)
                && DateTimeOffset.TryParse(startedValue.GetString(), out DateTimeOffset parsed)
                ? parsed
                : null;
            alerts.Add(new AirAlertInfo(title, type, startedAt));
        }

        return alerts;
    }

    public static bool MatchesLocation(string locationTitle, string query) =>
        locationTitle.Contains(query, StringComparison.OrdinalIgnoreCase) ||
        query.Contains(locationTitle, StringComparison.OrdinalIgnoreCase);

    public void Dispose()
    {
        if (_ownsClient)
        {
            _client.Dispose();
        }
    }
}
