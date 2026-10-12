# MOZA Telemetry Helper 0.5.0

Shows what Pit House is likely to do to your wheel base before you start, and tells you afterwards if your preset changed.

- **Pit House preset preview.** The window has a new **Pit House preset** line, shown only when Pit House is installed. With Pit House's Motor preset *auto-load* on, the fake game name can make Pit House load a different preset. The preview reads your saved Pit House presets and says what to expect, for example *"switch your wheel base from Arcade Xbox 270 (135°, 75% torque) to R12-Forza Horizon 5-Official2 (450°, 100% torque)"*. When the data is missing or ambiguous it says so rather than guessing.
- **Change report on Stop.** Stopping compares Pit House's saved preset selection with the one before Start. If it changed, the log (and a tray notification when the window is hidden) tells you which preset to reselect. Pit House does not switch back on its own.
- **Read-only.** The app never edits Pit House files or settings. A saved selection is not proof of what the wheel base is currently running; Pit House shows that.
- **Smaller helper process.** The idle process that carries the game name is now a 1.5 MB native program instead of 37 MB.
- `--preset-check <name>` prints the preview as JSON without starting anything.
- Release assets now also include `MozaTelemetry.Core.0.5.0.nupkg` and `MozaTelemetry.Sentinel-0.5.0-win-x64.exe`, the shared pieces used by Wheelkit.

Download **MozaTelemetryHelper-0.5.0-Setup.exe** to install or update, or extract the complete **MozaTelemetryHelper-0.5.0-win-x64.zip** archive. Setup is per-user and self-contained; it remains unsigned.

Validation: 113 automated tests, including preset-preview fixtures (switch, same, auto-load off, owner default precedence, cross-game presets, ambiguous and unreadable inventories reported as unknown, and no file changes from reading). The process lifecycle smoke passed with the native helper process. On the development rig the preview predicted the same switch that was observed on 2026-10-12.
