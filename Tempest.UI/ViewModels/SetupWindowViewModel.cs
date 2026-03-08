using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Tempest.UI.Views;

namespace Tempest.UI.ViewModels;

public partial class SetupWindowViewModel : ViewModelBase
{
    private readonly Window _window;
    
    [ObservableProperty] private string _apiToken = string.Empty;
    [ObservableProperty] private string _stationId = string.Empty;
    [ObservableProperty] private string _deviceId = string.Empty;
    [ObservableProperty] private string _backendUrl = "http://localhost:5000";
    
    [ObservableProperty] private string _apiTokenError = string.Empty;
    [ObservableProperty] private string _stationIdError = string.Empty;
    [ObservableProperty] private string _deviceIdError = string.Empty;
    
    [ObservableProperty] private string _statusMessage = string.Empty;
    [ObservableProperty] private IBrush _statusColor = ThemeManager.GetThemeBrush("TextPrimaryBrush", "#FFFFFF");
    [ObservableProperty] private bool _isSaving;

    public bool HasApiTokenError => !string.IsNullOrEmpty(ApiTokenError);
    public bool HasStationIdError => !string.IsNullOrEmpty(StationIdError);
    public bool HasDeviceIdError => !string.IsNullOrEmpty(DeviceIdError);
    public bool HasStatusMessage => !string.IsNullOrEmpty(StatusMessage);

    public SetupWindowViewModel(Window window)
    {
        _window = window;
    }

    [RelayCommand]
    private void Exit()
    {
        Environment.Exit(0);
    }

    [RelayCommand]
    private async Task Save()
    {
        // Clear previous errors
        ApiTokenError = string.Empty;
        StationIdError = string.Empty;
        DeviceIdError = string.Empty;
        StatusMessage = string.Empty;

        // Validate inputs
        bool isValid = true;

        if (string.IsNullOrWhiteSpace(ApiToken))
        {
            ApiTokenError = "API Token is required";
            isValid = false;
        }

        if (string.IsNullOrWhiteSpace(StationId) || !int.TryParse(StationId, out _))
        {
            StationIdError = "Valid Station ID is required";
            isValid = false;
        }

        if (string.IsNullOrWhiteSpace(DeviceId) || !int.TryParse(DeviceId, out _))
        {
            DeviceIdError = "Valid Device ID is required";
            isValid = false;
        }

        if (!isValid)
        {
            OnPropertyChanged(nameof(HasApiTokenError));
            OnPropertyChanged(nameof(HasStationIdError));
            OnPropertyChanged(nameof(HasDeviceIdError));
            return;
        }

        IsSaving = true;
        StatusMessage = "Saving configuration...";
        StatusColor = ThemeManager.GetThemeBrush("TextPrimaryBrush", "#FFFFFF");
        OnPropertyChanged(nameof(HasStatusMessage));

        try
        {
            // Save UI configuration
            await SaveUiConfiguration();
            
            // Save backend configuration
            await SaveBackendConfiguration();

            StatusMessage = "Configuration saved successfully! Starting application...";
            StatusColor = ThemeManager.GetThemeBrush("AccentBrush", "#4ECDC4");
            OnPropertyChanged(nameof(HasStatusMessage));

            await Task.Delay(1500);
            
            // Close setup window and open main window
            _window.Close();
            
            var mainWindow = new MainWindow
            {
                DataContext = new MainWindowViewModel()
            };
            mainWindow.Show();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error saving configuration: {ex.Message}";
            StatusColor = ThemeManager.GetThemeBrush("DangerBrush", "#FF6B6B");
            OnPropertyChanged(nameof(HasStatusMessage));
        }
        finally
        {
            IsSaving = false;
        }
    }

    private async Task SaveUiConfiguration()
    {
        var uiConfig = new
        {
            BackendUrl = BackendUrl,
            Ui = new
            {
                SelectedTheme = ThemeManager.CurrentThemeName
            },
            WeatherFlow = new
            {
                ApiToken = ApiToken,
                StationId = int.Parse(StationId)
            }
        };

        var json = JsonSerializer.Serialize(uiConfig, new JsonSerializerOptions { WriteIndented = true });
        var uiConfigPath = Path.Combine(AppContext.BaseDirectory, "appsettings.Production.json");
        await File.WriteAllTextAsync(uiConfigPath, json);
    }

    private async Task SaveBackendConfiguration()
    {
        // Find the backend directory (TempestBlazorApp)
        var baseDir = AppContext.BaseDirectory;
        var projectRoot = Directory.GetParent(baseDir)?.Parent?.Parent?.Parent?.Parent?.FullName;
        
        if (projectRoot != null)
        {
            var backendConfigPath = Path.Combine(projectRoot, "TempestBlazorApp", "appsettings.Production.json");
            
            // Only create if the backend project exists
            if (Directory.Exists(Path.GetDirectoryName(backendConfigPath)))
            {
                var backendConfig = new
                {
                    WeatherFlow = new
                    {
                        ApiToken = ApiToken,
                        StationId = int.Parse(StationId),
                        DeviceId = int.Parse(DeviceId)
                    }
                };

                var json = JsonSerializer.Serialize(backendConfig, new JsonSerializerOptions { WriteIndented = true });
                await File.WriteAllTextAsync(backendConfigPath, json);
            }
        }
    }
}
