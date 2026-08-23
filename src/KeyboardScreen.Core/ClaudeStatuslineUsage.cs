using System.Text.Json;

namespace KeyboardScreen.Core;

/// <summary>
/// Reads the subscription limits Claude Code itself reports, with no request to
/// claude.ai at all.
///
/// Claude Code hands its status line command a JSON blob on stdin that carries
/// <c>rate_limits.five_hour</c> and <c>rate_limits.seven_day</c> - the same
/// numbers <c>/usage</c> shows. A tiny shim writes that blob to a file and this
/// reads it, so nothing here needs a cookie, a token or a network call, and
/// Cloudflare never enters into it.
///
/// The data is conditional by design, and the parser treats every one of these
/// as normal rather than as an error:
/// <list type="bullet">
/// <item>it appears only for Claude.ai subscribers, and only after the session's
/// first API response, so an empty or partial blob is expected;</item>
/// <item>either window can be absent on its own;</item>
/// <item><c>used_percentage</c> is documented as 0-100 but is known to carry a
/// stray epoch timestamp while a window has no data, so anything outside the
/// range is discarded rather than drawn as "1776950400%";</item>
/// <item><c>resets_at</c> is Unix epoch SECONDS here, not the ISO 8601 the web
/// endpoint returns.</item>
/// </list>
/// </summary>
public static class ClaudeStatuslineUsage
{
    /// <summary>Where the shim writes the blob, next to Claude Code's own files.</summary>
    public static string DefaultPath()
    {
        string? configured = Environment.GetEnvironmentVariable("CLAUDE_CONFIG_DIR");
        string root = string.IsNullOrWhiteSpace(configured)
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".claude")
            : configured;
        return Path.Combine(root, "keyboard-screen-studio-usage.json");
    }

    /// <summary>
    /// A window is stale once its own reset time has passed: Claude Code only
    /// rewrites the file while it runs, so yesterday's numbers must not be shown
    /// as today's.
    /// </summary>
    public static ClaudeUsageSnapshot Parse(string? json, DateTimeOffset now, DateTimeOffset? writtenAt = null)
    {
        JsonElement root;
        try
        {
            using JsonDocument document = JsonDocument.Parse(json ?? string.Empty);
            root = document.RootElement.Clone();
        }
        catch (JsonException)
        {
            return ClaudeUsageSnapshot.Unavailable(Loc.T("ClaudeStatuslineUnreadable"));
        }

        if (root.ValueKind != JsonValueKind.Object
            || !root.TryGetProperty("rate_limits", out JsonElement limits)
            || limits.ValueKind != JsonValueKind.Object)
        {
            return ClaudeUsageSnapshot.Unavailable(Loc.T("ClaudeStatuslineNoLimits"));
        }

        ClaudeUsageWindow? session = ReadWindow(limits, "five_hour", ClaudeUsageWindowKind.Session, now);
        ClaudeUsageWindow? week = ReadWindow(limits, "seven_day", ClaudeUsageWindowKind.Week, now);
        if (session is null && week is null)
        {
            return ClaudeUsageSnapshot.Unavailable(Loc.T("ClaudeStatuslineNoLimits"));
        }

        return new ClaudeUsageSnapshot(true, session, week, null, writtenAt ?? now);
    }

    /// <summary>Reads the file the shim maintains; a missing file simply means Claude Code has not run.</summary>
    public static ClaudeUsageSnapshot Read(string? path = null, DateTimeOffset? now = null)
    {
        string file = path ?? DefaultPath();
        DateTimeOffset moment = now ?? DateTimeOffset.Now;
        try
        {
            if (!File.Exists(file))
            {
                return ClaudeUsageSnapshot.Unavailable(Loc.T("ClaudeStatuslineNotSetUp"));
            }

            return Parse(File.ReadAllText(file), moment, new FileInfo(file).LastWriteTime);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return ClaudeUsageSnapshot.Unavailable(Loc.T("ClaudeStatuslineUnreadable"));
        }
    }

    private static ClaudeUsageWindow? ReadWindow(
        JsonElement limits,
        string propertyName,
        ClaudeUsageWindowKind kind,
        DateTimeOffset now)
    {
        if (!limits.TryGetProperty(propertyName, out JsonElement window) || window.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        if (ReadPercent(window) is not { } percent)
        {
            return null;
        }

        DateTimeOffset? resetsAt = ReadResetsAt(window);
        // A window whose reset has already passed describes a period that is
        // over; showing it as current would be a lie of omission.
        return resetsAt is { } reset && reset <= now ? null : new ClaudeUsageWindow(kind, percent, resetsAt);
    }

    /// <summary>Documented as 0-100; anything else is the known epoch-in-percentage bug and is dropped.</summary>
    private static double? ReadPercent(JsonElement window) =>
        window.TryGetProperty("used_percentage", out JsonElement value)
            && value.ValueKind == JsonValueKind.Number
            && value.TryGetDouble(out double percent)
            && percent is >= 0 and <= 100
                ? percent
                : null;

    /// <summary>Unix epoch seconds here - the web endpoint's ISO 8601 does not apply.</summary>
    private static DateTimeOffset? ReadResetsAt(JsonElement window)
    {
        if (!window.TryGetProperty("resets_at", out JsonElement value)
            || value.ValueKind != JsonValueKind.Number
            || !value.TryGetInt64(out long epochSeconds))
        {
            return null;
        }

        // Guard the conversion: a bad value must not throw inside a render.
        return epochSeconds is > 0 and < 4_102_444_800
            ? DateTimeOffset.FromUnixTimeSeconds(epochSeconds).ToLocalTime()
            : null;
    }

    /// <summary>
    /// The status line command Claude Code should run: it copies the blob to
    /// <paramref name="targetPath"/> and prints nothing, so a user without an
    /// existing status line sees no change in Claude Code itself.
    /// </summary>
    public static string ShimCommand(string? targetPath = null)
    {
        string target = targetPath ?? DefaultPath();
        // PowerShell reads the whole of stdin, writes it, and stays silent. The
        // trailing comment is what lets a rerun recognise its own command, so it
        // must not depend on the file name - that is configurable.
        return "powershell -NoProfile -Command \"$input | Out-File -Encoding utf8 -FilePath '"
            + target.Replace("'", "''")
            + "'\" #"
            + ClaudeStatuslineSetup.Marker;
    }
}
