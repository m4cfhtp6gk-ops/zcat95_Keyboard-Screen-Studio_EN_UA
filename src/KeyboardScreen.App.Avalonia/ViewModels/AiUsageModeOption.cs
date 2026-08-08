using KeyboardScreen.Core;

namespace KeyboardScreen.App.Avalonia.ViewModels;

public sealed record AiUsageModeOption(
    AiUsageDataKind Kind,
    string Name,
    string Description);
