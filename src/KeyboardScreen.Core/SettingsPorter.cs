using System.Text.Json;

namespace KeyboardScreen.Core;

/// <summary>
/// Export/import of the settings file. An export NEVER carries credentials:
/// the claude.ai session key (and its cached organization id), the GitHub
/// token and the Telegram api_id/api_hash/phone are blanked, so the file is
/// safe to share or sync. An import keeps whatever secrets already live on
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
        clone.ClaudeUsage ??= new ClaudeUsageSettings();
        clone.ClaudeUsage.SessionKey = string.Empty;
        clone.ClaudeUsage.OrganizationId = string.Empty;
        clone.GitHub ??= new GitHubSettings();
        clone.GitHub.Token = string.Empty;
        clone.Telegram ??= new TelegramSettings();
        clone.Telegram.ApiId = string.Empty;
        clone.Telegram.ApiHash = string.Empty;
        clone.Telegram.PhoneNumber = string.Empty;
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
        if (string.IsNullOrWhiteSpace(imported.ClaudeUsage.SessionKey))
        {
            imported.ClaudeUsage.SessionKey = current.ClaudeUsage?.SessionKey ?? string.Empty;
            imported.ClaudeUsage.OrganizationId = current.ClaudeUsage?.OrganizationId ?? string.Empty;
        }

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

        return imported;
    }

    private static AppSettings Clone(AppSettings settings) =>
        JsonSerializer.Deserialize<AppSettings>(JsonSerializer.Serialize(settings))
        ?? new AppSettings();
}
