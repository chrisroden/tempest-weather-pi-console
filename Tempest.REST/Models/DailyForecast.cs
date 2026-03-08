using System.Text.Json.Serialization;

namespace Tempest.REST.Models;

public class DailyForecast
{
    [JsonPropertyName("air_temp_high")] public double AirTempHigh { get; set; }
    [JsonPropertyName("air_temp_low")] public double AirTempLow { get; set; }
    [JsonPropertyName("conditions")] public string Conditions { get; set; } = string.Empty;
    [JsonPropertyName("day_num")] public int DayNum { get; set; }
    [JsonPropertyName("day_start_local")] public int DayStartLocal { get; set; }
    [JsonPropertyName("icon")] public string Icon { get; set; } = string.Empty;
    [JsonPropertyName("month_num")] public int MonthNum { get; set; }
    [JsonPropertyName("precip_icon")] public string PrecipIcon { get; set; } = string.Empty;
    [JsonPropertyName("precip_probability")] public int PrecipProbability { get; set; }
    [JsonPropertyName("precip_type")] public string PrecipType { get; set; } = string.Empty;
    [JsonPropertyName("sunrise")] public int Sunrise { get; set; }
    [JsonPropertyName("sunset")] public int Sunset { get; set; }
}