# Roadmap

## v0.1 — initial implementation

- [x] Configurable owned process; default FH5; start/stop and parent-death cleanup.
- [x] WinForms UI with saved settings and bounded headless operation.
- [x] Optional Windows sign-in startup, close to tray, tray Show/Start/Stop/Exit menu.
- [x] Live process-availability indicator and disabled duplicate Start.
- [x] Executable presets with FH5 default and Custom textbox, preserving existing saved names.
- [x] Distinct amber gauge icon for executable, taskbar, and tray.
- [x] FH5 relay and demo generator.
- [x] Codemasters legacy 264-byte and PCARS2 physics-v2 codecs.
- [x] Unit and local UDP integration tests; Windows packaging and CI.
- [x] Confirm physical MOZA wheel display with demo telemetry (user confirmed 2026-09-12, Pit House 1.4.0.30).
- [ ] Confirm process-only behavior with an existing FH5-output arcade port.

## Compatibility matrix

- [ ] Live DiRT 3 packet capture: version, size, RPM scale, reverse and neutral.
- [ ] Live DiRT 4 native route versus FH5 bridge.
- [ ] Identify the requested GRID edition; measure its legacy packet and implement shorter layouts where necessary.
- [ ] Live PCARS2 v2 broadcast input, game-selection precedence in Pit House, RPM/gear match.
- [ ] Track Pit House and wheel firmware versions alongside each result.

## Next adapters / features

- [ ] PCARS2 state/timing joins, pause/replay handling, sequence tracking.
- [ ] Named game profiles and launch/exit integration for LaunchBox.
- [ ] User-selected game process lifetime trigger (distinct from fake process name).
- [ ] Packet capture/replay diagnostics with explicit user-controlled retention.
- [ ] Source-aware active-race semantics for legacy Codemasters.
- [ ] Extra fields: tyre temperatures, timing, fuel, where source data exists.
- [ ] Other telemetry sources through normalized samples: newer F1, Assetto Corsa shared memory, etc.
- [ ] Evaluate MOZA-supported custom telemetry integration as an alternative to process detection, if available.
