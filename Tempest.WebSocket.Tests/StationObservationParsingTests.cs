using System;
using System.Collections.Generic;
using Tempest.WebSocket.Models.Responses;
using Tempest.WebSocket.Models.Responses.Enums;
using Xunit;

namespace Tempest.WebSocket.Tests;

public class StationObservationParsingTests
{
    [Fact]
    public void StationObservation_WithEmptyObs_UsesNullDefaults()
    {
        var observation = new StationObservation
        {
            DeviceId = 10,
            Obs = new List<List<double?>>()
        };

        Assert.Null(observation.TimeEpochInSeconds);
        Assert.Null(observation.AirTemperatureInCelsius);
        Assert.Null(observation.StationPressureInMillibar);
        Assert.Null(observation.RelativeHumidity);
        Assert.Null(observation.PrecipitationType);
        Assert.Null(observation.PrecipitationRate);
    }

    [Fact]
    public void StationObservation_WithMappedIndices_ReturnsExpectedValues()
    {
        var epoch = 1771164597d;
        var row = new List<double?>
        {
            epoch, // 0 timestamp
            null,
            null,
            null,
            null,
            null,
            1004.4, // 6 pressure
            23.1, // 7 temp
            64, // 8 humidity
            null,
            null,
            null,
            0.8, // 12 rain accumulated
            2, // 13 precip type hail
            14, // 14 avg lightning distance
            3, // 15 lightning count
            null,
            null,
            4.2, // 18 day rain accumulation
            0.9 // 19 precip rate
        };

        var observation = new StationObservation
        {
            DeviceId = 10,
            Obs = new List<List<double?>> { row },
            Summary = new StationObservationSummary
            {
                FeelsLike = 22.6,
                PrecipTotal1h = 1.1,
                StrikeCount1h = 2,
                StrikeCount3h = 5,
                StrikeLastDist = 9
            }
        };

        Assert.Equal(DateTimeOffset.FromUnixTimeSeconds((long)epoch).UtcDateTime, observation.TimeEpochInSeconds);
        Assert.NotNull(observation.AirTemperatureInCelsius);
        Assert.Equal(23.1, observation.AirTemperatureInCelsius!.Value, 3);
        Assert.NotNull(observation.StationPressureInMillibar);
        Assert.Equal(1004.4, observation.StationPressureInMillibar!.Value, 3);
        Assert.Equal(64, observation.RelativeHumidity);
        Assert.NotNull(observation.RainAccumulatedInMm);
        Assert.Equal(0.8, observation.RainAccumulatedInMm!.Value, 3);
        Assert.Equal(PrecipitationType.Hail, observation.PrecipitationType);
        Assert.Equal(14, observation.LightningStrikeAvgDistanceInKm);
        Assert.Equal(3, observation.LightningStrikeCount);
        Assert.NotNull(observation.LocalDayRainAccumulationMm);
        Assert.Equal(4.2, observation.LocalDayRainAccumulationMm!.Value, 3);
        Assert.NotNull(observation.PrecipitationRate);
        Assert.Equal(0.9, observation.PrecipitationRate!.Value, 3);

        Assert.NotNull(observation.Summary);
        Assert.NotNull(observation.Summary!.FeelsLike);
        Assert.Equal(22.6, observation.Summary.FeelsLike!.Value, 3);
        Assert.NotNull(observation.Summary.PrecipTotal1h);
        Assert.Equal(1.1, observation.Summary.PrecipTotal1h!.Value, 3);
        Assert.Equal(2, observation.Summary.StrikeCount1h);
        Assert.Equal(5, observation.Summary.StrikeCount3h);
        Assert.Equal(9, observation.Summary.StrikeLastDist);
    }
}