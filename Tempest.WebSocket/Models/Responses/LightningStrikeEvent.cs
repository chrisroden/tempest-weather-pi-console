using System.Text.Json;
using System.Text.Json.Serialization;

namespace Tempest.WebSocket.Models.Responses;

public class LightningStrikeEvent : ResponseMessageBase
{
    public LightningStrikeEvent()
    {
        Event = new List<int>();
    }
    
    [JsonPropertyName("device_id")] public int DeviceId { get; set; }
    [JsonPropertyName("evt")] public required List<int> Event { private get; set; }

    public DateTime TimeEpochInSeconds => DateTimeOffset.FromUnixTimeSeconds(Event[0]).UtcDateTime;
    public int DistanceInKilometers => Event[1];
    public int Energy => Event[2];
}