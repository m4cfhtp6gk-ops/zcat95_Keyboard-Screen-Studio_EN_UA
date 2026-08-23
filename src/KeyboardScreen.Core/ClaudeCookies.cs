using System.Globalization;

namespace KeyboardScreen.Core;

/// <summary>
/// The cookie half of talking to claude.ai.
///
/// A browser sends far more than <c>sessionKey</c>: Cloudflare issues
/// <c>__cf_bm</c> on nearly every response and <c>cf_clearance</c> once a
/// challenge is solved, and a request that never carries them looks like a bot
/// no matter how good its headers are. So the settings field accepts the whole
/// Cookie header copied out of the browser's devtools, and whatever the server
/// hands back is kept for the next request.
///
/// Pure string handling, so the smoke tests cover it without a network.
/// </summary>
public static class ClaudeCookies
{
    /// <summary>Cookies Cloudflare issues; carrying them back is what keeps a session trusted.</summary>
    public static readonly IReadOnlyList<string> ChallengeCookieNames = ["cf_clearance", "__cf_bm"];

    /// <summary>
    /// Turns whatever the user pasted into a Cookie header value. A bare key
    /// becomes <c>sessionKey=…</c>; a full header (with or without a leading
    /// "Cookie:") is kept as it is, minus stray whitespace.
    /// </summary>
    public static string Normalize(string? pasted)
    {
        string text = (pasted ?? string.Empty).Trim();
        if (text.Length == 0)
        {
            return string.Empty;
        }

        if (text.StartsWith("Cookie:", StringComparison.OrdinalIgnoreCase))
        {
            text = text["Cookie:".Length..].Trim();
        }

        if (!text.Contains('='))
        {
            return "sessionKey=" + text;
        }

        string[] pairs = text
            .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(pair => pair.Contains('='))
            .ToArray();
        return string.Join("; ", pairs);
    }

    /// <summary>Splits a Cookie header value into its pairs, last occurrence winning.</summary>
    public static Dictionary<string, string> Parse(string? cookieHeader)
    {
        var jar = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (string pair in (cookieHeader ?? string.Empty)
                     .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            int split = pair.IndexOf('=');
            if (split > 0)
            {
                jar[pair[..split].Trim()] = pair[(split + 1)..].Trim();
            }
        }
        return jar;
    }

    /// <summary>The name=value of one <c>Set-Cookie</c> line, ignoring its attributes.</summary>
    public static (string Name, string Value)? ReadSetCookie(string? setCookie)
    {
        string first = (setCookie ?? string.Empty).Split(';')[0].Trim();
        int split = first.IndexOf('=');
        if (split <= 0)
        {
            return null;
        }

        string name = first[..split].Trim();
        string value = first[(split + 1)..].Trim();
        // A deletion (empty value) must not shadow a good cookie we already hold.
        return name.Length > 0 && value.Length > 0 ? (name, value) : null;
    }

    /// <summary>
    /// The Cookie header to send: what the user pasted, plus what the server has
    /// issued since. The user's own values win - a stale <c>sessionKey</c> from a
    /// response must never replace the one they just pasted.
    /// </summary>
    public static string Merge(string? userCookies, IReadOnlyDictionary<string, string>? serverCookies)
    {
        Dictionary<string, string> jar = Parse(userCookies);
        foreach ((string name, string value) in serverCookies ?? new Dictionary<string, string>())
        {
            if (!jar.ContainsKey(name))
            {
                jar[name] = value;
            }
        }

        return string.Join("; ", jar.Select(pair => pair.Key + "=" + pair.Value));
    }

    /// <summary>Whether the request carries a Cloudflare cookie, for the diagnostics line.</summary>
    public static bool HasChallengeCookie(string? cookieHeader)
    {
        Dictionary<string, string> jar = Parse(cookieHeader);
        return ChallengeCookieNames.Any(jar.ContainsKey);
    }

    /// <summary>Masks everything but the shape, so a diagnostic can be pasted into a chat safely.</summary>
    public static string Describe(string? cookieHeader)
    {
        Dictionary<string, string> jar = Parse(cookieHeader);
        if (jar.Count == 0)
        {
            return "-";
        }

        return string.Join(", ", jar.Select(pair =>
            pair.Key + "(" + pair.Value.Length.ToString(CultureInfo.InvariantCulture) + ")"));
    }
}
