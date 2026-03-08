using System.Text.Json.Serialization;

namespace Tempest.REST.Models;

public class Units
{
    [JsonPropertyName("units_air_density")] public string UnitsAirDensity { get; set; } = string.Empty;
    [JsonPropertyName("units_brightness")] public string UnitsBrightness { get; set; } = string.Empty;
    [JsonPropertyName("units_distance")] public string UnitsDistance { get; set; } = string.Empty;
    [JsonPropertyName("units_other")] public string UnitsOther { get; set; } = string.Empty;
    [JsonPropertyName("units_precip")] public string UnitsPrecip { get; set; } = string.Empty;
    [JsonPropertyName("units_pressure")] public string UnitsPressure { get; set; } = string.Empty;
    [JsonPropertyName("units_solar_radiation")] public string UnitsSolarRadiation { get; set; } = string.Empty;
    [JsonPropertyName("units_temp")] public string UnitsTemp { get; set; } = string.Empty;
    [JsonPropertyName("units_wind")] public string UnitsWind { get; set; } = string.Empty;
}