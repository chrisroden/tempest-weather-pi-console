namespace Tempest.WebSocket.Models.Responses;

using System.Text.Json.Serialization;

public abstract class ResponseMessageBase
{
    [JsonPropertyName("type")]
    public string ResponseType => GetType().Name.ToLower();
}