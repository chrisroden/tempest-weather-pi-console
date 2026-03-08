using System.Text.Json.Serialization;

namespace Tempest.REST.Models;

public class ForecastResponse
{
    [JsonPropertyName("current_conditions")] public CurrentConditions CurrentConditions { get; set; } = new();
    [JsonPropertyName("forecast")] public Forecast Forecast { get; set; } = new();
    [JsonPropertyName("latitude")] public double Latitude { get; set; }
    [JsonPropertyName("location_name")] public string LocationName { get; set; } = string.Empty;
    [JsonPropertyName("longitude")] public double Longitude { get; set; }
    [JsonPropertyName("source_id_conditions")] public int SourceIdConditions { get; set; }
    [JsonPropertyName("station")] public Station Station { get; set; } = new();
    [JsonPropertyName("status")] public Status Status { get; set; } = new();
    [JsonPropertyName("timezone")] public string Timezone { get; set; } = string.Empty;
    [JsonPropertyName("timezone_offset_minutes")] public int TimezoneOffsetMinutes { get; set; }
    [JsonPropertyName("units")] public Units Units { get; set; } = new();
}