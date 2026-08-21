using KeyboardScreen.App.Avalonia.Infrastructure;
using KeyboardScreen.Core;

namespace KeyboardScreen.App.Avalonia.ViewModels;

public sealed class ThemeItemViewModel(IScreenTheme theme) : ObservableObject
{
    private bool _isSelected;

    public IScreenTheme Theme { get; } = theme;
    public string Id => Theme.Id;
    public string Name => Theme.DisplayName;
    public string Description => Theme.Description;
    public string Details => Theme.Details;

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }

    /// <summary>Re-reads the theme's name and copy after a language change.</summary>
    public void RaiseLocalizedTextChanged()
    {
        OnPropertyChanged(nameof(Name));
        OnPropertyChanged(nameof(Description));
        OnPropertyChanged(nameof(Details));
    }
}

/// <summary>A named section of the theme sidebar; the name follows the app language.</summary>
public sealed class ThemeGroupViewModel(string nameKey, IReadOnlyList<ThemeItemViewModel> themes)
    : ObservableObject
{
    public string Name => Loc.T(nameKey);

    public IReadOnlyList<ThemeItemViewModel> Themes { get; } = themes;

    public void RaiseLocalizedTextChanged()
    {
        OnPropertyChanged(nameof(Name));
        foreach (ThemeItemViewModel item in Themes)
        {
            item.RaiseLocalizedTextChanged();
        }
    }
}
