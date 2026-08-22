using System.Net;
using System.Net.Http;
using System.Text.Json;

namespace KeyboardScreen.Core;

public enum AirAlertTakeoverMode
{
    Off = 0,
    FullScreen = 1,
    Popup = 2
}

public sealed class AirAlertSettings
{
    /// <summary>API token from alerts.in.ua; the card links to where to get one.</summary>
    public string Token { get; set; } = string.Empty;

    /// <summary>
    /// Location to watch, matched case-insensitively against the feed's
    /// location titles and oblasts (e.g. "Київ" matches both "м. Київ" and
    /// "Київська область"). Empty shows the whole-country summary.
    /// </summary>
    public string Location { get; set; } = string.Empty;

    /// <summary>
    /// How an alert in the watched location takes over the screen while some
    /// other theme is showing: not at all, as a full-screen switch to the
    /// alerts theme, or as a popup banner over the current theme.
    /// </summary>
    public AirAlertTakeoverMode Takeover { get; set; } = AirAlertTakeoverMode.Off;

    /// <summary>Keep the takeover up until the all-clear (otherwise <see cref="TakeoverMinutes"/>).</summary>
    public bool TakeoverUntilClear { get; set; } = true;

    /// <summary>Takeover duration in minutes when not holding until the all-clear.</summary>
    public int TakeoverMinutes { get; set; } = 5;

    public bool IsConfigured => !string.IsNullOrWhiteSpace(Token);
}

/// <summary>The region names alerts.in.ua reports at oblast level, for the settings dropdown.</summary>
public static class AirAlertRegions
{
    public static readonly IReadOnlyList<string> All =
    [
        "м. Київ",
        "Київська область",
        "Вінницька область",
        "Волинська область",
        "Дніпропетровська область",
        "Донецька область",
        "Житомирська область",
        "Закарпатська область",
        "Запорізька область",
        "Івано-Франківська область",
        "Кіровоградська область",
        "Луганська область",
        "Львівська область",
        "Миколаївська область",
        "Одеська область",
        "Полтавська область",
        "Рівненська область",
        "Сумська область",
        "Тернопільська область",
        "Харківська область",
        "Херсонська область",
        "Хмельницька область",
        "Черкаська область",
        "Чернівецька область",
        "Чернігівська область",
        "Автономна Республіка Крим",
        "м. Севастополь"
    ];
}

public sealed record AirAlertInfo(
    string LocationTitle,
    string AlertType,
    DateTimeOffset? StartedAt,
    string LocationOblast = "");

public sealed record AirAlertSnapshot(
    bool Available,
    bool AlertActiveAtLocation,
    IReadOnlyList<AirAlertInfo> ActiveAlerts,
    string Location,
    DateTimeOffset UpdatedAt,
    bool IsStale = false,
    string? ErrorMessage = null,
    DateTimeOffset? LastAlertStartedAt = null,
    DateTimeOffset? LastAlertEndedAt = null)
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
    private readonly HttpClient _client;
    private readonly bool _ownsClient;
    private AirAlertSnapshot? _cached;
    private string _cachedKey = string.Empty;
    private DateTimeOffset _lastFetch;

    // The location's alert history, so the clear screen can say how long the
    // last alert lasted. In-memory only: after a restart during quiet skies
    // there is nothing to show, because the API lists active alerts only.
    private string _trackedKey = string.Empty;
    private DateTimeOffset? _activeSince;
    private DateTimeOffset? _lastAlertStartedAt;
    private DateTimeOffset? _lastAlertEndedAt;

    public AirAlertSource(HttpClient? client = null)
    {
        _ownsClient = client is null;
        _client = client ?? new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
        _client.DefaultRequestHeaders.UserAgent.ParseAdd("KeyboardScreenStudio/1.0");
    }

    /// <summary>Overridable for tests.</summary>
    public string BaseUrl { get; init; } = "https://api.alerts.in.ua/v1/alerts/active.json";

    /// <summary>Thirty seconds in production (their polling guidance); tests shorten it.</summary>
    public TimeSpan CacheDuration { get; init; } = TimeSpan.FromSeconds(30);

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

            bool active = location.Length > 0 && alerts.Any(alert => Matches(alert, location));
            TrackLastAlert(key, location, active, alerts, now);

            _cached = new AirAlertSnapshot(
                true,
                active,
                alerts,
                location,
                now,
                LastAlertStartedAt: _lastAlertStartedAt,
                LastAlertEndedAt: _lastAlertEndedAt);
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
            string oblast = alert.TryGetProperty("location_oblast", out JsonElement oblastValue)
                && oblastValue.ValueKind == JsonValueKind.String
                ? oblastValue.GetString() ?? string.Empty
                : string.Empty;
            alerts.Add(new AirAlertInfo(title, type, startedAt, oblast));
        }

        return alerts;
    }

    /// <summary>
    /// Remembers when the location's alert began and, once the feed goes
    /// quiet again, how long it lasted. A new alert clears the record.
    /// </summary>
    private void TrackLastAlert(string key, string location, bool active, List<AirAlertInfo> alerts, DateTimeOffset now)
    {
        if (_trackedKey != key)
        {
            _trackedKey = key;
            _activeSince = null;
            _lastAlertStartedAt = null;
            _lastAlertEndedAt = null;
        }

        if (active)
        {
            _activeSince = EarliestStart(alerts, location) ?? _activeSince ?? now;
            _lastAlertStartedAt = null;
            _lastAlertEndedAt = null;
        }
        else if (_activeSince is { } started)
        {
            _lastAlertStartedAt = started;
            _lastAlertEndedAt = now;
            _activeSince = null;
        }
    }

    /// <summary>The start time of the location's alert: the earliest among the matching entries.</summary>
    public static DateTimeOffset? EarliestStart(IEnumerable<AirAlertInfo> alerts, string location) =>
        alerts
            .Where(alert => Matches(alert, location))
            .Select(alert => alert.StartedAt)
            .Where(startedAt => startedAt is not null)
            .OrderBy(startedAt => startedAt)
            .FirstOrDefault();

    public static bool MatchesLocation(string locationTitle, string query) =>
        locationTitle.Contains(query, StringComparison.OrdinalIgnoreCase) ||
        query.Contains(locationTitle, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// A raion or hromada alert also counts for its oblast, so picking
    /// "Харківська область" from the dropdown reacts to "Куп'янський район".
    /// </summary>
    public static bool Matches(AirAlertInfo alert, string query) =>
        MatchesLocation(alert.LocationTitle, query) ||
        (alert.LocationOblast.Length > 0 && MatchesLocation(alert.LocationOblast, query));

    public void Dispose()
    {
        if (_ownsClient)
        {
            _client.Dispose();
        }
    }
}
