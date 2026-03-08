using System;
using System.Collections.Generic;
using Tempest.WebSocket.Models.Responses;
using Xunit;

namespace Tempest.WebSocket.Tests;

public class RapidWindParsingTests
{
    [Fact]
    public void RapidWind_WithEmptyObservation_UsesSafeDefaults()
    {
        var rapidWind = new RapidWind
        {
            DeviceId = 42,
            Observation = new List<double?>()
        };

        Assert.Null(rapidWind.TimeEpochInSeconds);
        Assert.Equal(0, rapidWind.WindSpeedInMetersPerSecond);
        Assert.Equal(0, rapidWind.WindDirectionInDegrees);
    }

    [Fact]
    public void RapidWind_WithCompleteObservation_MapsExpectedValues()
    {
        var epoch = 1771164597d;
        var rapidWind = new RapidWind
        {
            DeviceId = 42,
            Observation = new List<double?>
            {
                epoch,
                7.25,
                181.9
            }
        };

        Assert.Equal(DateTimeOffset.FromUnixTimeSeconds((long)epoch).UtcDateTime, rapidWind.TimeEpochInSeconds);
        Assert.Equal(7.25, rapidWind.WindSpeedInMetersPerSecond, 3);
        Assert.Equal(181, rapidWind.WindDirectionInDegrees);
    }

    [Fact]
    public void RapidWind_WithNullObservationValues_UsesSafeDefaults()
    {
        var rapidWind = new RapidWind
        {
            DeviceId = 42,
            Observation = new List<double?>
            {
                null,
                null,
                null
            }
        };

        Assert.Null(rapidWind.TimeEpochInSeconds);
        Assert.Equal(0, rapidWind.WindSpeedInMetersPerSecond);
        Assert.Equal(0, rapidWind.WindDirectionInDegrees);
    }
}