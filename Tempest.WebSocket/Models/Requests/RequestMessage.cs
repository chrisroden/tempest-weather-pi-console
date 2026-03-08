using System.Text.Json;
using System.Text.Json.Serialization;

namespace Tempest.WebSocket.Models.Requests;

public class RequestMessage
{
    [JsonPropertyName("type")] public required string MessageType { get; set; }

    [JsonPropertyName("device_id")] public int DeviceId { get; set; }

    [JsonPropertyName("id")] public required string Id { get; set; }
}