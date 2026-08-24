namespace KeyboardScreen.Core;

/// <summary>
/// Builds the Claude usage snapshot from Claude Code's own transcripts.
///
/// Two earlier designs sat here and both decayed in the user's hands. Scraping
/// claude.ai needed a browser cookie bound to the browser that solved the
/// Cloudflare challenge, TLS fingerprint included - unreachable from a desktop
/// app by construction. Reading a status line needed Claude Code to volunteer
/// limit figures that are not persisted anywhere on disk, so the screen sat on
/// "has not reported yet" forever.
///
/// What is actually on disk is every assistant turn Claude Code has written,
/// each one carrying a timestamp, a model id and a usage block. That is enough
/// to compute real rolling windows, and it cannot be rate-limited, challenged,
/// logged out or deprecated from the far end. The trade is the denominator: the
/// meters read against the user's own budget rather than an account quota,
/// because no such quota is readable here and inventing one would be a lie
/// drawn at thirty pixels tall.
/// </summary>
public sealed class ClaudeUsageSnapshotSource : IDisposable
{
    /// <summary>The rolling session window Claude Code itself reports against.</summary>
    public static readonly TimeSpan SessionWindow = TimeSpan.FromHours(5);

    public static readonly TimeSpan WeekWindow = TimeSpan.FromDays(7);

    private readonly ClaudeCodeTokenReader _tokenReader;

    public ClaudeUsageSnapshotSource(ClaudeCodeTokenReader? tokenReader = null)
    {
        _tokenReader = tokenReader ?? new ClaudeCodeTokenReader();
    }

    public Task<ClaudeUsageSnapshot> ReadAsync(
        ClaudeUsageSettings settings,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(Read(settings, DateTimeOffset.Now));
    }

    /// <summary>Pure and clock-injected so the windows are testable.</summary>
    public ClaudeUsageSnapshot Read(ClaudeUsageSettings settings, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(settings);

        string scope = string.IsNullOrWhiteSpace(settings.ModelScope)
            ? ClaudeUsageSettings.DefaultModelScope
            : settings.ModelScope.Trim();

        ClaudeCodeTokenTotals totals;
        try
        {
            totals = _tokenReader.Read(now - SessionWindow, now - WeekWindow, scope);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return ClaudeUsageSnapshot.Unavailable(ex.Message);
        }

        if (!totals.Available)
        {
            // Nothing to read is not an error the user can act on by retrying;
            // it means Claude Code has not run on this machine. Say that.
            return ClaudeUsageSnapshot.Unavailable(Loc.T("ClaudeNoTranscripts"));
        }

        return new ClaudeUsageSnapshot(
            true,
            Session: BuildWindow(
                ClaudeUsageWindowKind.Session,
                totals.Session,
                settings.EffectiveSessionBudget,
                totals.SessionOldest,
                SessionWindow),
            Week: BuildWindow(
                ClaudeUsageWindowKind.Week,
                totals.Week,
                settings.EffectiveWeekBudget,
                totals.WeekOldest,
                WeekWindow),
            ModelWeek: BuildWindow(
                ClaudeUsageWindowKind.ModelWeek,
                totals.ModelWeek,
                settings.EffectiveModelWeekBudget,
                totals.WeekOldest,
                WeekWindow,
                scope),
            UpdatedAt: now);
    }

    /// <summary>
    /// A rolling window drains as its oldest entry ages out, so the reset time is
    /// that entry plus the window length. With nothing in the window there is
    /// nothing to reset and the row reads a true zero.
    /// </summary>
    private static ClaudeUsageWindow BuildWindow(
        ClaudeUsageWindowKind kind,
        long tokens,
        long budget,
        DateTimeOffset? oldest,
        TimeSpan window,
        string? scopeName = null)
    {
        double percent = budget > 0 ? tokens / (double)budget * 100d : 0d;
        DateTimeOffset? resetsAt = tokens > 0 && oldest is { } start ? start + window : null;
        return new ClaudeUsageWindow(kind, percent, resetsAt, tokens, scopeName);
    }

    /// <summary>
    /// The diagnostic button. There is no round-trip to report on any more, so it
    /// answers the two questions that can actually go wrong locally: is the
    /// transcript directory there, and did it yield anything.
    /// </summary>
    public Task<ClaudeConnectionReport> CheckAsync(ClaudeUsageSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        if (!_tokenReader.TranscriptsExist)
        {
            return Task.FromResult(new ClaudeConnectionReport(
                false,
                Loc.T("ClaudeCheckNoDirectory", _tokenReader.ProjectsDirectory)));
        }

        ClaudeUsageSnapshot snapshot = Read(settings, DateTimeOffset.Now);
        long week = snapshot.Week?.TokensUsed ?? 0;
        if (!snapshot.Available)
        {
            return Task.FromResult(new ClaudeConnectionReport(
                false,
                snapshot.ErrorMessage ?? Loc.T("ClaudeNoTranscripts")));
        }

        return Task.FromResult(new ClaudeConnectionReport(
            true,
            _tokenReader.ProjectsDirectory,
            week));
    }

    /// <summary>
    /// Nothing to release: the source owns no socket and no handle. Kept so the
    /// app layer's disposal stays uniform across every data source.
    /// </summary>
    public void Dispose()
    {
    }
}
