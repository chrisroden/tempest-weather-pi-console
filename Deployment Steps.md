# Tempest Weather Station - Complete Raspberry Pi Setup Guide

> Quick start: for most installs, use the interactive installer in the GitHub Wiki: https://github.com/chrisroden/tempest-weather-pi-console/wiki/Install-on-Raspberry-Pi

## Quick Path (Recommended)

If your goal is fast, repeatable installation, use the Pi scripts first and only use the rest of this document for advanced manual troubleshooting.

From repo root on the Pi:

```bash
chmod +x scripts/pi/*.sh
./scripts/pi/bootstrap-pi.sh
./scripts/pi/smoke-test-pi.sh
```

Unattended variant:

```bash
cp scripts/pi/install.env.example scripts/pi/install.env
chmod 600 scripts/pi/install.env
./scripts/pi/bootstrap-pi.sh --config scripts/pi/install.env --yes
./scripts/pi/smoke-test-pi.sh
```

For desktop installs where UI is required, run:

```bash
./scripts/pi/smoke-test-pi.sh --mode both
```

## Prerequisites

- Raspberry Pi (tested on Pi 3B+/4/5)
- 7" touchscreen display (1024x600) or similar
- MicroSD card (16GB minimum, 32GB recommended)
- Internet connection for the Pi
- Mac/PC for building the application
- SSH access enabled on Raspberry Pi

## Part 1: Raspberry Pi Initial Setup

### 1. Install Raspberry Pi OS

1. Download and install [Raspberry Pi Imager](https://www.raspberrypi.com/software/)
2. Flash Raspberry Pi OS (64-bit, Desktop recommended) to your SD card
3. In the Imager settings (gear icon), configure:
   - Set hostname (e.g., `tempest-office.local`)
   - Enable SSH
   - Set username and password
   - Configure WiFi if needed
   - Set locale settings
4. Insert SD card into Pi and boot up

### 2. Initial Configuration

SSH into your Raspberry Pi or use the desktop:

```bash
# Update system
sudo apt-get update
sudo apt-get upgrade -y

# Install .NET 9 Runtime (required for the application)
wget https://dot.net/v1/dotnet-install.sh -O dotnet-install.sh
chmod +x ./dotnet-install.sh
./dotnet-install.sh --channel 9.0 --runtime dotnet --install-dir /usr/share/dotnet
sudo ln -s /usr/share/dotnet/dotnet /usr/bin/dotnet

# Verify .NET installation
dotnet --version
```

### 3. Configure Display Settings (for 7" touchscreen)

Edit the boot config:

```bash
sudo nano /boot/firmware/config.txt
```

Add or modify these lines for the official 7" touchscreen:

```ini
# Display settings
hdmi_group=2
hdmi_mode=87
hdmi_cvt=1024 600 60 6 0 0 0
hdmi_drive=1
# Per-device orientation (set per Pi, no app rebuild needed)
# 0 = normal, 1 = 90° clockwise, 2 = 180° (upside-down), 3 = 270° clockwise
display_rotate=0
```

`display_rotate` is intended to be configured per Raspberry Pi. This lets one Pi run normal (`0`) while another uses upside-down mounting (`2`) with the same deployed application binaries.

Save and exit, then reboot:

```bash
sudo reboot
```

If rotation does not apply, your Pi is likely using KMS (`dtoverlay=vc4-kms-v3d`). In that case, set rotation via kernel command line instead of `display_rotate`:

```bash
sudo cp /boot/firmware/cmdline.txt /boot/firmware/cmdline.txt.bak
sudo nano /boot/firmware/cmdline.txt
```

First, identify the connected output name:

```bash
for f in /sys/class/drm/*/status; do printf "%s: %s\n" "$f" "$(cat "$f" 2>/dev/null)"; done
```

Use the connector whose status is `connected` (usually `HDMI-A-1` or `HDMI-A-2`) in the `video=` value below.

Keep everything on a **single line** and append one of these per-device values:

- Normal: `video=HDMI-A-1:1024x600M@60,rotate=0` (or `HDMI-A-2`)
- Upside-down: `video=HDMI-A-1:1024x600M@60,rotate=180` (or `HDMI-A-2`)

Then reboot:

```bash
sudo reboot
```

Rollback if needed:

```bash
sudo cp /boot/firmware/cmdline.txt.bak /boot/firmware/cmdline.txt
sudo reboot
```

### 4. Disable Screen Blanking (optional but recommended)

For kiosk mode, prevent the screen from turning off:

```bash
# Edit lightdm config
sudo nano /etc/lightdm/lightdm.conf
```

Find the `[Seat:*]` section and add:

```ini
xserver-command=X -s 0 -dpms
```

Also edit the autostart file:

```bash
mkdir -p ~/.config/lxsession/LXDE-pi
nano ~/.config/lxsession/LXDE-pi/autostart
```

Add these lines:

```
@xset s noblank
@xset s off
@xset -dpms
```

### 5. Optional - Hide Mouse Cursor

For a clean kiosk display:

```bash
sudo apt-get install -y unclutter
mkdir -p ~/.config/autostart
nano ~/.config/autostart/unclutter.desktop
```

Content:

```ini
[Desktop Entry]
Type=Application
Name=Unclutter
Exec=unclutter -idle 0.1
X-GNOME-Autostart-enabled=true
```

## Part 2: Install the Application

The application is distributed as pre-built self-contained binaries via GitHub Releases. You do not need to build locally or copy files manually — the installer downloads everything from the latest release automatically.

### 1. Run the Installer (Interactive)

SSH into your Pi and run:

```bash
curl -fsSL https://raw.githubusercontent.com/chrisroden/tempest-weather-pi-console/main/scripts/pi/install-pi.sh | sudo bash
```

The installer will prompt for your WeatherFlow API token, Station ID, Device ID, and other options.

### 2. Unattended Install

Pass all required values on the command line to skip prompts:

```bash
curl -fsSL https://raw.githubusercontent.com/chrisroden/tempest-weather-pi-console/main/scripts/pi/install-pi.sh | sudo bash -s -- \
  --mode both \
  --token YOUR_API_TOKEN \
  --station-id YOUR_STATION_ID \
  --device-id YOUR_DEVICE_ID \
  --yes
```

Or save options in a config file:

```bash
cp scripts/pi/install.env.example scripts/pi/install.env
chmod 600 scripts/pi/install.env
# Edit install.env with your values, then:
curl -fsSL https://raw.githubusercontent.com/chrisroden/tempest-weather-pi-console/main/scripts/pi/install-pi.sh | sudo bash -s -- --config scripts/pi/install.env --yes
```

### 3. What the Installer Does

The installer (`install-pi.sh`) performs all of the following automatically:

- Downloads the latest release tarballs from GitHub
- Installs binaries to `/opt/tempest/backend/` and `/opt/tempest/ui/`
- Writes `appsettings.Production.json` with your WeatherFlow credentials
- Registers and enables `tempest-backend.service` and `tempest-ui.service` as systemd services
- Starts both services immediately
- Copies itself to `/opt/tempest/install-pi.sh` so future updates work without re-bootstrapping

### 4. Installer Reference

```
Install options:
  --mode <backend|ui|both>
  --install-root <path>            Default: /opt/tempest
  --service-user <user>
  --token <weatherflow-api-token>
  --station-id <number>
  --device-id <number>
  --port <number>
  --backend-url <url>
  --stale-threshold-seconds <n>
  --theme <name>
  --enable-at-boot <yes|no>
  --config <env-file>
  --write-config <env-file>
  --yes                            Skip all prompts
  --dry-run                        Preview without making changes

Update options:
  --update                         Check GitHub and apply latest release
  --install-root <path>            Default: /opt/tempest
  --yes                            Apply without prompting
```

## Part 3: Verify Services

The installer registers and starts both services via systemd automatically. After installation:

```bash
# Check both services are running
sudo systemctl status tempest-backend.service
sudo systemctl status tempest-ui.service

# Follow live logs
sudo journalctl -u tempest-backend -f
sudo journalctl -u tempest-ui -f
```

Both services are enabled at boot by default. To disable auto-start:

```bash
sudo systemctl disable tempest-backend.service tempest-ui.service
```

## Part 4: Testing and Verification

### 1. Health Verification Checklist (Post-Deploy)

Run these commands on the Pi after installation and confirm expected results:

```bash
# 1) Basic health
curl -s http://localhost:5000/health

# 2) Detailed diagnostics
curl -s http://localhost:5000/health/details

# 3) SignalR negotiate endpoint
curl -s -o /dev/null -w "%{http_code}\n" -X POST "http://localhost:5000/weatherHub/negotiate?negotiateVersion=1"
```

Expected:
- `/health` returns JSON with `status`, `reasonCodes`, and `error`
- `/health/details` returns JSON with `reconnectAttemptCount`, `totalReconnects`, `successfulConnectionCount`, and `lastSuccessfulBroadcastUtc`
- SignalR negotiate returns `200`

### 2. Check Service Status

```bash
# Service status
sudo systemctl status tempest-backend.service
sudo systemctl status tempest-ui.service

# Live logs
sudo journalctl -u tempest-backend -f
sudo journalctl -u tempest-ui -f

# Stop / start manually
sudo systemctl stop tempest-backend.service
sudo systemctl start tempest-backend.service
```

### 3. Test Restart Button

The UI includes a Restart button (red) that will:
1. Show a red connection indicator immediately
2. Restart the backend service and wait for the health check to pass
3. Restart the UI service
4. Show orange status messages during the process
5. Show red error messages if restart fails

To test:
- Click the Restart button in the UI
- Watch the status banner and connection indicator
- Both services should restart within 15–20 seconds

### 4. Reboot and Verify Auto-Start

```bash
sudo reboot
```

After reboot both services start automatically. The UI displays on the Pi's screen within a few seconds of the desktop loading.

## Part 5: Configuration Notes

### Station Configuration

The application requires configuration with your WeatherFlow Tempest station details.

See [CONFIGURATION.md](CONFIGURATION.md) for complete setup instructions on how to:
- Get your API token from WeatherFlow
- Find your Station ID and Device ID
- Configure both backend and UI with your credentials

Configuration is stored in `appsettings.Production.json` files under `/opt/tempest/backend/` and `/opt/tempest/ui/` (not committed to git for security). The installer writes these files automatically. To reconfigure, run:

```bash
sudo /opt/tempest/install-pi.sh --reconfigure
```

### Network Requirements

- Backend runs on `http://0.0.0.0:5000` (accessible from all interfaces)
- Backend provides SignalR hub at `/weatherhub`
- Backend health check endpoint at `/health`
- Backend diagnostics endpoint at `/health/details` (includes reconnect counters and last successful broadcast timestamp)
- UI connects to `http://localhost:5000` via SignalR for real-time updates
- Backend connects to WeatherFlow API for data
- Restart functionality is handled by systemd service restarts

### Health Threshold Configuration

- Configure stale-stream detection with `WeatherFlow:Health:StaleThresholdSeconds` in `appsettings.Production.json`
- Default value is `15` seconds if the setting is omitted
- Example:

```json
"WeatherFlow": {
  "Health": {
    "StaleThresholdSeconds": 15
  }
}
```

### Display Configuration

The application is optimized for:
- Resolution: 1024x600
- Fullscreen mode (no window decorations via `SystemDecorations="None"`)
- Touch-friendly interface with large buttons
- Three control buttons: Exit (cyan), Restart (red), Reboot (orange)
- Connection status indicator (green/red dot)
- Status notification banner with color-coded messages (orange for info, red for errors)

## Troubleshooting

### Backend Issues

**Backend won't start:**
```bash
# Check service status
sudo systemctl status tempest-backend.service

# View recent logs
sudo journalctl -u tempest-backend -n 50

# Check permissions
ls -la /opt/tempest/backend/TempestBlazorApp

# Check .NET runtime
dotnet --info
```

**Backend crashes or restarts:**
```bash
# View recent logs
sudo journalctl -u tempest-backend -n 100

# Check for port conflicts
sudo netstat -tlnp | grep 5000

# Check service is still running
sudo systemctl status tempest-backend.service
```

**Restart button fails:**
- Restart uses `sudo systemctl restart tempest-backend.service` then `tempest-ui.service` (not home-directory scripts)
- Check both units: `sudo systemctl status tempest-backend tempest-ui`
- Logs: `sudo journalctl -u tempest-backend -u tempest-ui -n 80`
- Service user needs passwordless systemctl for those units via `/etc/sudoers.d/tempest` (written by installer)

**Reboot / Exit / Restart permissions:**
- UI actions run as the **service user** (`User=` in the unit files) with no password TTY
- Installer writes `/etc/sudoers.d/tempest` allowing passwordless:
  - `systemctl restart|stop|start` for `tempest-backend.service` and `tempest-ui.service`
  - `/usr/sbin/reboot`
- Manual repair (replace `SERVICE_USER`):

```bash
echo 'SERVICE_USER ALL=(ALL) NOPASSWD: /usr/bin/systemctl restart tempest-backend.service, /usr/bin/systemctl restart tempest-ui.service, /usr/bin/systemctl stop tempest-backend.service, /usr/bin/systemctl stop tempest-ui.service, /usr/bin/systemctl start tempest-backend.service, /usr/bin/systemctl start tempest-ui.service, /usr/sbin/reboot' \
  | sudo tee /etc/sudoers.d/tempest
sudo chmod 440 /etc/sudoers.d/tempest
sudo visudo -cf /etc/sudoers.d/tempest
sudo -u SERVICE_USER sudo -n -l
```

### UI Issues

**UI doesn't appear on screen:**
```bash
# Check service status
sudo systemctl status tempest-ui.service

# View recent logs
sudo journalctl -u tempest-ui -n 50

# Check .xsession-errors
tail -50 ~/.xsession-errors

# Restart the service
sudo systemctl restart tempest-ui.service
```

**XOpenDisplay failed error:**
- This means X11 display access is not available when the service starts
- The service unit file sets `DISPLAY` and `XAUTHORITY` — verify the service file under `/etc/systemd/system/tempest-ui.service` has the correct values for your user
- Reboot the Pi to trigger autostart with the full desktop environment available

**UI shows but no data:**
- Check backend is running: `sudo systemctl status tempest-backend.service`
- Test health endpoint: `curl http://localhost:5000/health`
- Test diagnostics endpoint: `curl http://localhost:5000/health/details`
- Follow backend logs: `sudo journalctl -u tempest-backend -f`
- Check network connectivity to WeatherFlow API

**Icons or images not showing:**
- Verify Assets folder exists: `ls /opt/tempest/ui/Assets/`
- Ensure all PNG files are present (47 weather and icon files)
- Check file permissions: `chmod -R 644 /opt/tempest/ui/Assets/*`
- Run `sudo /opt/tempest/install-pi.sh --update --yes` to re-download and replace all files from the latest release

**Connection indicator stuck red:**
- Check backend service: `sudo systemctl status tempest-backend.service`
- Check backend health: `curl http://localhost:5000/health`
- Check backend diagnostics: `curl http://localhost:5000/health/details`
- Follow both logs: `sudo journalctl -u tempest-backend -u tempest-ui -f`
- Try the Restart button in the UI to restart both services

### Display Issues

**Screen resolution wrong:**
```bash
# Edit boot config
sudo nano /boot/firmware/config.txt

# Adjust hdmi_cvt values for your display
# Format: hdmi_cvt=<width> <height> <framerate> <aspect> <margins> <interlace> <rb>
```

**Touch not working:**
```bash
# Install touch drivers
sudo apt-get install xserver-xorg-input-evdev

# Reboot
sudo reboot
```

**Touch points are offset after display rotation (e.g., top-right taps register bottom-left):**

This commonly occurs on Wayland/KMS when display rotation is applied but touch calibration is not.

Apply a persistent 180° touchscreen transform:

```bash
echo 'ACTION=="add|change", KERNEL=="event*", ENV{ID_INPUT_TOUCHSCREEN}=="1", ENV{LIBINPUT_CALIBRATION_MATRIX}="-1 0 1 0 -1 1"' | sudo tee /etc/udev/rules.d/99-touchscreen-rotation.rules >/dev/null
sudo reboot
```

Rollback if needed:

```bash
sudo rm -f /etc/udev/rules.d/99-touchscreen-rotation.rules
sudo reboot
```

**Revert everything to right-side-up (normal orientation):**

Use this if the display/frame is changed back to standard mounting.

```bash
# Reset firmware rotation
if grep -q '^display_rotate=' /boot/firmware/config.txt; then
  sudo sed -i 's/^display_rotate=.*/display_rotate=0/' /boot/firmware/config.txt
else
  echo 'display_rotate=0' | sudo tee -a /boot/firmware/config.txt >/dev/null
fi

# Remove KMS kernel rotation token (if present)
line="$(cat /boot/firmware/cmdline.txt | tr -d '\n' | sed 's/video=HDMI-A-[0-9]:[^ ]*rotate=[0-9][0-9]*//g' | xargs)"
echo "$line" | sudo tee /boot/firmware/cmdline.txt >/dev/null

# Remove touchscreen calibration override (if present)
sudo rm -f /etc/udev/rules.d/99-touchscreen-rotation.rules

# Apply changes
sudo reboot
```

**Screen blanks after idle:**
- Verify screen blanking is disabled (see Part 1, Step 4)
- Check autostart settings in `~/.config/lxsession/LXDE-pi/autostart`

### Performance Issues

**App is slow or laggy:**
```bash
# Check CPU temperature
vcgencmd measure_temp

# Check memory usage
free -h

# Monitor system resources
htop
```

## Updating the Application

Once the installer is in place at `/opt/tempest/install-pi.sh`, updates are a single command:

```bash
sudo /opt/tempest/install-pi.sh --update --yes
```

This downloads the latest release from GitHub, swaps the binaries, restarts both services, and refreshes the installer script itself so future updates continue to work.

### Preview Before Applying

```bash
sudo /opt/tempest/install-pi.sh --update --dry-run
```

### First-Time Bootstrap (installer not yet on Pi)

If the Pi has not yet been installed, or the installer file is missing:

```bash
curl -fsSL https://raw.githubusercontent.com/chrisroden/tempest-weather-pi-console/main/scripts/pi/install-pi.sh | sudo bash -s -- --update --yes
```

## Additional Tips

### Remote Access

```bash
# SSH with key-based auth (more secure)
ssh-copy-id pi@raspberrypi.local

# VNC for remote desktop (install on Pi)
sudo apt-get install realvnc-vnc-server
sudo raspi-config  # Enable VNC under Interface Options
```

### Backup Configuration

```bash
# Back up production config files
ssh pi@raspberrypi.local "sudo tar -czf tempest-config-backup.tar.gz /opt/tempest/backend/appsettings.Production.json /opt/tempest/ui/appsettings.Production.json"
scp pi@raspberrypi.local:~/tempest-config-backup.tar.gz ./
```

### Multiple Deployments

Run the installer on each Pi separately. Pass credentials on the command line or use a shared config file:

```bash
# Pi 1
curl -fsSL https://raw.githubusercontent.com/chrisroden/tempest-weather-pi-console/main/scripts/pi/install-pi.sh | \
  sudo bash -s -- --mode both --token YOUR_TOKEN --station-id YOUR_STATION_ID --device-id YOUR_DEVICE_ID --yes

# To update all Pis later, run on each:
sudo /opt/tempest/install-pi.sh --update --yes
```

## Complete Example - Fresh Deployment

```bash
# One command — interactive (prompts for credentials)
curl -fsSL https://raw.githubusercontent.com/chrisroden/tempest-weather-pi-console/main/scripts/pi/install-pi.sh | sudo bash

# One command — fully unattended
curl -fsSL https://raw.githubusercontent.com/chrisroden/tempest-weather-pi-console/main/scripts/pi/install-pi.sh | sudo bash -s -- \
  --mode both \
  --token YOUR_API_TOKEN \
  --station-id YOUR_STATION_ID \
  --device-id YOUR_DEVICE_ID \
  --yes
```

The installer handles everything: downloading binaries, writing config, registering systemd services, and starting the application.

**Prerequisites (complete Part 1 first):**
1. Raspberry Pi OS Lite 64-bit with desktop
2. Display and touchscreen configured
3. Screen blanking and cursor hidden

---

Your Tempest Weather Station should now be fully deployed and running on your Raspberry Pi.