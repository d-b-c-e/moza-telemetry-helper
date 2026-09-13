# MOZA Telemetry Helper

## Repository Purpose
Windows utility that runs an owned process with a configurable executable name to test MOZA Pit House game detection, and optionally bridges supported UDP telemetry into FH5 packets. Default identity: `ForzaHorizon5.exe`.

## Repository Structure
- `src/MozaTelemetry.Core`: protocol codecs and UDP bridge.
- `src/MozaTelemetry.App`: .NET 10 WinForms UI and owned-process lifecycle.
- `src/MozaTelemetry.Sentinel`: small, idle Windows process copied under the selected name.
- `tests/MozaTelemetry.Tests`: protocol and UDP integration tests.
- `scripts`: build, publish, and process lifecycle smoke tests.
- `docs`: setup, protocol references, and hardware verification evidence.

## Conventions
- Use C#/.NET 10 and PowerShell. Build and test before pushing.
- Run `dotnet test MozaTelemetry.slnx -c Release` and `scripts/smoke-test.ps1` after process or transport changes.
- Keep offsets, units, and provenance explicit. Synthetic packet tests do not establish game or hardware compatibility.
- Never overwrite game executables, inject into games, or stop processes by name. Only stop the exact child process we created.
- Do not edit Pit House or game configs automatically. Show manual instructions.
- Bind loopback by default; broadcast sources require explicit all-interface binding.
- Stop forwarding stale racing data. UI must distinguish running helper, packets sent, and hardware confirmation.
- Keep machine-specific paths and telemetry captures out of source control. Record actual hardware results, including failures.
- Ask before interacting with the user's desktop: mouse, keyboard, window focus, or live UI automation. Prefer background builds and tests. The user explicitly requested this on 2026-09-12.
