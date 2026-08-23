using System.Net;
using System.Net.Sockets;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;

namespace KeyboardScreen.App.Avalonia.Controls;

public sealed partial class IpAddressEditor : UserControl
{
    public static readonly StyledProperty<string> ValueProperty =
        AvaloniaProperty.Register<IpAddressEditor, string>(
            nameof(Value),
            string.Empty,
            defaultBindingMode: global::Avalonia.Data.BindingMode.TwoWay);

    private readonly TextBox[] _parts;
    private bool _synchronizing;

    public IpAddressEditor()
    {
        InitializeComponent();
        _parts =
        [
            this.FindControl<TextBox>("Part1")!,
            this.FindControl<TextBox>("Part2")!,
            this.FindControl<TextBox>("Part3")!,
            this.FindControl<TextBox>("Part4")!
        ];
        foreach (TextBox part in _parts)
        {
            part.TextChanged += PartOnTextChanged;
            part.KeyDown += PartOnKeyDown;
            part.AddHandler(TextInputEvent, PartOnTextInput, RoutingStrategies.Tunnel);
        }
        PropertyChanged += (_, args) =>
        {
            if (args.Property == ValueProperty)
            {
                PopulateParts(Value);
            }
        };
        PopulateParts(Value);
    }

    public string Value
    {
        get => GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

    private void PartOnTextInput(object? sender, TextInputEventArgs e)
    {
        if (string.IsNullOrEmpty(e.Text))
        {
            return;
        }

        // A whole address arriving at once is a paste. Spreading it across the
        // four boxes is the only reading that can be intended - the alternative
        // is the digit filter silently eating the entire paste.
        if (e.Text.Count(character => character == '.') == 3 && TryParseIPv4(e.Text, out string[] octets))
        {
            e.Handled = true;
            _synchronizing = true;
            try
            {
                for (int i = 0; i < _parts.Length; i++)
                {
                    _parts[i].Text = octets[i];
                }
            }
            finally
            {
                _synchronizing = false;
            }

            SetCurrentValue(ValueProperty, string.Join(".", octets));
            _parts[^1].Focus();
            _parts[^1].CaretIndex = _parts[^1].Text?.Length ?? 0;
            return;
        }

        if (e.Text.Any(character => !char.IsDigit(character)))
        {
            e.Handled = true;
        }
    }

    /// <summary>
    /// Deliberately requires all four octets. IPAddress.TryParse still accepts
    /// shorthand - "19" would come back as 0.0.0.19 - so a looser test would
    /// turn two typed digits into a pasted address.
    /// </summary>
    private static bool TryParseIPv4(string text, out string[] octets)
    {
        octets = [];
        if (!IPAddress.TryParse(text.Trim(), out IPAddress? address)
            || address.AddressFamily != AddressFamily.InterNetwork)
        {
            return false;
        }

        octets = address.ToString().Split('.');
        return octets.Length == 4;
    }

    private void PartOnTextChanged(object? sender, TextChangedEventArgs e)
    {
        if (_synchronizing || sender is not TextBox textBox)
        {
            return;
        }

        if (int.TryParse(textBox.Text, out int value) && value > 255)
        {
            textBox.Text = "255";
            textBox.CaretIndex = textBox.Text.Length;
        }

        _synchronizing = true;
        try
        {
            SetCurrentValue(ValueProperty, string.Join(".", _parts.Select(part => part.Text?.Trim() ?? string.Empty)));
        }
        finally
        {
            _synchronizing = false;
        }
        if (textBox.Text?.Length == 3)
        {
            int index = Array.IndexOf(_parts, textBox);
            if (index >= 0 && index < _parts.Length - 1)
            {
                _parts[index + 1].Focus();
                _parts[index + 1].SelectAll();
            }
        }
    }

    private void PartOnKeyDown(object? sender, KeyEventArgs e)
    {
        if (sender is not TextBox textBox)
        {
            return;
        }

        int index = Array.IndexOf(_parts, textBox);
        if (e.Key == Key.Back && string.IsNullOrEmpty(textBox.Text) && index > 0)
        {
            _parts[index - 1].Focus();
            _parts[index - 1].CaretIndex = _parts[index - 1].Text?.Length ?? 0;
            e.Handled = true;
        }
        else if ((e.Key == Key.OemPeriod || e.Key == Key.Decimal) && index >= 0 && index < _parts.Length - 1)
        {
            // A three-digit octet already advanced the focus on its own. Advancing
            // again on the dot the user then types would skip the box entirely and
            // silently turn 192.168.1.50 into 192..168.150, so an empty box just
            // swallows the separator it has already been given.
            if (!string.IsNullOrEmpty(textBox.Text))
            {
                _parts[index + 1].Focus();
                _parts[index + 1].SelectAll();
            }

            e.Handled = true;
        }
    }

    private void PopulateParts(string value)
    {
        if (_synchronizing)
        {
            return;
        }

        string candidate = value?.Trim() ?? string.Empty;
        if (!IPAddress.TryParse(candidate, out IPAddress? address) ||
            address.AddressFamily != AddressFamily.InterNetwork)
        {
            if (string.IsNullOrWhiteSpace(candidate))
            {
                _synchronizing = true;
                foreach (TextBox part in _parts)
                {
                    part.Text = string.Empty;
                }
                _synchronizing = false;
            }
            return;
        }

        string[] segments = address.ToString().Split('.');
        _synchronizing = true;
        for (int index = 0; index < _parts.Length; index++)
        {
            _parts[index].Text = segments[index];
        }
        _synchronizing = false;
        ClearProgrammaticSelections();
        Dispatcher.UIThread.Post(ClearProgrammaticSelections, DispatcherPriority.Loaded);
    }

    private void ClearProgrammaticSelections()
    {
        foreach (TextBox part in _parts)
        {
            int caretIndex = part.Text?.Length ?? 0;
            part.CaretIndex = caretIndex;
            part.SelectionStart = caretIndex;
            part.SelectionEnd = caretIndex;
        }
    }
}