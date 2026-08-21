using System;
using System.Collections.Generic;
using System.Globalization;

namespace KeyboardScreen.Core;

/// <summary>
/// Describes one shipped <see cref="AppLanguage"/>: the identifier persisted in
/// settings, the name shown in the language picker, and the culture used for
/// dates, weekday names and numbers.
/// </summary>
public sealed record AppLanguageInfo(AppLanguage Language, string Id, string NativeName, string CultureName)
{
    /// <summary>Culture used to format dates, weekday names and numbers.</summary>
    public CultureInfo Culture { get; } = ResolveCulture(CultureName);

    /// <summary>Logical name of the embedded JSON catalogue for this language.</summary>
    public string ResourceName => $"KeyboardScreen.Core.Localization.Strings.{Id}.json";

    /// <summary>Every shipped language, in the order the picker lists them.</summary>
    public static IReadOnlyList<AppLanguageInfo> All { get; } =
    [
        new(AppLanguage.English, "en", "English", "en-US"),
        new(AppLanguage.Ukrainian, "uk", "Українська", "uk-UA"),
        new(AppLanguage.ChineseSimplified, "zh-Hans", "简体中文", "zh-CN")
    ];

    public static AppLanguageInfo For(AppLanguage language)
    {
        foreach (AppLanguageInfo info in All)
        {
            if (info.Language == language)
            {
                return info;
            }
        }

        return All[0];
    }

    /// <summary>Reads back an identifier persisted in settings; unknown values fall back to English.</summary>
    public static AppLanguage Parse(string? id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return AppLanguage.English;
        }

        foreach (AppLanguageInfo info in All)
        {
            if (string.Equals(info.Id, id, StringComparison.OrdinalIgnoreCase))
            {
                return info.Language;
            }
        }

        return AppLanguage.English;
    }

    /// <summary>
    /// Picks the closest shipped language for the machine's own UI culture.
    /// Used once, on first run, before the user has made a choice.
    /// </summary>
    public static AppLanguage FromSystemCulture(CultureInfo? culture = null)
    {
        string name = (culture ?? CultureInfo.CurrentUICulture).TwoLetterISOLanguageName;
        return name switch
        {
            "uk" => AppLanguage.Ukrainian,
            "zh" => AppLanguage.ChineseSimplified,
            _ => AppLanguage.English
        };
    }

    private static CultureInfo ResolveCulture(string cultureName)
    {
        try
        {
            return CultureInfo.GetCultureInfo(cultureName);
        }
        catch (CultureNotFoundException)
        {
            // Globalization-invariant runtimes know no named cultures.
            return CultureInfo.InvariantCulture;
        }
    }
}
