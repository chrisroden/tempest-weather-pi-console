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
EOF
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
  ${SUDO} apt-get install -y curl jq ca-certificates tar

  if ! command -v dotnet >/dev/null 2>&1; then
    warn ".NET not found; installing .NET 9 runtime and SDK via dotnet-install.sh"
    local dotnet_install
    dotnet_install="$(mktemp)"
    curl -fsSL https://dot.net/v1/dotnet-install.sh -o "${dotnet_install}"
    chmod +x "${dotnet_install}"
    ${SUDO} mkdir -p /usr/share/dotnet
    ${SUDO} "${dotnet_install}" --channel 9.0 --runtime dotnet --install-dir /usr/share/dotnet
    ${SUDO} "${dotnet_install}" --channel 9.0 --install-dir /usr/share/dotnet
    ${SUDO} ln -sf /usr/share/dotnet/dotnet /usr/bin/dotnet
    rm -f "${dotnet_install}"
  fi

  require_command dotnet
}

prepare_publish_dirs() {
  mkdir -p "${REPO_ROOT}/publish/backend" "${REPO_ROOT}/publish/ui"
}

build_local_artifacts() {
  info "Publishing backend and UI locally for ${RID}..."
  dotnet publish "${REPO_ROOT}/TempestBlazorApp/TempestBlazorApp.csproj" -c Release -r "${RID}" --self-contained -o "${REPO_ROOT}/publish/backend"
  dotnet publish "${REPO_ROOT}/Tempest.UI/Tempest.UI.csproj" -c Release -r "${RID}" --self-contained -o "${REPO_ROOT}/publish/ui"
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

  rm -rf "${REPO_ROOT}/publish/backend" "${REPO_ROOT}/publish/ui"
  mkdir -p "${REPO_ROOT}/publish/backend" "${REPO_ROOT}/publish/ui"

  tar -xzf "${tmp_backend}" -C "${REPO_ROOT}/publish/backend"
  tar -xzf "${tmp_ui}" -C "${REPO_ROOT}/publish/ui"

  rm -f "${tmp_backend}" "${tmp_ui}"
}

run_installer() {
  local installer
  installer="${SCRIPT_DIR}/install-pi.sh"

  if [[ ! -x "${installer}" ]]; then
    chmod +x "${installer}"
  fi

  info "Running install-pi.sh..."
  "${installer}" \
    --backend-source "${REPO_ROOT}/publish/backend" \
    --ui-source "${REPO_ROOT}/publish/ui" \
    "${INSTALL_ARGS[@]}"
}

main() {
  parse_args "$@"
  detect_platform

  info "Detected OS: ${OS_PRETTY}"
  info "Architecture: ${ARCH} (RID ${RID})"
  info "Desktop detected: ${HAS_DESKTOP}"
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
