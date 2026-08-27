# Tempest Weather Pi Console

Tempest Weather Pi Console includes:

- A Raspberry Pi desktop UI built with Avalonia (`Tempest.UI`)
- A Blazor web app (`TempestBlazorApp`)
- Shared REST and WebSocket components

## Setup and Deployment

See `Deployment Steps.md` for full setup and deployment guidance.

On a Pi install, the header menu **About** item shows the installed package version from `/opt/tempest/VERSION` (written by `install-pi.sh` on install/update) and checks GitHub for a newer release when the dialog opens. If you are current, it says you are on the latest version. If a newer release exists, **Update now** runs `/usr/local/sbin/tempest-update` (which calls `install-pi.sh --update --yes --keep-ui-running`) and streams progress in the dialog. After a successful update, tap **Restart** to load the new binaries. Existing Pis need one root install/update of a release that includes this helper before in-app updates work.

## Security

Security policy and reporting guidance: `.github/SECURITY.md`

## Third-Party Notices

Open-source dependency notices are documented in `THIRD_PARTY_NOTICES.md`.

This includes Avalonia packages used by the UI application.

## Project License

This project is licensed under the MIT License. See `LICENSE`.

## Disclaimer

This software is provided "as is", without warranty of any kind. You are responsible for validating behavior, configuration, and deployment safety for your environment.

## Privacy and Data Use

This project processes weather-related telemetry and configuration data used to operate the application.

When deploying this project, do not commit secrets, API tokens, or private station information.