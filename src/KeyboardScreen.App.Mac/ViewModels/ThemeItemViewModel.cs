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
}

public sealed record ThemeGroupViewModel(string Name, IReadOnlyList<ThemeItemViewModel> Themes);
