using System;
using Avalonia.Data;
using Avalonia.Markup.Xaml;
using KeyboardScreen.Core;

namespace KeyboardScreen.App.Avalonia.Infrastructure;

/// <summary>
/// XAML shorthand for a localized string: <c>Text="{infra:Localize NavScreen}"</c>.
///
/// The value is a binding against the shared catalogue, so switching language
/// updates windows that are already open. <see cref="Binding.FallbackValue"/>
/// holds the string as it reads at load time, which keeps the label correct
/// even if the binding itself cannot attach.
/// </summary>
public sealed class LocalizeExtension : MarkupExtension
{
    public LocalizeExtension()
    {
    }

    public LocalizeExtension(string key)
    {
        Key = key;
    }

    public string Key { get; set; } = string.Empty;

    public override object ProvideValue(IServiceProvider serviceProvider) =>
        new Binding
        {
            Path = $"[{Key}]",
            Source = Loc.Instance,
            Mode = BindingMode.OneWay,
            FallbackValue = Loc.T(Key)
        };
}
