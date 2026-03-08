# Tempest Weather Station - Complete Raspberry Pi Setup Guide

> Quick start: for most installs, use the interactive installer documented in `Wiki/Install-on-Raspberry-Pi.md`.

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

## Part 2: Build and Deploy the Application

### 1. Build for Raspberry Pi (ARM/Linux)

**On Mac/Linux:**

```bash
# Configure these variables for your environment
PROJECT_PATH="/path/to/Tempest Weather Station Console"
PI_USER="pi"
PI_HOST="raspberrypi.local"
BACKEND_DEPLOY="$HOME/tempest-backend-deploy"
UI_DEPLOY="$HOME/tempest-ui-deploy"

# Build the applications
cd "$PROJECT_PATH"

# Run core parser and model safety tests before publishing
dotnet test Tempest.WebSocket.Tests/Tempest.WebSocket.Tests.csproj -c Release

# Build the Blazor backend
dotnet publish TempestBlazorApp/TempestBlazorApp.csproj -c Release -r linux-arm64 --self-contained -o "$BACKEND_DEPLOY"

# Build the Avalonia UI
dotnet publish Tempest.UI/Tempest.UI.csproj -c Release -r linux-arm64 --self-contained -o "$UI_DEPLOY"
```

**On Windows:**

```powershell
# Configure these variables for your environment
$ProjectPath = "C:\path\to\Tempest Weather Station Console"
$PiUser = "pi"
$PiHost = "raspberrypi.local"
$BackendDeploy = "$env:USERPROFILE\tempest-backend-deploy"
$UiDeploy = "$env:USERPROFILE\tempest-ui-deploy"

# Build the applications
cd $ProjectPath

# Run core parser and model safety tests before publishing
dotnet test Tempest.WebSocket.Tests/Tempest.WebSocket.Tests.csproj -c Release

# Build the Blazor backend
dotnet publish TempestBlazorApp/TempestBlazorApp.csproj -c Release -r linux-arm64 --self-contained -o $BackendDeploy

# Build the Avalonia UI
dotnet publish Tempest.UI/Tempest.UI.csproj -c Release -r linux-arm64 --self-contained -o $UiDeploy
```

### 2. Create Directories on Raspberry Pi

SSH into your Pi and create the necessary directories:

```bash
ssh pi@raspberrypi.local
mkdir -p ~/tempest-backend/linux-arm64 ~/tempest-ui/linux-arm64 ~/.config/autostart
```

### 3. Copy Files to Raspberry Pi

**On Mac/Linux:**

Use rsync to copy the built files efficiently:

```bash
# Use the variables from the build step above, or define them again:
# PI_USER="pi"
# PI_HOST="raspberrypi.local"
# BACKEND_DEPLOY="$HOME/tempest-backend-deploy"
# UI_DEPLOY="$HOME/tempest-ui-deploy"

rsync -av "$BACKEND_DEPLOY/" "${PI_USER}@${PI_HOST}:~/tempest-backend/linux-arm64/"
rsync -av --exclude 'appsettings.Production.json' "$UI_DEPLOY/" "${PI_USER}@${PI_HOST}:~/tempest-ui/linux-arm64/"
```

**On Windows:**

Use scp or a tool like WinSCP. With PowerShell scp:

```powershell
# Use the variables from the build step above, or define them again:
# $PiUser = "pi"
# $PiHost = "raspberrypi.local"
# $BackendDeploy = "$env:USERPROFILE\tempest-backend-deploy"
# $UiDeploy = "$env:USERPROFILE\tempest-ui-deploy"

scp -r "${BackendDeploy}\*" "${PiUser}@${PiHost}:~/tempest-backend/linux-arm64/"
scp -r "${UiDeploy}\*" "${PiUser}@${PiHost}:~/tempest-ui/linux-arm64/"

# Note: scp cannot exclude files. Re-copy your Pi-specific appsettings.Production.json afterward if needed.
```

### 4. Make Executables Runnable

SSH into your Pi and set permissions:

```bash
ssh pi@raspberrypi.local "chmod +x ~/tempest-backend/linux-arm64/TempestBlazorApp ~/tempest-ui/linux-arm64/Tempest.UI"
```

## Part 3: Configure Auto-Start Services

### 1. Create Startup Scripts

The application uses simple bash scripts to start the services. These scripts handle background execution with proper logging.

SSH into your Pi and create the backend startup script:

```bash
ssh pi@raspberrypi.local
cat > ~/tempest-backend/start-tempest-backend.sh << 'EOF'
#!/bin/bash
cd ~/tempest-backend/linux-arm64
nohup ./TempestBlazorApp --urls http://0.0.0.0:5000 > ~/tempest-backend.log 2>&1 &
EOF
chmod +x ~/tempest-backend/start-tempest-backend.sh
```

Create the UI restart script:

```bash
cat > ~/tempest-ui/restart-tempest-ui.sh << 'EOF'
#!/bin/bash
pkill -9 -f Tempest.UI
sleep 2
cd ~/tempest-ui/linux-arm64
DISPLAY=:0 XAUTHORITY=~/.Xauthority nohup ./Tempest.UI > ~/tempest-ui.log 2>&1 &
EOF
chmod +x ~/tempest-ui/restart-tempest-ui.sh
```

Create a single launcher script and a Raspberry Pi OS menu item (so you can relaunch everything after exiting UI):

```bash
cat > ~/tempest-ui/launch-tempest.sh << 'EOF'
#!/bin/bash
bash ~/tempest-backend/start-tempest-backend.sh
sleep 3
bash ~/tempest-ui/restart-tempest-ui.sh
EOF
chmod +x ~/tempest-ui/launch-tempest.sh

mkdir -p ~/.local/share/applications
cat > ~/.local/share/applications/tempest-launch.desktop << 'EOF'
[Desktop Entry]
Type=Application
Name=Tempest Launch
Comment=Start Tempest backend and UI
Exec=/home/pi/tempest-ui/launch-tempest.sh
Icon=utilities-terminal
Terminal=false
Categories=Utility;
EOF
```

If your Pi username is not `pi`, replace `/home/pi` in `Exec=` with your user home path.

### 2. Set Up XDG Autostart

Create autostart desktop entries for both services. These will launch automatically when the desktop session starts:

**Backend Autostart:**

```bash
cat > ~/.config/autostart/tempest-backend.desktop << 'EOF'
[Desktop Entry]
Type=Application
Name=Tempest Backend
Exec=/home/pi/tempest-backend/start-tempest-backend.sh
Hidden=false
NoDisplay=false
X-GNOME-Autostart-enabled=true
EOF
```

**UI Autostart:**

```bash
cat > ~/.config/autostart/tempest-ui.desktop << 'EOF'
[Desktop Entry]
Type=Application
Name=Tempest UI
Exec=/home/pi/tempest-ui/linux-arm64/Tempest.UI
Hidden=false
NoDisplay=false
X-GNOME-Autostart-enabled=true
EOF
```

### 3. Start Services Manually (First Time)

Before rebooting, test the services manually:

```bash
# Start backend
bash ~/tempest-backend/start-tempest-backend.sh

# Wait a few seconds for backend to initialize
sleep 3

# Verify backend is running
curl http://localhost:5000/health

# Start UI from the Pi's desktop terminal
cd ~/tempest-ui/linux-arm64 && ./Tempest.UI
```

The UI should appear on the screen. If you see "XOpenDisplay failed", you need to run it from a terminal on the Pi itself (not via SSH).

```bash
sudo reboot
```

After reboot:
- Both services should auto-start via XDG autostart
- The backend will run in the background (check with `pgrep -f TempestBlazorApp`)
- The UI will launch on the desktop in fullscreen mode
- The Restart button in the UI can restart both services without requiring a reboot

## Part 4: Testing and Verification

### 1. Test Backend Service

```bash
# Check if backend is running
pgrep -f TempestBlazorApp

# View backend logs
tail -f ~/tempest-backend.log

# Test health endpoint
curl http://localhost:5000/health

# Test detailed diagnostics endpoint
curl http://localhost:5000/health/details

# Stop the backend (if needed)
pkill -f TempestBlazorApp

# Restart the backend
bash ~/tempest-backend/start-tempest-backend.sh
```

### 1a. Health Verification Checklist (Post-Deploy)

Run these commands after deployment and confirm expected results:

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

### 2. Test UI

To relaunch both backend and UI from the Pi desktop menu after exiting, use **Menu → Accessories (or Utilities) → Tempest Launch**.

Check if UI is running:

```bash
pgrep -f Tempest.UI

# View UI logs
tail -f ~/tempest-ui.log

# Stop UI (if needed)
pkill -f Tempest.UI

# Restart UI from terminal on the Pi
cd ~/tempest-ui/linux-arm64 && ./Tempest.UI
```

### 3. Test Restart Button

The UI includes a Restart button (red) that will:
1. Show a red connection indicator immediately
2. Kill and restart the backend using the startup script
3. Wait for backend health check to pass
4. Kill and restart the UI using the restart script
5. Show orange status messages during the process
6. Show red error messages if restart fails

To test:
- Click the Restart button in the UI
- Watch the status banner and connection indicator
- Both services should restart within 15-20 seconds

### 4. Reboot and Verify Auto-Start

```bash
sudo reboot
```

After reboot:
- The backend service should start automatically
- The UI should launch when the desktop loads
- The display should show the weather station in fullscreen mode

### 4. Check Processes

```bash
# Check if backend is running
ps aux | grep TempestBlazorApp | grep -v grep

# Check if UI is running
ps aux | grep Tempest.UI | grep -v grep
```

## Part 5: Configuration Notes

### Station Configuration

The application requires configuration with your WeatherFlow Tempest station details.

See [CONFIGURATION.md](CONFIGURATION.md) for complete setup instructions on how to:
- Get your API token from WeatherFlow
- Find your Station ID and Device ID
- Configure both backend and UI with your credentials

Configuration is stored in `appsettings.Production.json` files (not committed to git for security).

### Network Requirements

- Backend runs on `http://0.0.0.0:5000` (accessible from all interfaces)
- Backend provides SignalR hub at `/weatherhub`
- Backend health check endpoint at `/health`
- Backend diagnostics endpoint at `/health/details` (includes reconnect counters and last successful broadcast timestamp)
- UI connects to `http://localhost:5000` via SignalR for real-time updates
- Backend connects to WeatherFlow API for data
- Restart functionality uses scripts to restart both backend and UI

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
# Check if it's running
pgrep -f TempestBlazorApp

# Check logs
tail -50 ~/tempest-backend.log

# Check permissions
ls -la ~/tempest-backend/linux-arm64/TempestBlazorApp

# Check .NET runtime
dotnet --info

# Manually start to see errors
cd ~/tempest-backend/linux-arm64
./TempestBlazorApp --urls http://0.0.0.0:5000
```

**Backend crashes or restarts:**
```bash
# View full logs
tail -100 ~/tempest-backend.log

# Check for port conflicts
sudo netstat -tlnp | grep 5000

# Verify --urls parameter
ps aux | grep TempestBlazorApp
```

**Restart button fails:**
- Check that startup scripts exist and are executable:
  - `~/tempest-backend/start-tempest-backend.sh`
  - `~/tempest-ui/restart-tempest-ui.sh`
- Verify scripts have correct paths (using your home directory `/home/your-username/`)
- Check logs for error messages during restart attempt
- Backend must start with `--urls http://0.0.0.0:5000` parameter

### UI Issues

**UI doesn't appear on screen:**
```bash
# Check if running
pgrep -f Tempest.UI

# Check UI logs
tail -50 ~/tempest-ui.log

# Check .xsession-errors
tail -50 ~/.xsession-errors

# Verify DISPLAY variable (from desktop terminal)
echo $DISPLAY

# Try manual start from desktop terminal
cd ~/tempest-ui/linux-arm64 && ./Tempest.UI
```

**XOpenDisplay failed error:**
- This means X11 display access is not available
- Cannot start UI via SSH without X forwarding
- Must start from a terminal on the Pi's desktop, OR
- Use the autostart mechanism which has proper display access
- Reboot the Pi to trigger autostart

**UI shows but no data:**
- Check backend is running: `pgrep -f TempestBlazorApp`
- Test health endpoint: `curl http://localhost:5000/health`
- Test diagnostics endpoint: `curl http://localhost:5000/health/details`
- Verify SignalR connection in backend logs: `tail -f ~/tempest-backend.log`
- Check network connectivity to WeatherFlow API

**Icons or images not showing:**
- Verify Assets folder exists and was deployed: `ls ~/tempest-ui/linux-arm64/Assets/`
- Ensure all PNG files are present (47 weather and icon files)
- Check file permissions: `chmod -R 644 ~/tempest-ui/linux-arm64/Assets/*`
- Assets must be in the `linux-arm64` deployment directory, not the parent

**Connection indicator stuck red:**
- Backend may not be running: `pgrep -f TempestBlazorApp`
- Check backend health: `curl http://localhost:5000/health`
- Check backend diagnostics: `curl http://localhost:5000/health/details`
- SignalR connection may be broken - check both logs
- Try the Restart button to restart both services

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

### To Deploy Updates

**On Mac/Linux:**

```bash
# Configure these variables for your environment
PROJECT_PATH="/path/to/Tempest Weather Station Console"
PI_USER="pi"
PI_HOST="raspberrypi.local"
BACKEND_DEPLOY="$HOME/tempest-backend-deploy"
UI_DEPLOY="$HOME/tempest-ui-deploy"

# Rebuild
cd "$PROJECT_PATH"
dotnet publish TempestBlazorApp/TempestBlazorApp.csproj -c Release -r linux-arm64 --self-contained -o "$BACKEND_DEPLOY"
dotnet publish Tempest.UI/Tempest.UI.csproj -c Release -r linux-arm64 --self-contained -o "$UI_DEPLOY"
```

**On Windows:**

```powershell
# Configure these variables for your environment
$ProjectPath = "C:\path\to\Tempest Weather Station Console"
$PiUser = "pi"
$PiHost = "raspberrypi.local"
$BackendDeploy = "$env:USERPROFILE\tempest-backend-deploy"
$UiDeploy = "$env:USERPROFILE\tempest-ui-deploy"

# Rebuild
cd $ProjectPath
dotnet publish TempestBlazorApp/TempestBlazorApp.csproj -c Release -r linux-arm64 --self-contained -o $BackendDeploy
dotnet publish Tempest.UI/Tempest.UI.csproj -c Release -r linux-arm64 --self-contained -o $UiDeploy
```

**Stop services and copy files:**

**On Mac/Linux:**

```bash
# Stop services on Pi
ssh "${PI_USER}@${PI_HOST}" "pkill -f TempestBlazorApp && pkill -f Tempest.UI"

# Copy updated files
rsync -av "$BACKEND_DEPLOY/" "${PI_USER}@${PI_HOST}:~/tempest-backend/linux-arm64/"
rsync -av --exclude 'appsettings.Production.json' "$UI_DEPLOY/" "${PI_USER}@${PI_HOST}:~/tempest-ui/linux-arm64/"

# Reboot to restart with new code
ssh "${PI_USER}@${PI_HOST}" "sudo reboot"
```

**On Windows:**

```powershell
# Stop services on Pi
ssh "${PiUser}@${PiHost}" "pkill -f TempestBlazorApp && pkill -f Tempest.UI"

# Copy updated files
scp -r "${BackendDeploy}\*" "${PiUser}@${PiHost}:~/tempest-backend/linux-arm64/"
scp -r "${UiDeploy}\*" "${PiUser}@${PiHost}:~/tempest-ui/linux-arm64/"

# Reboot to restart with new code
ssh "${PiUser}@${PiHost}" "sudo reboot"
```

### To Update Just the UI

If you only changed UI code:

**On Mac/Linux:**

```bash
# Configure these variables for your environment
PROJECT_PATH="/path/to/Tempest Weather Station Console"
PI_USER="pi"
PI_HOST="raspberrypi.local"
UI_DEPLOY="$HOME/tempest-ui-deploy"

# Rebuild UI
cd "$PROJECT_PATH"
dotnet publish Tempest.UI/Tempest.UI.csproj -c Release -r linux-arm64 --self-contained -o "$UI_DEPLOY"

# Stop UI on Pi
ssh "${PI_USER}@${PI_HOST}" "pkill -f Tempest.UI"

# Copy just the UI binary and DLL
rsync -av "$UI_DEPLOY/Tempest.UI" "$UI_DEPLOY/Tempest.UI.dll" "$UI_DEPLOY/Tempest.UI.pdb" "${PI_USER}@${PI_HOST}:~/tempest-ui/linux-arm64/"

# Restart UI from Pi's desktop terminal or reboot
```

This UI-only update method preserves your Pi-specific `appsettings.Production.json` (including `Ui:SelectedTheme`) because it copies only selected binaries.

**On Windows:**

```powershell
# Configure these variables for your environment
$ProjectPath = "C:\path\to\Tempest Weather Station Console"
$PiUser = "pi"
$PiHost = "raspberrypi.local"
$UiDeploy = "$env:USERPROFILE\tempest-ui-deploy"

# Rebuild UI
cd $ProjectPath
dotnet publish Tempest.UI/Tempest.UI.csproj -c Release -r linux-arm64 --self-contained -o $UiDeploy

# Stop UI on Pi
ssh "${PiUser}@${PiHost}" "pkill -f Tempest.UI"

# Copy just the UI binary and DLL
scp "${UiDeploy}\Tempest.UI" "${UiDeploy}\Tempest.UI.dll" "${UiDeploy}\Tempest.UI.pdb" "${PiUser}@${PiHost}:~/tempest-ui/linux-arm64/"

# Restart UI from Pi's desktop terminal or reboot
```

### To Update Assets Only

**On Mac/Linux:**

```bash
# Configure these variables for your environment
PROJECT_PATH="/path/to/Tempest Weather Station Console"
PI_USER="pi"
PI_HOST="raspberrypi.local"
UI_DEPLOY="$HOME/tempest-ui-deploy"

# Rebuild to get latest assets (they're included in publish output)
cd "$PROJECT_PATH"
dotnet publish Tempest.UI/Tempest.UI.csproj -c Release -r linux-arm64 --self-contained -o "$UI_DEPLOY"

# Copy just the Assets folder
rsync -av "$UI_DEPLOY/Assets/" "${PI_USER}@${PI_HOST}:~/tempest-ui/linux-arm64/Assets/"

# Restart UI using the Restart button, or:
ssh "${PI_USER}@${PI_HOST}" "pkill -f Tempest.UI"
# Then start from Pi's desktop terminal
```

**On Windows:**

```powershell
# Configure these variables for your environment
$ProjectPath = "C:\path\to\Tempest Weather Station Console"
$PiUser = "pi"
$PiHost = "raspberrypi.local"
$UiDeploy = "$env:USERPROFILE\tempest-ui-deploy"

# Rebuild to get latest assets (they're included in publish output)
cd $ProjectPath
dotnet publish Tempest.UI/Tempest.UI.csproj -c Release -r linux-arm64 --self-contained -o $UiDeploy

# Copy just the Assets folder
scp -r "${UiDeploy}\Assets\*" "${PiUser}@${PiHost}:~/tempest-ui/linux-arm64/Assets/"

# Restart UI using the Restart button, or:
ssh "${PiUser}@${PiHost}" "pkill -f Tempest.UI"
# Then start from Pi's desktop terminal
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

### Backup

```bash
# Backup the entire deployment
ssh pi@raspberrypi.local "tar -czf tempest-backup.tar.gz ~/tempest-backend ~/tempest-ui ~/.config/autostart/tempest-ui.desktop"
scp pi@raspberrypi.local:~/tempest-backup.tar.gz ./
```

### Multiple Deployments

To deploy to multiple Raspberry Pis:

```bash
# Pi 1 (hostname: pi1.local or IP: 192.168.1.100)
rsync -av ~/tempest-backend-deploy/ pi@pi1.local:~/tempest-backend/linux-arm64/
rsync -av ~/tempest-ui-deploy/ pi@pi1.local:~/tempest-ui/linux-arm64/

# Pi 2 (hostname: pi2.local or IP: 192.168.1.101)
rsync -av ~/tempest-backend-deploy/ pi@pi2.local:~/tempest-backend/linux-arm64/
rsync -av ~/tempest-ui-deploy/ pi@pi2.local:~/tempest-ui/linux-arm64/

# Configure scripts and autostart on each Pi as described in Part 3
```

## Complete Example - Fresh Deployment

Here's a complete script for deploying to a fresh Pi. This assumes you've already configured the Pi with .NET runtime and display settings (Part 1).

```bash
#!/bin/bash

# Configuration - ADJUST THESE FOR YOUR PI
PI_USER="pi"  # Your Pi username
PI_HOST="192.168.1.100"  # Your Pi's IP address or hostname
PROJECT_PATH="/path/to/Tempest Weather Station Console"  # Path to your project
HOME_DIR="/home/${PI_USER}"

# Build applications
cd "$PROJECT_PATH"
echo "Building backend..."
dotnet publish TempestBlazorApp/TempestBlazorApp.csproj -c Release -r linux-arm64 --self-contained -o ~/tempest-backend-deploy

echo "Building UI..."
dotnet publish Tempest.UI/Tempest.UI.csproj -c Release -r linux-arm64 --self-contained -o ~/tempest-ui-deploy

# Create directories on Pi
echo "Creating directories..."
ssh ${PI_USER}@${PI_HOST} "mkdir -p ~/tempest-backend/linux-arm64 ~/tempest-ui/linux-arm64 ~/.config/autostart"

# Copy files
echo "Copying backend..."
rsync -av ~/tempest-backend-deploy/ ${PI_USER}@${PI_HOST}:~/tempest-backend/linux-arm64/

echo "Copying UI..."
rsync -av ~/tempest-ui-deploy/ ${PI_USER}@${PI_HOST}:~/tempest-ui/linux-arm64/

# Set permissions
echo "Setting permissions..."
ssh ${PI_USER}@${PI_HOST} "chmod +x ~/tempest-backend/linux-arm64/TempestBlazorApp ~/tempest-ui/linux-arm64/Tempest.UI"

# Create backend startup script
echo "Creating backend startup script..."
ssh ${PI_USER}@${PI_HOST} "cat > ~/tempest-backend/start-tempest-backend.sh << 'EOF'
#!/bin/bash
cd ~/tempest-backend/linux-arm64
nohup ./TempestBlazorApp --urls http://0.0.0.0:5000 > ~/tempest-backend.log 2>&1 &
EOF
chmod +x ~/tempest-backend/start-tempest-backend.sh"

# Create UI restart script
echo "Creating UI restart script..."
ssh ${PI_USER}@${PI_HOST} "cat > ~/tempest-ui/restart-tempest-ui.sh << 'EOF'
#!/bin/bash
pkill -f Tempest.UI
sleep 2
cd ~/tempest-ui/linux-arm64
DISPLAY=:0 XAUTHORITY=~/.Xauthority nohup ./Tempest.UI > ~/tempest-ui.log 2>&1 &
EOF
chmod +x ~/tempest-ui/restart-tempest-ui.sh"

# Create combined launcher script
echo "Creating combined launcher script..."
ssh ${PI_USER}@${PI_HOST} "cat > ~/tempest-ui/launch-tempest.sh << 'EOF'
#!/bin/bash
bash ~/tempest-backend/start-tempest-backend.sh
sleep 3
bash ~/tempest-ui/restart-tempest-ui.sh
EOF
chmod +x ~/tempest-ui/launch-tempest.sh"

# Create Pi menu launcher item
echo "Creating Pi menu launcher item..."
ssh ${PI_USER}@${PI_HOST} "mkdir -p ~/.local/share/applications && cat > ~/.local/share/applications/tempest-launch.desktop << EOF
[Desktop Entry]
Type=Application
Name=Tempest Launch
Comment=Start Tempest backend and UI
Exec=${HOME_DIR}/tempest-ui/launch-tempest.sh
Icon=utilities-terminal
Terminal=false
Categories=Utility;
EOF"

# Create backend autostart
echo "Creating backend autostart..."
ssh ${PI_USER}@${PI_HOST} "cat > ~/.config/autostart/tempest-backend.desktop << EOF
[Desktop Entry]
Type=Application
Name=Tempest Backend
Exec=${HOME_DIR}/tempest-backend/start-tempest-backend.sh
Hidden=false
NoDisplay=false
X-GNOME-Autostart-enabled=true
EOF"

# Create UI autostart
echo "Creating UI autostart..."
ssh ${PI_USER}@${PI_HOST} "cat > ~/.config/autostart/tempest-ui.desktop << EOF
[Desktop Entry]
Type=Application
Name=Tempest UI
Exec=${HOME_DIR}/tempest-ui/linux-arm64/Tempest.UI
Hidden=false
NoDisplay=false
X-GNOME-Autostart-enabled=true
EOF"

echo "Deployment complete! Rebooting Pi..."
ssh ${PI_USER}@${PI_HOST} "sudo reboot"

echo ""
echo "After reboot, both services should start automatically."
echo "Backend will be available at http://${PI_HOST}:5000"
echo "UI will display on the Pi's screen."
```

Save this as `deploy.sh`, make it executable (`chmod +x deploy.sh`), and run it to deploy everything automatically.

**Note:** This script assumes you've already:
1. Configured the Pi with .NET runtime (see Part 1, Step 2)
2. Set up display settings if using a touchscreen (see Part 1, Step 3)
3. Configured `appsettings.Production.json` files with your WeatherFlow credentials

---

Your Tempest Weather Station should now be fully deployed and running on your Raspberry Pi! 🌤️