using System.Text.Json.Serialization;

namespace Tempest.REST.Models;

public class Forecast
{
    public Forecast()
    {
        DailyForecasts = new List<DailyForecast>();
        HourlyForecasts = new List<HourlyForecast>();
    }
    
    [JsonPropertyName("daily")] public List<DailyForecast> DailyForecasts { get; set; }
    [JsonPropertyName("hourly")] public List<HourlyForecast> HourlyForecasts { get; set; }
}