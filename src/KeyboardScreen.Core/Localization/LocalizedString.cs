using System.ComponentModel;

namespace KeyboardScreen.Core;

/// <summary>
/// One catalogue entry as a bindable object. XAML binds to <see cref="Value"/>,
/// a plain property, rather than to an indexer, so the usual
/// <see cref="INotifyPropertyChanged"/> path refreshes it when the language
/// changes. Instances are cached per key by <see cref="Loc.Observe"/>.
/// </summary>
public sealed class LocalizedString : INotifyPropertyChanged
{
    internal LocalizedString(string key)
    {
        Key = key;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public string Key { get; }

    public string Value => Loc.T(Key);

    public override string ToString() => Value;

    internal void Refresh() =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Value)));
}
