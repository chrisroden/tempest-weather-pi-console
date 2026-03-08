// Tempest.WebSocket/Hubs/WeatherHub.cs
using Microsoft.AspNetCore.SignalR;

namespace Tempest.WebSocket.Hubs;

public class WeatherHub : Hub
{
    public async Task SendTestMessage(string message)
    {
        await Clients.All.SendAsync("ReceiveWeatherUpdate", message);
        Console.WriteLine($"Sent test from hub: {message}");
    }
}