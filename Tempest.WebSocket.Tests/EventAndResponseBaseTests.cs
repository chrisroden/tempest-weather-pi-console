using System;
using System.Collections.Generic;
using Tempest.WebSocket.Models.Responses;
using Xunit;

namespace Tempest.WebSocket.Tests;

public class EventAndResponseBaseTests
{
    [Fact]
    public void LightningStrikeEvent_WithCompleteEvent_MapsExpectedValues()
    {
        var epoch = 1771164597;
        var lightningEvent = new LightningStrikeEvent
        {
            DeviceId = 100,
            Event = new List<int> { epoch, 7, 1234 }
        };

        Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(epoch).UtcDateTime, lightningEvent.TimeEpochInSeconds);
        Assert.Equal(7, lightningEvent.DistanceInKilometers);
        Assert.Equal(1234, lightningEvent.Energy);
    }

    [Fact]
    public void LightningStrikeEvent_WithIncompleteEvent_ThrowsForOutOfRangeAccess()
    {
        var lightningEvent = new LightningStrikeEvent
        {
            DeviceId = 100,
            Event = new List<int> { 1771164597 }
        };

        Assert.Throws<ArgumentOutOfRangeException>(() => _ = lightningEvent.DistanceInKilometers);
    }

    [Fact]
    public void ResponseType_UsesLowerCasedConcreteTypeName()
    {
        var rapid = new RapidWind();
        var sky = new Sky { DeviceId = 1, Observation = new List<List<double?>>() };

        Assert.Equal("rapidwind", rapid.ResponseType);
        Assert.Equal("sky", sky.ResponseType);
    }
}