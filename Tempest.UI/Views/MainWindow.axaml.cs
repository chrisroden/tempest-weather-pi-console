using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using System;
using System.Threading.Tasks;
using Tempest.UI.ViewModels;
using Tempest.UI;

namespace Tempest.UI.Views;

public partial class MainWindow : Window
{
    private bool _isOpeningThemesDialog;

    public MainWindow()
    {
        InitializeComponent();
        WindowState = WindowState.FullScreen;
        ExtendClientAreaToDecorationsHint = true;

        AddHandler(InputElement.PointerPressedEvent, OnAnyPointerPressed, RoutingStrategies.Tunnel | RoutingStrategies.Bubble, handledEventsToo: true);
    }

    private void OnAnyPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (HeaderMenuPopup.IsVisible)
        {
            var sourceVisual = e.Source as Visual;
            var sourceElement = e.Source as StyledElement;
            var isInMenu = sourceVisual?.FindAncestorOfType<Border>()?.Name == "HeaderMenuPopup";
            var isMenuButton = sourceVisual?.FindAncestorOfType<Button>()?.Name == "MenuButton" || sourceElement?.Name == "MenuButton";

            if (!isInMenu && !isMenuButton)
            {
                SetHeaderMenuOpen(false);
            }
        }
    }

    private void SetHeaderMenuOpen(bool isOpen)
    {
        HeaderMenuPopup.IsVisible = isOpen;
    }

    private void OnMenuButtonClick(object? sender, RoutedEventArgs e)
    {
        SetHeaderMenuOpen(!HeaderMenuPopup.IsVisible);
        Console.WriteLine($"[UI-MENU] Toggle -> Open={HeaderMenuPopup.IsVisible} at {DateTime.Now:O}");
        Console.Out.Flush();
        e.Handled = true;
    }

    private async void OnThemesMenuItemClick(object? sender, RoutedEventArgs e)
    {
        Console.WriteLine($"[UI-MENU] Themes click at {DateTime.Now:O}");
        Console.Out.Flush();
        SetHeaderMenuOpen(false);
        await OpenThemesDialogAsync();
    }

    private void OnRestartMenuItemClick(object? sender, RoutedEventArgs e)
    {
        Console.WriteLine($"[UI-MENU] Restart click at {DateTime.Now:O}");
        Console.Out.Flush();
        SetHeaderMenuOpen(false);
        if (DataContext is MainWindowViewModel vm && vm.RestartBackendCommand.CanExecute(null))
        {
            vm.RestartBackendCommand.Execute(null);
        }
    }

    private void OnRebootMenuItemClick(object? sender, RoutedEventArgs e)
    {
        Console.WriteLine($"[UI-MENU] Reboot click at {DateTime.Now:O}");
        Console.Out.Flush();
        SetHeaderMenuOpen(false);
        if (DataContext is MainWindowViewModel vm && vm.RebootPiCommand.CanExecute(null))
        {
            vm.RebootPiCommand.Execute(null);
        }
    }

    private void OnExitMenuItemClick(object? sender, RoutedEventArgs e)
    {
        Console.WriteLine($"[UI-MENU] Exit click at {DateTime.Now:O}");
        Console.Out.Flush();
        SetHeaderMenuOpen(false);
        if (DataContext is MainWindowViewModel vm && vm.ExitAppCommand.CanExecute(null))
        {
            vm.ExitAppCommand.Execute(null);
        }
    }

    private async Task OpenThemesDialogAsync()
    {
        if (_isOpeningThemesDialog)
        {
            return;
        }

        _isOpeningThemesDialog = true;

        try
        {
        var dialog = new ThemeSelectionWindow(ThemeManager.GetAvailableThemeNames(), ThemeManager.CurrentThemeName);
        var selectedTheme = await dialog.ShowDialog<string?>(this);

        if (!string.IsNullOrWhiteSpace(selectedTheme))
        {
            ThemeManager.ApplyTheme(selectedTheme, persistSelection: true);
        }
        }
        finally
        {
            _isOpeningThemesDialog = false;
        }
    }
}