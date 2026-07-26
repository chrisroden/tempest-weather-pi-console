using System;
using System.Linq;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Configuration;
using Tempest.REST;
using Tempest.REST.Models;
using Tempest.WebSocket.Models.Responses;
using Tempest.WebSocket.Models.Responses.Enums;
using System.Text.Json;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using System.Runtime.InteropServices;

namespace Tempest.UI.ViewModels;

public partial class MainWindowViewModel : ViewModelBase, IAsyncDisposable
{
    private HubConnection? _hubConnection;
    private readonly TempestRESTService _restService;
    private readonly string _backendUrl;
    private readonly string? _locationNameOverride;
    private readonly System.Net.Http.HttpClient _diagnosticsHttpClient;
    private DateTime _lastDataReceived = DateTime.Now;
    private System.Timers.Timer? _heartbeatTimer;
    private System.Timers.Timer? _forecastRefreshTimer;
    private bool _isAttemptingRestart = false;
    private int _restartAttempts = 0;
    private bool _connectionStateKnown;
    private readonly int _forecastRefreshMinutes;
    private int _forecastRefreshRunning;
    private const string HealthDiagnosticsPrefix = "Backend health:";
    private const string UnknownConnectionStatusMessage = "Connection status unknown — waiting for backend data.";
    private static string StatusInfoColor => ThemeManager.GetThemeString("StatusInfoColor", "#F7931E");
    private static string StatusErrorColor => ThemeManager.GetThemeString("StatusErrorColor", "#DC3545");

    // Current Conditions
    [ObservableProperty] private double _currentTemperature;
    [ObservableProperty] private double _feelsLike;
    [ObservableProperty] private double _highTemperature;
    [ObservableProperty] private double _lowTemperature;
    [ObservableProperty] private int _humidity;
    [ObservableProperty] private string _weatherCondition = "Loading...";
    [ObservableProperty] private Bitmap? _weatherIcon;
    [ObservableProperty] private bool _isRaining = false;
    [ObservableProperty] private string _baseWeatherCondition = "Loading..."; // Store forecast condition
    [ObservableProperty] private string _baseWeatherIcon = "cloudy"; // Store forecast icon
    
    // Wind Data
    [ObservableProperty] private double _windSpeed;
    [ObservableProperty] private int _windDirection;
    [ObservableProperty] private string _windDirectionCardinal = "N";
    [ObservableProperty] private double _windGust;
    
    // Pressure & Other
    [ObservableProperty] private double _pressure;
    [ObservableProperty] private string _pressureTrend = "steady";
    [ObservableProperty] private int _uvIndex;
    [ObservableProperty] private double _precipitation24h;
    [ObservableProperty] private int _lightningStrikes;
    
    // Forecast Data
    [ObservableProperty] private string _day1Temp = "--";
    [ObservableProperty] private Bitmap? _day1Icon;
    [ObservableProperty] private string _day1Precip = "0%";
    [ObservableProperty] private string _day1Label = "--";
    
    [ObservableProperty] private string _day2Temp = "--";
    [ObservableProperty] private Bitmap? _day2Icon;
    [ObservableProperty] private string _day2Precip = "0%";
    [ObservableProperty] private string _day2Label = "--";
    
    [ObservableProperty] private string _day3Temp = "--";
    [ObservableProperty] private Bitmap? _day3Icon;
    [ObservableProperty] private string _day3Precip = "0%";
    [ObservableProperty] private string _day3Label = "--";
    
    [ObservableProperty] private string _day4Temp = "--";
    [ObservableProperty] private Bitmap? _day4Icon;
    [ObservableProperty] private string _day4Precip = "0%";
    [ObservableProperty] private string _day4Label = "--";
    
    [ObservableProperty] private string _day5Temp = "--";
    [ObservableProperty] private Bitmap? _day5Icon;
    [ObservableProperty] private string _day5Precip = "0%";
    [ObservableProperty] private string _day5Label = "--";
    
    [ObservableProperty] private string _day6Temp = "--";
    [ObservableProperty] private Bitmap? _day6Icon;
    [ObservableProperty] private string _day6Precip = "0%";
    [ObservableProperty] private string _day6Label = "--";
    
    [ObservableProperty] private string _day7Temp = "--";
    [ObservableProperty] private Bitmap? _day7Icon;
    [ObservableProperty] private string _day7Precip = "0%";
    [ObservableProperty] private string _day7Label = "--";
    
    // Status
    [ObservableProperty] private string _locationName = "Tempest Weather Station";
    [ObservableProperty] private string _lastUpdated = DateTime.Now.ToString("HH:mm:ss");
    [ObservableProperty] private bool _isConnected = false;
    [ObservableProperty] private string _currentDate = DateTime.Now.ToString("MMMM d, yyyy");
    [ObservableProperty] private string _currentDayOfWeek = DateTime.Now.ToString("dddd");
    [ObservableProperty] private string _currentTime = DateTime.Now.ToString("h:mm tt");
    
    // Status Notification
    [ObservableProperty] private string _statusMessage = "";
    [ObservableProperty] private bool _showStatusMessage = false;
    [ObservableProperty] private string _statusMessageColor = StatusInfoColor;

    public MainWindowViewModel()
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .AddJsonFile($"appsettings.{Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production"}.json", optional: true, reloadOnChange: true)
            .Build();

        _backendUrl = configuration.GetValue<string>("BackendUrl") ?? throw new InvalidOperationException("BackendUrl not configured in appsettings.json");
        _locationNameOverride = configuration.GetValue<string>("Ui:ScreenshotLocationLabel");
        if (!string.IsNullOrWhiteSpace(_locationNameOverride))
        {
            LocationName = _locationNameOverride;
        }
        _restService = new TempestRESTService(configuration);
        _forecastRefreshMinutes = Math.Max(15, configuration.GetValue<int?>("ForecastRefreshMinutes") ?? 360);
        _diagnosticsHttpClient = new System.Net.Http.HttpClient
        {
            Timeout = TimeSpan.FromSeconds(3)
        };

        ShowUnknownConnectionStatus();
        _ = InitializeAsync();
    }

    private void ShowUnknownConnectionStatus()
    {
        if (_connectionStateKnown)
        {
            return;
        }

        StatusMessage = UnknownConnectionStatusMessage;
        StatusMessageColor = StatusErrorColor;
        ShowStatusMessage = true;
    }

    private void SetConnectionState(bool isConnected)
    {
        IsConnected = isConnected;
        _connectionStateKnown = true;

        if (string.Equals(StatusMessage, UnknownConnectionStatusMessage, StringComparison.Ordinal))
        {
            ShowStatusMessage = false;
            StatusMessage = string.Empty;
        }
    }

    private async Task InitializeAsync()
    {
        try
        {
            // Load forecast data from REST API
            await LoadForecastData();

            // Schedule forecast refreshes
            StartForecastRefreshTimer();
            
            // Connect to SignalR hub for real-time updates
            await ConnectToSignalR();
            
            // Start heartbeat timer to detect stale data
            StartHeartbeatTimer();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Initialization error: {ex.Message}");
            WeatherCondition = "Error loading data";
            SetConnectionState(false);
            StatusMessage = $"Startup failed: {ex.Message}";
            StatusMessageColor = StatusErrorColor;
            ShowStatusMessage = true;
        }
    }
    
    private void StartHeartbeatTimer()
    {
        _heartbeatTimer = new System.Timers.Timer(30000); // Check every 30 seconds
        _heartbeatTimer.Elapsed += async (sender, e) =>
        {
            var timeSinceLastData = DateTime.Now - _lastDataReceived;
            if (timeSinceLastData.TotalMinutes > 2 && !_isAttemptingRestart) // No data for 2 minutes
            {
                Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] WARNING: No data received for {timeSinceLastData.TotalMinutes:F1} minutes - attempting automatic restart");
                SetConnectionState(false);
                WeatherCondition = "Connection Lost";
                _isAttemptingRestart = true;
                
                // Attempt automatic restart
                await AttemptAutoRestart();
            }

            await PollBackendHealthDetails();
        };
        _heartbeatTimer.Start();
    }

    private void StartForecastRefreshTimer()
    {
        _forecastRefreshTimer = new System.Timers.Timer(TimeSpan.FromMinutes(_forecastRefreshMinutes).TotalMilliseconds)
        {
            AutoReset = true
        };

        _forecastRefreshTimer.Elapsed += async (_, _) => await RefreshForecastDataAsync();
        _forecastRefreshTimer.Start();
        Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Forecast refresh scheduled every {_forecastRefreshMinutes} minutes");
    }

    private async Task RefreshForecastDataAsync()
    {
        if (Interlocked.Exchange(ref _forecastRefreshRunning, 1) == 1)
        {
            return;
        }

        try
        {
            await LoadForecastData();
        }
        finally
        {
            Interlocked.Exchange(ref _forecastRefreshRunning, 0);
        }
    }

    private async Task PollBackendHealthDetails()
    {
        try
        {
            var response = await _diagnosticsHttpClient.GetAsync($"{_backendUrl}/health/details");
            if (!response.IsSuccessStatusCode)
            {
                return;
            }

            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
            {
                return;
            }

            if (!doc.RootElement.TryGetProperty("status", out var statusElement) || statusElement.ValueKind != JsonValueKind.String)
            {
                return;
            }

            var status = statusElement.GetString();
            if (string.Equals(status, "degraded", StringComparison.OrdinalIgnoreCase))
            {
                var reasonCodes = doc.RootElement.TryGetProperty("reasonCodes", out var reasonsElement) && reasonsElement.ValueKind == JsonValueKind.Array
                    ? string.Join(", ", reasonsElement.EnumerateArray().Select(x => x.GetString()).Where(x => !string.IsNullOrWhiteSpace(x)))
                    : "unknown";

                Dispatcher.UIThread.Post(() =>
                {
                    StatusMessage = $"{HealthDiagnosticsPrefix} degraded ({reasonCodes})";
                    StatusMessageColor = StatusInfoColor;
                    ShowStatusMessage = true;
                });
            }
            else if (string.Equals(status, "ok", StringComparison.OrdinalIgnoreCase))
            {
                Dispatcher.UIThread.Post(() =>
                {
                    if (StatusMessage.StartsWith(HealthDiagnosticsPrefix, StringComparison.Ordinal))
                    {
                        ShowStatusMessage = false;
                        StatusMessage = string.Empty;
                    }
                });
            }
        }
        catch
        {
        }
    }
    
    private async Task AttemptAutoRestart()
    {
        _restartAttempts++;
        Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Auto-restart attempt #{_restartAttempts}");

        try
        {
            // Heartbeat runs on a thread-pool timer; all UI / hub work must run on the UI thread.
            await Dispatcher.UIThread.InvokeAsync(async () =>
            {
                StatusMessage = $"Connection lost - attempting restart (attempt {_restartAttempts})...";
                StatusMessageColor = StatusInfoColor;
                ShowStatusMessage = true;
                await RestartBackendCore(throwOnFailure: true);
            });
            // Successful path restarts the UI service (or exits); this only runs on failure.
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] ERROR: Auto-restart failed: {ex.Message}");

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                StatusMessage = $"Restart failed (attempt {_restartAttempts}). Will retry in 3 minutes.";
                StatusMessageColor = StatusErrorColor;
                ShowStatusMessage = true;
            });

            await Task.Delay(180000); // 3 minutes
            _isAttemptingRestart = false;
        }
    }

    private async Task LoadForecastData()
    {
        try
        {
            var forecast = await _restService.GetForecast();

            Day1Label = "--";
            Day2Label = "--";
            Day3Label = "--";
            Day4Label = "--";
            Day5Label = "--";
            Day6Label = "--";
            Day7Label = "--";
            
            // For documentation screenshots, allow overriding station label via config.
            LocationName = string.IsNullOrWhiteSpace(_locationNameOverride) ? forecast.LocationName : _locationNameOverride;
            
            // Update current conditions
            var current = forecast.CurrentConditions;
            CurrentTemperature = current.AirTemperature;
            FeelsLike = current.FeelsLike;
            Humidity = current.RelativeHumidity;
            Pressure = current.SeaLevelPressure;
            PressureTrend = current.PressureTrend;
            BaseWeatherCondition = current.Conditions; // Store base forecast condition
            BaseWeatherIcon = current.Icon; // Store base forecast icon
            
            // Fix icon mismatch: if condition says "Rain" or "Thunderstorm" but icon doesn't match, use appropriate icon
            var fixedIcon = current.Icon;
            
            // If condition has "Rain" AND lightning, always use thunderstorm-rain icon
            if (current.Conditions.Contains("Rain", StringComparison.OrdinalIgnoreCase) && 
                current.LightningStrikeCountLast1hr > 0)
            {
                fixedIcon = "thunderstorm-rain";
                BaseWeatherIcon = "thunderstorm-rain";
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Rain + Lightning detected in forecast: Condition='{current.Conditions}', Lightning={current.LightningStrikeCountLast1hr}, using 'thunderstorm-rain' icon");
            }
            // If condition says "Thunderstorm" but icon doesn't show it
            else if (current.Conditions.Contains("Thunderstorm", StringComparison.OrdinalIgnoreCase) && 
                !current.Icon.Contains("thunderstorm", StringComparison.OrdinalIgnoreCase))
            {
                fixedIcon = "thunderstorm-rain";
                BaseWeatherIcon = "thunderstorm-rain";
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Icon mismatch detected: Condition='{current.Conditions}' but Icon='{current.Icon}', using 'thunderstorm-rain' instead");
            }
            // If condition has "Rain" but icon doesn't match (no lightning)
            else if (current.Conditions.Contains("Rain", StringComparison.OrdinalIgnoreCase) && 
                     !current.Icon.Contains("rain", StringComparison.OrdinalIgnoreCase) &&
                     !current.Icon.Contains("thunderstorm", StringComparison.OrdinalIgnoreCase))
            {
                fixedIcon = "rainy";
                BaseWeatherIcon = "rainy";
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Icon mismatch detected: Condition='{current.Conditions}' but Icon='{current.Icon}', using 'rainy' instead");
            }
            
            // Only set if not currently raining (real-time override)
            if (!IsRaining)
            {
                WeatherCondition = current.Conditions;
                WeatherIcon = GetWeatherIcon(fixedIcon);
            }
            WindSpeed = current.WindAvg;
            WindDirection = current.WindDirection;
            WindDirectionCardinal = current.WindDirectionCardinal;
            WindGust = current.WindGust;
            UvIndex = current.Uv;
            Precipitation24h = current.PrecipAccumLocalDay;
            LightningStrikes = current.LightningStrikeCountLast1hr;
            
            // Get today's forecast for high/low
            if (forecast.Forecast.DailyForecasts.Count > 0)
            {
                var today = forecast.Forecast.DailyForecasts[0];
                HighTemperature = today.AirTempHigh;
                LowTemperature = today.AirTempLow;
            }
            
            // Update 6-day forecast
            if (forecast.Forecast.DailyForecasts.Count > 1)
            {
                var day1 = forecast.Forecast.DailyForecasts[1];
                Day1Label = FormatForecastDate(day1.DayStartLocal);
                Day1Temp = $"{day1.AirTempLow:F0}° / {day1.AirTempHigh:F0}°";
                Day1Icon = GetWeatherIcon(day1.Icon);
                Day1Precip = $"{day1.PrecipProbability}%";
            }
            
            if (forecast.Forecast.DailyForecasts.Count > 2)
            {
                var day2 = forecast.Forecast.DailyForecasts[2];
                Day2Label = FormatForecastDate(day2.DayStartLocal);
                Day2Temp = $"{day2.AirTempLow:F0}° / {day2.AirTempHigh:F0}°";
                Day2Icon = GetWeatherIcon(day2.Icon);
                Day2Precip = $"{day2.PrecipProbability}%";
            }
            
            if (forecast.Forecast.DailyForecasts.Count > 3)
            {
                var day3 = forecast.Forecast.DailyForecasts[3];
                Day3Label = FormatForecastDate(day3.DayStartLocal);
                Day3Temp = $"{day3.AirTempLow:F0}° / {day3.AirTempHigh:F0}°";
                Day3Icon = GetWeatherIcon(day3.Icon);
                Day3Precip = $"{day3.PrecipProbability}%";
            }
            
            if (forecast.Forecast.DailyForecasts.Count > 4)
            {
                var day4 = forecast.Forecast.DailyForecasts[4];
                Day4Label = FormatForecastDate(day4.DayStartLocal);
                Day4Temp = $"{day4.AirTempLow:F0}° / {day4.AirTempHigh:F0}°";
                Day4Icon = GetWeatherIcon(day4.Icon);
                Day4Precip = $"{day4.PrecipProbability}%";
            }
            
            if (forecast.Forecast.DailyForecasts.Count > 5)
            {
                var day5 = forecast.Forecast.DailyForecasts[5];
                Day5Label = FormatForecastDate(day5.DayStartLocal);
                Day5Temp = $"{day5.AirTempLow:F0}° / {day5.AirTempHigh:F0}°";
                Day5Icon = GetWeatherIcon(day5.Icon);
                Day5Precip = $"{day5.PrecipProbability}%";
            }
            
            if (forecast.Forecast.DailyForecasts.Count > 6)
            {
                var day6 = forecast.Forecast.DailyForecasts[6];
                Day6Label = FormatForecastDate(day6.DayStartLocal);
                Day6Temp = $"{day6.AirTempLow:F0}° / {day6.AirTempHigh:F0}°";
                Day6Icon = GetWeatherIcon(day6.Icon);
                Day6Precip = $"{day6.PrecipProbability}%";
            }
            
            if (forecast.Forecast.DailyForecasts.Count > 7)
            {
                var day7 = forecast.Forecast.DailyForecasts[7];
                Day7Label = FormatForecastDate(day7.DayStartLocal);
                Day7Temp = $"{day7.AirTempLow:F0}° / {day7.AirTempHigh:F0}°";
                Day7Icon = GetWeatherIcon(day7.Icon);
                Day7Precip = $"{day7.PrecipProbability}%";
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error loading forecast: {ex.Message}");
            WeatherCondition = "Forecast Error";
            StatusMessage = $"Failed to load forecast: {ex.Message}";
            StatusMessageColor = StatusErrorColor;
            ShowStatusMessage = true;
        }
    }

    private async Task ConnectToSignalR()
    {
        try
        {
            // Connect to the Blazor app's SignalR hub
            _hubConnection = new HubConnectionBuilder()
                .WithUrl($"{_backendUrl}/weatherHub")
                .WithKeepAliveInterval(TimeSpan.FromSeconds(15))
                .WithServerTimeout(TimeSpan.FromSeconds(30))
                .WithAutomaticReconnect()
                .Build();

            _hubConnection.On<string>("ReceiveWeatherUpdateRaw", async (rawMsg) =>
            {
                // Marshal to UI thread and await processing to preserve ordering and avoid fire-and-forget updates
                await Dispatcher.UIThread.InvokeAsync(() => ProcessWeatherUpdate(rawMsg));
            });

            _hubConnection.On<string>("ReceiveHeartbeat", _ =>
            {
                // Internal-only: keep heartbeat traffic off user-facing "Updated" time.
            });

            _hubConnection.Reconnected += connectionId =>
            {
                SetConnectionState(true);
                // Don't update timestamp here - wait for actual data from backend
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] SignalR reconnected, waiting for backend data...");
                return Task.CompletedTask;
            };

            _hubConnection.Reconnecting += ex =>
            {
                SetConnectionState(false);
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] SignalR reconnecting: {ex?.Message}");
                return Task.CompletedTask;
            };

            _hubConnection.Closed += ex =>
            {
                SetConnectionState(false);
                return Task.CompletedTask;
            };

            var initialRetryDelays = new[] { TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(5) };
            Exception? lastStartException = null;
            var started = false;

            for (var attempt = 0; attempt <= initialRetryDelays.Length; attempt++)
            {
                try
                {
                    await _hubConnection.StartAsync();
                    started = true;
                    break;
                }
                catch (Exception ex)
                {
                    lastStartException = ex;
                    if (attempt == initialRetryDelays.Length)
                    {
                        break;
                    }

                    Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] SignalR initial connect attempt {attempt + 1} failed: {ex.Message}. Retrying in {initialRetryDelays[attempt].TotalSeconds:F0}s...");
                    await Task.Delay(initialRetryDelays[attempt]);
                }
            }

            if (!started)
            {
                throw new InvalidOperationException("Failed to establish SignalR connection after retries.", lastStartException);
            }

            SetConnectionState(true);
            Console.WriteLine("Connected to SignalR hub");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"SignalR connection error: {ex.Message}");
            SetConnectionState(false);
        }
    }

    private void ProcessWeatherUpdate(string rawJson)
    {
        try
        {
            _lastDataReceived = DateTime.Now;
            using var doc = JsonDocument.Parse(rawJson);
            var root = doc.RootElement;
            
            if (root.TryGetProperty("type", out var typeElement))
            {
                var type = typeElement.GetString();
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Processing message type: {type}");
                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    AllowTrailingCommas = true,
                    NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.AllowReadingFromString
                };

                if (type == "rapidwind")
                {
                    var rapidWind = JsonSerializer.Deserialize<RapidWind>(rawJson, options);
                    if (rapidWind != null)
                    {
                        Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] UI Received RapidWind: Speed={rapidWind.WindSpeedInMetersPerSecond * 2.23694:F1}mph, Direction={rapidWind.WindDirectionInDegrees}°");
                        SetConnectionState(true);
                        WindSpeed = rapidWind.WindSpeedInMetersPerSecond * 2.23694; // Convert to MPH
                        WindDirection = rapidWind.WindDirectionInDegrees;
                        WindDirectionCardinal = GetCardinalDirection(WindDirection);
                        
                        // Use backend timestamp, not UI time
                        if (rapidWind.TimeEpochInSeconds.HasValue)
                        {
                            var backendTime = ConvertUtcToLocal(rapidWind.TimeEpochInSeconds.Value);
                            LastUpdated = backendTime.ToString("HH:mm:ss");
                            CurrentTime = backendTime.ToString("h:mm tt");
                            CurrentDate = backendTime.ToString("MMMM d, yyyy");
                            CurrentDayOfWeek = backendTime.ToString("dddd");
                        }
                        else
                        {
                            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] ERROR: RapidWind missing timestamp!");
                        }
                        
                        if (WeatherCondition == "Connection Lost") WeatherCondition = "Live";
                    }
                }
                else if (type == "stationobservation")
                {
                    var stationObs = JsonSerializer.Deserialize<StationObservation>(rawJson, options);
                    if (stationObs != null)
                    {
                        Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] UI Received StationObservation: PrecipType={stationObs.PrecipitationType}, Rate={stationObs.PrecipitationRate}mm/hr");
                        SetConnectionState(true);
                        if (stationObs.AirTemperatureInCelsius.HasValue)
                        {
                            CurrentTemperature = stationObs.AirTemperatureInCelsius.Value * 9 / 5 + 32; // Convert to F
                        }
                        
                        if (stationObs.StationPressureInMillibar.HasValue)
                        {
                            Pressure = stationObs.StationPressureInMillibar.Value;
                        }
                        
                        if (stationObs.RelativeHumidity.HasValue)
                        {
                            Humidity = stationObs.RelativeHumidity.Value;
                        }
                        
                        if (stationObs.Summary?.FeelsLike.HasValue == true)
                        {
                            FeelsLike = stationObs.Summary.FeelsLike.Value;
                        }
                        
                        // Update 24-hour precipitation accumulation (convert from mm to inches)
                        if (stationObs.LocalDayRainAccumulationMm.HasValue)
                        {
                            Precipitation24h = stationObs.LocalDayRainAccumulationMm.Value / 25.4;
                        }
                        
                        // Update lightning strikes (last hour)
                        if (stationObs.Summary?.StrikeCount1h.HasValue == true)
                        {
                            LightningStrikes = stationObs.Summary.StrikeCount1h.Value;
                            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Lightning data updated: {LightningStrikes} strikes in last hour");
                        }
                        
                        // Check for active precipitation in station observation
                        // Use multiple signals: precipitation type, rate, and 1-hour accumulation
                        var precipRate = stationObs.PrecipitationRate ?? 0;
                        var precip1h = stationObs.Summary?.PrecipTotal1h ?? 0;
                        var precipType = stationObs.PrecipitationType ?? Tempest.WebSocket.Models.Responses.Enums.PrecipitationType.None;
                        
                        Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Precipitation check: Type={precipType}, Rate={precipRate}mm/hr, 1h={precip1h}mm, IsRaining={IsRaining}");
                        
                        // Rain is detected if: type is not None, OR rate > 0 (real-time only, no historical data)
                        bool isActivelyRaining = (precipType != Tempest.WebSocket.Models.Responses.Enums.PrecipitationType.None) || 
                                                (precipRate > 0);
                        
                        if (isActivelyRaining)
                        {
                            IsRaining = true;
                            
                            if (precipType == Tempest.WebSocket.Models.Responses.Enums.PrecipitationType.Hail)
                            {
                                WeatherCondition = "Hail";
                                WeatherIcon = GetWeatherIcon("sleet");
                                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] HAIL DETECTED!");
                            }
                            else
                            {
                                // Check if there's recent lightning activity (within last hour)
                                bool hasRecentLightning = LightningStrikes > 0;
                                
                                if (hasRecentLightning)
                                {
                                    WeatherCondition = "Thunderstorm";
                                    WeatherIcon = GetWeatherIcon("thunderstorm-rain");
                                    Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] THUNDERSTORM DETECTED! Rain rate: {precipRate}mm/hr, Lightning strikes (1h): {LightningStrikes}");
                                }
                                else
                                {
                                    WeatherCondition = "Raining";
                                    WeatherIcon = GetWeatherIcon("rainy");
                                    Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] RAIN DETECTED! Rate: {precipRate}mm/hr, 1hr Total: {precip1h}mm");
                                }
                            }
                        }
                        else
                        {
                            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] No precipitation detected");
                            
                            // Check for dry lightning (lightning without rain)
                            bool hasRecentLightning = LightningStrikes > 0;
                            
                            if (hasRecentLightning)
                            {
                                // Lightning detected without rain - show dry lightning
                                IsRaining = false;
                                WeatherCondition = "Lightning";
                                WeatherIcon = GetWeatherIcon("thunderstorm");
                                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] DRY LIGHTNING DETECTED! Lightning strikes (1h): {LightningStrikes}");
                            }
                            else if (IsRaining)
                            {
                                // No precipitation and no lightning, return to forecast condition
                                IsRaining = false;
                                WeatherCondition = BaseWeatherCondition;
                                WeatherIcon = GetWeatherIcon(BaseWeatherIcon);
                                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Precipitation ended, returning to forecast condition: {BaseWeatherCondition}");
                            }
                        }
                        
                        // Use backend timestamp, not UI time
                        if (stationObs.TimeEpochInSeconds.HasValue)
                        {
                            var backendTime = ConvertUtcToLocal(stationObs.TimeEpochInSeconds.Value);
                            LastUpdated = backendTime.ToString("HH:mm:ss");
                            CurrentTime = backendTime.ToString("h:mm tt");
                            CurrentDate = backendTime.ToString("MMMM d, yyyy");
                            CurrentDayOfWeek = backendTime.ToString("dddd");
                        }
                        else
                        {
                            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] ERROR: StationObservation missing timestamp!");
                        }
                        
                        if (WeatherCondition == "Connection Lost") WeatherCondition = IsRaining ? "Raining" : BaseWeatherCondition;
                    }
                }
                else if (type == "sky")
                {
                    var skyObs = JsonSerializer.Deserialize<Sky>(rawJson, options);
                    if (skyObs != null)
                    {
                        IsConnected = true;
                        
                        // Check for active precipitation using multiple signals
                        var rainAccum = skyObs.RainAccumulatedinMm ?? 0;
                        var dailyRainAccum = skyObs.LocalDailyRainAccumulationMm ?? 0;
                        var precipType = skyObs.PrecipitationType ?? Tempest.WebSocket.Models.Responses.Enums.PrecipitationType.None;
                        
                        Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Sky precipitation check: Type={precipType}, MinuteAccum={rainAccum}mm, DailyAccum={dailyRainAccum}mm");
                        
                        // Rain is detected if: type is not None, OR minute accumulation > 0
                        bool isActivelyRaining = (precipType != Tempest.WebSocket.Models.Responses.Enums.PrecipitationType.None) || 
                                                (rainAccum > 0);
                        
                        if (isActivelyRaining)
                        {
                            IsRaining = true;
                            
                            if (precipType == Tempest.WebSocket.Models.Responses.Enums.PrecipitationType.Hail)
                            {
                                WeatherCondition = "Hail";
                                WeatherIcon = GetWeatherIcon("sleet");
                                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] HAIL DETECTED!");
                            }
                            else
                            {
                                // Check if there's recent lightning activity (within last hour)
                                bool hasRecentLightning = LightningStrikes > 0;
                                
                                if (hasRecentLightning)
                                {
                                    WeatherCondition = "Thunderstorm";
                                    WeatherIcon = GetWeatherIcon("thunderstorm-rain");
                                    Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] THUNDERSTORM DETECTED! Rain accumulation: {rainAccum}mm, Lightning strikes (1h): {LightningStrikes}");
                                }
                                else
                                {
                                    WeatherCondition = "Raining";
                                    WeatherIcon = GetWeatherIcon("rainy");
                                    Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] RAIN DETECTED! Minute accumulation: {rainAccum}mm");
                                }
                            }
                        }
                        else
                        {
                            // No active precipitation - check for dry lightning
                            bool hasRecentLightning = LightningStrikes > 0;
                            
                            if (hasRecentLightning)
                            {
                                // Lightning detected without rain - show dry lightning
                                IsRaining = false;
                                WeatherCondition = "Lightning";
                                WeatherIcon = GetWeatherIcon("thunderstorm");
                                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] DRY LIGHTNING DETECTED! Lightning strikes (1h): {LightningStrikes}");
                            }
                            else if (IsRaining)
                            {
                                // No precipitation and no lightning, return to forecast condition
                                IsRaining = false;
                                WeatherCondition = BaseWeatherCondition;
                                WeatherIcon = GetWeatherIcon(BaseWeatherIcon);
                                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Precipitation ended, returning to forecast condition: {BaseWeatherCondition}");
                            }
                        }
                        
                        // Use backend timestamp, not UI time
                        if (skyObs.TimeEpochInSeconds.HasValue)
                        {
                            var backendTime = ConvertUtcToLocal(skyObs.TimeEpochInSeconds.Value);
                            LastUpdated = backendTime.ToString("HH:mm:ss");
                            CurrentTime = backendTime.ToString("h:mm tt");
                            CurrentDate = backendTime.ToString("MMMM d, yyyy");
                            CurrentDayOfWeek = backendTime.ToString("dddd");
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error processing weather update: {ex.Message}");
            WeatherCondition = "Update Error";
            IsConnected = false;
            StatusMessage = $"Failed to process update: {ex.Message}";
            StatusMessageColor = StatusErrorColor;
            ShowStatusMessage = true;
        }
    }

    private Bitmap? GetWeatherIcon(string iconName)
    {
        var basePath = System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
        var assetsPath = System.IO.Path.Combine(basePath!, "Assets");
        
        var fileName = iconName.ToLower() switch
        {
            "clear-day" => "clear-day.png",
            "clear-night" => "clear-night.png",
            "cloudy" => "cloudy.png",
            "foggy" => "foggy.png",
            "partly-cloudy-day" => "partly-cloudy-day.png",
            "partly-cloudy-night" => "partly-cloudy-night.png",
            "possibly-rainy-day" => "possibly-rain-day.png",
            "possibly-rainy-night" => "possibly-rany-night.png",
            "possibly-sleet-day" => "possibly-sleet-day.png",
            "possibly-sleet-night" => "possibly-sleet-night.png",
            "possibly-snow-day" => "possibly-snow-day.png",
            "possibly-snow-night" => "possibly-snow-night.png",
            "possibly-thunderstorm-day" => "possibly-thunderstorm-day.png",
            "possibly-thunderstorm-night" => "possibly-thunderstorm-night.png",
            "rainy" => "rainy.png",
            "sleet" => "sleet.png",
            "snow" => "snow.png",
            "thunderstorm" => "thunderstorm.png",
            "thunderstorm-rain" => "thunderstorm-rain.png",
            "windy" => "windy.png",
            _ => "cloudy.png"
        };
        
        var fullPath = System.IO.Path.Combine(assetsPath, fileName);
        
        try
        {
            if (System.IO.File.Exists(fullPath))
            {
                Console.WriteLine($"Loading weather icon: {iconName} -> {fullPath}");
                return new Bitmap(fullPath);
            }
            else
            {
                Console.WriteLine($"Weather icon file not found: {fullPath}");
                return null;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error loading weather icon {iconName}: {ex.Message}");
            return null;
        }
    }

    private static string FormatForecastDate(int dayStartLocal)
    {
        if (dayStartLocal <= 0)
        {
            return "--";
        }

        return DateTimeOffset.FromUnixTimeSeconds(dayStartLocal).ToLocalTime().ToString("MM/dd");
    }

    private string GetCardinalDirection(int degrees)
    {
        var directions = new[] { "N", "NNE", "NE", "ENE", "E", "ESE", "SE", "SSE", "S", "SSW", "SW", "WSW", "W", "WNW", "NW", "NNW" };
        var index = (int)Math.Round(degrees / 22.5) % 16;
        return directions[index];
    }

    private static DateTime ConvertUtcToLocal(DateTime utcDateTime)
    {
        return DateTime.SpecifyKind(utcDateTime, DateTimeKind.Utc).ToLocalTime();
    }

    [RelayCommand]
    private async Task RestartBackend()
    {
        if (!Dispatcher.UIThread.CheckAccess())
        {
            await Dispatcher.UIThread.InvokeAsync(async () => await RestartBackendCore(throwOnFailure: false));
            return;
        }

        await RestartBackendCore(throwOnFailure: false);
    }

    /// <summary>
    /// Production installs use systemd units under /opt/tempest. Restart goes through
    /// systemctl so we do not spawn parallel home-directory script instances.
    /// Must run on the Avalonia UI thread (property / hub access).
    /// </summary>
    private async Task RestartBackendCore(bool throwOnFailure)
    {
        try
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                StatusMessage = "Restart is only supported on Linux (Raspberry Pi).";
                StatusMessageColor = StatusErrorColor;
                ShowStatusMessage = true;
                return;
            }

            Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] USER ACTION: Backend/UI restart via systemctl");

            IsConnected = false;
            if (_hubConnection is not null)
            {
                try
                {
                    await _hubConnection.StopAsync();
                }
                catch (Exception hubEx)
                {
                    Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Hub stop during restart: {hubEx.Message}");
                }
            }

            StatusMessage = "Restarting backend service...";
            StatusMessageColor = StatusInfoColor;
            ShowStatusMessage = true;

            if (!await RunSudoSystemctlAsync("restart", "tempest-backend.service"))
            {
                StatusMessage = "Failed to restart backend (sudo/systemctl). Check /etc/sudoers.d/tempest.";
                StatusMessageColor = StatusErrorColor;
                Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] ERROR: systemctl restart tempest-backend.service failed");
                if (throwOnFailure)
                {
                    throw new InvalidOperationException("systemctl restart tempest-backend.service failed.");
                }

                return;
            }

            StatusMessage = "Backend restarting... Checking health...";
            const int maxAttempts = 20;
            var backendReady = false;
            using (var httpClient = new System.Net.Http.HttpClient { Timeout = TimeSpan.FromSeconds(2) })
            {
                for (var attempt = 1; attempt <= maxAttempts; attempt++)
                {
                    try
                    {
                        var response = await httpClient.GetAsync($"{_backendUrl}/health");
                        if (response.IsSuccessStatusCode)
                        {
                            backendReady = true;
                            Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Backend healthy after {attempt} attempt(s)");
                            break;
                        }
                    }
                    catch
                    {
                        // not ready yet
                    }

                    await Task.Delay(1000);
                }
            }

            if (!backendReady)
            {
                StatusMessage = "Backend failed health check after restart.";
                StatusMessageColor = StatusErrorColor;
                IsConnected = false;
                Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] ERROR: Backend did not respond after {maxAttempts}s");
                if (throwOnFailure)
                {
                    throw new InvalidOperationException($"Backend did not respond after {maxAttempts} seconds.");
                }

                return;
            }

            StatusMessage = "Restarting UI service...";
            Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Restarting tempest-ui.service via systemctl");

            // systemctl restart kills this process; no need to Environment.Exit afterward.
            if (!await RunSudoSystemctlAsync("restart", "tempest-ui.service"))
            {
                // Fallback: exit and let Restart=always bring the UI back.
                Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] WARN: systemctl restart UI failed; exiting for systemd Restart=always");
                StatusMessage = "UI restart via systemctl failed; exiting for systemd respawn...";
                await Task.Delay(300);
                Environment.Exit(0);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] ERROR: Failed to restart services - {ex.Message}");
            if (throwOnFailure)
            {
                throw;
            }
        }
    }

    /// <summary>
    /// Runs passwordless sudo systemctl for tempest units. Requires /etc/sudoers.d/tempest.
    /// </summary>
    private static async Task<bool> RunSudoSystemctlAsync(string verb, string unit)
    {
        try
        {
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "sudo",
                    Arguments = $"-n /usr/bin/systemctl {verb} {unit}",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };
            process.Start();
            var stdout = await process.StandardOutput.ReadToEndAsync();
            var stderr = await process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();
            if (process.ExitCode != 0)
            {
                Console.WriteLine(
                    $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] systemctl {verb} {unit} exit={process.ExitCode} stderr={stderr.Trim()} stdout={stdout.Trim()}");
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] systemctl {verb} {unit} exception: {ex.Message}");
            return false;
        }
    }

    [RelayCommand]
    private async Task ExitApp()
    {
        Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] USER ACTION: Exit application requested via UI button");

        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                // systemctl stop keeps units down (unlike pkill, which races Restart=always).
                await RunSudoSystemctlAsync("stop", "tempest-backend.service");
                await RunSudoSystemctlAsync("stop", "tempest-ui.service");
                Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Stopped tempest-backend and tempest-ui via systemctl");
                return;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] ERROR: Failed to stop services - {ex.Message}");
        }

        Environment.Exit(0);
    }

    [RelayCommand]
    private void RebootPi()
    {
        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] USER ACTION: Pi reboot requested via UI button");
                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "sudo",
                        Arguments = "-n /usr/sbin/reboot",
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    }
                };
                process.Start();
                var stderr = process.StandardError.ReadToEnd();
                process.WaitForExit(5000);
                if (process.ExitCode != 0)
                {
                    Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] ERROR: reboot failed exit={process.ExitCode} stderr={stderr.Trim()}");
                }
                else
                {
                    Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Pi reboot command issued - system will restart shortly");
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] ERROR: Failed to reboot Pi - {ex.Message}");
        }
    }

    public async ValueTask DisposeAsync()
    {
        _heartbeatTimer?.Stop();
        _heartbeatTimer?.Dispose();
        _forecastRefreshTimer?.Stop();
        _forecastRefreshTimer?.Dispose();
        _diagnosticsHttpClient.Dispose();
        
        if (_hubConnection != null)
        {
            await _hubConnection.DisposeAsync();
        }
    }
}