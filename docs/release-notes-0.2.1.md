# MOZA Telemetry Helper 0.2.1

Setup now offers **Start MOZA Telemetry Helper with Windows (in the system tray)** and **Automatically check GitHub for updates** alongside the desktop shortcut option. Windows startup opens the app when you sign in; use the tray menu to start the process helper.

- Windows startup is off and automatic update checks are on by default on fresh installations; no administrator rights required.
- Both checkboxes use the same settings as the app.
- Upgrades preserve your current app preference, including changes made since the previous installation.
- Deselecting the option removes startup; uninstall removes its own startup entry.

Download **MozaTelemetryHelper-0.2.1-Setup.exe** for installation or updates, or extract the entire **MozaTelemetryHelper-0.2.1-win-x64.zip** archive. Both are self-contained Windows x64 builds; Setup remains unsigned. Private-repository updates require an existing GitHub CLI login with access.

Validation: 78 automated app tests plus isolated silent installer checks for both preferences, defaults, enable/disable, preserving app changes and unrelated settings across upgrades, path quoting/repair, and uninstall cleanup. Tests use separate registry/settings locations.
