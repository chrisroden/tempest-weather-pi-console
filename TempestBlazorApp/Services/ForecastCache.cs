using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Tempest.REST;
using Tempest.REST.Models;

namespace TempestBlazorApp.Services;

/// <summary>
/// Caches the latest WeatherFlow forecast and refreshes it on a fixed schedule.
/// </summary>
public sealed class ForecastCache : BackgroundService
{
    private readonly TempestRESTService _restService;
    private readonly ILogger<ForecastCache> _logger;
    private readonly TimeSpan _refreshInterval;
    private readonly object _gate = new();
    private ForecastResponse? _latestForecast;
    private DateTimeOffset? _lastUpdatedUtc;

    /// <summary>
    /// Initializes a new instance of the <see cref="ForecastCache"/> class.
    /// </summary>
    /// <param name="restService">REST client used to fetch forecast data.</param>
    /// <param name="configuration">Configuration for refresh interval settings.</param>
    /// <param name="logger">Logger for refresh diagnostics.</param>
    public ForecastCache(TempestRESTService restService, IConfiguration configuration, ILogger<ForecastCache> logger)
    {
        _restService = restService;
        _logger = logger;
        var refreshMinutes = Math.Max(15, configuration.GetValue<int?>("ForecastRefreshMinutes") ?? 360);
        _refreshInterval = TimeSpan.FromMinutes(refreshMinutes);
    }

    /// <summary>
    /// Gets a snapshot of the latest cached forecast and update timestamp.
    /// </summary>
    /// <returns>A snapshot of the cached forecast data.</returns>
    public ForecastSnapshot GetSnapshot()
    {
        lock (_gate)
        {
            return new ForecastSnapshot(_latestForecast, _lastUpdatedUtc);
        }
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await RefreshForecastAsync(stoppingToken);
        using var timer = new PeriodicTimer(_refreshInterval);

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await RefreshForecastAsync(stoppingToken);
        }
    }

    private async Task RefreshForecastAsync(CancellationToken stoppingToken)
    {
        try
        {
            var forecast = await _restService.GetForecast();
            lock (_gate)
            {
                _latestForecast = forecast;
                _lastUpdatedUtc = DateTimeOffset.UtcNow;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Forecast refresh failed.");
        }
    }
}

/// <summary>
/// Represents a read-only view of the cached forecast data.
/// </summary>
/// <param name="Forecast">The cached forecast, if available.</param>
/// <param name="LastUpdatedUtc">The timestamp of the last successful refresh.</param>
public sealed record ForecastSnapshot(ForecastResponse? Forecast, DateTimeOffset? LastUpdatedUtc)
{
    /// <summary>
    /// Gets an empty snapshot when no data has been cached yet.
    /// </summary>
    public static ForecastSnapshot Empty { get; } = new(null, null);
}
