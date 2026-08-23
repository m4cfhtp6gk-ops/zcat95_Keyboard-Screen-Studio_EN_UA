namespace KeyboardScreen.Core;

public enum ClaudeUsageSourceKind
{
    /// <summary>Claude Code's own rate-limit report: no credentials, no network, no Cloudflare.</summary>
    StatusLine = 0,

    /// <summary>The claude.ai web endpoint with a browser cookie; the only source of the per-model window.</summary>
    WebCookie = 1
}

public sealed class ClaudeUsageSettings
{
    /// <summary>The model whose weekly window fills the third meter.</summary>
    public const string DefaultModelScope = "Fable";

    /// <summary>
    /// Where the limits come from. The status line needs no credentials and
    /// never touches claude.ai, so it is the default; the cookie path stays for
    /// the per-model window it alone reports.
    /// </summary>
    public ClaudeUsageSourceKind SourceKind { get; set; } = ClaudeUsageSourceKind.StatusLine;

    /// <summary>
    /// The claude.ai session cookie. It is kept in this machine's settings file
    /// and sent to claude.ai and nowhere else.
    /// </summary>
    public string SessionKey { get; set; } = string.Empty;

    /// <summary>
    /// Organization id, resolved once from the session key and cached so each
    /// refresh costs a single request.
    /// </summary>
    public string OrganizationId { get; set; } = string.Empty;

    public string ModelScope { get; set; } = DefaultModelScope;

    /// <summary>Count tokens from the local Claude Code transcripts.</summary>
    public bool CountLocalTokens { get; set; } = true;

    /// <summary>The cookie path needs a key; the status line needs nothing at all.</summary>
    public bool IsConfigured => SourceKind == ClaudeUsageSourceKind.StatusLine
        || !string.IsNullOrWhiteSpace(SessionKey);
}
