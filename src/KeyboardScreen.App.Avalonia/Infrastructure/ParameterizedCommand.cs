using System.Windows.Input;

namespace KeyboardScreen.App.Avalonia.Infrastructure;

public sealed class ParameterizedCommand<T>(Action<T?> execute, Func<T?, bool>? canExecute = null) : ICommand
{
    public event EventHandler? CanExecuteChanged;

    public bool CanExecute(object? parameter) =>
        canExecute?.Invoke(parameter is T value ? value : default) ?? true;

    public void Execute(object? parameter) =>
        execute(parameter is T value ? value : default);

    public void NotifyCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}
