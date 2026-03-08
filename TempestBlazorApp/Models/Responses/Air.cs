using System.Text.Json.Serialization;

namespace TempestBlazorApp.Models.Responses;

public class Air : ResponseMessageBase
{
    public Air()
    {
        Observation = new List<List<double?>>();
    }

    [JsonPropertyName("device_id")] public int DeviceId { get; set; }
    [JsonPropertyName("obs")] public List<List<double?>> Observation { private get; set; }
    private List<double?> _observation => Observation.First();

    public DateTime TimeEpochInSeconds => DateTimeOffset.FromUnixTimeSeconds((int)_observation![0]!).DateTime;
    public double StationPressureInMillibar => _observation[1] ?? 0;
    public double AirTemperatureInCelcius => _observation[2] ?? 0;
    public int RelativeHumidityPercent => (int)_observation[3]!;
    public int LightningStrikeCount => (int)_observation[4]!;
    public int LightningStrikeAvgDistanceInKm => (int)_observation[5]!;
    public double BatteryVolts => _observation[6] ?? 0;
    public int ReportIntervalMinutes => (int)_observation[7]!;
}