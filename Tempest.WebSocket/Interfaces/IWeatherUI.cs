using Tempest.WebSocket.Models.Responses;

namespace Tempest.WebSocket.Interfaces;

public interface IWeatherUI
{
    
    List<IResponseMessage> ResponseMessages { get; set; }
}