using Avalonia.Controls;
using KeyboardScreen.App.Avalonia.Platform;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Media;

namespace KeyboardScreen.App.Avalonia;

public sealed partial class AccentColorWindow : Window
{
    private ColorView Picker => this.FindControl<ColorView>("Picker")!;

    public AccentColorWindow()
    {
        AvaloniaXamlLoader.Load(this);
        WindowsRoundedWindow.Attach(this);
    }

    public AccentColorWindow(string initialColor) : this()
    {
        if (Color.TryParse(initialColor, out Color color))
        {
            Picker.Color = color;
        }
    }

    private void CancelButton_OnClick(object? sender, RoutedEventArgs e) => Close(null);

    private void ApplyButton_OnClick(object? sender, RoutedEventArgs e) =>
        Close($"#{Picker.Color.R:X2}{Picker.Color.G:X2}{Picker.Color.B:X2}");
}
