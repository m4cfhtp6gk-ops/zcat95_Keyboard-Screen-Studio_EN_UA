using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace KeyboardScreen.Core;

public sealed class GitHubSettings
{
    public string Username { get; set; } = string.Empty;

    /// <summary>
    /// Optional token for the GraphQL API (adds private contributions). Stored
    /// locally and sent to api.github.com only; the risk notice says so.
    /// </summary>
    public string Token { get; set; } = string.Empty;

    public bool IsConfigured => !string.IsNullOrWhiteSpace(Username);
}

public sealed record GitHubContributionDay(DateOnly Date, int Level, int Count);

public sealed record GitHubContributionSnapshot(
    bool Available,
    string Username,
    IReadOnlyList<GitHubContributionDay> Days,
    DateTimeOffset UpdatedAt,
    bool IsStale = false,
    string? ErrorMessage = null)
{
    public static GitHubContributionSnapshot Unavailable(string? message = null) =>
        new(false, string.Empty, [], DateTimeOffset.MinValue, false, message);
}

/// <summary>
/// The contribution calendar. Without a token it parses the public HTML
/// fragment at github.com/users/{name}/contributions; with a token it asks the
/// GraphQL API, which also counts private contributions.
/// </summary>
public sealed partial class GitHubContributionSource : IDisposable
{
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(30);

    private readonly HttpClient _client;
    private readonly bool _ownsClient;
    private GitHubContributionSnapshot? _cached;
    private string _cachedKey = string.Empty;
    private DateTimeOffset _lastFetch;

    public GitHubContributionSource(HttpClient? client = null)
    {
        _ownsClient = client is null;
        _client = client ?? new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
        _client.DefaultRequestHeaders.UserAgent.ParseAdd("KeyboardScreenStudio/1.0");
    }

    /// <summary>Overridable for tests.</summary>
    public string HtmlBaseUrl { get; init; } = "https://github.com/users/";

    /// <summary>Overridable for tests.</summary>
    public string GraphQlUrl { get; init; } = "https://api.github.com/graphql";

    public async Task<GitHubContributionSnapshot> ReadAsync(GitHubSettings settings, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);
        string username = (settings.Username ?? string.Empty).Trim().TrimStart('@');
        string token = (settings.Token ?? string.Empty).Trim();
        if (username.Length == 0)
        {
            return GitHubContributionSnapshot.Unavailable();
        }

        string key = username + "|" + (token.Length > 0);
        DateTimeOffset now = DateTimeOffset.Now;
        if (_cached is not null && _cachedKey == key && now - _lastFetch < CacheDuration)
        {
            return _cached;
        }

        try
        {
            List<GitHubContributionDay> days = token.Length > 0
                ? await ReadGraphQlAsync(username, token, cancellationToken)
                : await ReadHtmlAsync(username, cancellationToken);
            days.Sort((a, b) => a.Date.CompareTo(b.Date));
            if (days.Count == 0)
            {
                throw new InvalidOperationException(Loc.T("GitHubParseFailed"));
            }

            _cached = new GitHubContributionSnapshot(true, username, days, now);
            _cachedKey = key;
            _lastFetch = now;
            return _cached;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException or InvalidOperationException)
        {
            return _cached is not null && _cachedKey == key
                ? _cached with { IsStale = true, ErrorMessage = ex.Message }
                : GitHubContributionSnapshot.Unavailable(ex.Message) with { Username = username, UpdatedAt = now };
        }
    }

    private async Task<List<GitHubContributionDay>> ReadHtmlAsync(string username, CancellationToken cancellationToken)
    {
        using HttpResponseMessage response = await _client.GetAsync(
            HtmlBaseUrl + Uri.EscapeDataString(username) + "/contributions", cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            throw new InvalidOperationException(Loc.T("GitHubUserNotFound", username));
        }

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(Loc.T("GitHubRequestFailed", (int)response.StatusCode));
        }

        string html = await response.Content.ReadAsStringAsync(cancellationToken);
        return ParseContributionHtml(html);
    }

    /// <summary>
    /// Parses the calendar cells and their tool-tip captions. Attribute order
    /// inside the td tag is not guaranteed, so each attribute is pulled out on
    /// its own; a day whose caption cannot be joined keeps Count = -1.
    /// </summary>
    public static List<GitHubContributionDay> ParseContributionHtml(string html)
    {
        var counts = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (Match tip in ToolTipPattern().Matches(html))
        {
            string text = tip.Groups[2].Value.Trim();
            Match leading = LeadingNumberPattern().Match(text);
            counts[tip.Groups[1].Value] = leading.Success
                ? int.Parse(leading.Groups[1].Value.Replace(",", ""))
                : text.StartsWith("No ", StringComparison.OrdinalIgnoreCase) ? 0 : -1;
        }

        var days = new List<GitHubContributionDay>();
        foreach (Match cell in DayCellPattern().Matches(html))
        {
            string tag = cell.Value;
            Match date = DateAttributePattern().Match(tag);
            Match level = LevelAttributePattern().Match(tag);
            if (!date.Success || !level.Success ||
                !DateOnly.TryParse(date.Groups[1].Value, out DateOnly day) ||
                !int.TryParse(level.Groups[1].Value, out int levelValue))
            {
                continue;
            }

            Match id = IdAttributePattern().Match(tag);
            int count = id.Success && counts.TryGetValue(id.Groups[1].Value, out int known) ? known : -1;
            days.Add(new GitHubContributionDay(day, Math.Clamp(levelValue, 0, 4), count));
        }

        return days;
    }

    private async Task<List<GitHubContributionDay>> ReadGraphQlAsync(string username, string token, CancellationToken cancellationToken)
    {
        const string query = "query($login:String!){user(login:$login){contributionsCollection{contributionCalendar{weeks{contributionDays{date contributionCount contributionLevel}}}}}}";
        string payload = JsonSerializer.Serialize(new { query, variables = new { login = username } });
        using var request = new HttpRequestMessage(HttpMethod.Post, GraphQlUrl)
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json")
        };
        request.Headers.TryAddWithoutValidation("Authorization", "bearer " + token);

        using HttpResponseMessage response = await _client.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(Loc.T("GitHubRequestFailed", (int)response.StatusCode));
        }

        await using Stream stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using JsonDocument document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        if (!document.RootElement.TryGetProperty("data", out JsonElement data) ||
            data.ValueKind != JsonValueKind.Object ||
            !data.TryGetProperty("user", out JsonElement user) ||
            user.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidOperationException(Loc.T("GitHubUserNotFound", username));
        }

        var days = new List<GitHubContributionDay>();
        foreach (JsonElement week in user
                     .GetProperty("contributionsCollection")
                     .GetProperty("contributionCalendar")
                     .GetProperty("weeks").EnumerateArray())
        {
            foreach (JsonElement day in week.GetProperty("contributionDays").EnumerateArray())
            {
                if (!DateOnly.TryParse(day.GetProperty("date").GetString(), out DateOnly date))
                {
                    continue;
                }

                int count = day.GetProperty("contributionCount").GetInt32();
                int level = day.GetProperty("contributionLevel").GetString() switch
                {
                    "FIRST_QUARTILE" => 1,
                    "SECOND_QUARTILE" => 2,
                    "THIRD_QUARTILE" => 3,
                    "FOURTH_QUARTILE" => 4,
                    _ => 0
                };
                days.Add(new GitHubContributionDay(date, level, count));
            }
        }

        return days;
    }

    [GeneratedRegex("data-date=\"([^\"]*)\"", RegexOptions.CultureInvariant)]
    private static partial Regex DateAttributePattern();

    [GeneratedRegex("data-level=\"([^\"]*)\"", RegexOptions.CultureInvariant)]
    private static partial Regex LevelAttributePattern();

    [GeneratedRegex(" id=\"([^\"]*)\"", RegexOptions.CultureInvariant)]
    private static partial Regex IdAttributePattern();

    [GeneratedRegex("<td\\b[^>]*data-date=\"[^\"]*\"[^>]*>", RegexOptions.CultureInvariant)]
    private static partial Regex DayCellPattern();

    [GeneratedRegex("<tool-tip[^>]*for=\"([^\"]+)\"[^>]*>([^<]*)</tool-tip>", RegexOptions.CultureInvariant)]
    private static partial Regex ToolTipPattern();

    [GeneratedRegex("^([0-9,]+)", RegexOptions.CultureInvariant)]
    private static partial Regex LeadingNumberPattern();

    public void Dispose()
    {
        if (_ownsClient)
        {
            _client.Dispose();
        }
    }
}
