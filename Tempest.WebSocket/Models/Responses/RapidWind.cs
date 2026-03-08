namespace Tempest.WebSocket.Models.Responses;

using System.Text.Json.Serialization;

public class RapidWind : ResponseMessageBase
{
    [JsonPropertyName("device_id")]
    public int DeviceId { get; set; }

    [JsonPropertyName("serial_number")]
    public string? SerialNumber { get; set; }

    [JsonPropertyName("hub_sn")]
    public string? HubSerialNumber { get; set; }

    [JsonPropertyName("ob")]
    public List<double?> Observation { get; set; } = new List<double?>(); // Initialize to avoid null

    [JsonIgnore]
    public DateTime? TimeEpochInSeconds => Observation.Count > 0 && Observation[0].HasValue 
        ? DateTimeOffset.FromUnixTimeSeconds((long)Observation[0]!.Value).UtcDateTime 
        : null;

    [JsonIgnore]
    public double WindSpeedInMetersPerSecond => Observation.Count > 1 && Observation[1].HasValue ? Observation[1]!.Value : 0;

    [JsonIgnore]
    public int WindDirectionInDegrees => Observation.Count > 2 && Observation[2].HasValue ? (int)Observation[2]!.Value : 0;
}