#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "${SCRIPT_DIR}/../.." && pwd)"

SUDO=""
if [[ "${EUID}" -ne 0 ]]; then
  SUDO="sudo"
fi

color() {
  local code="$1"
  shift
  printf "\033[%sm%s\033[0m\n" "${code}" "$*"
}

info() { color "36" "[INFO] $*"; }
warn() { color "33" "[WARN] $*"; }
ok() { color "32" "[ OK ] $*"; }
err() { color "31" "[ERR ] $*"; }

run_with_heartbeat() {
  local label="$1"
  shift

  info "${label}"
  "$@" &
  local pid=$!

  while kill -0 "${pid}" >/dev/null 2>&1; do
    printf "."
    sleep 2
  done
  printf "\n"

  wait "${pid}"
}

MODE=""
INSTALL_ROOT=""
SERVICE_USER=""
BACKEND_SOURCE=""
UI_SOURCE=""
WEATHERFLOW_API_TOKEN=""
WEATHERFLOW_STATION_ID=""
WEATHERFLOW_DEVICE_ID=""
BACKEND_PORT=""
BACKEND_URL=""
HEALTH_STALE_SECONDS=""
UI_THEME=""
ENABLE_AT_BOOT=""
CONFIG_FILE=""
WRITE_CONFIG_FILE=""
AUTO_YES="no"
DRY_RUN="no"

usage() {
  cat <<'EOF'
Usage: install-pi.sh [options]

Options:
  --mode <backend|ui|both>
  --install-root <path>
  --service-user <user>
  --backend-source <path>
  --ui-source <path>
  --token <weatherflow-api-token>
  --station-id <number>
  --device-id <number>
  --port <number>
  --backend-url <url>
  --stale-threshold-seconds <number>
  --theme <name>
  --enable-at-boot <yes|no>
  --config <env-file>
  --write-config <env-file>
  --yes
  --dry-run
  -h, --help

When an option is omitted, the script prompts interactively.
EOF
}

normalize_yes_no() {
  local raw="${1:-}"
  raw="${raw,,}"
  case "${raw}" in
    y|yes|true|1|on) printf "yes" ;;
    n|no|false|0|off) printf "no" ;;
    *) printf "" ;;
  esac
}

parse_mode() {
  local raw="${1:-}"
  raw="${raw,,}"
  case "${raw}" in
    1|backend) printf "backend" ;;
    2|ui) printf "ui" ;;
    3|both) printf "both" ;;
    *) printf "" ;;
  esac
}

parse_args() {
  while [[ $# -gt 0 ]]; do
    case "$1" in
      --mode) MODE="${2:-}"; shift 2 ;;
      --install-root) INSTALL_ROOT="${2:-}"; shift 2 ;;
      --service-user) SERVICE_USER="${2:-}"; shift 2 ;;
      --backend-source) BACKEND_SOURCE="${2:-}"; shift 2 ;;
      --ui-source) UI_SOURCE="${2:-}"; shift 2 ;;
      --token) WEATHERFLOW_API_TOKEN="${2:-}"; shift 2 ;;
      --station-id) WEATHERFLOW_STATION_ID="${2:-}"; shift 2 ;;
      --device-id) WEATHERFLOW_DEVICE_ID="${2:-}"; shift 2 ;;
      --port) BACKEND_PORT="${2:-}"; shift 2 ;;
      --backend-url) BACKEND_URL="${2:-}"; shift 2 ;;
      --stale-threshold-seconds) HEALTH_STALE_SECONDS="${2:-}"; shift 2 ;;
      --theme) UI_THEME="${2:-}"; shift 2 ;;
      --enable-at-boot) ENABLE_AT_BOOT="${2:-}"; shift 2 ;;
      --config) CONFIG_FILE="${2:-}"; shift 2 ;;
      --write-config) WRITE_CONFIG_FILE="${2:-}"; shift 2 ;;
      --yes) AUTO_YES="yes"; shift ;;
      --dry-run) DRY_RUN="yes"; shift ;;
      -h|--help) usage; exit 0 ;;
      *)
        err "Unknown argument: $1"
        usage
        exit 1
        ;;
    esac
  done
}

load_config_file() {
  local file_path="$1"
  if [[ -z "${file_path}" ]]; then
    return
  fi
  if [[ ! -f "${file_path}" ]]; then
    err "Config file not found: ${file_path}"
    exit 1
  fi

  # shellcheck disable=SC1090
  source "${file_path}"

  MODE="${MODE:-${INSTALL_MODE:-}}"
  INSTALL_ROOT="${INSTALL_ROOT:-}"
  SERVICE_USER="${SERVICE_USER:-}"
  BACKEND_SOURCE="${BACKEND_SOURCE:-}"
  UI_SOURCE="${UI_SOURCE:-}"
  WEATHERFLOW_API_TOKEN="${WEATHERFLOW_API_TOKEN:-}"
  WEATHERFLOW_STATION_ID="${WEATHERFLOW_STATION_ID:-}"
  WEATHERFLOW_DEVICE_ID="${WEATHERFLOW_DEVICE_ID:-}"
  BACKEND_PORT="${BACKEND_PORT:-}"
  BACKEND_URL="${BACKEND_URL:-}"
  HEALTH_STALE_SECONDS="${HEALTH_STALE_SECONDS:-}"
  UI_THEME="${UI_THEME:-}"
  ENABLE_AT_BOOT="${ENABLE_AT_BOOT:-}"
}

save_config_file() {
  local out_file="$1"
  local tmp_file
  tmp_file="$(mktemp)"

  cat >"${tmp_file}" <<EOF
# Tempest Pi installer config
# Keep this file private (contains API token).

INSTALL_MODE='${MODE}'
INSTALL_ROOT='${INSTALL_ROOT}'
SERVICE_USER='${SERVICE_USER}'
BACKEND_SOURCE='${BACKEND_SOURCE}'
UI_SOURCE='${UI_SOURCE}'
WEATHERFLOW_API_TOKEN='${WEATHERFLOW_API_TOKEN}'
WEATHERFLOW_STATION_ID='${WEATHERFLOW_STATION_ID}'
WEATHERFLOW_DEVICE_ID='${WEATHERFLOW_DEVICE_ID}'
BACKEND_PORT='${BACKEND_PORT}'
BACKEND_URL='${BACKEND_URL}'
HEALTH_STALE_SECONDS='${HEALTH_STALE_SECONDS}'
UI_THEME='${UI_THEME}'
ENABLE_AT_BOOT='${ENABLE_AT_BOOT}'
EOF

  install -m 600 "${tmp_file}" "${out_file}"
  rm -f "${tmp_file}"
  ok "Wrote config file: ${out_file}"
}

require_command() {
  local cmd="$1"
  if ! command -v "${cmd}" >/dev/null 2>&1; then
    err "Missing required command: ${cmd}"
    exit 1
  fi
}

prompt_default() {
  local prompt="$1"
  local default_val="$2"
  local out
  read -r -p "${prompt} [${default_val}]: " out
  if [[ -z "${out}" ]]; then
    printf "%s" "${default_val}"
  else
    printf "%s" "${out}"
  fi
}

prompt_secret() {
  local prompt="$1"
  local out
  read -r -s -p "${prompt}: " out
  printf "\n" >&2
  printf "%s" "${out}"
}

require_non_empty() {
  local label="$1"
  local val="$2"
  if [[ -z "${val}" ]]; then
    err "${label} cannot be empty."
    exit 1
  fi
}

prompt_yes_no() {
  local prompt="$1"
  local default_yes="$2"
  local raw
  local hint="y/N"
  if [[ "${default_yes}" == "yes" ]]; then
    hint="Y/n"
  fi

  while true; do
    read -r -p "${prompt} (${hint}): " raw
    raw="${raw,,}"
    if [[ -z "${raw}" ]]; then
      [[ "${default_yes}" == "yes" ]] && return 0 || return 1
    fi
    case "${raw}" in
      y|yes) return 0 ;;
      n|no) return 1 ;;
      *) warn "Please answer y or n." ;;
    esac
  done
}

detect_platform() {
  if [[ ! -f /etc/os-release ]]; then
    err "Cannot detect OS: /etc/os-release not found"
    exit 1
  fi

  # shellcheck disable=SC1091
  source /etc/os-release

  OS_ID="${ID:-unknown}"
  OS_PRETTY="${PRETTY_NAME:-unknown}"
  OS_CODENAME="${VERSION_CODENAME:-unknown}"
  ARCH="$(dpkg --print-architecture 2>/dev/null || uname -m)"

  PI_MODEL="unknown"
  if [[ -f /proc/device-tree/model ]]; then
    PI_MODEL="$(tr -d '\0' </proc/device-tree/model)"
  fi

  HAS_DESKTOP="false"
  if command -v startx >/dev/null 2>&1 || dpkg -l 2>/dev/null | grep -qE 'raspberrypi-ui-mods|lxsession|xserver-xorg'; then
    HAS_DESKTOP="true"
  fi

  OS_TRACK="current"
  case "${OS_CODENAME}" in
    bookworm|bullseye|buster) OS_TRACK="legacy" ;;
    trixie) OS_TRACK="current" ;;
  esac

  if [[ "${OS_ID}" != "raspbian" && "${OS_ID}" != "debian" ]]; then
    warn "This installer is tuned for Raspberry Pi OS (Debian-based). Detected ID=${OS_ID}."
  fi
}

choose_mode() {
  local default_mode="both"
  if [[ "${HAS_DESKTOP}" != "true" ]]; then
    default_mode="backend"
  fi

  printf "\nInstall mode:\n"
  printf "  1) backend only\n"
  printf "  2) ui only\n"
  printf "  3) both backend + ui\n"

  while true; do
    local choice
    read -r -p "Choose mode [default=${default_mode}]: " choice
    if [[ -z "${choice}" ]]; then
      MODE="${default_mode}"
      return
    fi
    case "${choice}" in
      1|backend) MODE="backend"; return ;;
      2|ui) MODE="ui"; return ;;
      3|both) MODE="both"; return ;;
      *) warn "Invalid selection." ;;
    esac
  done
}

validate_numeric() {
  local label="$1"
  local val="$2"
  if ! [[ "${val}" =~ ^[0-9]+$ ]]; then
    err "${label} must be numeric."
    exit 1
  fi
}

render_backend_config() {
  local out_file="$1"
  cat >"${out_file}" <<EOF
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft": "Warning"
    }
  },
  "WeatherFlow": {
    "ApiToken": "${WEATHERFLOW_API_TOKEN}",
    "StationId": ${WEATHERFLOW_STATION_ID},
    "DeviceId": ${WEATHERFLOW_DEVICE_ID},
    "Health": {
      "StaleThresholdSeconds": ${HEALTH_STALE_SECONDS}
    },
    "WebSocket": {
      "EnableVerboseLogging": false,
      "MaxMessageBytes": 262144
    }
  }
}
EOF
}

render_ui_config() {
  local out_file="$1"
  cat >"${out_file}" <<EOF
{
  "BackendUrl": "${BACKEND_URL}",
  "Ui": {
    "SelectedTheme": "${UI_THEME}"
  },
  "WeatherFlow": {
    "ApiToken": "${WEATHERFLOW_API_TOKEN}",
    "StationId": ${WEATHERFLOW_STATION_ID}
  }
}
EOF
}

write_service_file() {
  local template_file="$1"
  local out_file="$2"

  sed \
    -e "s|__SERVICE_USER__|${SERVICE_USER}|g" \
    -e "s|__INSTALL_ROOT__|${INSTALL_ROOT}|g" \
    -e "s|__BACKEND_PORT__|${BACKEND_PORT}|g" \
    -e "s|__DISPLAY__|${DISPLAY_VALUE}|g" \
    -e "s|__XAUTHORITY__|${XAUTHORITY_VALUE}|g" \
    "${template_file}" | ${SUDO} tee "${out_file}" >/dev/null
}

main() {
  parse_args "$@"
  if [[ -n "${CONFIG_FILE}" ]]; then
    load_config_file "${CONFIG_FILE}"
  fi

  require_command systemctl
  require_command sed
  require_command cp
  require_command install

  detect_platform

  info "Detected: ${OS_PRETTY}"
  info "Codename: ${OS_CODENAME} (${OS_TRACK})"
  info "Architecture: ${ARCH}"
  info "Model: ${PI_MODEL}"
  info "Desktop detected: ${HAS_DESKTOP}"

  if [[ -z "${MODE}" ]]; then
    choose_mode
  else
    MODE="$(parse_mode "${MODE}")"
    if [[ -z "${MODE}" ]]; then
      err "Invalid mode. Use backend, ui, or both."
      exit 1
    fi
  fi

  if [[ "${HAS_DESKTOP}" != "true" && ("${MODE}" == "ui" || "${MODE}" == "both") ]]; then
    warn "No desktop environment detected. UI service likely cannot launch."
    if ! prompt_yes_no "Continue with UI install anyway" "no"; then
      err "Aborted."
      exit 1
    fi
  fi

  if [[ -z "${INSTALL_ROOT}" ]]; then
    if [[ "${AUTO_YES}" == "yes" ]]; then
      INSTALL_ROOT="/opt/tempest"
    else
      INSTALL_ROOT="$(prompt_default "Install root" "/opt/tempest")"
    fi
  fi
  if [[ -z "${SERVICE_USER}" ]]; then
    if [[ "${AUTO_YES}" == "yes" ]]; then
      SERVICE_USER="${SUDO_USER:-${USER}}"
    else
      SERVICE_USER="$(prompt_default "Service user" "${SUDO_USER:-${USER}}")"
    fi
  fi

  BACKEND_SOURCE_DEFAULT="${REPO_ROOT}/publish/backend"
  UI_SOURCE_DEFAULT="${REPO_ROOT}/publish/ui"
  if [[ -z "${BACKEND_SOURCE}" ]]; then
    if [[ "${AUTO_YES}" == "yes" ]]; then
      BACKEND_SOURCE="${BACKEND_SOURCE_DEFAULT}"
    else
      BACKEND_SOURCE="$(prompt_default "Backend publish directory" "${BACKEND_SOURCE_DEFAULT}")"
    fi
  fi
  if [[ -z "${UI_SOURCE}" ]]; then
    if [[ "${AUTO_YES}" == "yes" ]]; then
      UI_SOURCE="${UI_SOURCE_DEFAULT}"
    else
      UI_SOURCE="$(prompt_default "UI publish directory" "${UI_SOURCE_DEFAULT}")"
    fi
  fi

  if [[ -z "${WEATHERFLOW_API_TOKEN}" ]]; then
    if [[ "${AUTO_YES}" == "yes" ]]; then
      err "WeatherFlow API token is required when using --yes."
      exit 1
    fi
    WEATHERFLOW_API_TOKEN="$(prompt_secret "WeatherFlow API token")"
  fi
  require_non_empty "WeatherFlow API token" "${WEATHERFLOW_API_TOKEN}"
  if [[ -z "${WEATHERFLOW_STATION_ID}" ]]; then
    if [[ "${AUTO_YES}" == "yes" ]]; then
      WEATHERFLOW_STATION_ID="0"
    else
      WEATHERFLOW_STATION_ID="$(prompt_default "WeatherFlow station ID" "0")"
    fi
  fi
  validate_numeric "WeatherFlow station ID" "${WEATHERFLOW_STATION_ID}"

  if [[ "${MODE}" == "backend" || "${MODE}" == "both" ]]; then
    if [[ -z "${WEATHERFLOW_DEVICE_ID}" ]]; then
      if [[ "${AUTO_YES}" == "yes" ]]; then
        WEATHERFLOW_DEVICE_ID="0"
      else
        WEATHERFLOW_DEVICE_ID="$(prompt_default "WeatherFlow device ID" "0")"
      fi
    fi
    validate_numeric "WeatherFlow device ID" "${WEATHERFLOW_DEVICE_ID}"
  else
    WEATHERFLOW_DEVICE_ID="0"
  fi

  if [[ -z "${BACKEND_PORT}" ]]; then
    if [[ "${AUTO_YES}" == "yes" ]]; then
      BACKEND_PORT="5000"
    else
      BACKEND_PORT="$(prompt_default "Backend HTTP port" "5000")"
    fi
  fi
  validate_numeric "Backend HTTP port" "${BACKEND_PORT}"
  if [[ -z "${BACKEND_URL}" ]]; then
    if [[ "${AUTO_YES}" == "yes" ]]; then
      BACKEND_URL="http://localhost:${BACKEND_PORT}"
    else
      BACKEND_URL="$(prompt_default "UI backend URL" "http://localhost:${BACKEND_PORT}")"
    fi
  fi
  if [[ -z "${HEALTH_STALE_SECONDS}" ]]; then
    if [[ "${AUTO_YES}" == "yes" ]]; then
      HEALTH_STALE_SECONDS="15"
    else
      HEALTH_STALE_SECONDS="$(prompt_default "Stale stream threshold seconds" "15")"
    fi
  fi
  validate_numeric "Stale stream threshold seconds" "${HEALTH_STALE_SECONDS}"

  if [[ "${MODE}" == "ui" || "${MODE}" == "both" ]]; then
    if [[ -z "${UI_THEME}" ]]; then
      if [[ "${AUTO_YES}" == "yes" ]]; then
        UI_THEME="Default"
      else
        UI_THEME="$(prompt_default "UI theme name" "Default")"
      fi
    fi
  else
    UI_THEME="${UI_THEME:-Default}"
  fi

  if [[ -n "${ENABLE_AT_BOOT}" ]]; then
    ENABLE_AT_BOOT="$(normalize_yes_no "${ENABLE_AT_BOOT}")"
    if [[ -z "${ENABLE_AT_BOOT}" ]]; then
      err "Invalid --enable-at-boot value. Use yes or no."
      exit 1
    fi
  elif [[ "${AUTO_YES}" == "yes" ]]; then
    ENABLE_AT_BOOT="yes"
  else
    ENABLE_AT_BOOT="no"
    if prompt_yes_no "Enable services at boot" "yes"; then
      ENABLE_AT_BOOT="yes"
    fi
  fi

  SERVICE_HOME="$(getent passwd "${SERVICE_USER}" | cut -d: -f6 || true)"
  if [[ -z "${SERVICE_HOME}" ]]; then
    SERVICE_HOME="/home/${SERVICE_USER}"
  fi

  DISPLAY_VALUE=":0"
  XAUTHORITY_VALUE="${SERVICE_HOME}/.Xauthority"

  if [[ "${MODE}" == "backend" || "${MODE}" == "both" ]]; then
    backend_bin="${BACKEND_SOURCE}/TempestBlazorApp"
    if [[ -f "${backend_bin}" ]] && command -v file >/dev/null 2>&1; then
      backend_file_desc="$(file "${backend_bin}" || true)"
      if [[ "${ARCH}" == "armhf" && "${backend_file_desc}" == *"aarch64"* ]]; then
        warn "Backend binary appears to be arm64 while OS architecture is armhf."
      fi
    fi
  fi

  if [[ "${MODE}" == "ui" || "${MODE}" == "both" ]]; then
    ui_bin="${UI_SOURCE}/Tempest.UI"
    if [[ -f "${ui_bin}" ]] && command -v file >/dev/null 2>&1; then
      ui_file_desc="$(file "${ui_bin}" || true)"
      if [[ "${ARCH}" == "armhf" && "${ui_file_desc}" == *"aarch64"* ]]; then
        warn "UI binary appears to be arm64 while OS architecture is armhf."
      fi
    fi
  fi

  printf "\nInstall summary:\n"
  printf "  Mode: %s\n" "${MODE}"
  printf "  Install root: %s\n" "${INSTALL_ROOT}"
  printf "  Service user: %s\n" "${SERVICE_USER}"
  printf "  Backend source: %s\n" "${BACKEND_SOURCE}"
  printf "  UI source: %s\n" "${UI_SOURCE}"
  printf "  Boot enabled: %s\n" "${ENABLE_AT_BOOT}"

  if [[ "${AUTO_YES}" != "yes" ]]; then
    if ! prompt_yes_no "Proceed with install" "yes"; then
      err "Aborted."
      exit 1
    fi
  fi

  if [[ -n "${WRITE_CONFIG_FILE}" ]]; then
    save_config_file "${WRITE_CONFIG_FILE}"
  fi

  if [[ "${DRY_RUN}" == "yes" ]]; then
    ok "Dry run complete. No filesystem or service changes were made."
    exit 0
  fi

  ${SUDO} install -d -m 755 "${INSTALL_ROOT}" "${INSTALL_ROOT}/backend" "${INSTALL_ROOT}/ui" "${INSTALL_ROOT}/config"

  if [[ "${MODE}" == "backend" || "${MODE}" == "both" ]]; then
    if [[ ! -d "${BACKEND_SOURCE}" ]]; then
      err "Backend source directory not found: ${BACKEND_SOURCE}"
      exit 1
    fi
    info "Copying backend publish output..."
    ${SUDO} cp -a "${BACKEND_SOURCE}/." "${INSTALL_ROOT}/backend/"
    ${SUDO} chmod +x "${INSTALL_ROOT}/backend/TempestBlazorApp" || true
    if [[ -f "${INSTALL_ROOT}/backend/appsettings.Production.json" ]]; then
      ${SUDO} cp "${INSTALL_ROOT}/backend/appsettings.Production.json" "${INSTALL_ROOT}/backend/appsettings.Production.json.bak.$(date +%Y%m%d%H%M%S)"
    fi
    tmp_backend_cfg="$(mktemp)"
    render_backend_config "${tmp_backend_cfg}"
    ${SUDO} install -m 640 "${tmp_backend_cfg}" "${INSTALL_ROOT}/backend/appsettings.Production.json"
    rm -f "${tmp_backend_cfg}"
  fi

  if [[ "${MODE}" == "ui" || "${MODE}" == "both" ]]; then
    if [[ ! -d "${UI_SOURCE}" ]]; then
      err "UI source directory not found: ${UI_SOURCE}"
      exit 1
    fi
    info "Copying UI publish output..."
    ${SUDO} cp -a "${UI_SOURCE}/." "${INSTALL_ROOT}/ui/"
    ${SUDO} chmod +x "${INSTALL_ROOT}/ui/Tempest.UI" || true
    if [[ -f "${INSTALL_ROOT}/ui/appsettings.Production.json" ]]; then
      ${SUDO} cp "${INSTALL_ROOT}/ui/appsettings.Production.json" "${INSTALL_ROOT}/ui/appsettings.Production.json.bak.$(date +%Y%m%d%H%M%S)"
    fi
    tmp_ui_cfg="$(mktemp)"
    render_ui_config "${tmp_ui_cfg}"
    ${SUDO} install -m 640 "${tmp_ui_cfg}" "${INSTALL_ROOT}/ui/appsettings.Production.json"
    rm -f "${tmp_ui_cfg}"
  fi

  if id -u "${SERVICE_USER}" >/dev/null 2>&1; then
    ${SUDO} chown -R "${SERVICE_USER}":"${SERVICE_USER}" "${INSTALL_ROOT}"
  else
    warn "Service user ${SERVICE_USER} does not exist. Skipping ownership update."
  fi

  if [[ "${MODE}" == "backend" || "${MODE}" == "both" ]]; then
    write_service_file \
      "${SCRIPT_DIR}/systemd/tempest-backend.service.template" \
      "/etc/systemd/system/tempest-backend.service"
  fi

  if [[ "${MODE}" == "ui" || "${MODE}" == "both" ]]; then
    write_service_file \
      "${SCRIPT_DIR}/systemd/tempest-ui.service.template" \
      "/etc/systemd/system/tempest-ui.service"
  fi

  info "Reloading systemd..."
  ${SUDO} systemctl daemon-reload

  if [[ "${MODE}" == "backend" || "${MODE}" == "both" ]]; then
    if [[ "${ENABLE_AT_BOOT}" == "yes" ]]; then
      ${SUDO} systemctl enable tempest-backend.service
    fi
    ${SUDO} systemctl restart tempest-backend.service
  fi

  if [[ "${MODE}" == "ui" || "${MODE}" == "both" ]]; then
    if [[ "${ENABLE_AT_BOOT}" == "yes" ]]; then
      ${SUDO} systemctl enable tempest-ui.service
    fi
    ${SUDO} systemctl restart tempest-ui.service || true
  fi

  printf "\nService status:\n"
  info "Gathering service status (this can take a few minutes on first run)..."

  if [[ "${MODE}" == "backend" || "${MODE}" == "both" ]]; then
    info "Checking backend service status..."
    ${SUDO} systemctl --no-pager --full status tempest-backend.service | sed -n '1,12p' || true
    if ! run_with_heartbeat "Waiting for backend /health response" curl --max-time 180 -fsS "http://localhost:${BACKEND_PORT}/health"; then
      warn "Backend /health check failed or timed out."
    fi
  fi

  if [[ "${MODE}" == "ui" || "${MODE}" == "both" ]]; then
    info "Checking UI service status..."
    ${SUDO} systemctl --no-pager --full status tempest-ui.service | sed -n '1,12p' || true
  fi

  ok "Service status check complete."

  ok "Install complete."
  printf "\nUseful commands:\n"
  printf "  sudo systemctl status tempest-backend\n"
  printf "  sudo systemctl status tempest-ui\n"
  printf "  sudo journalctl -u tempest-backend -f\n"
  printf "  sudo journalctl -u tempest-ui -f\n"
}

main "$@"
