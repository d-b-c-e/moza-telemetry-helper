# MOZA Telemetry Helper 0.2.1

Setup now offers **Start MOZA Telemetry Helper with Windows (in the system tray)** alongside the desktop shortcut option. It opens the app when you sign in; use the tray menu to start the process helper.

- Off by default on fresh installations; no administrator rights required.
- Uses the same setting as the app's Start app with Windows checkbox.
- Upgrades preserve your current app preference, including changes made since the previous installation.
- Deselecting the option removes startup; uninstall removes its own startup entry.

Download **MozaTelemetryHelper-0.2.1-Setup.exe** for installation or updates, or extract the entire **MozaTelemetryHelper-0.2.1-win-x64.zip** archive. Both are self-contained Windows x64 builds; Setup remains unsigned. Private-repository updates require an existing GitHub CLI login with access.

Validation: 73 automated app tests plus isolated silent installer checks for startup defaults, enable/disable, preserving app changes across upgrades, path quoting/repair, and uninstall cleanup. Tests use a separate registry key and leave the real Windows startup preference alone.
