using System.Collections.Generic;
using Tempest.WebSocket.Models.Responses;
using Tempest.WebSocket.Models.Responses.Enums;
using Xunit;

namespace Tempest.WebSocket.Tests;

public class SkyAirParsingTests
{
    [Fact]
    public void Sky_WithShortObservation_DoesNotThrow_AndReturnsDefaults()
    {
        var sky = new Sky
        {
            DeviceId = 1,
            Observation = new List<List<double?>>
            {
                new() { 1771164597 }
            }
        };

        Assert.NotNull(sky.TimeEpochInSeconds);
        Assert.Equal(0, sky.UVIndex);
        Assert.Null(sky.RainAccumulatedinMm);
        Assert.Equal(0, sky.WindDirectionDegrees);
        Assert.Null(sky.PrecipitationType);
    }

    [Fact]
    public void Air_WithShortObservation_DoesNotThrow_AndReturnsNullables()
    {
        var air = new Air
        {
            DeviceId = 1,
            Observation = new List<List<double?>>
            {
                new() { 1771164597 }
            }
        };

        Assert.NotNull(air.TimeEpochInSeconds);
        Assert.Null(air.StationPressureInMillibar);
        Assert.Null(air.AirTemperatureInCelcius);
        Assert.Null(air.RelativeHumidityPercent);
        Assert.Null(air.ReportIntervalMinutes);
    }

    [Fact]
    public void Sky_WithEmptyObservation_DoesNotThrow_AndUsesSafeDefaults()
    {
        var sky = new Sky
        {
            DeviceId = 1,
            Observation = new List<List<double?>>()
        };

        Assert.Null(sky.TimeEpochInSeconds);
        Assert.Equal(0, sky.IlluminanceInLux);
        Assert.Null(sky.LocalDailyRainAccumulationMm);
    }

    [Fact]
    public void Sky_WithCompleteObservation_MapsExpectedValues()
    {
        var sky = new Sky
        {
            DeviceId = 1,
            Observation = new List<List<double?>>
            {
                new()
                {
                    1771164597,
                    12345,
                    8,
                    0.3,
                    1.2,
                    3.4,
                    5.6,
                    182,
                    2.7,
                    1,
                    640,
                    4.1,
                    1,
                    3,
                    0,
                    4.2,
                    2
                }
            }
        };

        Assert.NotNull(sky.TimeEpochInSeconds);
        Assert.Equal(12345, sky.IlluminanceInLux);
        Assert.Equal(8, sky.UVIndex);
        Assert.NotNull(sky.RainAccumulatedinMm);
        Assert.Equal(0.3, sky.RainAccumulatedinMm!.Value, 3);
        Assert.NotNull(sky.WindLullInMetersPerSecond);
        Assert.Equal(1.2, sky.WindLullInMetersPerSecond!.Value, 3);
        Assert.NotNull(sky.WindAvgInMetersPerSecond);
        Assert.Equal(3.4, sky.WindAvgInMetersPerSecond!.Value, 3);
        Assert.NotNull(sky.WindGustInMetersPerSecond);
        Assert.Equal(5.6, sky.WindGustInMetersPerSecond!.Value, 3);
        Assert.Equal(182, sky.WindDirectionDegrees);
        Assert.NotNull(sky.BatteryVoltage);
        Assert.Equal(2.7, sky.BatteryVoltage!.Value, 3);
        Assert.Equal(1, sky.ReportIntervalInMinutes);
        Assert.Equal(640, sky.SolarRadiationWm2);
        Assert.NotNull(sky.LocalDailyRainAccumulationMm);
        Assert.Equal(4.1, sky.LocalDailyRainAccumulationMm!.Value, 3);
        Assert.Equal(PrecipitationType.Rain, sky.PrecipitationType);
        Assert.Equal(3, sky.WindSampleIntervalInSeconds);
        Assert.Equal(0, sky.RainAccumulatedFinalMm);
        Assert.NotNull(sky.LocalDailyRainAccumulatedFinalMm);
        Assert.Equal(4.2, sky.LocalDailyRainAccumulatedFinalMm!.Value, 3);
        Assert.Equal(PrecipitationAnalysisType.RainCheckWithUserDisplayOff, sky.PrecipitationAnalysisType);
    }

    [Fact]
    public void Air_WithCompleteObservation_MapsExpectedValues()
    {
        var air = new Air
        {
            DeviceId = 1,
            Observation = new List<List<double?>>
            {
                new()
                {
                    1771164597,
                    1012.6,
                    22.4,
                    61,
                    4,
                    8,
                    2.45,
                    1
                }
            }
        };

        Assert.NotNull(air.TimeEpochInSeconds);
        Assert.NotNull(air.StationPressureInMillibar);
        Assert.Equal(1012.6, air.StationPressureInMillibar!.Value, 3);
        Assert.NotNull(air.AirTemperatureInCelcius);
        Assert.Equal(22.4, air.AirTemperatureInCelcius!.Value, 3);
        Assert.Equal(61, air.RelativeHumidityPercent);
        Assert.Equal(4, air.LightningStrikeCount);
        Assert.Equal(8, air.LightningStrikeAvgDistanceInKm);
        Assert.NotNull(air.BatteryVolts);
        Assert.Equal(2.45, air.BatteryVolts!.Value, 3);
        Assert.Equal(1, air.ReportIntervalMinutes);
    }
}
