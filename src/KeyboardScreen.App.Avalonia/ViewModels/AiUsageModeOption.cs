using KeyboardScreen.App.Avalonia.Infrastructure;
using KeyboardScreen.Core;

namespace KeyboardScreen.App.Avalonia.ViewModels;

/// <summary>
/// One entry in the AI usage metric picker. The instance is stable so the
/// selection survives a language change; only its text is re-read.
/// </summary>
public sealed class AiUsageModeOption(AiUsageDataKind kind, string nameKey, string descriptionKey)
    : ObservableObject
{
    public AiUsageDataKind Kind { get; } = kind;

    public string Name => Loc.T(nameKey);

    public string Description => Loc.T(descriptionKey);

    public void RaiseLocalizedTextChanged()
    {
        OnPropertyChanged(nameof(Name));
        OnPropertyChanged(nameof(Description));
    }
}
