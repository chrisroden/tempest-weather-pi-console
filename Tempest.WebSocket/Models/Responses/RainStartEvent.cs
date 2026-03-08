using System.Text.Json;
using System.Text.Json.Serialization;

namespace Tempest.WebSocket.Models.Responses;

public class RainStartEvent : ResponseMessageBase
{
    [JsonPropertyName("device_id")] public int DeviceId { get; set; }
}