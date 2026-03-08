using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core;
using Avalonia.Data.Core.Plugins;
using Avalonia.Markup.Xaml;
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
    }

    public override void OnFrameworkInitializationCompleted()
    {
        ThemeManager.InitializeFromConfiguration();

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // Line below is needed to remove Avalonia data validation.
            // Without this line you will get duplicate validations from both Avalonia and CT
            BindingPlugins.DataValidators.RemoveAt(0);
            
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