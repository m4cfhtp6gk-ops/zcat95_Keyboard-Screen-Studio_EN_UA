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
/// One Claude limit window.
///
/// <see cref="UtilizationPercent"/> is what the account actually reports. Token
/// counts are not part of any limit response, so <see cref="TokensUsed"/> is
/// summed from the local Claude Code transcripts and only covers this machine —
/// it is a floor on real usage, never the account total, and it is null when no
/// transcripts were read.
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
/// What one diagnostic round-trip found. Deliberately verbatim: the point is to
/// replace guesswork with the status code and body the server actually sent.
/// </summary>
/// <param name="Cookies">Cookie names and value lengths only - never the values.</param>
public sealed record ClaudeConnectionReport(
    bool Success,
    string Stage,
    int StatusCode,
    string Message,
    string Cookies)
{
    public string ToDisplayString() => Success
        ? Loc.T("ClaudeCheckLineOk", Message, Cookies)
        : Loc.T("ClaudeCheckLineFailed", Stage, StatusCode, Message, Cookies);
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
