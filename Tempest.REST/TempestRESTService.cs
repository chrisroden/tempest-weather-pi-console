using System.Text.Json;
using System.Text.Json.Serialization;
using Tempest.REST.Models;
using Microsoft.Extensions.Configuration;

namespace Tempest.REST;

public class TempestRESTService
{
    private string _baseAddress = "https://swd.weatherflow.com";
    private int _stationId;
    private string _tempUnits = "f";
    private string _windUnits = "mph";
    private string _pressureUnits = "mb";
    private string _precipitationUnits = "in";
    private string _distanceUnits = "mi";
    private string _apiKey;

    private readonly HttpClientUtil _httpClientUtil = HttpClientUtil.Instance;
    private static readonly JsonSerializerOptions ForecastJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        AllowTrailingCommas = true,
        NumberHandling = JsonNumberHandling.AllowReadingFromString
    };

    public TempestRESTService(IConfiguration configuration)
    {
        _stationId = configuration.GetValue<int>("WeatherFlow:StationId");
        _apiKey = configuration.GetValue<string>("WeatherFlow:ApiToken") ?? throw new InvalidOperationException("WeatherFlow:ApiToken not configured");
    }

    public async Task<ForecastResponse> GetForecast()
    {
        var responseMessage = await _httpClientUtil.HttpClient.GetAsync($"{_baseAddress}/swd/rest/better_forecast?station_id={_stationId}&units_temp={_tempUnits}&units_wind={_windUnits}&units_pressure={_pressureUnits}&units_precip={_precipitationUnits}&units_distance={_distanceUnits}&token={_apiKey}");

        if (!responseMessage.IsSuccessStatusCode)
        {
            var errorBody = await responseMessage.Content.ReadAsStringAsync();
            throw new Exception($"Forecast request failed ({(int)responseMessage.StatusCode} {responseMessage.StatusCode}): {errorBody}");
        }

        var jsonString = await responseMessage.Content.ReadAsStringAsync();
        var forecast = JsonSerializer.Deserialize<ForecastResponse>(jsonString, ForecastJsonOptions);
        if (forecast is null)
        {
            throw new JsonException("Forecast payload deserialized to null.");
        }

        return forecast;
    }

}