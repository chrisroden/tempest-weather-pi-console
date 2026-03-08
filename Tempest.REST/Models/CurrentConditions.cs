using System.Text.Json.Serialization;

namespace Tempest.REST.Models;

public class CurrentConditions
{
    [JsonPropertyName("air_density")] public double AirDensity { get; set; }
    [JsonPropertyName("air_temperature")] public double AirTemperature { get; set; }
    [JsonPropertyName("brightness")] public int Brightness { get; set; }
    [JsonPropertyName("conditions")] public string Conditions { get; set; } = string.Empty;
    [JsonPropertyName("delta_t")] public double DeltaT { get; set; }
    [JsonPropertyName("dew_point")] public double DewPoint { get; set; }
    [JsonPropertyName("feels_like")] public double FeelsLike { get; set; }
    [JsonPropertyName("icon")] public string Icon { get; set; } = string.Empty;
    [JsonPropertyName("is_precip_local_day_rain_check")] public bool IsPrecipLocalDayRainCheck { get; set; }
    [JsonPropertyName("is_precip_local_yesterday_rain_check")] public bool IsPrecipLocalYesterdayRainCheck { get; set; }
    [JsonPropertyName("lightning_strike_count_last_1hr")] public int LightningStrikeCountLast1hr { get; set; }
    [JsonPropertyName("lightning_strike_count_last_3hr")] public int LightningStrikeCountLast3hr { get; set; }
    [JsonPropertyName("lightning_strike_last_distance")] public int LightningStrikeLastDistance { get; set; }
    [JsonPropertyName("lightning_strike_last_distance_msg")] public string LightningStrikeLastDistanceMsg { get; set; } = string.Empty;
    [JsonPropertyName("lightning_strike_last_epoch")] public int LightningStrikeLastEpoch { get; set; }
    [JsonPropertyName("precip_accum_local_day")] public double PrecipAccumLocalDay { get; set; }
    [JsonPropertyName("precip_accum_local_yesterday")] public double PrecipAccumLocalYesterday { get; set; }
    [JsonPropertyName("precip_minutes_local_day")] public double PrecipMinutesInLocalDay { get; set; }
    [JsonPropertyName("precip_minutes_local_yesterday")] public double PrecipMinutesInLocalYesterday { get; set; }
    [JsonPropertyName("pressure_trend")] public string PressureTrend { get; set; } = string.Empty;
    [JsonPropertyName("relative_humidity")] public int RelativeHumidity { get; set; }
    [JsonPropertyName("sea_level_pressure")] public double SeaLevelPressure { get; set; }
    [JsonPropertyName("solar_radiation")] public int SolarRadiation { get; set; }
    [JsonPropertyName("station_pressure")] public double StationPressure { get; set; }
    [JsonPropertyName("time")] public int Time { get; set; }
    [JsonPropertyName("uv")] public int Uv { get; set; }
    [JsonPropertyName("wet_bulb_globe_temperature")] public double WetBulbGlobeTemperature { get; set; }
    [JsonPropertyName("wet_bulb_temperature")] public double WetBulbTemperature { get; set; }
    [JsonPropertyName("wind_avg")] public double WindAvg { get; set; }
    [JsonPropertyName("wind_direction")] public int WindDirection { get; set; }
    [JsonPropertyName("wind_direction_cardinal")] public string WindDirectionCardinal { get; set; } = string.Empty;
    [JsonPropertyName("wind_gust")] public double WindGust { get; set; }
}