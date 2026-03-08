using System.Text.Json.Serialization;

namespace TempestBlazorApp.Models.Responses;

public interface IResponseMessage
{
    [JsonPropertyName("type")] string ResponseType { get; set; }
}