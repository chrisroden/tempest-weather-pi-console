using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
#if DEBUG
using AvaloniaUI.DiagnosticsSupport;
#endif
using Tempest.UI.ViewModels;
using Tempest.UI.Views;
using System.IO;
using System;

namespace Tempest.UI;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
#if DEBUG
        this.AttachDeveloperTools();
#endif
    }

    public override void OnFrameworkInitializationCompleted()
    {
        ThemeManager.InitializeFromConfiguration();

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // Check if configuration exists
            if (IsConfigurationMissing())
            {
                // Show setup window
                desktop.MainWindow = new SetupWindow();
            }
            else
            {
                // Show main window
                desktop.MainWindow = new MainWindow
                {
                    DataContext = new MainWindowViewModel(),
                };
            }
        }

        base.OnFrameworkInitializationCompleted();
    }

    private bool IsConfigurationMissing()
    {
        var configPath = Path.Combine(AppContext.BaseDirectory, "appsettings.Production.json");
        return !File.Exists(configPath);
    }
}