using System.Text.Json.Serialization;

namespace Tempest.REST.Models;

public class Station
{
    [JsonPropertyName("agl")] public double Agl { get; set; }
    [JsonPropertyName("elevation")] public double Elevation { get; set; }
    [JsonPropertyName("is_station_online")] public bool IsStationOnline { get; set; }
    [JsonPropertyName("state")] public double State { get; set; }
    [JsonPropertyName("station_id")] public int StationId { get; set; }
}