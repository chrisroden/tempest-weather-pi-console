#!/usr/bin/env bash
set -euo pipefail

SUDO=""
if [[ "${EUID}" -ne 0 ]]; then
  SUDO="sudo"
fi

PORT="5000"
MODE="auto"
TIMEOUT_SECONDS="45"

color() {
  local code="$1"
  shift
  printf "\033[%sm%s\033[0m\n" "${code}" "$*"
}

info() { color "36" "[INFO] $*"; }
ok() { color "32" "[ OK ] $*"; }
warn() { color "33" "[WARN] $*"; }
err() { color "31" "[ERR ] $*"; }

usage() {
  cat <<'EOF'
Usage: smoke-test-pi.sh [options]

Options:
  --mode <auto|backend|ui|both>   Which service checks to run (default: auto)
  --port <number>                 Backend port (default: 5000)
  --timeout <seconds>             Max wait for backend health (default: 45)
  -h, --help

Examples:
  ./scripts/pi/smoke-test-pi.sh
  ./scripts/pi/smoke-test-pi.sh --mode both --port 5000
EOF
}

parse_mode() {
  local raw="${1:-}"
  raw="${raw,,}"
  case "${raw}" in
    auto|backend|ui|both) printf "%s" "${raw}" ;;
    *) printf "" ;;
  esac
}

parse_args() {
  while [[ $# -gt 0 ]]; do
    case "$1" in
      --mode)
        MODE="${2:-}"
        shift 2
        ;;
      --port)
        PORT="${2:-}"
        shift 2
        ;;
      --timeout)
        TIMEOUT_SECONDS="${2:-}"
        shift 2
        ;;
      -h|--help)
        usage
        exit 0
        ;;
      *)
        err "Unknown argument: $1"
        usage
        exit 1
        ;;
    esac
  done
}

require_command() {
  local cmd="$1"
  if ! command -v "${cmd}" >/dev/null 2>&1; then
    err "Missing required command: ${cmd}"
    exit 1
  fi
}

require_numeric() {
  local label="$1"
  local val="$2"
  if ! [[ "${val}" =~ ^[0-9]+$ ]]; then
    err "${label} must be numeric."
    exit 1
  fi
}

service_exists() {
  local svc="$1"
  ${SUDO} systemctl list-unit-files --type=service --no-legend | awk '{print $1}' | grep -qx "${svc}"
}

service_is_active() {
  local svc="$1"
  ${SUDO} systemctl is-active --quiet "${svc}"
}

wait_for_backend_health() {
  local end_ts
  end_ts=$(( $(date +%s) + TIMEOUT_SECONDS ))
  while [[ $(date +%s) -lt ${end_ts} ]]; do
    if curl -fsS "http://localhost:${PORT}/health" >/dev/null 2>&1; then
      return 0
    fi
    sleep 2
  done
  return 1
}

main() {
  parse_args "$@"
  MODE="$(parse_mode "${MODE}")"
  if [[ -z "${MODE}" ]]; then
    err "Invalid mode. Use auto, backend, ui, or both."
    exit 1
  fi

  require_numeric "port" "${PORT}"
  require_numeric "timeout" "${TIMEOUT_SECONDS}"
  require_command systemctl
  require_command curl

  local check_backend="no"
  local check_ui="no"

  case "${MODE}" in
    backend)
      check_backend="yes"
      ;;
    ui)
      check_ui="yes"
      ;;
    both)
      check_backend="yes"
      check_ui="yes"
      ;;
    auto)
      if service_exists "tempest-backend.service"; then
        check_backend="yes"
      fi
      if service_exists "tempest-ui.service"; then
        check_ui="yes"
      fi
      ;;
  esac

  if [[ "${check_backend}" == "no" && "${check_ui}" == "no" ]]; then
    err "No Tempest services found to test."
    exit 1
  fi

  info "Running smoke tests (mode=${MODE}, port=${PORT}, timeout=${TIMEOUT_SECONDS}s)..."

  if [[ "${check_backend}" == "yes" ]]; then
    if service_is_active "tempest-backend.service"; then
      ok "tempest-backend.service is active"
    else
      err "tempest-backend.service is not active"
      ${SUDO} systemctl --no-pager --full status tempest-backend.service || true
      exit 1
    fi

    if wait_for_backend_health; then
      ok "Backend /health responded"
    else
      err "Backend /health did not respond in time"
      ${SUDO} journalctl -u tempest-backend -n 80 --no-pager || true
      exit 1
    fi

    curl -fsS "http://localhost:${PORT}/health" >/dev/null
    ok "GET /health succeeded"

    curl -fsS "http://localhost:${PORT}/health/details" >/dev/null
    ok "GET /health/details succeeded"

    local negotiate_code
    negotiate_code="$(curl -s -o /dev/null -w "%{http_code}" -X POST "http://localhost:${PORT}/weatherHub/negotiate?negotiateVersion=1")"
    if [[ "${negotiate_code}" == "200" ]]; then
      ok "POST /weatherHub/negotiate returned 200"
    else
      err "POST /weatherHub/negotiate returned ${negotiate_code}"
      exit 1
    fi
  fi

  if [[ "${check_ui}" == "yes" ]]; then
    if service_is_active "tempest-ui.service"; then
      ok "tempest-ui.service is active"
    else
      warn "tempest-ui.service is not active (headless systems may not have graphical target)"
      ${SUDO} systemctl --no-pager --full status tempest-ui.service || true
      if [[ "${MODE}" == "ui" || "${MODE}" == "both" ]]; then
        exit 1
      fi
    fi
  fi

  ok "Smoke test complete."
}

main "$@"
