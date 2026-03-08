using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Tempest.WebSocket.Models.Responses;
using Microsoft.AspNetCore.SignalR;
using Tempest.WebSocket.Hubs;
using Microsoft.Extensions.Configuration;

namespace Tempest.WebSocket;

public class TempestWebSocketService
{
    private static readonly JsonSerializerOptions ParseJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        AllowTrailingCommas = true,
        NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.AllowReadingFromString
    };

    private static readonly JsonSerializerOptions BroadcastJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    private readonly IHubContext<WeatherHub> _hubContext;
    private readonly string _apiKey;
    private readonly int _deviceId;
    private readonly string _stationId;
    private readonly bool _enableVerboseParsingLogs;
    private readonly int _maxMessageBytes;
    private readonly object _webSocketGate = new();
    private CancellationTokenSource? _heartbeatCancellationTokenSource;
    private Task? _heartbeatTask;
    private ClientWebSocket? _activeWebSocket;

    public DateTimeOffset ServiceStartedUtc { get; } = DateTimeOffset.UtcNow;
    public bool IsUpstreamConnected { get; private set; }
    public DateTimeOffset? LastUpstreamMessageUtc { get; private set; }
    public DateTimeOffset? LastSuccessfulBroadcastUtc { get; private set; }
    public int ReconnectAttemptCount { get; private set; }
    public long TotalReconnects { get; private set; }
    public long SuccessfulConnectionCount { get; private set; }

    private void UpdateUpstreamConnectionState(bool connected, string trigger)
    {
        if (connected == IsUpstreamConnected)
        {
            return;
        }

        IsUpstreamConnected = connected;

        if (connected)
        {
            Console.WriteLine($"[WS-HEALTH {DateTime.Now:HH:mm:ss}] RECOVERED via {trigger}");
        }
        else
        {
            Console.WriteLine($"[WS-HEALTH {DateTime.Now:HH:mm:ss}] DEGRADED via {trigger}");
        }
    }

    public TempestWebSocketService(IHubContext<WeatherHub> hubContext, IConfiguration configuration)
    {
        _hubContext = hubContext;
        _apiKey = configuration.GetValue<string>("WeatherFlow:ApiToken") ?? throw new InvalidOperationException("WeatherFlow:ApiToken not configured");
        _deviceId = configuration.GetValue<int>("WeatherFlow:DeviceId");
        _stationId = configuration.GetValue<string>("WeatherFlow:StationId") ?? throw new InvalidOperationException("WeatherFlow:StationId not configured");
        var isDevelopment = string.Equals(Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT"), "Development", StringComparison.OrdinalIgnoreCase);
        _enableVerboseParsingLogs = configuration.GetValue<bool?>("WeatherFlow:WebSocket:EnableVerboseLogging") ?? isDevelopment;
        var configuredMaxMessageBytes = configuration.GetValue<int?>("WeatherFlow:WebSocket:MaxMessageBytes") ?? RuntimeDefaults.DefaultMaxMessageBytes;
        _maxMessageBytes = Math.Max(4 * 1024, configuredMaxMessageBytes);
        StartHeartbeat();
    }

    private void StartHeartbeat()
    {
        _heartbeatCancellationTokenSource = new CancellationTokenSource();
        _heartbeatTask = Task.Run(async () =>
        {
            using var timer = new PeriodicTimer(TimeSpan.FromSeconds(3));

            while (await timer.WaitForNextTickAsync(_heartbeatCancellationTokenSource.Token))
            {
                try
                {
                    var heartbeat = new { type = "heartbeat", timestamp = DateTime.Now.ToString("HH:mm:ss"), epoch = DateTimeOffset.Now.ToUnixTimeSeconds() };
                    var serialized = JsonSerializer.Serialize(heartbeat);
                    await _hubContext.Clients.All.SendAsync("ReceiveHeartbeat", serialized, _heartbeatCancellationTokenSource.Token);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Heartbeat error: {ex.Message}");
                }
            }
        });

        Console.WriteLine("[Heartbeat] Started - sending updates every 3 seconds");
    }

    public event EventHandler<ResponseMessageEvenArgs>? ResponseMessageReceived;

    public async Task StartPolling(CancellationToken cancellationToken)
    {
        var reconnectAttempt = 0;

        while (!cancellationToken.IsCancellationRequested)
        {
            Console.WriteLine($"[WS-CONN {DateTime.Now:HH:mm:ss}] Attempting upstream connection...");
            using var ws = new ClientWebSocket();
            try
            {
                await ws.ConnectAsync(new Uri($"wss://ws.weatherflow.com/swd/data?api_key={_apiKey}"), cancellationToken);
                lock (_webSocketGate)
                {
                    _activeWebSocket = ws;
                }
                UpdateUpstreamConnectionState(true, "connect");
                reconnectAttempt = 0;
                ReconnectAttemptCount = 0;
                SuccessfulConnectionCount++;
                Console.WriteLine($"[WS-CONN {DateTime.Now:HH:mm:ss}] Connected to WeatherFlow");

                await SendMessage(ws, $"{{\"type\":\"listen_start\", \"device_id\":{_deviceId}, \"id\":\"{_stationId}\"}}", cancellationToken);
                Console.WriteLine($"[WS-SUB {DateTime.Now:HH:mm:ss}] Subscribed to device observations");

                await SendMessage(ws, $"{{\"type\":\"listen_rapid_start\", \"device_id\":{_deviceId}, \"id\":\"{_stationId}\"}}", cancellationToken);
                Console.WriteLine($"[WS-SUB {DateTime.Now:HH:mm:ss}] Subscribed to rapid wind updates");

                Console.WriteLine($"[WS-RECV {DateTime.Now:HH:mm:ss}] Listening for upstream data");
                while (ws.State == WebSocketState.Open && !cancellationToken.IsCancellationRequested)
                {
                    using var messageBuffer = new MemoryStream();
                    WebSocketReceiveResult result;
                    var exceededMaxMessageSize = false;

                    do
                    {
                        var buffer = new byte[1024 * 4];
                        result = await ws.ReceiveAsync(new ArraySegment<byte>(buffer), cancellationToken);

                        if (result.MessageType == WebSocketMessageType.Close)
                        {
                            Console.WriteLine($"[WS-CONN {DateTime.Now:HH:mm:ss}] Received close frame from WeatherFlow");
                            break;
                        }

                        messageBuffer.Write(buffer, 0, result.Count);

                        if (messageBuffer.Length > _maxMessageBytes)
                        {
                            exceededMaxMessageSize = true;
                        }
                    } while (!result.EndOfMessage);

                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        break;
                    }

                    if (exceededMaxMessageSize)
                    {
                        Console.WriteLine($"[WS-ERR {DateTime.Now:HH:mm:ss}] Dropping oversized upstream message ({messageBuffer.Length} bytes)");
                        continue;
                    }

                    if (result.MessageType == WebSocketMessageType.Text)
                    {
                        var message = Encoding.UTF8.GetString(messageBuffer.ToArray());
                        LastUpstreamMessageUtc = DateTimeOffset.UtcNow;
                        var responseMessage = await ParseResponseMessage(message); // Await the task
                        if (responseMessage != null)
                        {
                            await OnResponseMessageReceived(new ResponseMessageEvenArgs(responseMessage));
                        }
                    }
                }
                
                Console.WriteLine($"[WS-CONN {DateTime.Now:HH:mm:ss}] Upstream socket closed. State: {ws.State}");
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine($"[WS-CONN {DateTime.Now:HH:mm:ss}] Polling cancellation requested");
                break;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[WS-ERR {DateTime.Now:HH:mm:ss}] WebSocket error: {ex.Message}");
                Console.WriteLine($"StackTrace: {ex.StackTrace}");
            }
            finally
            {
                UpdateUpstreamConnectionState(false, "disconnect");
                lock (_webSocketGate)
                {
                    if (ReferenceEquals(_activeWebSocket, ws))
                    {
                        _activeWebSocket = null;
                    }
                }
            }
            
            // Wait before reconnecting
            if (cancellationToken.IsCancellationRequested)
            {
                break;
            }

            var reconnectDelay = RuntimeDefaults.ReconnectDelays[Math.Min(reconnectAttempt, RuntimeDefaults.ReconnectDelays.Length - 1)];
            reconnectAttempt++;
            ReconnectAttemptCount = reconnectAttempt;
            TotalReconnects++;
            Console.WriteLine($"[WS-RETRY {DateTime.Now:HH:mm:ss}] Reconnecting in {reconnectDelay.TotalSeconds:F0}s (attempt {reconnectAttempt})");

            try
            {
                await Task.Delay(reconnectDelay, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    public Task StopAsync()
    {
        UpdateUpstreamConnectionState(false, "stop");
        ReconnectAttemptCount = 0;

        if (_heartbeatCancellationTokenSource != null)
        {
            _heartbeatCancellationTokenSource.Cancel();
            _heartbeatCancellationTokenSource.Dispose();
            _heartbeatCancellationTokenSource = null;
        }

        if (_heartbeatTask != null)
        {
            try
            {
                _heartbeatTask.GetAwaiter().GetResult();
            }
            catch (OperationCanceledException)
            {
            }
            finally
            {
                _heartbeatTask = null;
            }
            Console.WriteLine("[Heartbeat] Stopped");
        }

        ClientWebSocket? webSocketToClose;
        lock (_webSocketGate)
        {
            webSocketToClose = _activeWebSocket;
            _activeWebSocket = null;
        }

        if (webSocketToClose != null &&
            (webSocketToClose.State == WebSocketState.Open || webSocketToClose.State == WebSocketState.CloseReceived))
        {
            try
            {
                webSocketToClose.CloseAsync(WebSocketCloseStatus.NormalClosure, "Service stopping", CancellationToken.None)
                    .GetAwaiter().GetResult();
            }
            catch
            {
            }
        }

        return Task.CompletedTask;
    }

    private async Task SendMessage(ClientWebSocket ws, string message, CancellationToken cancellationToken)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(message);
        await ws.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, cancellationToken);
    }

    private async Task<ResponseMessageBase?> ParseResponseMessage(string jsonMessage)
    {
        try
        {
            using var doc = JsonDocument.Parse(jsonMessage);
            var root = doc.RootElement;
            if (!root.TryGetProperty("type", out var typeElement) || typeElement.ValueKind != JsonValueKind.String)
            {
                Console.WriteLine($"No 'type' property found or invalid type in JSON: {jsonMessage}");
                return null;
            }

            var type = typeElement.GetString();
            if (string.IsNullOrEmpty(type))
            {
                Console.WriteLine($"Type is null or empty in JSON: {jsonMessage}");
                return null;
            }

            if (_enableVerboseParsingLogs)
            {
                Console.WriteLine($"Parsing message of type: {type}");
            }
            switch (type)
            {
                case "rapid_wind":
                {
                    var rapidWind = JsonSerializer.Deserialize<RapidWind>(jsonMessage, ParseJsonOptions);
                    if (rapidWind != null)
                    {
                        if (_enableVerboseParsingLogs)
                        {
                            Console.WriteLine($"Deserialized RapidWind raw: DeviceId={rapidWind.DeviceId}, Speed={rapidWind.WindSpeedInMetersPerSecond}, Direction={rapidWind.WindDirectionInDegrees}");
                        }
                    }
                    else
                    {
                        Console.WriteLine($"Failed to deserialize RapidWind: {jsonMessage}");
                    }
                    return rapidWind;
                }
                case "connection_opened":
                case "ack":
                {
                    return null;
                }
                case "obs_st":
                {
                    var obsSt = JsonSerializer.Deserialize<StationObservation>(jsonMessage, ParseJsonOptions);
                    if (obsSt != null)
                    {
                        if (_enableVerboseParsingLogs)
                        {
                            Console.WriteLine($"Deserialized StationObservation: DeviceId={obsSt.DeviceId}, PrecipType={obsSt.PrecipitationType}, PrecipRate={obsSt.PrecipitationRate}mm/hr, 1hrTotal={obsSt.Summary?.PrecipTotal1h}mm");
                        }
                    }
                    return obsSt;
                }
                case "obs_sky":
                {
                    var obsSky = JsonSerializer.Deserialize<Sky>(jsonMessage, ParseJsonOptions);
                    if (obsSky != null)
                    {
                        if (_enableVerboseParsingLogs)
                        {
                            Console.WriteLine($"Deserialized Sky Observation: DeviceId={obsSky.DeviceId}, PrecipType={obsSky.PrecipitationType}, RainAccum={obsSky.RainAccumulatedinMm}mm");
                        }
                    }
                    return obsSky;
                }
                default:
                {
                    Console.WriteLine($"Unsupported message type: {type}");
                    return null;
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Parse Error at {DateTime.Now.ToString("HH:mm:ss")}: {ex.Message}\n{ex.StackTrace}");
            return null;
        }
    }

    protected virtual async Task OnResponseMessageReceived(ResponseMessageEvenArgs e)
    {
        try
        {
            if (ResponseMessageReceived != null)
            {
                ResponseMessageReceived.Invoke(this, e);
            }

            if (_enableVerboseParsingLogs)
            {
                Console.WriteLine(
                    $"Broadcasting at {DateTime.Now.ToString("HH:mm:ss")}: {e.ResponseMessage?.ResponseType}");
            }
            if (e.ResponseMessage is RapidWind rapidWind)
            {
                // Explicitly serialize as RapidWind to include derived properties
                var serializedMessage = JsonSerializer.Serialize(rapidWind, BroadcastJsonOptions);
                await _hubContext.Clients.All.SendAsync("ReceiveWeatherUpdate", rapidWind);
                await _hubContext.Clients.All.SendAsync("ReceiveWeatherUpdateRaw", serializedMessage);
            }
            else if (e.ResponseMessage is StationObservation obsSt)
            {
                var serializedMessage = JsonSerializer.Serialize(obsSt, BroadcastJsonOptions);
                await _hubContext.Clients.All.SendAsync("ReceiveWeatherUpdate", obsSt);
                await _hubContext.Clients.All.SendAsync("ReceiveWeatherUpdateRaw", serializedMessage);
            }
            else if (e.ResponseMessage is Sky obsSky)
            {
                var serializedMessage = JsonSerializer.Serialize(obsSky, BroadcastJsonOptions);
                await _hubContext.Clients.All.SendAsync("ReceiveWeatherUpdate", obsSky);
                await _hubContext.Clients.All.SendAsync("ReceiveWeatherUpdateRaw", serializedMessage);
            }
            else
            {
                var serializedMessage = JsonSerializer.Serialize(e.ResponseMessage, BroadcastJsonOptions);
                await _hubContext.Clients.All.SendAsync("ReceiveWeatherUpdate", e.ResponseMessage);
                await _hubContext.Clients.All.SendAsync("ReceiveWeatherUpdateRaw", serializedMessage);
            }

            LastSuccessfulBroadcastUtc = DateTimeOffset.UtcNow;

            if (_enableVerboseParsingLogs)
            {
                Console.WriteLine($"Broadcasted at {DateTime.Now.ToString("HH:mm:ss")}: {e.ResponseMessage?.ResponseType}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine(
                $"SignalR Broadcast Error at {DateTime.Now.ToString("HH:mm:ss")}: {ex.Message}\n{ex.StackTrace}");
        }
    }
}