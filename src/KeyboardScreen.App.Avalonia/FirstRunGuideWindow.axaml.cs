using System.Net;
using System.Net.Sockets;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Media.Transformation;

namespace KeyboardScreen.App.Avalonia;

public sealed partial class FirstRunGuideWindow : Window
{

    public FirstRunGuideWindow() : this(string.Empty)
    {
    }

    public FirstRunGuideWindow(string initialIp)
    {
        AvaloniaXamlLoader.Load(this);
        Controls.IpAddressEditor? editor = this.FindControl<Controls.IpAddressEditor>("IpEditor");
        if (editor is not null)
        {
            editor.Value = initialIp;
        }

        Opened += (_, _) =>
        {
            Border? root = this.FindControl<Border>("GuideRoot");
            if (root is not null)
            {
                root.Opacity = 1;
                root.RenderTransform = TransformOperations.Parse("translate(0px, 0px)");
            }
        };
    }

    private void CloseButton_OnClick(object? sender, RoutedEventArgs e) =>
        Close(null);

    private void LaterButton_OnClick(object? sender, RoutedEventArgs e) =>
        Close(string.Empty);

    private void ContinueButton_OnClick(object? sender, RoutedEventArgs e)
    {
        Controls.IpAddressEditor? editor = this.FindControl<Controls.IpAddressEditor>("IpEditor");
        if (editor is null)
        {
            Close(string.Empty);
            return;
        }

        string candidate = editor.Value.Trim();
        if (!IPAddress.TryParse(candidate, out IPAddress? address) ||
            address.AddressFamily != AddressFamily.InterNetwork)
        {
            editor.Focus();
            return;
        }

        TextBlock? validation = this.FindControl<TextBlock>("ValidationText");
        if (validation is not null)
        {
            validation.Text = string.Empty;
        }
        Close(address.ToString());
    }
}
