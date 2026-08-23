using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Avalonia.Styling;
using Avalonia.Media.Transformation;
using KeyboardScreen.App.Avalonia.Platform;
using KeyboardScreen.App.Avalonia.ViewModels;
using KeyboardScreen.Core;

namespace KeyboardScreen.App.Avalonia;

public sealed partial class MainWindow : Window
{
    private readonly MainWindowViewModel _viewModel;
    private readonly StartupGate _startupGate = new();
    private bool _noticeOpen;

    public MainWindow()
    {
        AvaloniaXamlLoader.Load(this);
        WindowsRoundedWindow.UseCustomCorners(this);
        _viewModel = new MainWindowViewModel(new WindowsDesktopServices());
        _viewModel.PickImageAsync = PickImageAsync;
        _viewModel.PickExportPathAsync = PickExportPathAsync;
        _viewModel.PickImportPathAsync = PickImportPathAsync;
        _viewModel.ShowTelegramNoticeAsync = async () =>
        {
            var dialog = new RiskNoticeWindow(
                Loc.T("RiskTelegramTitle"),
                Loc.T("RiskTelegramHeading"),
                Loc.T("RiskTelegramBody"));
            await dialog.ShowDialog<bool>(this);
        };
        DataContext = _viewModel;
        _viewModel.PropertyChanged += ViewModel_OnPropertyChanged;
        Opened += MainWindow_OnOpened;
        Closed += MainWindow_OnClosed;
    }

    private async void MainWindow_OnOpened(object? sender, EventArgs e)
    {
        bool startupMinimize = await InitializeOnceAsync();
        ApplyUiTheme();
        AnimateCurrentPage();
        if (startupMinimize)
        {
            WindowState = WindowState.Minimized;
        }
    }

    /// <summary>
    /// Runs the one-time startup initialization without requiring the window
    /// to be shown. Used when the app starts hidden (auto-start minimized).
    /// </summary>
    internal async Task InitializeHiddenStartupAsync() => await InitializeOnceAsync();

    private async Task<bool> InitializeOnceAsync()
    {
        if (!_startupGate.TryBeginInitialization())
        {
            return false;
        }

        try
        {
            var settingsStore = new JsonSettingsStore();
            AppSettings settings = await settingsStore.LoadAsync();
            if (!settings.HasCompletedOnboarding)
            {
                await RunOnboardingAsync(settingsStore, settings);
            }

            await _viewModel.InitializeAsync();
        }
        catch (Exception exception)
        {
            System.Diagnostics.Debug.WriteLine($"KSS startup initialization failed: {exception}");
        }

        bool hasStartupArgument = Environment.GetCommandLineArgs().Any(argument =>
            string.Equals(argument, "--startup", StringComparison.OrdinalIgnoreCase));
        return _startupGate.ConsumeStartupMinimize(_viewModel.StartMinimized, hasStartupArgument);
    }

    private async Task RunOnboardingAsync(JsonSettingsStore settingsStore, AppSettings settings)
    {
        try
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
        catch (Exception exception)
        {
            settings.HasCompletedOnboarding = true;
            System.Diagnostics.Debug.WriteLine($"KSS onboarding failed: {exception}");
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
        else if (e.PropertyName == nameof(MainWindowViewModel.SelectedNavigation))
        {
            AnimateCurrentPage();
        }
        else if (e.PropertyName == nameof(MainWindowViewModel.SelectedTheme))
        {
            _ = ShowPendingThemeNoticeAsync();
        }
    }

    private void AnimateCurrentPage()
    {
        Dispatcher.UIThread.Post(() =>
        {
            StackPanel? page = _viewModel.SelectedNavigation switch
            {
                "screen" => this.FindControl<StackPanel>("ScreenPage"),
                "theme" => this.FindControl<StackPanel>("ThemePage"),
                "automation" => this.FindControl<StackPanel>("AutomationPage"),
                "settings" => this.FindControl<StackPanel>("SettingsPage"),
                "about" => this.FindControl<StackPanel>("AboutPage"),
                _ => null
            };

            if (page is null || !page.IsVisible)
            {
                return;
            }

            page.Opacity = 0;
            page.RenderTransform = TransformOperations.Parse("translate(0px, 8px)");
            Dispatcher.UIThread.Post(() =>
            {
                page.Opacity = 1;
                page.RenderTransform = TransformOperations.Parse("translate(0px, 0px)");
            }, DispatcherPriority.Render);
        }, DispatcherPriority.Loaded);
    }

    private async Task ShowPendingThemeNoticeAsync()
    {
        if (_noticeOpen || !_viewModel.NeedsCurrentThemeNotice)
        {
            return;
        }

        _noticeOpen = true;
        try
        {
            (string title, string subtitle, string body) = _viewModel.SelectedTheme?.Id switch
            {
                "stocks" => (
                    Loc.T("RiskStocksTitle"),
                    Loc.T("RiskStocksHeading"),
                    Loc.T("RiskStocksBody")),
                "ai-quota" => (
                    Loc.T("RiskAiTitle"),
                    Loc.T("RiskAiHeading"),
                    Loc.T("RiskAiBody")),
                "claude-usage" => (
                    Loc.T("RiskClaudeTitle"),
                    Loc.T("RiskClaudeHeading"),
                    Loc.T("RiskClaudeBody")),
                "github" => (
                    Loc.T("RiskGitHubTitle"),
                    Loc.T("RiskGitHubHeading"),
                    Loc.T("RiskGitHubBody")),
                "alerts" => (
                    Loc.T("RiskAlertsTitle"),
                    Loc.T("RiskAlertsHeading"),
                    Loc.T("RiskAlertsBody")),
                _ => (string.Empty, string.Empty, string.Empty)
            };

            if (string.IsNullOrEmpty(title))
            {
                return;
            }

            var dialog = new RiskNoticeWindow(title, subtitle, body);
            await dialog.ShowDialog<bool>(this);
            await _viewModel.AcknowledgeCurrentThemeNoticeAsync();
        }
        finally
        {
            _noticeOpen = false;
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

    private async void ThemeAccentButton_OnClick(object? sender, RoutedEventArgs e)
    {
        string current = _viewModel.CurrentThemeAccentColor.Length > 0
            ? _viewModel.CurrentThemeAccentColor
            : _viewModel.AccentColor;
        var dialog = new AccentColorWindow(current);
        string? selected = await dialog.ShowDialog<string?>(this);
        if (!string.IsNullOrWhiteSpace(selected))
        {
            _viewModel.CurrentThemeAccentColor = selected;
        }
    }

    private void ThemeAccentResetButton_OnClick(object? sender, RoutedEventArgs e) =>
        _viewModel.CurrentThemeAccentColor = string.Empty;

    private void ComposerAddButton_OnClick(object? sender, RoutedEventArgs e) =>
        _viewModel.AddComposerWidget();

    private void ComposerMoveUpButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if ((sender as Control)?.DataContext is ComposerRowViewModel row)
        {
            _viewModel.MoveComposerRow(row, -1);
        }
    }

    private void ComposerMoveDownButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if ((sender as Control)?.DataContext is ComposerRowViewModel row)
        {
            _viewModel.MoveComposerRow(row, 1);
        }
    }

    private void ComposerRemoveButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if ((sender as Control)?.DataContext is ComposerRowViewModel row)
        {
            _viewModel.RemoveComposerRow(row);
        }
    }

    private async Task<string?> PickExportPathAsync()
    {
        IStorageFile? file = await StorageProvider.SaveFilePickerAsync(
            new FilePickerSaveOptions
            {
                SuggestedFileName = "kss-settings.json",
                DefaultExtension = "json",
                FileTypeChoices =
                [
                    new FilePickerFileType(Loc.T("PortJsonFileType")) { Patterns = ["*.json"] }
                ]
            });
        return file?.TryGetLocalPath();
    }

    private async Task<string?> PickImportPathAsync()
    {
        IReadOnlyList<IStorageFile> files = await StorageProvider.OpenFilePickerAsync(
            new FilePickerOpenOptions
            {
                AllowMultiple = false,
                FileTypeFilter =
                [
                    new FilePickerFileType(Loc.T("PortJsonFileType")) { Patterns = ["*.json"] }
                ]
            });
        return files.FirstOrDefault()?.TryGetLocalPath();
    }

    private async Task<string?> PickImageAsync()
    {
        IReadOnlyList<IStorageFile> files = await StorageProvider.OpenFilePickerAsync(
            new FilePickerOpenOptions
            {
                Title = Loc.T("PickImageTitle"),
                AllowMultiple = false,
                FileTypeFilter =
                [
                    new FilePickerFileType(Loc.T("PickImageFileType"))
                    {
                        Patterns = ["*.jpg", "*.jpeg", "*.png", "*.webp", "*.bmp"]
                    }
                ]
            });
        return files.FirstOrDefault()?.TryGetLocalPath();
    }
}
