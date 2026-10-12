# MOZA Telemetry Helper 0.4.0

Keeps the helper available in the system tray from Windows sign-in, so starting it is always one right-click away.

- **Start with Windows is now on by default for new installs.** Setup's **Start MOZA Telemetry Helper with Windows (in the system tray)** checkbox starts checked. Upgrades keep your current choice: if you turned startup off, it stays off.
- **Close window to tray is now on by default for new users.** The X button hides the window to the tray instead of exiting; use tray → **Exit** to quit. Saved settings keep your existing choice.
- **One instance only.** Opening the app from the Start menu or desktop while it is already in the tray now shows the existing window instead of starting a second copy with a second tray icon. The sign-in launch stays silent if the app is already open.
- **No window flash at sign-in.** The tray launch no longer briefly shows the main window before hiding it.

Starting the app never starts the helper process or telemetry by itself. Use the tray menu, the window, or the optional global hotkey to start a session.

Download **MozaTelemetryHelper-0.4.0-Setup.exe** to install or update, or extract the complete **MozaTelemetryHelper-0.4.0-win-x64.zip** archive. Public GitHub update checks require no login. Setup is per-user and self-contained; it remains unsigned.

Validation: 104 automated tests, including single-instance and default-setting tests, the hidden owned-process lifecycle checks, and the isolated installer test (fresh install enables startup; upgrades preserve a disabled or enabled startup choice; uninstall removes it).
