using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Text.Json;

namespace KeyboardScreen.Core;

/// <summary>
/// String catalogue shared by the desktop UI and the rendered screen themes.
///
/// Catalogues are embedded JSON — one file per language — rather than satellite
/// assemblies, so the single-file portable build keeps working unchanged. A
/// lookup falls back to English and then to the key itself, so a missing
/// translation shows readable text instead of an empty label.
/// </summary>
public sealed class Loc : INotifyPropertyChanged
{
    /// <summary>
    /// Property name Avalonia's indexer bindings listen for. Raising it is what
    /// makes every <c>{i18n:Localize}</c> binding re-read its string when the
    /// language changes.
    /// </summary>
    private const string IndexerPropertyName = "Item[]";

    private static readonly IReadOnlyDictionary<string, string> EmptyCatalogue =
        new Dictionary<string, string>(0);

    private readonly Dictionary<AppLanguage, IReadOnlyDictionary<string, string>> _catalogues = new();

    private AppLanguage _language = AppLanguage.English;
    private CultureInfo _culture = AppLanguageInfo.For(AppLanguage.English).Culture;

    private Loc()
    {
        foreach (AppLanguageInfo info in AppLanguageInfo.All)
        {
            _catalogues[info.Language] = LoadCatalogue(info.ResourceName);
        }
    }

    public static Loc Instance { get; } = new();

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>Raised after the language changed, for code holding cached text.</summary>
    public event EventHandler? LanguageChanged;

    public static AppLanguage Language
    {
        get => Instance.CurrentLanguage;
        set => Instance.CurrentLanguage = value;
    }

    /// <summary>Culture matching the active language; pass it explicitly when formatting.</summary>
    public static CultureInfo Culture => Instance._culture;

    public AppLanguage CurrentLanguage
    {
        get => _language;
        set
        {
            if (_language == value)
            {
                return;
            }

            _language = value;
            _culture = AppLanguageInfo.For(value).Culture;
            ApplyCulture(_culture);

            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(IndexerPropertyName));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(null));
            LanguageChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>Indexer the XAML <c>{i18n:Localize Key}</c> bindings resolve against.</summary>
    public string this[string key] => Get(key);

    public static string T(string key) => Instance.Get(key);

    public static string T(string key, params object?[] arguments) =>
        string.Format(Instance._culture, Instance.Get(key), arguments);

    public string Get(string key)
    {
        if (string.IsNullOrEmpty(key))
        {
            return string.Empty;
        }

        if (Catalogue(_language).TryGetValue(key, out string? value))
        {
            return value;
        }

        if (_language != AppLanguage.English &&
            Catalogue(AppLanguage.English).TryGetValue(key, out value))
        {
            return value;
        }

        return key;
    }

    /// <summary>Day, month and year, e.g. "21 Aug 2026".</summary>
    public static string LongDate(DateTimeOffset value) =>
        value.ToString(T("FormatLongDate"), Instance._culture);

    /// <summary>Day and month only, e.g. "21 Aug".</summary>
    public static string ShortDate(DateTimeOffset value) =>
        value.ToString(T("FormatShortDate"), Instance._culture);

    /// <summary>Localised weekday name.</summary>
    public static string DayName(DateTimeOffset value) =>
        value.ToString("dddd", Instance._culture);

    /// <summary>Every key defined for a language; the smoke tests use it to check parity.</summary>
    public IEnumerable<string> Keys(AppLanguage language) => Catalogue(language).Keys;

    /// <summary>Applies the language without notifying, for start-up before any binding exists.</summary>
    public void Initialize(AppLanguage language)
    {
        _language = language;
        _culture = AppLanguageInfo.For(language).Culture;
        ApplyCulture(_culture);
    }

    private static void ApplyCulture(CultureInfo culture)
    {
        CultureInfo.DefaultThreadCurrentCulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;
        CultureInfo.CurrentCulture = culture;
        CultureInfo.CurrentUICulture = culture;
    }

    private IReadOnlyDictionary<string, string> Catalogue(AppLanguage language) =>
        _catalogues.TryGetValue(language, out IReadOnlyDictionary<string, string>? catalogue)
            ? catalogue
            : EmptyCatalogue;

    private static IReadOnlyDictionary<string, string> LoadCatalogue(string resourceName)
    {
        try
        {
            using Stream? stream = typeof(Loc).Assembly.GetManifestResourceStream(resourceName);
            if (stream is null)
            {
                return EmptyCatalogue;
            }

            using var reader = new StreamReader(stream);
            return JsonSerializer.Deserialize<Dictionary<string, string>>(reader.ReadToEnd()) ?? EmptyCatalogue;
        }
        catch (JsonException)
        {
            return EmptyCatalogue;
        }
    }
}
