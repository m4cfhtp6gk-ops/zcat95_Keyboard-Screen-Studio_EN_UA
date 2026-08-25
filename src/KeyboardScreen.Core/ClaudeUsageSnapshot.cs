namespace KeyboardScreen.Core;

/// <summary>Which limit window a <see cref="ClaudeUsageWindow"/> describes.</summary>
public enum ClaudeUsageWindowKind
{
    /// <summary>The rolling five-hour session window.</summary>
    Session,

    /// <summary>The weekly window across every model.</summary>
    Week,

    /// <summary>The weekly window for one model, named by <see cref="ClaudeUsageWindow.ScopeName"/>.</summary>
    ModelWeek
}

/// <summary>
/// One Claude usage window, exactly as the account reports it.
///
/// <see cref="UtilizationPercent"/> is the account's own figure, not arithmetic
/// done here. There is deliberately no token count: the usage payload carries
/// none, and the previous design's local tally measured one machine against a
/// budget the user invented, which looked like data and was not.
/// </summary>
public sealed record ClaudeUsageWindow(
    ClaudeUsageWindowKind Kind,
    double UtilizationPercent,
    DateTimeOffset? ResetsAt = null,
    string? ScopeName = null)
{
    public double ClampedPercent => Math.Clamp(UtilizationPercent, 0d, 100d);

    /// <summary>A window whose reset time has passed reads as empty again.</summary>
    public bool HasReset => ResetsAt is { } resetsAt && resetsAt <= DateTimeOffset.Now;

    public double EffectivePercent => HasReset ? 0d : ClampedPercent;
}

/// <summary>
/// What one diagnostic pass found: whether a Claude Code credential exists on
/// this machine and what claude.ai said when it was used. Never the token.
/// </summary>
/// <param name="Detail">Where the credential came from, or why the call failed.</param>
public sealed record ClaudeConnectionReport(bool Success, string Detail)
{
    public string ToDisplayString() => Success
        ? Loc.T("ClaudeCheckLineOk", Detail)
        : Loc.T("ClaudeCheckLineFailed", Detail);
}

public sealed record ClaudeUsageSnapshot(
    bool Available,
    ClaudeUsageWindow? Session = null,
    ClaudeUsageWindow? Week = null,
    ClaudeUsageWindow? ModelWeek = null,
    DateTimeOffset UpdatedAt = default,
    bool IsStale = false,
    string? ErrorMessage = null)
{
    /// <summary>
    /// Every per-model window the account reported, whether or not one of them
    /// is the configured scope.
    ///
    /// The model row is chosen by a name the user types, and until this existed
    /// a name the account does not meter - "fable", say - simply produced no
    /// third row and no explanation. Anthropic decides which models get their
    /// own weekly window; the only honest thing the app can do is show which
    /// ones came back.
    /// </summary>
    public IReadOnlyList<string> AvailableModelScopes { get; init; } = [];

    public static ClaudeUsageSnapshot Unavailable(string? message = null) =>
        new(false, UpdatedAt: DateTimeOffset.MinValue, ErrorMessage: message);

    /// <summary>Windows in display order, skipping any the account did not report.</summary>
    public IEnumerable<ClaudeUsageWindow> Windows
    {
        get
        {
            if (Session is { } session)
            {
                yield return session;
            }

            if (Week is { } week)
            {
                yield return week;
            }

            if (ModelWeek is { } modelWeek)
            {
                yield return modelWeek;
            }
        }
    }
}
