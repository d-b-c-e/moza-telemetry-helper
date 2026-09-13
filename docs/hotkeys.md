# Toggle hotkey

The app can register a global keyboard shortcut to toggle its helper session while another app is focused or this app is in the tray.

1. Open **Toggle hotkey** in the main window.
2. The suggested shortcut is **Ctrl+Alt+F10**. Click the shortcut button to change it: select Ctrl and/or Alt, optional Shift, and a letter, number, or F1–F11. Click **Save**.
3. Check **Enable global hotkey**. The status line confirms when the shortcut is active. The tray's Start and Stop items also show the registered shortcut.
4. Press the shortcut once to start and again to stop. Keep the app open or in the tray; exiting it releases the shortcut.

Hotkeys default off, including on upgrades from earlier versions. The shortcut and enabled state are saved with the rest of your app settings. **Start app with Windows** can open the app in the tray and register an enabled shortcut at sign-in, without starting a helper until you press it.

## What it toggles

The shortcut uses the same Start/Stop actions as the window and tray. In **Process only** mode, it toggles the owned process. If you select a telemetry mode, it starts/stops that session's telemetry too. Telemetry-only sessions can also be toggled when **Run process helper** is unchecked.

The usual process-name and port checks still apply. A real game or another helper with the selected name blocks a new start; pressing the shortcut never stops that unrelated process. Failures appear in the log and process status. Repeated presses during startup/shutdown, update installation, or shortcut configuration are ignored. Holding the keys does not repeatedly toggle the session.

## Choosing a combination

Use Ctrl or Alt, optionally both and/or Shift. Windows-reserved keys such as F12 and Windows-key combinations are excluded. If a combination is already registered by another app or another helper instance, choose a different combination. Failed replacements leave the previous working shortcut active. A conflict on app startup leaves the saved preference intact and reports the shortcut as inactive so you can change it or retry enabling it.

The implementation uses Windows [RegisterHotKey](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-registerhotkey) with `MOD_NOREPEAT`, a message-only window, and [UnregisterHotKey](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-unregisterhotkey). It uses no keyboard hook or input injection.

Automated coverage exercises configuration, registration/conflict handling, message filtering, and Start/Stop dispatch using a fake registration API. No live desktop hotkey or fullscreen-game test has been performed yet.
