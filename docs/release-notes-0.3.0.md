# MOZA Telemetry Helper 0.3.0

Adds an optional global hotkey to start or stop the helper while the app is in the tray or another app is focused.

- **Toggle hotkey** in the main window offers Ctrl+Alt+F10 by default, with a button to choose another combination. Check **Enable global hotkey** to activate it.
- The shortcut uses the same Start/Stop actions as the window and tray, including the selected telemetry mode. It never stops a real game or another instance's process.
- Holding the keys does not repeat the toggle; presses during a transition or update installation are ignored.
- Conflicts are reported, and a failed shortcut change leaves the previous working shortcut active.
- The shortcut is saved with your settings, works while the app stays in the tray, and is released on exit. It defaults off for new and existing users.

Download **MozaTelemetryHelper-0.3.0-Setup.exe** to install or update, or extract the complete **MozaTelemetryHelper-0.3.0-win-x64.zip** archive. Public GitHub update checks require no login. Setup is per-user and self-contained; it remains unsigned.

Validation: 100 automated tests, including hotkey registration and toggle dispatch through a fake registration API, plus hidden owned-process lifecycle checks. No live desktop shortcut or fullscreen-game test has been performed.
