using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Media.Transformation;
using KeyboardScreen.Core;

namespace KeyboardScreen.App.Avalonia;

public sealed partial class RiskNoticeWindow : Window
{
    public RiskNoticeWindow() : this(Loc.T("RiskNoticeTitle"), string.Empty, string.Empty)
    {
    }

    public RiskNoticeWindow(string title, string subtitle, string body)
    {
        AvaloniaXamlLoader.Load(this);
        this.FindControl<TextBlock>("NoticeTitle")!.Text = title;
        this.FindControl<TextBlock>("NoticeSubtitle")!.Text = subtitle;
        this.FindControl<TextBlock>("NoticeBody")!.Text = body;
        Opened += (_, _) =>
        {
            Border root = this.FindControl<Border>("NoticeRoot")!;
            root.Opacity = 1;
            root.RenderTransform = TransformOperations.Parse("translate(0px, 0px)");
        };
    }

    private void CloseButton_OnClick(object? sender, RoutedEventArgs e) => Close(true);

    private void ContinueButton_OnClick(object? sender, RoutedEventArgs e) => Close(true);
}
