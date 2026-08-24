namespace KeyboardScreen.Core;

/// <summary>
/// Which subscription the budget presets are sized for. The numbers behind each
/// preset are orientation figures, not a published quota - Anthropic does not
/// expose one - so every preset is editable and <see cref="Custom"/> exists for
/// the user who would rather set their own.
/// </summary>
public enum ClaudePlanKind
{
    Pro = 0,
    Max5 = 1,
    Max20 = 2,
    Custom = 3
}

/// <summary>
/// Settings for the Claude limits screen.
///
/// The screen reads Claude Code's own transcripts under <c>~/.claude/projects</c>
/// and nothing else: no cookie, no session key, no request to claude.ai, no
/// second program to install. That is the whole point of this design - every
/// credential-shaped path was tried first and every one of them decayed. A file
/// on this disk cannot be rate-limited, challenged or logged out.
///
/// The cost of that honesty is the denominator. No local file records the
/// account's real quota, so the meters read against a budget the user sets. A
/// percentage of your own target is a true number; a percentage of a quota we
/// guessed would not be.
/// </summary>
public sealed class ClaudeUsageSettings
{
    /// <summary>Matched against the transcript's model id, case-insensitively.</summary>
    public const string DefaultModelScope = "opus";

    /// <summary>Tokens allowed in the rolling five-hour window; 0 follows the plan preset.</summary>
    public long SessionTokenBudget { get; set; }

    /// <summary>Tokens allowed in the rolling seven-day window; 0 follows the plan preset.</summary>
    public long WeekTokenBudget { get; set; }

    public ClaudePlanKind Plan { get; set; } = ClaudePlanKind.Pro;

    /// <summary>The model whose weekly usage fills the third meter.</summary>
    public string ModelScope { get; set; } = DefaultModelScope;

    /// <summary>
    /// Nothing to configure and nothing to expire: the source is a directory on
    /// this machine, so the screen is always ready to try.
    /// </summary>
    public bool IsConfigured => true;

    /// <summary>
    /// Starting points, in tokens, for each plan. Counted the same way the
    /// reader counts - input, output and cache writes, never cache reads - so
    /// the ratio means something. Adjust rather than trust: a week of your own
    /// usage is a better calibration than any figure shipped in a build.
    /// </summary>
    public static (long Session, long Week) PresetFor(ClaudePlanKind plan) => plan switch
    {
        ClaudePlanKind.Max5 => (10_000_000L, 125_000_000L),
        ClaudePlanKind.Max20 => (40_000_000L, 500_000_000L),
        _ => (2_000_000L, 25_000_000L)
    };

    public long EffectiveSessionBudget => Resolve(SessionTokenBudget, PresetFor(Plan).Session);

    public long EffectiveWeekBudget => Resolve(WeekTokenBudget, PresetFor(Plan).Week);

    /// <summary>
    /// The per-model meter shares the weekly budget: it answers "how much of my
    /// week went to this model", which is the question a per-model row is for.
    /// </summary>
    public long EffectiveModelWeekBudget => EffectiveWeekBudget;

    /// <summary>An explicit value wins; anything non-positive falls back to the preset.</summary>
    private static long Resolve(long configured, long preset) => configured > 0 ? configured : preset;
}
