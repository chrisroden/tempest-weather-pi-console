using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Tempest.WebSocket;
using Tempest.WebSocket.Hubs;

AppDomain.CurrentDomain.UnhandledException += (sender, e) =>
{
    Console.WriteLine($"Unhandled Exception: {e.ExceptionObject}");
};

TaskScheduler.UnobservedTaskException += (sender, e) =>
{
    Console.WriteLine($"Unobserved Task Exception: {e.Exception}");
    e.SetObserved();
};

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor();
builder.Services.AddSignalR(options =>
{
    options.KeepAliveInterval = TimeSpan.FromSeconds(15);
    options.ClientTimeoutInterval = TimeSpan.FromSeconds(30);
}).AddJsonProtocol(options =>
{
    options.PayloadSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
    options.PayloadSerializerOptions.PropertyNameCaseInsensitive = true;
    options.PayloadSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
});
builder.Services.AddSingleton<TempestWebSocketService>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
}

app.UseStaticFiles();
app.UseRouting();

var defaultStaleThresholdSeconds = (int)RuntimeDefaults.HealthStaleThreshold.TotalSeconds;
var configuredStaleThresholdRaw = builder.Configuration["WeatherFlow:Health:StaleThresholdSeconds"];
var configuredStaleThresholdSeconds = int.TryParse(configuredStaleThresholdRaw, out var parsedStaleThresholdSeconds)
    ? parsedStaleThresholdSeconds
    : defaultStaleThresholdSeconds;
var upstreamStaleThreshold = TimeSpan.FromSeconds(Math.Max(1, configuredStaleThresholdSeconds));

var getHealthState = (TempestWebSocketService webSocketService) =>
{
    var reasonCodes = new List<string>();

    if (!webSocketService.IsUpstreamConnected)
    {
        reasonCodes.Add("upstream_disconnected");
    }

    if (!webSocketService.LastUpstreamMessageUtc.HasValue)
    {
        reasonCodes.Add("no_upstream_messages");
    }
    else if ((DateTimeOffset.UtcNow - webSocketService.LastUpstreamMessageUtc.Value) > upstreamStaleThreshold)
    {
        reasonCodes.Add("stale_upstream");
    }

    var isHealthy = reasonCodes.Count == 0;
    return new HealthState(
        IsHealthy: isHealthy,
        Status: isHealthy ? "ok" : "degraded",
        ReasonCodes: reasonCodes,
        Error: isHealthy ? null : new HealthErrorDto("degraded", reasonCodes));
};

app.MapBlazorHub();
app.MapHub<WeatherHub>("/weatherHub");
app.MapGet("/health", (TempestWebSocketService webSocketService) =>
{
    var health = getHealthState(webSocketService);
    return Results.Ok(new HealthStatusDto(
        Status: health.Status,
        Service: "TempestBlazorApp",
        UpstreamConnected: webSocketService.IsUpstreamConnected,
        LastUpstreamMessageUtc: webSocketService.LastUpstreamMessageUtc,
        ReasonCodes: health.ReasonCodes,
        Error: health.Error,
        TimestampUtc: DateTimeOffset.UtcNow));
});

app.MapGet("/health/details", (TempestWebSocketService webSocketService) =>
{
    var health = getHealthState(webSocketService);
    return Results.Ok(new HealthDetailsDto(
        Status: health.Status,
        Service: "TempestBlazorApp",
        ServiceStartedUtc: webSocketService.ServiceStartedUtc,
        UptimeSeconds: (DateTimeOffset.UtcNow - webSocketService.ServiceStartedUtc).TotalSeconds,
        UpstreamConnected: webSocketService.IsUpstreamConnected,
        LastUpstreamMessageUtc: webSocketService.LastUpstreamMessageUtc,
        LastSuccessfulBroadcastUtc: webSocketService.LastSuccessfulBroadcastUtc,
        ReconnectAttemptCount: webSocketService.ReconnectAttemptCount,
        TotalReconnects: webSocketService.TotalReconnects,
        SuccessfulConnectionCount: webSocketService.SuccessfulConnectionCount,
        ReasonCodes: health.ReasonCodes,
        Error: health.Error,
        TimestampUtc: DateTimeOffset.UtcNow));
});

// Note: Rain simulation test endpoints removed for production deployment
// To enable for testing, uncomment the endpoints below:
/*
app.MapGet("/api/test/rain/start", (TempestWebSocketService wsService) =>
{
    wsService.StartRainSimulation();
    return Results.Ok(new { message = "Rain simulation started" });
});

app.MapGet("/api/test/rain/stop", (TempestWebSocketService wsService) =>
{
    wsService.StopRainSimulation();
    return Results.Ok(new { message = "Rain simulation stopped" });
});
*/
app.MapFallbackToPage("/_Host");

var webSocketService = app.Services.GetRequiredService<TempestWebSocketService>();

await app.StartAsync();

using var pollingCancellationTokenSource = new CancellationTokenSource();
app.Lifetime.ApplicationStopping.Register(() => pollingCancellationTokenSource.Cancel());

var pollingTask = Task.Run(async () =>
{
    try
    {
        await webSocketService.StartPolling(pollingCancellationTokenSource.Token);
    }
    catch (OperationCanceledException)
    {
        Console.WriteLine($"WebSocket polling cancelled at {DateTime.Now:HH:mm:ss}");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"WebSocket Startup Error at {DateTime.Now.ToString("HH:mm:ss")}: {ex.Message}\n{ex.StackTrace}");
    }
});

await app.WaitForShutdownAsync();
pollingCancellationTokenSource.Cancel();
await webSocketService.StopAsync();

try
{
    await pollingTask;
}
catch (OperationCanceledException)
{
}

public sealed record HealthErrorDto(string Code, IReadOnlyList<string> Reasons);

public sealed record HealthStatusDto(
    string Status,
    string Service,
    bool UpstreamConnected,
    DateTimeOffset? LastUpstreamMessageUtc,
    IReadOnlyList<string> ReasonCodes,
    HealthErrorDto? Error,
    DateTimeOffset TimestampUtc);

public sealed record HealthDetailsDto(
    string Status,
    string Service,
    DateTimeOffset ServiceStartedUtc,
    double UptimeSeconds,
    bool UpstreamConnected,
    DateTimeOffset? LastUpstreamMessageUtc,
    DateTimeOffset? LastSuccessfulBroadcastUtc,
    int ReconnectAttemptCount,
    long TotalReconnects,
    long SuccessfulConnectionCount,
    IReadOnlyList<string> ReasonCodes,
    HealthErrorDto? Error,
    DateTimeOffset TimestampUtc);

public sealed record HealthState(
    bool IsHealthy,
    string Status,
    IReadOnlyList<string> ReasonCodes,
    HealthErrorDto? Error);

public partial class Program;