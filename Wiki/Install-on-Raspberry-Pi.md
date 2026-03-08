# Install on Raspberry Pi

This project includes an interactive installer that detects Raspberry Pi OS flavor and configures backend/UI services.

Before installing, review:

- [App Overview and Features](App-Overview.md)
- [Required Hardware (Tempest, Raspberry Pi, Display)](Hardware-Requirements.md)
- [Screenshots](Screenshots.md)

## What It Installs

- `tempest-backend.service` for `TempestBlazorApp`
- `tempest-ui.service` for `Tempest.UI` (desktop installs)
- Production appsettings generated from your prompted values

## Supported Modes

- `backend-only`
- `ui-only`
- `both` (default on desktop-capable Pi OS)

On Lite/headless systems, installer defaults to `backend-only` and warns before UI install.

## Run the Installer

From the repo root on your Raspberry Pi:

```bash
chmod +x scripts/pi/install-pi.sh scripts/pi/uninstall-pi.sh scripts/pi/reconfigure-pi.sh
./scripts/pi/install-pi.sh
```

The installer prompts for:

- install mode
- install root (default `/opt/tempest`)
- service user
- backend/ui publish directories
- WeatherFlow API token, station ID, device ID
- backend port and UI backend URL
- UI theme
- enable at boot

## OS Flavor Detection

The installer auto-detects:

- architecture (`arm64`, `armhf`)
- OS codename (`trixie`, `bookworm`, etc.)
- desktop presence (for UI viability)

## Manage Installation

Reconfigure interactively:

```bash
./scripts/pi/reconfigure-pi.sh
```

Uninstall:

```bash
./scripts/pi/uninstall-pi.sh
```

## Logs and Status

```bash
sudo systemctl status tempest-backend
sudo systemctl status tempest-ui
sudo journalctl -u tempest-backend -f
sudo journalctl -u tempest-ui -f
```

## Advanced Manual Flow

If you need full manual deployment or troubleshooting steps, use `Deployment Steps.md`.
