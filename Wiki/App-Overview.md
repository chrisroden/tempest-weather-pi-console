# App Overview and Features

## Purpose

Tempest Weather Station Console provides a dedicated weather display experience for a single Tempest station on Raspberry Pi.

The goal is reliability and readability for always-on use in homes, offices, and workshops.

## Components

- `TempestBlazorApp` (backend)
- `Tempest.UI` (Avalonia UI)

### Backend (`TempestBlazorApp`)

- Connects to upstream Tempest data stream
- Publishes updates over SignalR at `/weatherHub`
- Exposes health endpoints:
  - `/health`
  - `/health/details`
- Tracks connection and reconnect diagnostics
- Serves as the local data source for UI

### UI (`Tempest.UI`)

- Fullscreen, fixed-size layout (1024x600)
- High-contrast dashboard cards for quick readability
- Real-time weather updates from backend SignalR
- Connection indicator + timestamp of latest update
- Status banner for startup, degradation, and restart messaging

## Displayed Weather Data

- Current temperature and feels-like
- Daily high/low
- Weather condition text and icon
- Wind speed, gust, direction, and cardinal heading
- Humidity
- Pressure and trend
- 24-hour precipitation
- Lightning strike count (1h)
- UV index
- 7-day forecast with precip percentages

## Operations Features

- Auto-detect stale connection and attempt recovery
- Restart backend + UI from the in-app menu
- Reboot host from in-app menu
- Exit app from in-app menu
- Theme selection from in-app menu

## Runtime Topology

1. Tempest station data reaches backend service.
2. Backend normalizes and emits updates over SignalR.
3. UI subscribes and renders dashboard state.
4. UI periodically reads `/health/details` for diagnostics.

## Service Model on Raspberry Pi

- `tempest-backend.service`
- `tempest-ui.service`

Installer can deploy:

- backend-only (headless/Lite defaults)
- ui-only
- both services (desktop defaults)
