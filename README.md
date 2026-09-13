# MOZA Telemetry Helper

A Windows utility for running a small, idle process under a configurable executable name, defaulting to **ForzaHorizon5.exe**, so MOZA Pit House can select a familiar game telemetry reader. An optional UDP bridge relays FH5 telemetry or converts supported source packets to FH5.

**The default helper works on the development setup:** Pit House 1.4.0.30 activates its FH5 reader, and the user confirmed the demo updates the on-wheel screen. A process name is not a documented MOZA API, and other versions/devices need testing. The converters remain experimental until tested with each game. See [verification](docs/verification.md).

## Features

- Executable-name presets and Start/Stop controls; FH5 is the default. Choose **Custom** (last in the list) to reveal a name textbox. A live availability indicator shows existing process IDs and disables duplicate starts.
- Process only mode for games/ports already emitting FH5 directly to Pit House.
- FH5 relay with byte-for-byte forwarding of validated 324-byte packets.
- Experimental Codemasters legacy `extradata=3` (264-byte) → FH5 conversion.
- Experimental Project CARS 2 UDP car physics v2 (556-byte) → FH5 conversion.
- Demo signal with moving RPM, speed, and gear for testing without a game.
- Configurable IPv4 endpoints and Codemasters RPM multiplier.
- Live dashboard, packet/error counters, settings saved per Windows user.
- Optional start with Windows (app starts in tray) and close window to tray.
- Tray menu: Show, Start helper/telemetry, Stop, and Exit; double-click to reopen.
- Distinct amber gauge icon for the executable, taskbar, and system tray (adapted from Lucide, ISC license).
- Race-off packet after two seconds without valid data and on normal stop.
- Owned process exits when the helper stops or crashes. Existing games are never renamed or terminated.

## Getting started

Windows x64. A published self-contained build needs no separate .NET installation.

1. Build with the [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0):

   ```powershell
   ./scripts/publish.ps1
   ./artifacts/win-x64/MozaTelemetryHelper.exe
   ```

2. Start MOZA Pit House and leave **ForzaHorizon5.exe** selected in the helper.
3. Select **Process only** if your game already sends FH5 telemetry to Pit House. MOZA's documented FH5 destination is `127.0.0.1:20055`; use your configured Pit House port if changed.
4. Click **Start helper** and launch your game. Check Pit House detection and the physical display.
5. Click **Stop** when finished. Closing the window exits by default; with **Close window to tray** enabled, it keeps running. Use the tray menu's **Exit** to stop everything.

To test without a game, choose **Demo → FH5**. To use a telemetry source with a different output format, see [game setup](docs/game-setup.md).

Under **App behavior**, enable **Start app with Windows** to register this app for your current Windows account (no admin needed). It opens in the system tray at sign-in, ready for **right-click → Start helper**. This option starts the app, not a fake game or demo signal automatically. Keep the published folder in a stable location; if you move it, toggle this option off and back on to update the startup path. Both behavior options are off by default and apply when changed.

The tray icon is available while the app is open. Right-click for Show, Start, Stop, and Exit; double-click to show the window. With **Close window to tray** enabled, the X button hides the window and preserves a running session. Tray **Exit** still cleans up. **Stop** only stops a process created by that particular app instance.

If FH5 or another matching process is already running (including a headless helper), an amber process-status message shows its PID and Start is disabled. The status refreshes automatically. Stop the existing session, or uncheck **Run process helper** and choose a telemetry mode to use the bridge alone. A custom process name only changes detection identity; output remains FH5 and must be sent to an FH5 reader.

The executable dropdown includes FH5/FH4, Forza Motorsport (Steam), Project CARS/2, DiRT 4/Rally 2.0, Assetto Corsa/Competizione, Automobilista 2, and iRacing process names from Pit House's configuration. **Custom** reveals a textbox accepting a name with or without `.exe`. Existing saved custom names restore automatically. Selecting a preset changes the process identity only; it does not change the output protocol or destination port.

## Routing

| Mode | Route |
|---|---|
| Process only | Game/port → Pit House `20055`; helper only provides process identity |
| FH5 relay | Game/port → helper `20777` → Pit House `20055` |
| Codemasters | Legacy UDP source → helper `20777` → FH5 → Pit House `20055` |
| Project CARS 2 | Broadcast `5606` → helper → FH5 → Pit House `20055` |
| Demo | Generated dashboard signal → Pit House `20055` |

Input/output ports must differ. Use a free helper input port and configure the source accordingly. Two applications cannot reliably consume the same unicast UDP port; choose one receiver and forward from it. Do not configure Pit House to forward packets back to the helper's input. PCARS2 broadcasts require **0.0.0.0** binding and may need an inbound Windows firewall rule.

## Game coverage

| Source | Implementation | Live-game verification |
|---|---|---|
| Existing FH5-output ports | Process identity + 324-byte relay | Pending |
| DiRT legacy family | 264-byte codec, RPM ×10 by default | DiRT 3 / DiRT 4 individually pending |
| GRID family | Candidate for legacy codec if the edition emits 264 bytes | Exact edition/layout must be established; original GRID not claimed supported |
| Project CARS 2 | UDP v2 physics codec | Pending |

MOZA already lists **Project CARS 2 and DiRT 4** as native telemetry-compatible. Use native support when it works; this bridge provides another route for custom setups. Conversion maps only available dashboard fields, not every FH5 sensor or every property supported by MOZA. Unavailable fields are zero. PCARS2 lap timing/session categories are not yet joined, and the initial converters treat valid physics samples as race-active; replay/pause semantics need game testing.

## Development

```powershell
dotnet test MozaTelemetry.slnx -c Release
./scripts/publish.ps1 -FrameworkDependent # smaller local build; .NET 10 Desktop Runtime required
./scripts/publish.ps1 -OutputDirectory ./artifacts/win-x64-update # package separately while the current app remains open
./scripts/smoke-test.ps1
```

Use the published folder when testing process identity; `dotnet run` alone does not stage the sentinel beside the app. Copy the **whole** `artifacts/win-x64` folder for distribution. The CI workflow builds, tests, and uploads that folder as a private Actions artifact.

Bounded headless runs are available for diagnosis/automation:

```powershell
./artifacts/win-x64/MozaTelemetryHelper.exe --headless --mode ProcessOnly --process ForzaHorizon5.exe --seconds 30 --report "$env:TEMP/moza-helper-report.json"
./artifacts/win-x64/MozaTelemetryHelper.exe --headless --no-process --mode Codemasters --listen-port 20777 --output-port 20055 --seconds 60
```

Headless options: `--mode` (`ProcessOnly`, `Fh5Relay`, `Codemasters`, `ProjectCars2`, `Demo`), `--process`, `--no-process`, `--listen`, `--listen-port`, `--output`, `--output-port`, `--rpm-scale`, `--seconds` (default 30), `--report`. Headless settings do not change saved GUI settings. The report is written at startup and completion. Headless errors return exit code 1; use `--report` for details. Closing/crashing a headless parent releases the sentinel within about 500 ms, though abrupt termination cannot send race-off.

## Project structure

- `src/MozaTelemetry.App` — WinForms UI, settings, lifecycle, headless entry point.
- `src/MozaTelemetry.Sentinel` — idle child, parent-death monitoring, explicit stop/ready events.
- `src/MozaTelemetry.Core` — normalized samples, codecs, cancellable UDP relay.
- `tests/MozaTelemetry.Tests` — wire-format and UDP tests.
- `scripts` — build/package and lifecycle checks.
- `docs` — setup, references, verification, roadmap.

Settings and temporary process copies live under `%LocalAppData%/MozaTelemetryHelper`. Normal stop removes the session copy; a crash may leave an inert session directory. The app does not modify Pit House, game executables, game configurations, wheel settings, or force-feedback parameters.

Unofficial project; not affiliated with MOZA Racing, Microsoft, or the game publishers.
