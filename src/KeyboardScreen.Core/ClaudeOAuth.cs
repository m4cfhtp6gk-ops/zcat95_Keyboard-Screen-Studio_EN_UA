using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace KeyboardScreen.Core;

/// <summary>
/// A signed-in Claude account: the access token and the refresh token that
/// mints new ones. Held by this app rather than borrowed from Claude Code, so
/// unlike a borrowed credential it is written to disk - see
/// <see cref="ClaudeOAuthStore"/>, which keeps it encrypted.
/// </summary>
public sealed record ClaudeOAuthTokens(string AccessToken, string RefreshToken, DateTimeOffset ExpiresAt)
{
    /// <summary>Refresh a little before the hour is up, so a call never races the expiry.</summary>
    public bool NeedsRefresh => DateTimeOffset.Now >= ExpiresAt - TimeSpan.FromMinutes(2);

    public bool CanRefresh => RefreshToken.Length > 0;
}

/// <summary>What a sign-in attempt produced, so the UI can say why one failed.</summary>
public sealed record ClaudeOAuthResult(ClaudeOAuthTokens? Tokens, string? Error)
{
    public bool Success => Tokens is not null;
}

/// <summary>
/// The OAuth authorization-code-with-PKCE flow that Claude Code uses, run by
/// this app on the user's behalf.
///
/// It exists so the Claude screen works for someone who has never installed the
/// Claude Code command-line tool: press a button, approve in the browser, paste
/// the code back. What comes out is the same kind of bearer token the usage
/// endpoint already takes, so nothing downstream changes.
///
/// The client id is Claude Code's own public client. There is no secret - PKCE
/// is what a public client uses instead - and this app never sees the password:
/// the browser does the signing in, and hands back only a short-lived code that
/// is worthless without the verifier kept in memory here.
/// </summary>
public sealed class ClaudeOAuth
{
    // Claude Code's public OAuth client. A public client carries no secret; the
    // PKCE verifier is the proof that the app redeeming the code is the same one
    // that started the flow.
    public const string ClientId = "9d1c250a-e61b-44d9-88ed-5944d1962f5e";

    // The manual-code redirect: after approving, the browser lands on a page
    // that shows "CODE#STATE" to copy, rather than calling back to a local port.
    // No listener, no firewall prompt, works the same everywhere - the same
    // choice `claude setup-token` makes.
    public const string RedirectUri = "https://console.anthropic.com/oauth/code/callback";

    // Read usage and profile, nothing more. org:create_api_key is deliberately
    // not requested: this screen never creates a key, and a stored token that
    // could is a liability with no purpose here.
    public const string Scope = "user:profile user:inference";

    private const string AuthorizeUrl = "https://claude.ai/oauth/authorize";
    private const string TokenUrl = "https://console.anthropic.com/v1/oauth/token";

    private readonly HttpClient _client;

    public ClaudeOAuth(HttpClient? client = null)
    {
        _client = client ?? new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
    }

    /// <summary>Overridable so a test can point the exchange at a stub.</summary>
    public string TokenEndpoint { get; init; } = TokenUrl;

    public string AuthorizeEndpoint { get; init; } = AuthorizeUrl;

    /// <summary>
    /// One attempt at signing in, from a fresh challenge to the browser URL the
    /// user opens. The verifier and state are kept to finish the exchange.
    /// </summary>
    public ClaudeOAuthChallenge BeginSignIn()
    {
        string verifier = RandomUrlToken(32);
        string state = RandomUrlToken(24);
        string challenge = Base64Url(SHA256.HashData(Encoding.ASCII.GetBytes(verifier)));

        var query = new Dictionary<string, string>
        {
            ["code"] = "true",
            ["client_id"] = ClientId,
            ["response_type"] = "code",
            ["redirect_uri"] = RedirectUri,
            ["scope"] = Scope,
            ["code_challenge"] = challenge,
            ["code_challenge_method"] = "S256",
            ["state"] = state
        };

        string url = AuthorizeEndpoint + "?" + string.Join("&",
            query.Select(pair => Uri.EscapeDataString(pair.Key) + "=" + Uri.EscapeDataString(pair.Value)));
        return new ClaudeOAuthChallenge(url, verifier, state);
    }

    /// <summary>
    /// Finish the flow with what the user pasted back. The pasted value is
    /// "CODE#STATE"; the state must match the one we sent, or this is a code from
    /// a different attempt (or a forged one) and is refused.
    /// </summary>
    public async Task<ClaudeOAuthResult> CompleteSignInAsync(
        ClaudeOAuthChallenge challenge,
        string pastedCode,
        CancellationToken cancellationToken = default)
    {
        string trimmed = (pastedCode ?? string.Empty).Trim();
        if (trimmed.Length == 0)
        {
            return new ClaudeOAuthResult(null, Loc.T("ClaudeOAuthNoCode"));
        }

        string[] parts = trimmed.Split('#', 2);
        string code = parts[0].Trim();
        string returnedState = parts.Length > 1 ? parts[1].Trim() : string.Empty;

        // A returned state that does not match is the one thing worth stopping
        // for: it means the code belongs to a different flow than this verifier.
        if (returnedState.Length > 0 && !FixedTimeEquals(returnedState, challenge.State))
        {
            return new ClaudeOAuthResult(null, Loc.T("ClaudeOAuthStateMismatch"));
        }

        var body = new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code",
            ["code"] = code,
            ["state"] = returnedState.Length > 0 ? returnedState : challenge.State,
            ["client_id"] = ClientId,
            ["redirect_uri"] = RedirectUri,
            ["code_verifier"] = challenge.Verifier
        };

        return await PostForTokensAsync(body, cancellationToken);
    }

    /// <summary>Trade a refresh token for a fresh access token when the hour is up.</summary>
    public async Task<ClaudeOAuthResult> RefreshAsync(
        string refreshToken,
        CancellationToken cancellationToken = default)
    {
        var body = new Dictionary<string, string>
        {
            ["grant_type"] = "refresh_token",
            ["refresh_token"] = refreshToken,
            ["client_id"] = ClientId
        };

        return await PostForTokensAsync(body, cancellationToken);
    }

    private async Task<ClaudeOAuthResult> PostForTokensAsync(
        Dictionary<string, string> body,
        CancellationToken cancellationToken)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, TokenEndpoint)
            {
                Content = JsonContent.Create(body)
            };
            request.Headers.Accept.ParseAdd("application/json");

            using HttpResponseMessage response = await _client.SendAsync(request, cancellationToken);
            string payload = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return new ClaudeOAuthResult(null,
                    Loc.T("ClaudeOAuthExchangeFailed", (int)response.StatusCode));
            }

            ClaudeOAuthTokens? tokens = ParseTokens(payload, DateTimeOffset.Now);
            return tokens is null
                ? new ClaudeOAuthResult(null, Loc.T("ClaudeOAuthNoToken"))
                : new ClaudeOAuthResult(tokens, null);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            return new ClaudeOAuthResult(null, ex.Message);
        }
    }

    /// <summary>
    /// Reads the token response. A refresh reply sometimes omits the refresh
    /// token, meaning "keep the one you have", so a caller passes the previous
    /// one in as the fallback rather than losing the ability to refresh again.
    /// </summary>
    internal static ClaudeOAuthTokens? ParseTokens(
        string json, DateTimeOffset now, string? previousRefreshToken = null)
    {
        try
        {
            using JsonDocument document = JsonDocument.Parse(json);
            JsonElement root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            string access = ReadString(root, "access_token");
            if (access.Length == 0)
            {
                return null;
            }

            string refresh = ReadString(root, "refresh_token");
            if (refresh.Length == 0)
            {
                refresh = previousRefreshToken ?? string.Empty;
            }

            double seconds = root.TryGetProperty("expires_in", out JsonElement expires)
                && expires.ValueKind == JsonValueKind.Number
                && expires.TryGetDouble(out double parsed)
                    ? parsed
                    : 3600;

            return new ClaudeOAuthTokens(access, refresh, now.AddSeconds(seconds));
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string ReadString(JsonElement element, string name) =>
        element.TryGetProperty(name, out JsonElement value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? string.Empty
            : string.Empty;

    /// <summary>A URL-safe random token of <paramref name="bytes"/> bytes of entropy.</summary>
    internal static string RandomUrlToken(int bytes) => Base64Url(RandomNumberGenerator.GetBytes(bytes));

    private static string Base64Url(byte[] data) =>
        Convert.ToBase64String(data).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static bool FixedTimeEquals(string a, string b) =>
        CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(a), Encoding.UTF8.GetBytes(b));
}

/// <summary>
/// A sign-in in progress: the URL to open, and the two secrets kept in memory
/// to finish it. Neither the verifier nor the state is ever written down.
/// </summary>
public sealed record ClaudeOAuthChallenge(string Url, string Verifier, string State);
