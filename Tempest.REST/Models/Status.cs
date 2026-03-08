using System.Text.Json.Serialization;

namespace Tempest.REST.Models;

public class Status
{
    [JsonPropertyName("status_code")] public int StatusCode { get; set; }
    [JsonPropertyName("status_message")] public string StatusMessage { get; set; } = string.Empty;
}