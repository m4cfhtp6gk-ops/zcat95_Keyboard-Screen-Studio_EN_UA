namespace KeyboardScreen.Core;

/// <summary>
/// Settings for the Claude limits screen.
///
/// There is nothing to sign in to and nothing to paste. The screen borrows the
/// OAuth token Claude Code already keeps on this machine and asks claude.ai for
/// the real windows, so what is left here is which model fills the third meter
/// and one cached id.
///
/// An earlier design asked the user for a token budget and drew a percentage
/// against it. That number was arithmetic about a target the user invented, not
/// about the account, and it is gone: the meters now show what the account
/// actually reports, or the screen says plainly that it cannot reach it.
/// </summary>
public sealed class ClaudeUsageSettings
{
    /// <summary>Matched against the model id the usage payload reports, case-insensitively.</summary>
    public const string DefaultModelScope = "opus";

    /// <summary>The model whose weekly window fills the third meter.</summary>
    public string ModelScope { get; set; } = DefaultModelScope;

    /// <summary>
    /// Resolved once from the token and kept so each refresh costs a single
    /// request. Cleared automatically when the server stops recognising it.
    /// </summary>
    public string OrganizationId { get; set; } = string.Empty;

    /// <summary>
    /// Nothing to configure: the credential either exists on this machine or it
    /// does not, and that is reported on the screen rather than guarded here.
    /// </summary>
    public bool IsConfigured => true;
}
