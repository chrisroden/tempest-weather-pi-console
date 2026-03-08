using System.Text.Json;
using System.Text.Json.Serialization;

namespace Tempest.WebSocket.Models.Responses;

public class AcknowledgementResponse : ResponseMessageBase
{
    [JsonPropertyName("id")] public required string Id { get; set; }
}