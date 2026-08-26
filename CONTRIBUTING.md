# Contributing

Thanks for contributing to Tempest Weather Station Console.

## Development Setup

Prerequisites:

- .NET SDK 9.0.x
- Git

Restore dependencies:

```bash
dotnet restore TempestConsole.sln
```

Build locally:

```bash
dotnet build TempestConsole.sln --configuration Release
```

## Required Local Test Commands

Run all tests:

```bash
dotnet test TempestConsole.sln --nologo
```

Run test projects individually:

```bash
dotnet test Tempest.WebSocket.Tests/Tempest.WebSocket.Tests.csproj --configuration Release --nologo
dotnet test TempestBlazorApp.Tests/TempestBlazorApp.Tests.csproj --configuration Release --nologo
dotnet test Tempest.UI.Tests/Tempest.UI.Tests.csproj --configuration Release --nologo
```

Run coverage gates locally (same threshold checks used by CI):

```bash
dotnet test Tempest.WebSocket.Tests/Tempest.WebSocket.Tests.csproj --configuration Release --nologo /p:CollectCoverage=true /p:CoverletOutput=./TestResults/WebSocket/ /p:CoverletOutputFormat=cobertura /p:IncludeTestAssembly=true /p:Include="[Tempest.WebSocket.Tests]*" /p:Threshold=70 /p:ThresholdType=line /p:ThresholdStat=total
dotnet test TempestBlazorApp.Tests/TempestBlazorApp.Tests.csproj --configuration Release --nologo /p:CollectCoverage=true /p:CoverletOutput=./TestResults/Blazor/ /p:CoverletOutputFormat=cobertura /p:IncludeTestAssembly=true /p:Include="[TempestBlazorApp.Tests]*" /p:Threshold=70 /p:ThresholdType=line /p:ThresholdStat=total
dotnet test Tempest.UI.Tests/Tempest.UI.Tests.csproj --configuration Release --nologo /p:CollectCoverage=true /p:CoverletOutput=./TestResults/UI/ /p:CoverletOutputFormat=cobertura /p:IncludeTestAssembly=true /p:Include="[Tempest.UI.Tests]*" /p:Threshold=70 /p:ThresholdType=line /p:ThresholdStat=total
```

CI enforces a minimum of 70% line coverage for each test project module.

## Pull Request Checklist

Before opening a PR, verify all items below:

- [ ] Branch is up to date with `main`
- [ ] `dotnet build TempestConsole.sln --configuration Release` succeeds
- [ ] `dotnet test TempestConsole.sln --nologo` succeeds
- [ ] Coverage collection commands run successfully
- [ ] New behavior includes tests (or existing tests updated)
- [ ] No secrets or tokens are committed
- [ ] Documentation updated when behavior/config/setup changes (required — same change as the code)
- [ ] Wiki updated when install/ops guidance changes
- [ ] PR description includes what changed and why

## Coding Guidelines

- Keep changes focused and minimal.
- Prefer explicit, readable code over clever shortcuts.
- Add tests for bug fixes and feature changes.
- **Docs must match code:** update `Deployment Steps.md`, wiki, and related install docs in the same PR whenever behavior or setup changes. Do not leave references to obsolete home-directory layouts (`~/tempest-backend`, `~/tempest-ui`, start/restart shell scripts); production is `/opt/tempest` + systemd.

## Contribution License Expectation

By submitting a contribution to this repository, you agree your contribution is provided under the same MIT License as this project.