#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

echo "Launching Tempest Raspberry Pi reconfiguration..."
"${SCRIPT_DIR}/install-pi.sh"
