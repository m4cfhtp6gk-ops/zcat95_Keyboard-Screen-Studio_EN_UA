using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Platform.Storage;
using Avalonia.Styling;
using KeyboardScreen.App.Avalonia.Platform;
using KeyboardScreen.App.Avalonia.ViewModels;
using KeyboardScreen.Core;

namespace KeyboardScreen.App.Avalonia;

public sealed partial class MainWindow : Window
{
    private readonly MainWindowViewModel _viewModel;

    public MainWindow()
    {
        AvaloniaXamlLoader.Load(this);
        WindowsRoundedWindow.Attach(this);
        _viewModel = new MainWindowViewModel(new WindowsDesktopServices());
        _viewModel.PickImageAsync = PickImageAsync;
        DataContext = _viewModel;
        _viewModel.PropertyChanged += ViewModel_OnPropertyChanged;
        Opened += MainWindow_OnOpened;
        Closed += MainWindow_OnClosed;
    }

    private async void MainWindow_OnOpened(object? sender, EventArgs e)
    {
        var settingsStore = new JsonSettingsStore();
        AppSettings settings = await settingsStore.LoadAsync();
        if (!settings.HasCompletedOnboarding)
        {
            var guide = new FirstRunGuideWindow(ExtractDeviceIp(settings.DeviceEndpoint));
            string? selectedIp = await guide.ShowDialog<string?>(this);
            settings.HasCompletedOnboarding = true;
            if (!string.IsNullOrWhiteSpace(selectedIp))
            {
                settings.DeviceEndpoint = $"http://{selectedIp}/image/upload";
            }
            await settingsStore.SaveAsync(settings);
        }

        await _viewModel.InitializeAsync();
        ApplyUiTheme();
        if (_viewModel.StartMinimized ||
            Environment.GetCommandLineArgs().Any(argument =>
                string.Equals(argument, "--startup", StringComparison.OrdinalIgnoreCase)))
        {
            WindowState = WindowState.Minimized;
        }
    }

    private static string ExtractDeviceIp(string? value)
    {
        string candidate = value?.Trim() ?? string.Empty;
        return Uri.TryCreate(candidate, UriKind.Absolute, out Uri? uri) &&
               uri.Scheme is "http" or "https"
            ? uri.Host
            : candidate;
    }

    private async void MainWindow_OnClosed(object? sender, EventArgs e)
    {
        _viewModel.PropertyChanged -= ViewModel_OnPropertyChanged;
        await _viewModel.DisposeAsync();
    }

    private void ViewModel_OnPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainWindowViewModel.UiThemeMode))
        {
            ApplyUiTheme();
        }
    }

    private void TitleBar_OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            BeginMoveDrag(e);
        }
    }

    private void ThemeModeButton_OnClick(object? sender, RoutedEventArgs e)
    {
        bool isDark = Application.Current?.ActualThemeVariant == ThemeVariant.Dark;
        _viewModel.UiThemeMode = isDark ? UiThemeMode.Light : UiThemeMode.Dark;
    }

    private void ApplyUiTheme()
    {
        if (Application.Current is null)
        {
            return;
        }

        Application.Current.RequestedThemeVariant = _viewModel.UiThemeMode switch
        {
            UiThemeMode.Light => ThemeVariant.Light,
            UiThemeMode.Dark => ThemeVariant.Dark,
            _ => ThemeVariant.Default
        };
    }
    private void MinimizeButton_OnClick(object? sender, RoutedEventArgs e) =>
        WindowState = WindowState.Minimized;

    private void MaximizeButton_OnClick(object? sender, RoutedEventArgs e) =>
        WindowState = WindowState == WindowState.Maximized
            ? WindowState.Normal
            : WindowState.Maximized;

    private void CloseButton_OnClick(object? sender, RoutedEventArgs e) => Close();

    private async void ChooseImageButton_OnClick(object? sender, RoutedEventArgs e) =>
        await _viewModel.ChooseImageAsync();

    private async void AccentColorButton_OnClick(object? sender, RoutedEventArgs e)
    {
        var dialog = new AccentColorWindow(_viewModel.AccentColor);
        string? selected = await dialog.ShowDialog<string?>(this);
        if (!string.IsNullOrWhiteSpace(selected))
        {
            _viewModel.AccentColor = selected;
        }
    }

    private async Task<string?> PickImageAsync()
    {
        IReadOnlyList<IStorageFile> files = await StorageProvider.OpenFilePickerAsync(
            new FilePickerOpenOptions
            {
                Title = "选择屏幕图片",
                AllowMultiple = false,
                FileTypeFilter =
                [
                    new FilePickerFileType("图片")
                    {
                        Patterns = ["*.jpg", "*.jpeg", "*.png", "*.webp", "*.bmp"]
                    }
                ]
            });
        return files.FirstOrDefault()?.TryGetLocalPath();
    }
}
