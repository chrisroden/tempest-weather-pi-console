# Agent instructions (Tempest Weather Pi Console)

## Documentation must match code

Whenever you change behavior, configuration, install/update flow, UI controls, systemd units, sudoers, or deployment paths:

1. **Update in-repo docs in the same change** — at minimum any of: `Deployment Steps.md`, `README.md`, `CONTRIBUTING.md`, `scripts/pi/*.sh` comments / `install.env.example`, and relevant wiki pages.
2. **Update the GitHub wiki** when install/ops guidance changes (especially [Install on Raspberry Pi](https://github.com/chrisroden/tempest-weather-pi-console/wiki/Install-on-Raspberry-Pi)).
3. **Do not leave docs describing old paths or procedures.** Prefer `/opt/tempest` + systemd (`tempest-backend.service` / `tempest-ui.service`) over any home-directory layout.
4. Treat a behavior change without a matching doc update as incomplete work.

## Production layout (current)

| Item | Location |
|------|----------|
| Install root | `/opt/tempest` (default) |
| Backend | `/opt/tempest/backend` + `tempest-backend.service` |
| UI | `/opt/tempest/ui` + `tempest-ui.service` |
| Installer copy | `/opt/tempest/install-pi.sh` |
| Sudoers for UI controls | `/etc/sudoers.d/tempest` |
| Restart / Exit / Reboot | `systemctl` / `reboot` via passwordless sudo — **not** `~/tempest-*/` scripts |

Do not reintroduce home-directory start scripts (`~/tempest-backend`, `~/tempest-ui`, `start-tempest-*.sh`, `restart-tempest-*.sh`) for production control flow.
