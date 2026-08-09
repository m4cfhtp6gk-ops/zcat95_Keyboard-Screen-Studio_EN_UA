using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace KeyboardScreen.Core;

public sealed record UpdateCheckResult(
    bool Available,
    Version? LatestVersion,
    string? TagName,
    string? HtmlUrl,
    string? Error);

/// <summary>
/// Checks the GitHub releases feed for a newer public version of KSS.
/// </summary>
public sealed class ReleaseUpdateChecker
{
    public const string Repository = "zcat95/Keyboard-Screen-Studio";
    private const string LatestReleaseApi = "https://api.github.com/repos/" + Repository + "/releases/latest";
    private readonly HttpClient _http;

    public ReleaseUpdateChecker(HttpClient? http = null)
    {
        _http = http ?? new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(10)
        };
    }

    public static Version? TryParseTagVersion(string? tagName)
    {
        if (string.IsNullOrWhiteSpace(tagName))
        {
            return null;
        }

        string candidate = tagName.Trim().TrimStart('v', 'V');
        return Version.TryParse(candidate, out Version? version) ? version : null;
    }

    public static bool IsNewerThan(string? tagName, Version currentVersion) =>
        TryParseTagVersion(tagName) is Version latest && latest > currentVersion;

    public async Task<UpdateCheckResult> CheckAsync(
        Version currentVersion,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, LatestReleaseApi);
            request.Headers.TryAddWithoutValidation("User-Agent", "KeyboardScreenStudio");
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
            using HttpResponseMessage response = await _http.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return new UpdateCheckResult(false, null, null, null, $"HTTP {(int)response.StatusCode}");
            }

            using JsonDocument document = JsonDocument.Parse(
                await response.Content.ReadAsStringAsync(cancellationToken));
            JsonElement root = document.RootElement;
            string? tagName = root.TryGetProperty("tag_name", out JsonElement tag) ? tag.GetString() : null;
            string? htmlUrl = root.TryGetProperty("html_url", out JsonElement url) ? url.GetString() : null;
            Version? latest = TryParseTagVersion(tagName);
            return new UpdateCheckResult(
                latest is not null && latest > currentVersion,
                latest,
                tagName,
                htmlUrl,
                null);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new UpdateCheckResult(false, null, null, null, "超时");
        }
        catch (Exception exception)
        {
            return new UpdateCheckResult(false, null, null, null, exception.Message);
        }
    }
}
