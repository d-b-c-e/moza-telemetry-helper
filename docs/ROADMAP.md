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

## v0.2 — distribution and updates

- [x] Per-user Inno Setup installer and self-contained ZIP release assets.
- [x] Automatic GitHub checks, manual checks, and user-triggered installation while stopped.
- [x] Existing GitHub CLI authentication for private releases; anonymous downloads when public.
- [x] Installer size/SHA-256 verification, wait for app exit, preserve settings, and restart after update.
- [x] Tag-triggered draft release workflow and local packaging fallback.
- [ ] Code signing and SmartScreen reputation.
- [ ] Verify a real upgrade to a later version from the installed UI when desktop testing is authorized.

## v0.3 — hotkey control

- [x] Optional configurable global hotkey, default Ctrl+Alt+F10 with registration disabled until enabled.
- [x] Tray-compatible message-only listener, repeat suppression, conflict reporting, and registration cleanup.
- [x] Reuse owned-session Start/Stop guards and preserve existing game/process safety.
- [x] Automated registration and toggle-dispatch tests without desktop input.
- [ ] User verification of the shortcut while playing a game.

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
