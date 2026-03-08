namespace Tempest.WebSocket.Models.Responses;

using System.Text.Json.Serialization;
using Tempest.WebSocket.Models.Responses.Enums;

public class StationObservation : ResponseMessageBase
{
    [JsonPropertyName("device_id")]
    public int DeviceId { get; set; }

    [JsonPropertyName("serial_number")]
    public string? SerialNumber { get; set; }

    [JsonPropertyName("hub_sn")]
    public string? HubSerialNumber { get; set; }

    [JsonPropertyName("obs")]
    public List<List<double?>>? Obs { get; set; }

    [JsonPropertyName("summary")]
    public StationObservationSummary? Summary { get; set; }

    [JsonIgnore]
    private IReadOnlyList<double?>? ObservationValues => Obs is { Count: > 0 } ? Obs[0] : null;

    private double? GetObservationValue(int index)
    {
        var observationValues = ObservationValues;
        return observationValues != null && observationValues.Count > index ? observationValues[index] : null;
    }

    private int? GetObservationInt(int index) => GetObservationValue(index) is double value ? (int)value : null;

    [JsonIgnore]
    public DateTime? TimeEpochInSeconds => GetObservationValue(0) is double epoch
        ? DateTimeOffset.FromUnixTimeSeconds((long)epoch).UtcDateTime
        : null;

    [JsonIgnore]
    public double? AirTemperatureInCelsius => GetObservationValue(7); // Index 7

    [JsonIgnore]
    public double? StationPressureInMillibar => GetObservationValue(6); // Index 6

    [JsonIgnore]
    public int? RelativeHumidity => GetObservationInt(8); // Index 8
    
    [JsonIgnore]
    public double? RainAccumulatedInMm => GetObservationValue(12); // Index 12 - Rain accumulated in minute
    
    [JsonIgnore]
    public PrecipitationType? PrecipitationType => GetObservationInt(13) is int value ? (PrecipitationType?)value : null; // Index 13
    
    [JsonIgnore]
    public int? LightningStrikeAvgDistanceInKm => GetObservationInt(14); // Index 14
    
    [JsonIgnore]
    public int? LightningStrikeCount => GetObservationInt(15); // Index 15
    
    [JsonIgnore]
    public double? LocalDayRainAccumulationMm => GetObservationValue(18); // Index 18 - Daily rain accumulation in mm
    
    [JsonIgnore]
    public double? PrecipitationRate => GetObservationValue(19); // Index 19 - mm/hr
}

public class StationObservationSummary
{
    [JsonPropertyName("feels_like")]
    public double? FeelsLike { get; set; }

    [JsonPropertyName("precip_total_1h")]
    public double? PrecipTotal1h { get; set; }
    
    [JsonPropertyName("strike_count_1h")]
    public int? StrikeCount1h { get; set; }
    
    [JsonPropertyName("strike_count_3h")]
    public int? StrikeCount3h { get; set; }
    
    [JsonPropertyName("strike_last_dist")]
    public int? StrikeLastDist { get; set; }
}