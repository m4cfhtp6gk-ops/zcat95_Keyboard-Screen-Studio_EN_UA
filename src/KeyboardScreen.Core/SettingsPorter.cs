using System.Text.Json;

namespace KeyboardScreen.Core;

/// <summary>
/// Export/import of the settings file. An export NEVER carries credentials:
/// the claude.ai session key (and its cached organization id), the GitHub
/// token, the Telegram api_id/api_hash/phone and the alerts.in.ua token are
/// blanked, so the file is safe to share or sync. An import keeps whatever secrets already live on
/// this machine when the file carries blanks. The Telegram session file is a
/// separate artifact and never passes through here at all.
/// </summary>
public static class SettingsPorter
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public static string ExportJson(AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        AppSettings clone = Clone(settings);
        // Claude usage carries no secret any more: it reads this machine's own
        // Claude Code transcripts, so there is nothing here to strip.
        clone.ClaudeUsage ??= new ClaudeUsageSettings();
        clone.GitHub ??= new GitHubSettings();
        clone.GitHub.Token = string.Empty;
        clone.Telegram ??= new TelegramSettings();
        clone.Telegram.ApiId = string.Empty;
        clone.Telegram.ApiHash = string.Empty;
        clone.Telegram.PhoneNumber = string.Empty;
        clone.AirAlerts ??= new AirAlertSettings();
        clone.AirAlerts.Token = string.Empty;
        // A private ICS link embeds an access token in the URL itself.
        clone.Calendar ??= new CalendarSettings();
        clone.Calendar.IcsUrl = string.Empty;
        return JsonSerializer.Serialize(clone, JsonOptions);
    }

    public static AppSettings ImportJson(string json, AppSettings current)
    {
        ArgumentNullException.ThrowIfNull(current);
        AppSettings imported;
        try
        {
            imported = JsonSerializer.Deserialize<AppSettings>(json)
                ?? throw new InvalidOperationException(Loc.T("PortImportInvalid"));
        }
        catch (JsonException)
        {
            throw new InvalidOperationException(Loc.T("PortImportInvalid"));
        }

        // A stripped file must not wipe the secrets this machine already has.
        imported.ClaudeUsage ??= new ClaudeUsageSettings();

        imported.GitHub ??= new GitHubSettings();
        if (string.IsNullOrWhiteSpace(imported.GitHub.Token))
        {
            imported.GitHub.Token = current.GitHub?.Token ?? string.Empty;
        }

        imported.Telegram ??= new TelegramSettings();
        if (string.IsNullOrWhiteSpace(imported.Telegram.ApiHash))
        {
            imported.Telegram.ApiId = current.Telegram?.ApiId ?? string.Empty;
            imported.Telegram.ApiHash = current.Telegram?.ApiHash ?? string.Empty;
            imported.Telegram.PhoneNumber = current.Telegram?.PhoneNumber ?? string.Empty;
        }

        imported.AirAlerts ??= new AirAlertSettings();
        if (string.IsNullOrWhiteSpace(imported.AirAlerts.Token))
        {
            imported.AirAlerts.Token = current.AirAlerts?.Token ?? string.Empty;
        }

        imported.Calendar ??= new CalendarSettings();
        if (string.IsNullOrWhiteSpace(imported.Calendar.IcsUrl))
        {
            imported.Calendar.IcsUrl = current.Calendar?.IcsUrl ?? string.Empty;
        }

        return imported;
    }

    private static AppSettings Clone(AppSettings settings) =>
        JsonSerializer.Deserialize<AppSettings>(JsonSerializer.Serialize(settings))
        ?? new AppSettings();
}
