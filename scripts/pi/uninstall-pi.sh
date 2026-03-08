#!/usr/bin/env bash
set -euo pipefail

INSTALL_ROOT="${1:-/opt/tempest}"
SUDO=""
if [[ "${EUID}" -ne 0 ]]; then
  SUDO="sudo"
fi

echo "This will remove Tempest services and files from: ${INSTALL_ROOT}"
read -r -p "Continue? (y/N): " answer
answer="${answer,,}"
if [[ "${answer}" != "y" && "${answer}" != "yes" ]]; then
  echo "Aborted."
  exit 0
fi

for svc in tempest-ui.service tempest-backend.service; do
  if ${SUDO} systemctl list-unit-files | grep -q "^${svc}"; then
    ${SUDO} systemctl disable --now "${svc}" || true
  fi
done

${SUDO} rm -f /etc/systemd/system/tempest-backend.service /etc/systemd/system/tempest-ui.service
${SUDO} systemctl daemon-reload
${SUDO} rm -rf "${INSTALL_ROOT}"

echo "Uninstall complete."
