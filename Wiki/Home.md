# Tempest Weather Station Console

Tempest Weather Station Console is a Raspberry Pi-focused weather display stack for always-on wall/tablet displays powered by a WeatherFlow Tempest station.

This project is designed to run as a local two-process system:

- Backend service (`TempestBlazorApp`) for WeatherFlow ingest, SignalR broadcast, and health endpoints
- Fullscreen kiosk UI (`Tempest.UI`) for at-a-glance weather display and touch-friendly controls

## What This App Is

This app turns a Raspberry Pi and display into a dedicated, near-real-time weather console for a specific Tempest station.

It is not a generic weather app and requires:

- a Tempest Weather System (Hub + outdoor sensor)
- your WeatherFlow API token
- your station ID (and device ID for backend polling)

## Core Features

- Fullscreen 1024x600 dashboard optimized for a 10.1-inch kiosk-style display
- Current conditions: temperature, feels-like, condition icon, high/low
- Wind panel: speed, gust, direction (degrees + cardinal)
- Additional metrics: humidity, pressure + trend, precipitation, lightning strikes, UV
- 7-day forecast cards with icons and precipitation chance
- Live status indicator and backend health diagnostics banner
- Automatic connection-loss detection and auto-restart behavior
- In-app menu actions: theme selection, restart backend+UI, reboot, exit
- Multi-theme support (switchable in UI)

## App Architecture

- `Tempest.UI` connects to backend SignalR endpoint `/weatherHub`
- `Tempest.UI` reads backend health from `/health` and `/health/details`
- `TempestBlazorApp` runs WebSocket polling via `TempestWebSocketService`
- Install supports `backend-only`, `ui-only`, or `both`

## Start Here

- [Install on Raspberry Pi](Install-on-Raspberry-Pi)
- [App Overview and Features](App-Overview)
- [Required Hardware (Tempest, Raspberry Pi, Display)](Hardware-Requirements)
- [Screenshots](Screenshots)
- [Quick Tour (Annotated UI Walkthrough)](Quick-Tour)
- [Icon Legend](Icon-Legend)

## Additional Deployment Details

For manual and advanced deployment workflows, see:

- `Deployment Steps.md`
