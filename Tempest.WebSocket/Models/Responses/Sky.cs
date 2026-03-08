using System.Text.Json.Serialization;
using Tempest.WebSocket.Models.Responses.Enums;

namespace Tempest.WebSocket.Models.Responses;

public class Sky : ResponseMessageBase
{
    public Sky()
    {
        Observation = new List<List<double?>>();
    }
    
  [JsonPropertyName("device_id")] public required int DeviceId { get; set; }
  [JsonPropertyName("obs")] public required List<List<double?>> Observation { get; set; }

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

  public int IlluminanceInLux => GetObservationInt(1) ?? 0;
  public int UVIndex => GetObservationInt(2) ?? 0;
  public double? RainAccumulatedinMm => GetObservationValue(3);
  public double? WindLullInMetersPerSecond => GetObservationValue(4);
  public double? WindAvgInMetersPerSecond => GetObservationValue(5);
  public double? WindGustInMetersPerSecond => GetObservationValue(6);
  public int WindDirectionDegrees => GetObservationInt(7) ?? 0;
  public double? BatteryVoltage => GetObservationValue(8);
  public int ReportIntervalInMinutes => GetObservationInt(9) ?? 0;
  public int SolarRadiationWm2 => GetObservationInt(10) ?? 0;
  public double? LocalDailyRainAccumulationMm => GetObservationValue(11);
  public PrecipitationType? PrecipitationType => GetObservationInt(12) is int value ? (PrecipitationType?)value : null;
  public int WindSampleIntervalInSeconds => GetObservationInt(13) ?? 0;
  public int RainAccumulatedFinalMm => GetObservationInt(14) ?? 0;
  public double? LocalDailyRainAccumulatedFinalMm => GetObservationValue(15);
  public PrecipitationAnalysisType? PrecipitationAnalysisType => GetObservationInt(16) is int value ? (PrecipitationAnalysisType?)value : null;
 }