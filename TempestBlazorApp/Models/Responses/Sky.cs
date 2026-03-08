using System.Text.Json.Serialization;
using TempestBlazorApp.Models.Responses.Enums;

namespace TempestBlazorApp.Models.Responses;

public class Sky : ResponseMessageBase
{
    public Sky()
    {
        Observation = new List<List<double?>>();
    }
    
    [JsonPropertyName("device_id")] public required int DeviceId { get; set; }
    [JsonPropertyName("obs")] public required List<List<double?>> Observation { private get; set; }
  
    private List<double?> _observation => Observation.First();
  
    public DateTime TimeEpochInSeconds => DateTimeOffset.FromUnixTimeSeconds((int)_observation[0]!).DateTime;
    public int IlluminanceInLux => (int)_observation[1]!;
    public int UVIndex => (int)_observation[2]!;
    public double? RainAccumulatedinMm => _observation[3] ?? 0;
    public double? WindLullInMetersPerSecond => _observation[4] ?? 0;
    public double? WindAvgInMetersPerSecond => _observation[5] ?? 0;
    public double? WindGustInMetersPerSecond => _observation[6] ?? 0;
    public int WindDirectionDegrees => (int)_observation[7]!;
    public double? BatteryVoltage => _observation[8] ?? 0;
    public int ReportIntervalInMinutes => (int)_observation[9]!;
    public int SolarRadiationWm2 => (int)_observation[10]!;
    public double? LocalDailyRainAccumulationMm => _observation[11] ?? 0;
    public PrecipitationType? PrecipitationType => (PrecipitationType?)((int?)_observation[12]);
    public int WindSampleIntervalInSeconds => (int)_observation[13]!;
    public int RainAccumulatedFinalMm => (int)_observation[14]!;
    public double? LocalDailyRainAccumulatedFinalMm => _observation[15] ?? 0;
    public PrecipitationAnalysisType? PrecipitationAnalysisType => (PrecipitationAnalysisType?)((int?)_observation[16]);
}