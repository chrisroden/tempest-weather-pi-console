#!/usr/bin/env bash
set -euo pipefail

# deploy-test-artifacts.sh
# Helper to push locally built linux-arm64 self-contained artifacts to a Pi
# for testing changes (CPM, net9 alignment, Avalonia updates, SignalR cleanup, etc.).
#
# IMPORTANT: This script is careful to NEVER wipe user configuration data
# (appsettings.Production.json). It always preserves it during app updates.
#
# Usage:
#   ./scripts/pi/deploy-test-artifacts.sh [user@]host [options]
#
# Examples:
#   ./scripts/pi/deploy-test-artifacts.sh croden@192.168.3.233
#   ./scripts/pi/deploy-test-artifacts.sh pi@tempest-pi.local --mode backend
#
# It will:
#   - scp the tarballs (dist/tempest-*-linux-arm64.tar.gz)
#   - Stop the services on the Pi
#   - Create a timestamped backup under /opt/tempest.bak.* (full safety net)
#   - Safely replace application binaries/assets while preserving configuration
#   - Fix ownership based on the installed systemd unit
#   - Start the services
#   - Print health endpoints and basic status

TARGET="${1:-}"
MODE="both"
INSTALL_ROOT="/opt/tempest"
DIST_DIR="dist"
BACKEND_TAR="tempest-backend-linux-arm64.tar.gz"
UI_TAR="tempest-ui-linux-arm64.tar.gz"
BACKUP_SUFFIX="$(date +%Y%m%d-%H%M%S)"

# Local (Mac-side) logging helpers
color() { printf "\033[%sm%s\033[0m\n" "$1" "$*"; }
info() { color "36" "[INFO] $*"; }
ok()   { color "32" "[ OK ] $*"; }
warn() { color "33" "[WARN] $*"; }

if [[ -z "$TARGET" || "$TARGET" == --* ]]; then
  echo "Usage: $0 [user@]host [--mode backend|ui|both]"
  echo "Example: $0 croden@192.168.3.233"
  exit 1
fi
shift || true

while [[ $# -gt 0 ]]; do
  case "$1" in
    --mode)
      MODE="${2:-both}"; shift 2 ;;
    --install-root)
      INSTALL_ROOT="${2:-/opt/tempest}"; shift 2 ;;
    -h|--help)
      echo "See header for usage."; exit 0 ;;
    *)
      echo "Unknown arg: $1"; exit 1 ;;
  esac
done

# If the dist tarballs don't exist yet, but we have publish/ output from
# `dotnet publish`, auto-create clean tarballs (no macOS xattrs).
ensure_clean_tarball() {
  local src_dir="$1"
  local tar_path="$2"
  local label="$3"

  if [[ -f "$tar_path" ]]; then
    return 0
  fi

  if [[ -d "$src_dir" ]]; then
    info "Creating clean tarball for $label from $src_dir (avoiding macOS xattrs)..."
    mkdir -p "$(dirname "$tar_path")"
    # Try GNU-style --no-xattrs first; fall back to COPYFILE_DISABLE (macOS)
    if (cd "$src_dir" && tar --no-xattrs -czf "$tar_path" .) 2>/dev/null; then
      :
    else
      COPYFILE_DISABLE=1 tar -czf "$tar_path" -C "$src_dir" .
    fi
    ok "Created $tar_path"
  else
    echo "ERROR: Neither tarball $tar_path nor publish dir $src_dir exists."
    echo "Run the dotnet publish steps first (targeting publish/backend/... and publish/ui/...)."
    exit 1
  fi
}

ensure_clean_tarball "publish/backend/linux-arm64" "${DIST_DIR}/${BACKEND_TAR}" "backend"
ensure_clean_tarball "publish/ui/linux-arm64" "${DIST_DIR}/${UI_TAR}" "ui"

echo "==> Target: $TARGET"
echo "==> Mode: $MODE"
echo "==> Install root: $INSTALL_ROOT"
echo "==> Tarballs:"
ls -lh "${DIST_DIR}/${BACKEND_TAR}" "${DIST_DIR}/${UI_TAR}"

# The ensure_clean_tarball function already uses COPYFILE_DISABLE when creating tars.
# This export is kept for any direct tar usage and for the scp side.

# Transfer
echo ""
echo "==> Transferring tarballs to ${TARGET}:/tmp/ ..."
scp -o ConnectTimeout=15 "${DIST_DIR}/${BACKEND_TAR}" "${DIST_DIR}/${UI_TAR}" "${TARGET}:/tmp/"

# Remote deployment
echo ""
echo "==> Running remote deployment on $TARGET ..."
echo "    (If sudo asks for a password, the script will fail because this is non-interactive.)"
echo "    Recommendation: set up passwordless sudo on the Pi for easier deploys."
ssh -o ConnectTimeout=15 "$TARGET" bash -s -- "$MODE" "$INSTALL_ROOT" "$BACKUP_SUFFIX" <<'REMOTE_SCRIPT'
set -euo pipefail

MODE="$1"
INSTALL_ROOT="$2"
BACKUP_SUFFIX="$3"
BACKEND_TAR="/tmp/tempest-backend-linux-arm64.tar.gz"
UI_TAR="/tmp/tempest-ui-linux-arm64.tar.gz"

SUDO=""
if [[ "${EUID}" -ne 0 ]]; then
  SUDO="sudo"
fi

color() { printf "\033[%sm%s\033[0m\n" "$1" "$*"; }
info() { color "36" "[INFO] $*"; }
ok()   { color "32" "[ OK ] $*"; }
warn() { color "33" "[WARN] $*"; }
err()  { color "31" "[ERR ] $*"; }

# Detect service user from installed units (fallback to current user)
detect_user() {
  local unit="$1"
  if [[ -f "/etc/systemd/system/${unit}" ]]; then
    grep -E '^User=' "/etc/systemd/system/${unit}" | head -1 | cut -d= -f2 || echo ""
  else
    echo ""
  fi
}

BACKEND_USER="$(detect_user tempest-backend.service)"
UI_USER="$(detect_user tempest-ui.service)"

if [[ -z "$BACKEND_USER" ]]; then BACKEND_USER="$(id -un)"; fi
if [[ -z "$UI_USER" ]]; then UI_USER="$(id -un)"; fi

info "Backend will run as: $BACKEND_USER"
info "UI will run as: $UI_USER"

# Stop services
info "Stopping services..."
$SUDO systemctl stop tempest-backend.service tempest-ui.service || true
sleep 1

# Backup
BACKUP_DIR="${INSTALL_ROOT}.bak.${BACKUP_SUFFIX}"
if [[ -d "$INSTALL_ROOT" ]]; then
  info "Creating backup: $BACKUP_DIR"
  $SUDO cp -a "$INSTALL_ROOT" "$BACKUP_DIR"
else
  warn "No existing $INSTALL_ROOT found, skipping backup."
  $SUDO mkdir -p "$INSTALL_ROOT"
fi

# Ensure target dirs
$SUDO mkdir -p "${INSTALL_ROOT}/backend" "${INSTALL_ROOT}/ui"

deploy_component() {
  local name="$1" tarball="$2" dest="$3" owner="$4"

  if [[ "$MODE" != "both" && "$MODE" != "$name" ]]; then
    info "Skipping $name (mode=$MODE)"
    return
  fi

  info "Deploying $name to $dest ..."

  local tmpdir
  tmpdir="$(mktemp -d)"
  $SUDO tar -xzf "$tarball" -C "$tmpdir"

  # Protect user configuration at all costs. Never let a deployment
  # wipe appsettings.Production.json (contains tokens, station IDs, etc.).
  local config_file="appsettings.Production.json"
  local config_backup=""
  if [[ -f "${dest}/${config_file}" ]]; then
    config_backup="$(mktemp)"
    $SUDO cp "${dest}/${config_file}" "$config_backup"
    info "Preserving existing ${config_file} (will be restored after update)"
  fi

  # Safest replace: use rsync with --delete (removes stale app files)
  # but explicitly exclude the production config so it is never touched.
  if command -v rsync >/dev/null 2>&1; then
    $SUDO rsync -a --delete --exclude "${config_file}" "$tmpdir/" "$dest/"
  else
    warn "rsync not available — falling back to rm+cp (config will be restored afterwards)"
    $SUDO rm -rf "${dest:?}"/*
    $SUDO cp -a "$tmpdir"/* "$dest"/
  fi

  # Restore the config we saved earlier (defensive — rsync should have left it alone).
  if [[ -n "$config_backup" ]]; then
    $SUDO cp "$config_backup" "${dest}/${config_file}"
    rm -f "$config_backup"
  fi

  $SUDO chown -R "$owner:$owner" "$dest"
  rm -rf "$tmpdir"
  ok "$name deployed"
}

deploy_component "backend" "$BACKEND_TAR" "${INSTALL_ROOT}/backend" "$BACKEND_USER"
deploy_component "ui"     "$UI_TAR"     "${INSTALL_ROOT}/ui"     "$UI_USER"

# Reload + start
info "Reloading systemd and starting services..."
$SUDO systemctl daemon-reload || true
$SUDO systemctl start tempest-backend.service

# Give backend a moment to come up before starting UI
sleep 3
if [[ "$MODE" == "both" || "$MODE" == "ui" ]]; then
  $SUDO systemctl start tempest-ui.service
fi

sleep 2

echo ""
ok "Services started. Current status:"
$SUDO systemctl --no-pager status tempest-backend.service tempest-ui.service | cat || true

echo ""
info "Health check (backend):"
curl -s --max-time 5 "http://localhost:5000/health" || echo "(curl failed or backend not ready yet)"
echo ""
info "Health details:"
curl -s --max-time 5 "http://localhost:5000/health/details" || echo "(not ready)"

echo ""
ok "Deployment complete."
echo "Tail logs with:"
echo "  sudo journalctl -u tempest-backend -f"
echo "  sudo journalctl -u tempest-ui -f"
echo ""
echo "Run smoke test (if the repo/scripts are on the Pi):"
echo "  ./scripts/pi/smoke-test-pi.sh --mode $MODE"
REMOTE_SCRIPT

echo ""
ok "Local deployment helper finished."
echo "Check the output above for any errors from the Pi."
echo "If services are running, you can now run smoke tests on the Pi or check the UI on the screen."