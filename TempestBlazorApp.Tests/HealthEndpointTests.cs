using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Tempest.WebSocket;
using Xunit;

namespace TempestBlazorApp.Tests;

public sealed class HealthEndpointTests
{
    private static WebApplicationFactory<Program> CreateFactory(Dictionary<string, string?>? configOverrides = null)
    {
        return new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");
            if (configOverrides is { Count: > 0 })
            {
                builder.ConfigureAppConfiguration((_, configBuilder) => configBuilder.AddInMemoryCollection(configOverrides));
            }
        });
    }

    [Fact]
    public async Task Health_ReturnsDegraded_WhenNoUpstreamMessages()
    {
        using var factory = CreateFactory();
        SetServiceState(factory.Services, isConnected: false, lastUpstreamMessageUtc: null, lastSuccessfulBroadcastUtc: null);
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var payload = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(payload);
        var root = document.RootElement;

        Assert.Equal("degraded", root.GetProperty("status").GetString());
        Assert.True(root.TryGetProperty("service", out var service));
        Assert.Equal("TempestBlazorApp", service.GetString());
        Assert.False(root.GetProperty("upstreamConnected").GetBoolean());

        var reasonCodes = ReadReasonCodes(root);
        Assert.Contains("upstream_disconnected", reasonCodes);
        Assert.Contains("no_upstream_messages", reasonCodes);

        Assert.Equal("degraded", root.GetProperty("error").GetProperty("code").GetString());
        Assert.True(root.TryGetProperty("timestampUtc", out _));
    }

    [Fact]
    public async Task Health_ReturnsStaleUpstream_WhenThresholdExceeded()
    {
        using var factory = CreateFactory(new Dictionary<string, string?>
        {
            ["WeatherFlow:Health:StaleThresholdSeconds"] = "1"
        });
        SetServiceState(
            factory.Services,
            isConnected: true,
            lastUpstreamMessageUtc: DateTimeOffset.UtcNow.AddSeconds(-10),
            lastSuccessfulBroadcastUtc: null);
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var payload = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(payload);
        var root = document.RootElement;
        var reasonCodes = ReadReasonCodes(root);

        Assert.Equal("degraded", root.GetProperty("status").GetString());
        Assert.Contains("stale_upstream", reasonCodes);
        Assert.DoesNotContain("no_upstream_messages", reasonCodes);
    }

    [Fact]
    public async Task Health_ZeroThreshold_ClampsToMinimumAndCanMarkStale()
    {
        using var factory = CreateFactory(new Dictionary<string, string?>
        {
            ["WeatherFlow:Health:StaleThresholdSeconds"] = "0"
        });
        SetServiceState(
            factory.Services,
            isConnected: true,
            lastUpstreamMessageUtc: DateTimeOffset.UtcNow.AddSeconds(-2),
            lastSuccessfulBroadcastUtc: null);
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/health");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var payload = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(payload);
        var root = document.RootElement;
        var reasonCodes = ReadReasonCodes(root);

        Assert.Equal("degraded", root.GetProperty("status").GetString());
        Assert.Contains("stale_upstream", reasonCodes);
    }

    [Fact]
    public async Task Health_InvalidThreshold_FallsBackToDefaultAndRemainsOkWhenFreshEnough()
    {
        using var factory = CreateFactory(new Dictionary<string, string?>
        {
            ["WeatherFlow:Health:StaleThresholdSeconds"] = "invalid"
        });
        SetServiceState(
            factory.Services,
            isConnected: true,
            lastUpstreamMessageUtc: DateTimeOffset.UtcNow.AddSeconds(-5),
            lastSuccessfulBroadcastUtc: null);
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/health");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var payload = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(payload);
        var root = document.RootElement;
        var reasonCodes = ReadReasonCodes(root);

        Assert.Equal("ok", root.GetProperty("status").GetString());
        Assert.Empty(reasonCodes);
        Assert.Equal(JsonValueKind.Null, root.GetProperty("error").ValueKind);
    }

    [Fact]
    public async Task HealthDetails_ReturnsOkAndBroadcastTimestamp_WhenUpstreamFresh()
    {
        using var factory = CreateFactory(new Dictionary<string, string?>
        {
            ["WeatherFlow:Health:StaleThresholdSeconds"] = "30"
        });
        var now = DateTimeOffset.UtcNow;
        SetServiceState(
            factory.Services,
            isConnected: true,
            lastUpstreamMessageUtc: now,
            lastSuccessfulBroadcastUtc: now);
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/health/details");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var payload = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(payload);
        var root = document.RootElement;

        Assert.Equal("ok", root.GetProperty("status").GetString());
        Assert.True(root.TryGetProperty("serviceStartedUtc", out _));
        Assert.True(root.TryGetProperty("uptimeSeconds", out _));
        Assert.True(root.TryGetProperty("reconnectAttemptCount", out _));
        Assert.True(root.TryGetProperty("totalReconnects", out _));
        Assert.True(root.TryGetProperty("successfulConnectionCount", out _));
        Assert.True(root.TryGetProperty("lastSuccessfulBroadcastUtc", out var lastBroadcastUtc));
        Assert.Equal(JsonValueKind.String, lastBroadcastUtc.ValueKind);
        Assert.False(string.IsNullOrWhiteSpace(lastBroadcastUtc.GetString()));

        var reasonCodes = ReadReasonCodes(root);
        Assert.Empty(reasonCodes);
    }

    [Fact]
    public async Task HealthDetails_Degraded_HasErrorReasonsMatchingReasonCodes()
    {
        using var factory = CreateFactory();
        SetServiceState(factory.Services, isConnected: false, lastUpstreamMessageUtc: null, lastSuccessfulBroadcastUtc: null);
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/health/details");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var payload = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(payload);
        var root = document.RootElement;

        Assert.Equal("degraded", root.GetProperty("status").GetString());

        var reasonCodes = ReadReasonCodes(root);
        var errorReasons = ReadReasonCodes(root.GetProperty("error"), "reasons");

        Assert.Equal("degraded", root.GetProperty("error").GetProperty("code").GetString());
        Assert.Equal(reasonCodes.OrderBy(x => x), errorReasons.OrderBy(x => x));
    }

    [Fact]
    public async Task Health_Ok_ReturnsNullErrorAndNoReasonCodes()
    {
        using var factory = CreateFactory(new Dictionary<string, string?>
        {
            ["WeatherFlow:Health:StaleThresholdSeconds"] = "30"
        });
        SetServiceState(
            factory.Services,
            isConnected: true,
            lastUpstreamMessageUtc: DateTimeOffset.UtcNow,
            lastSuccessfulBroadcastUtc: DateTimeOffset.UtcNow);
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/health");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var payload = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(payload);
        var root = document.RootElement;

        Assert.Equal("ok", root.GetProperty("status").GetString());
        Assert.Empty(ReadReasonCodes(root));
        Assert.Equal(JsonValueKind.Null, root.GetProperty("error").ValueKind);
        Assert.Equal(JsonValueKind.String, root.GetProperty("timestampUtc").ValueKind);
    }

    [Fact]
    public async Task HealthDetails_Degraded_HasNullLastBroadcast_WhenNeverBroadcasted()
    {
        using var factory = CreateFactory(new Dictionary<string, string?>
        {
            ["WeatherFlow:Health:StaleThresholdSeconds"] = "5"
        });
        SetServiceState(
            factory.Services,
            isConnected: true,
            lastUpstreamMessageUtc: DateTimeOffset.UtcNow.AddMinutes(-1),
            lastSuccessfulBroadcastUtc: null);
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/health/details");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var payload = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(payload);
        var root = document.RootElement;

        Assert.Equal("degraded", root.GetProperty("status").GetString());
        Assert.Contains("stale_upstream", ReadReasonCodes(root));
        Assert.Equal(JsonValueKind.Null, root.GetProperty("lastSuccessfulBroadcastUtc").ValueKind);
    }

    private static List<string> ReadReasonCodes(JsonElement root, string propertyName = "reasonCodes")
    {
        return root.GetProperty(propertyName)
            .EnumerateArray()
            .Select(element => element.GetString())
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Cast<string>()
            .ToList();
    }

    private static void SetServiceState(
        IServiceProvider rootServices,
        bool isConnected,
        DateTimeOffset? lastUpstreamMessageUtc,
        DateTimeOffset? lastSuccessfulBroadcastUtc)
    {
        using var scope = rootServices.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<TempestWebSocketService>();

        SetProperty(service, nameof(TempestWebSocketService.IsUpstreamConnected), isConnected);
        SetProperty(service, nameof(TempestWebSocketService.LastUpstreamMessageUtc), lastUpstreamMessageUtc);
        SetProperty(service, nameof(TempestWebSocketService.LastSuccessfulBroadcastUtc), lastSuccessfulBroadcastUtc);
    }

    private static void SetProperty(object target, string propertyName, object? value)
    {
        var property = target.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                       ?? throw new InvalidOperationException($"Property '{propertyName}' not found on {target.GetType().Name}.");
        var setter = property.GetSetMethod(nonPublic: true)
                     ?? throw new InvalidOperationException($"Property '{propertyName}' does not have a setter.");
        setter.Invoke(target, new[] { value });
    }
}
