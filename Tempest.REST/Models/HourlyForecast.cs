using System.Text.Json.Serialization;

namespace Tempest.REST.Models;

public class HourlyForecast
{
    [JsonPropertyName("air_temperature")] public double AirTemperature { get; set; }
    [JsonPropertyName("conditions")] public string Conditions { get; set; } = string.Empty;
    [JsonPropertyName("feels_like")] public double FeelsLike { get; set; }
    [JsonPropertyName("icon")] public string Icon { get; set; } = string.Empty;
    [JsonPropertyName("local_day")] public int LocalDay { get; set; }
    [JsonPropertyName("local_hour")] public int LocalHour { get; set; }
    [JsonPropertyName("precip")] public double Precip { get; set; }
    [JsonPropertyName("precip_icon")] public string PrecipIcon { get; set; } = string.Empty;
    [JsonPropertyName("precip_probability")] public double PrecipProbability { get; set; }
    [JsonPropertyName("precip_type")] public string PrecipType { get; set; } = string.Empty;
    [JsonPropertyName("relative_humidity")] public int RelativeHumidity { get; set; }
    [JsonPropertyName("sea_level_pressure")] public double SeaLevelPressure { get; set; }
    [JsonPropertyName("time")] public int Time { get; set; }
    [JsonPropertyName("uv")] public double Uv { get; set; }
    [JsonPropertyName("wind_avg")] public double WindAvg { get; set; }
    [JsonPropertyName("wind_direction")] public int WindDirection { get; set; }
    [JsonPropertyName("wind_direction_cardinal")] public string WindDirectionCardinal { get; set; } = string.Empty;
    [JsonPropertyName("wind_gust")] public double WindGust { get; set; }
}