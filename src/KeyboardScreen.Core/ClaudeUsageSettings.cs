namespace KeyboardScreen.Core;

public sealed class ClaudeUsageSettings
{
    /// <summary>The model whose weekly window fills the third meter.</summary>
    public const string DefaultModelScope = "Fable";

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

    public bool IsConfigured => !string.IsNullOrWhiteSpace(SessionKey);
}
