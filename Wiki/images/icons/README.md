# UI Icon Reference

This folder contains copies of PNG assets from `Tempest.UI/Assets` for documentation use.

Source of truth in code:

- `Tempest.UI/ViewModels/MainWindowViewModel.cs` (`GetWeatherIcon`)

## Weather Icon Catalog

Each icon preview is shown at a consistent size.

| Preview | Key / File | Represents |
|---|---|---|
| <img src="clear-day.png" width="40" /> | `clear-day` / `clear-day.png` | Clear daytime conditions |
| <img src="clear-night.png" width="40" /> | `clear-night` / `clear-night.png` | Clear nighttime conditions |
| <img src="cloudy.png" width="40" /> | `cloudy` / `cloudy.png` | Overcast or mostly cloudy conditions |
| <img src="foggy.png" width="40" /> | `foggy` / `foggy.png` | Fog or reduced visibility |
| <img src="partly-cloudy-day.png" width="40" /> | `partly-cloudy-day` / `partly-cloudy-day.png` | Partly cloudy daytime conditions |
| <img src="partly-cloudy-night.png" width="40" /> | `partly-cloudy-night` / `partly-cloudy-night.png` | Partly cloudy nighttime conditions |
| <img src="possibly-rain-day.png" width="40" /> | `possibly-rainy-day` / `possibly-rain-day.png` | Chance of rain during daytime |
| <img src="possibly-rany-night.png" width="40" /> | `possibly-rainy-night` / `possibly-rany-night.png` | Chance of rain during nighttime |
| <img src="possibly-sleet-day.png" width="40" /> | `possibly-sleet-day` / `possibly-sleet-day.png` | Chance of sleet during daytime |
| <img src="possibly-sleet-night.png" width="40" /> | `possibly-sleet-night` / `possibly-sleet-night.png` | Chance of sleet during nighttime |
| <img src="possibly-snow-day.png" width="40" /> | `possibly-snow-day` / `possibly-snow-day.png` | Chance of snow during daytime |
| <img src="possibly-snow-night.png" width="40" /> | `possibly-snow-night` / `possibly-snow-night.png` | Chance of snow during nighttime |
| <img src="possibly-thunderstorm-day.png" width="40" /> | `possibly-thunderstorm-day` / `possibly-thunderstorm-day.png` | Chance of thunderstorms during daytime |
| <img src="possibly-thunderstorm-night.png" width="40" /> | `possibly-thunderstorm-night` / `possibly-thunderstorm-night.png` | Chance of thunderstorms during nighttime |
| <img src="rainy.png" width="40" /> | `rainy` / `rainy.png` | Active rain |
| <img src="sleet.png" width="40" /> | `sleet` / `sleet.png` | Active sleet or hail-like frozen precipitation |
| <img src="snow.png" width="40" /> | `snow` / `snow.png` | Active snowfall |
| <img src="thunderstorm.png" width="40" /> | `thunderstorm` / `thunderstorm.png` | Thunderstorm or dry-lightning state |
| <img src="thunderstorm-rain.png" width="40" /> | `thunderstorm-rain` / `thunderstorm-rain.png` | Rain with lightning activity |
| <img src="windy.png" width="40" /> | `windy` / `windy.png` | Windy conditions |

Fallback mapping:

- Unknown keys fall back to `cloudy.png`

## State-Based Usage Notes

Based on `ProcessWeatherUpdate` logic in `MainWindowViewModel`:

- Active rain (no recent lightning): uses `rainy`
- Active rain with recent lightning: uses `thunderstorm-rain`
- Hail detected: uses `sleet`
- Dry lightning (lightning without rain): uses `thunderstorm`
- After precipitation/lightning clears: UI returns to forecast base icon (`BaseWeatherIcon`)

## Metric Icon Catalog

| Preview | File | Represents |
|---|---|---|
| <img src="humidity-icon.png" width="40" /> | `humidity-icon.png` | Humidity metric |
| <img src="pressure-icon.png" width="40" /> | `pressure-icon.png` | Pressure metric |
| <img src="precip-icon.png" width="40" /> | `precip-icon.png` | Precipitation metric |
| <img src="lightning-icon.png" width="40" /> | `lightning-icon.png` | Lightning activity metric |
| <img src="wind-icon.png" width="40" /> | `wind-icon.png` | Wind metric |
| <img src="sun-icon.png" width="40" /> | `sun-icon.png` | UV / sun-related metric |
| <img src="gust-icon.png" width="40" /> | `gust-icon.png` | Wind gust metric |
