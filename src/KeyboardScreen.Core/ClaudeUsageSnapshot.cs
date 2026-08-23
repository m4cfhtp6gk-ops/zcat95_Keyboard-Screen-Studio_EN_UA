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
/// One Claude usage window.
///
/// <see cref="TokensUsed"/> is summed from the local Claude Code transcripts and
/// only covers this machine — a floor on real usage, never the account total.
/// <see cref="UtilizationPercent"/> is that figure measured against the budget
/// the user set, not a quota reported by any server: no local file records the
/// account's real limits, and every remote path that claimed to has since
/// stopped working. A percentage of your own target is honest; a percentage of
/// a guessed quota would not be.
/// </summary>
public sealed record ClaudeUsageWindow(
    ClaudeUsageWindowKind Kind,
    double UtilizationPercent,
    DateTimeOffset? ResetsAt = null,
    long? TokensUsed = null,
    string? ScopeName = null)
{
    public double ClampedPercent => Math.Clamp(UtilizationPercent, 0d, 100d);

    /// <summary>A window whose reset time has passed reads as empty again.</summary>
    public bool HasReset => ResetsAt is { } resetsAt && resetsAt <= DateTimeOffset.Now;

    public double EffectivePercent => HasReset ? 0d : ClampedPercent;
}

/// <summary>The three limit windows drawn by <see cref="ClaudeUsageTheme"/>.</summary>
/// <summary>
/// What one diagnostic pass found on this machine. There is no server in this
/// path any more, so the useful facts are local: whether Claude Code's
/// transcript directory is where we expect it, and how much it actually yielded.
/// </summary>
/// <param name="Detail">The directory checked, or the reason it produced nothing.</param>
public sealed record ClaudeConnectionReport(
    bool Success,
    string Detail,
    long TokensThisWeek = 0)
{
    public string ToDisplayString() => Success
        ? Loc.T("ClaudeCheckLineOk", ClaudeUsageTheme.FormatTokens(TokensThisWeek), Detail)
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
