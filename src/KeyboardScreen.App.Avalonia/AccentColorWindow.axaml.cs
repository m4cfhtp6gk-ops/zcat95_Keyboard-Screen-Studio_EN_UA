using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Media;

namespace KeyboardScreen.App.Avalonia;

public sealed partial class AccentColorWindow : Window
{

    public AccentColorWindow()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public AccentColorWindow(string initialColor) : this()
    {
        if (Color.TryParse(initialColor, out Color color))
        {
            ColorView? picker = this.FindControl<ColorView>("Picker");
            if (picker is not null)
            {
                picker.Color = color;
            }
        }
    }

    private void CancelButton_OnClick(object? sender, RoutedEventArgs e) => Close(null);

    private void ApplyButton_OnClick(object? sender, RoutedEventArgs e)
    {
        ColorView? picker = this.FindControl<ColorView>("Picker");
        if (picker is null)
        {
            Close(null);
            return;
        }

        Close($"#{picker.Color.R:X2}{picker.Color.G:X2}{picker.Color.B:X2}");
    }
}
