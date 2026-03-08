# Pi Install Rollout Checklist

Use this checklist when validating install instructions with teammates so everyone reports results the same way.

## Test Metadata

- Date:
- Tester:
- Pi model:
- OS image/version:
- Desktop or Lite:
- Architecture (`arm64` or `armhf`):
- Hostname:

## Preflight

- [ ] Repo is present on Pi at expected path.
- [ ] Scripts are executable: `chmod +x scripts/pi/*.sh`.
- [ ] Network access is working.
- [ ] WeatherFlow token/station/device values are ready.

## Install Run

- [ ] Ran bootstrap: `./scripts/pi/bootstrap-pi.sh`
- [ ] Installer completed without fatal errors.
- [ ] Backend service created (`tempest-backend.service`).
- [ ] UI service created when expected (`tempest-ui.service`).
- [ ] Config files generated under `/opt/tempest`.

## Smoke Test

- [ ] Ran: `./scripts/pi/smoke-test-pi.sh`
- [ ] `/health` check passed.
- [ ] `/health/details` check passed.
- [ ] SignalR negotiate check returned `200`.
- [ ] If desktop install: ran `./scripts/pi/smoke-test-pi.sh --mode both` and UI service passed.

## Reboot Validation

- [ ] Rebooted Pi.
- [ ] Backend auto-started after reboot.
- [ ] UI auto-started after reboot (desktop installs).
- [ ] Smoke test still passes after reboot.

## Reconfigure and Uninstall

- [ ] Reconfigure flow tested: `./scripts/pi/reconfigure-pi.sh`
- [ ] Uninstall tested: `./scripts/pi/uninstall-pi.sh`
- [ ] Services and `/opt/tempest` removed after uninstall.

## Notes for Failures

- Observed issue:
- Exact command run:
- Error output:
- Log excerpts:
- Reproducible on second attempt: Yes/No
