using System.Net;
using System.Net.Sockets;
using Avalonia.Controls;
using KeyboardScreen.App.Avalonia.Platform;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Media.Transformation;

namespace KeyboardScreen.App.Avalonia;

public sealed partial class FirstRunGuideWindow : Window
{
    private Controls.IpAddressEditor IpEditor =>
        this.FindControl<Controls.IpAddressEditor>("IpEditor")!;

    public FirstRunGuideWindow() : this(string.Empty)
    {
    }

    public FirstRunGuideWindow(string initialIp)
    {
        AvaloniaXamlLoader.Load(this);
        WindowsRoundedWindow.Attach(this);
        IpEditor.Value = initialIp;
        Opened += (_, _) =>
        {
            Border root = this.FindControl<Border>("GuideRoot")!;
            root.Opacity = 1;
            root.RenderTransform = TransformOperations.Parse("translate(0px, 0px)");
        };
    }

    private void CloseButton_OnClick(object? sender, RoutedEventArgs e) =>
        Close(null);

    private void LaterButton_OnClick(object? sender, RoutedEventArgs e) =>
        Close(string.Empty);

    private void ContinueButton_OnClick(object? sender, RoutedEventArgs e)
    {
        string candidate = IpEditor.Value.Trim();
        if (!IPAddress.TryParse(candidate, out IPAddress? address) ||
            address.AddressFamily != AddressFamily.InterNetwork)
        {
            IpEditor.Focus();
            return;
        }

        Close(address.ToString());
    }
}
