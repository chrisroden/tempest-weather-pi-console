using System.Text.Json.Serialization;

namespace TempestBlazorApp.Models.Responses;

public class RapidWind : ResponseMessageBase
{
    public RapidWind()
    {
        Observation = new List<double?>();
    }

    [JsonPropertyName("device_id")] public int DeviceId { get; set; }
    [JsonPropertyName("serial_number")] public required string SerialNumber { get; set; }
    [JsonPropertyName("hub_sn")] public required string HubSerialNumber { get; set; }
    
    [JsonPropertyName("ob")] public List<double?> Observation { private get; set; }
    
    public DateTime TimeEpochInSeconds => DateTimeOffset.FromUnixTimeSeconds((int)Observation[0]!).DateTime;
    public double WindSpeedInMetersPerSecond => Observation[1] ?? 0;
    public int WindDirectionInDegrees => (int)Observation[2]!;
}