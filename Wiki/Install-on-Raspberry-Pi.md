# Install on Raspberry Pi

This project includes an interactive installer that detects Raspberry Pi OS flavor and configures backend/UI services.

Before installing, review:

- [App Overview and Features](App-Overview)
- [Required Hardware (Tempest, Raspberry Pi, Display)](Hardware-Requirements)
- [Screenshots](Screenshots)

## Quickstart (Recommended)

Run one bootstrap command from repo root on the Pi:

```bash
chmod +x scripts/pi/*.sh
./scripts/pi/bootstrap-pi.sh
```

What bootstrap does:

- installs prerequisites (`curl`, `jq`, `tar`, `.NET` if missing)
- publishes backend/UI to `publish/backend` and `publish/ui`
- launches `install-pi.sh` with those publish directories

If you prefer prebuilt artifacts instead of building on the Pi:

```bash
./scripts/pi/bootstrap-pi.sh \
	--download-release \
	--backend-archive-url "https://example.com/tempest-backend-linux-arm64.tar.gz" \
	--ui-archive-url "https://example.com/tempest-ui-linux-arm64.tar.gz"
```

## Unattended Install (Config File)

Create a private config file:

```bash
cp scripts/pi/install.env.example scripts/pi/install.env
nano scripts/pi/install.env
chmod 600 scripts/pi/install.env
```

Then run:

```bash
./scripts/pi/bootstrap-pi.sh --config scripts/pi/install.env --yes
```

`install-pi.sh` also supports direct non-interactive usage:

```bash
./scripts/pi/install-pi.sh \
	--mode both \
	--install-root /opt/tempest \
	--service-user pi \
	--backend-source "$PWD/publish/backend" \
	--ui-source "$PWD/publish/ui" \
	--token "<token>" \
	--station-id 12345 \
	--device-id 67890 \
	--port 5000 \
	--backend-url "http://localhost:5000" \
	--stale-threshold-seconds 15 \
	--theme Default \
	--enable-at-boot yes \
	--yes
```

Useful extra flags:

- `--dry-run` validates and prints summary without making changes
- `--write-config <file>` writes an env file from interactive answers

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

Smoke test after install:

```bash
chmod +x scripts/pi/smoke-test-pi.sh
./scripts/pi/smoke-test-pi.sh
```

For desktop installs where UI must be running too:

```bash
./scripts/pi/smoke-test-pi.sh --mode both
```

## Release Artifacts For Download Mode

The workflow `.github/workflows/release-pi-artifacts.yml` builds release tarballs for:

- `linux-arm64`
- `linux-arm`

It runs on tag pushes (`v*.*.*`) or manually (`workflow_dispatch`) and uploads:

- `tempest-backend-<rid>.tar.gz`
- `tempest-ui-<rid>.tar.gz`

Use those URLs with bootstrap download mode:

```bash
./scripts/pi/bootstrap-pi.sh \
	--download-release \
	--backend-archive-url "https://github.com/<owner>/<repo>/releases/download/<tag>/tempest-backend-linux-arm64.tar.gz" \
	--ui-archive-url "https://github.com/<owner>/<repo>/releases/download/<tag>/tempest-ui-linux-arm64.tar.gz"
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
