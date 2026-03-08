using System.Text.Json.Serialization;

namespace TempestBlazorApp.Models.Responses;

public class ResponseMessageBase : IResponseMessage
{
    [JsonPropertyName("type")] public required string ResponseType { get; set; }
}