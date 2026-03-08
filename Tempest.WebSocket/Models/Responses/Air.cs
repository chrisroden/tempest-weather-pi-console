using System.Text.Json.Serialization;

namespace Tempest.WebSocket.Models.Responses;

public class Air : ResponseMessageBase
{
    public Air()
    {
        Observation = new List<List<double?>>();
    }

    [JsonPropertyName("device_id")] public int DeviceId { get; set; }
    [JsonPropertyName("obs")] public List<List<double?>> Observation { get; set; }

    [JsonIgnore]
    private IReadOnlyList<double?>? ObservationValues => Observation is { Count: > 0 } ? Observation[0] : null;

    private double? GetObservationValue(int index)
    {
        var observationValues = ObservationValues;
        return observationValues != null && observationValues.Count > index ? observationValues[index] : null;
    }

    private int? GetObservationInt(int index) => GetObservationValue(index) is double value ? (int)value : null;

    public DateTime? TimeEpochInSeconds => GetObservationValue(0) is double epoch
        ? DateTimeOffset.FromUnixTimeSeconds((long)epoch).UtcDateTime
        : null;
    public double? StationPressureInMillibar => GetObservationValue(1);
    public double? AirTemperatureInCelcius => GetObservationValue(2);
    public int? RelativeHumidityPercent => GetObservationInt(3);
    public int? LightningStrikeCount => GetObservationInt(4);
    public int? LightningStrikeAvgDistanceInKm => GetObservationInt(5);
    public double? BatteryVolts => GetObservationValue(6);
    public int? ReportIntervalMinutes => GetObservationInt(7);
}