#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "${SCRIPT_DIR}/../.." && pwd)"
WORK_ROOT="${REPO_ROOT}"
PUBLISH_ROOT="${WORK_ROOT}/publish"
HAS_REPO_LAYOUT="false"
INSTALLER_URL="${INSTALLER_URL:-https://raw.githubusercontent.com/chrisroden/tempest-weather-pi-console/main/scripts/pi/install-pi.sh}"
BACKEND_TEMPLATE_URL="${BACKEND_TEMPLATE_URL:-https://raw.githubusercontent.com/chrisroden/tempest-weather-pi-console/main/scripts/pi/systemd/tempest-backend.service.template}"
UI_TEMPLATE_URL="${UI_TEMPLATE_URL:-https://raw.githubusercontent.com/chrisroden/tempest-weather-pi-console/main/scripts/pi/systemd/tempest-ui.service.template}"

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

run_with_spinner() {
  local label="$1"
  shift

  info "${label}"
  "$@" &
  local pid=$!
  local spin='|/-\\'
  local i=0

  while kill -0 "${pid}" >/dev/null 2>&1; do
    i=$(( (i + 1) % 4 ))
    printf "\r\033[36m[INFO] %s... %c\033[0m" "${label}" "${spin:${i}:1}"
    sleep 0.2
  done

  wait "${pid}"
  local rc=$?
  if [[ ${rc} -ne 0 ]]; then
    printf "\r\033[31m[ERR ] %s failed.\033[0m\n" "${label}"
    return "${rc}"
  fi

  printf "\r\033[32m[ OK ] %s complete.\033[0m\n" "${label}"
}

BUILD_LOCAL="yes"
DOWNLOAD_RELEASE="no"
BACKEND_ARCHIVE_URL=""
UI_ARCHIVE_URL=""
CONFIG_FILE=""
INSTALL_ARGS=()

usage() {
  cat <<'EOF'
Usage: bootstrap-pi.sh [options] [-- <install-pi args>]

Bootstrap options:
  --build-local                   Build backend/UI on the Pi (default)
  --download-release              Download prebuilt archives instead of building
  --backend-archive-url <url>     Required with --download-release
  --ui-archive-url <url>          Required with --download-release
  --config <path>                 Pass config file path to install-pi.sh
  -h, --help

All unknown args are passed through to install-pi.sh.

Notes:
  - In no-clone mode, bootstrap auto-downloads missing install and systemd template files.
EOF
}

detect_layout() {
  if [[ -f "${REPO_ROOT}/TempestBlazorApp/TempestBlazorApp.csproj" && -f "${REPO_ROOT}/Tempest.UI/Tempest.UI.csproj" ]]; then
    HAS_REPO_LAYOUT="true"
    WORK_ROOT="${REPO_ROOT}"
  else
    HAS_REPO_LAYOUT="false"
    WORK_ROOT="${SCRIPT_DIR}"
  fi

  PUBLISH_ROOT="${WORK_ROOT}/publish"
}

require_command() {
  local cmd="$1"
  if ! command -v "${cmd}" >/dev/null 2>&1; then
    err "Missing required command: ${cmd}"
    exit 1
  fi
}

parse_args() {
  while [[ $# -gt 0 ]]; do
    case "$1" in
      --build-local)
        BUILD_LOCAL="yes"
        DOWNLOAD_RELEASE="no"
        shift
        ;;
      --download-release)
        DOWNLOAD_RELEASE="yes"
        BUILD_LOCAL="no"
        shift
        ;;
      --backend-archive-url)
        BACKEND_ARCHIVE_URL="${2:-}"
        shift 2
        ;;
      --ui-archive-url)
        UI_ARCHIVE_URL="${2:-}"
        shift 2
        ;;
      --config)
        CONFIG_FILE="${2:-}"
        INSTALL_ARGS+=("--config" "${2:-}")
        shift 2
        ;;
      -h|--help)
        usage
        exit 0
        ;;
      --)
        shift
        while [[ $# -gt 0 ]]; do
          INSTALL_ARGS+=("$1")
          shift
        done
        ;;
      *)
        INSTALL_ARGS+=("$1")
        shift
        ;;
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
  OS_PRETTY="${PRETTY_NAME:-unknown}"
  ARCH="$(dpkg --print-architecture 2>/dev/null || uname -m)"

  if [[ "${ARCH}" == "arm64" || "${ARCH}" == "aarch64" ]]; then
    RID="linux-arm64"
  elif [[ "${ARCH}" == "armhf" || "${ARCH}" == "armv7l" ]]; then
    RID="linux-arm"
  else
    warn "Unknown architecture ${ARCH}; defaulting RID to linux-arm64"
    RID="linux-arm64"
  fi

  HAS_DESKTOP="false"
  if command -v startx >/dev/null 2>&1 || dpkg -l 2>/dev/null | grep -qE 'raspberrypi-ui-mods|lxsession|xserver-xorg'; then
    HAS_DESKTOP="true"
  fi
}

install_prereqs() {
  info "Installing prerequisite packages..."
  ${SUDO} apt-get update
  ${SUDO} apt-get install -y curl jq ca-certificates tar rsync

  if ! command -v dotnet >/dev/null 2>&1; then
    warn ".NET not found; installing .NET 9 runtime and SDK via dotnet-install.sh"
    local dotnet_install
    dotnet_install="$(mktemp)"
    curl -fsSL https://dot.net/v1/dotnet-install.sh -o "${dotnet_install}"
    chmod +x "${dotnet_install}"
    ${SUDO} mkdir -p /usr/share/dotnet
    run_with_spinner "Installing .NET runtime" ${SUDO} "${dotnet_install}" --channel 9.0 --runtime dotnet --install-dir /usr/share/dotnet
    run_with_spinner "Installing .NET SDK" ${SUDO} "${dotnet_install}" --channel 9.0 --install-dir /usr/share/dotnet
    ${SUDO} ln -sf /usr/share/dotnet/dotnet /usr/bin/dotnet
    rm -f "${dotnet_install}"
  fi

  require_command dotnet
}

prepare_publish_dirs() {
  mkdir -p "${PUBLISH_ROOT}/backend" "${PUBLISH_ROOT}/ui"
}

build_local_artifacts() {
  if [[ "${HAS_REPO_LAYOUT}" != "true" ]]; then
    err "--build-local requires cloned repository layout. Run from repo root, or use --download-release in standalone mode."
    exit 1
  fi

  info "Publishing backend and UI locally for ${RID}..."
  dotnet publish "${REPO_ROOT}/TempestBlazorApp/TempestBlazorApp.csproj" -c Release -r "${RID}" --self-contained -o "${PUBLISH_ROOT}/backend"
  dotnet publish "${REPO_ROOT}/Tempest.UI/Tempest.UI.csproj" -c Release -r "${RID}" --self-contained -o "${PUBLISH_ROOT}/ui"
}

download_release_artifacts() {
  if [[ -z "${BACKEND_ARCHIVE_URL}" || -z "${UI_ARCHIVE_URL}" ]]; then
    err "--download-release requires --backend-archive-url and --ui-archive-url"
    exit 1
  fi

  local tmp_backend tmp_ui
  tmp_backend="$(mktemp)"
  tmp_ui="$(mktemp)"

  info "Downloading backend archive..."
  curl -fL "${BACKEND_ARCHIVE_URL}" -o "${tmp_backend}"
  info "Downloading UI archive..."
  curl -fL "${UI_ARCHIVE_URL}" -o "${tmp_ui}"

  rm -rf "${PUBLISH_ROOT}/backend" "${PUBLISH_ROOT}/ui"
  mkdir -p "${PUBLISH_ROOT}/backend" "${PUBLISH_ROOT}/ui"

  tar -xzf "${tmp_backend}" -C "${PUBLISH_ROOT}/backend"
  tar -xzf "${tmp_ui}" -C "${PUBLISH_ROOT}/ui"

  rm -f "${tmp_backend}" "${tmp_ui}"
}

ensure_installer() {
  local installer
  installer="${SCRIPT_DIR}/install-pi.sh"

  if [[ -f "${installer}" ]]; then
    chmod +x "${installer}"
    return
  fi

  warn "install-pi.sh not found next to bootstrap script; attempting download..."
  curl -fsSL "${INSTALLER_URL}" -o "${installer}"
  chmod +x "${installer}"
  ok "Downloaded install-pi.sh"
}

ensure_systemd_templates() {
  local systemd_dir backend_tpl ui_tpl
  systemd_dir="${SCRIPT_DIR}/systemd"
  backend_tpl="${systemd_dir}/tempest-backend.service.template"
  ui_tpl="${systemd_dir}/tempest-ui.service.template"

  mkdir -p "${systemd_dir}"

  if [[ ! -f "${backend_tpl}" ]]; then
    warn "Missing backend service template; downloading..."
    curl -fsSL "${BACKEND_TEMPLATE_URL}" -o "${backend_tpl}"
    ok "Downloaded tempest-backend.service.template"
  fi

  if [[ ! -f "${ui_tpl}" ]]; then
    warn "Missing UI service template; downloading..."
    curl -fsSL "${UI_TEMPLATE_URL}" -o "${ui_tpl}"
    ok "Downloaded tempest-ui.service.template"
  fi
}

run_installer() {
  local installer
  installer="${SCRIPT_DIR}/install-pi.sh"

  ensure_installer
  ensure_systemd_templates

  info "Running install-pi.sh..."
  "${installer}" \
    --backend-source "${PUBLISH_ROOT}/backend" \
    --ui-source "${PUBLISH_ROOT}/ui" \
    "${INSTALL_ARGS[@]}"
}

main() {
  parse_args "$@"
  detect_layout
  detect_platform

  info "Detected OS: ${OS_PRETTY}"
  info "Architecture: ${ARCH} (RID ${RID})"
  info "Desktop detected: ${HAS_DESKTOP}"
  info "Working root: ${WORK_ROOT}"
  if [[ -n "${CONFIG_FILE}" && ! -f "${CONFIG_FILE}" ]]; then
    err "Config file not found: ${CONFIG_FILE}"
    exit 1
  fi

  install_prereqs
  prepare_publish_dirs

  if [[ "${DOWNLOAD_RELEASE}" == "yes" ]]; then
    download_release_artifacts
  else
    build_local_artifacts
  fi

  run_installer
  ok "Bootstrap complete."
}

main "$@"
