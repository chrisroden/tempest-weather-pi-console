using System.Text.Json;
using System.Text.Json.Serialization;

namespace Tempest.WebSocket.Models.Responses;

public interface IResponseMessage
{
    string ResponseType { get; set; }
}