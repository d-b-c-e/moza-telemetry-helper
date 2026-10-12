# Verification

2026-09-12, Windows x64, .NET SDK 10.0.401. Installed Pit House: 1.4.0.30.

## Automated

- 100 passing tests: wire offsets, SI units, gears, protocol rejection, input validation, localhost UDP, stale-data handling, startup quoting, executable selection, icons, updates, installer preference preservation, hotkey configuration/registration lifecycle, and guarded toggle dispatch.
- Process lifecycle smoke passed: custom Windows process name, duplicate guard, normal stop, session-directory cleanup, and child exit following forced parent termination.
- Self-contained Windows x64 application published successfully.
- v0.2 installer tested silently under a separate test AppId and temporary installation path, without shortcuts or production registry changes: complete payload, refusal to upgrade while the app mutex exists, successful upgrade, preservation of unrelated user data, installed-app lifecycle smoke checks, and uninstall cleanup all passed. The test never opens the app window.
- v0.2.1 installer preference checks passed using a private test registry key and settings file: startup off / update checks on for new users, explicit enable and disable, preservation of preferences changed in the app after installation, startup path repair, retention of unrelated registry/JSON values, and uninstall removing only its startup entry while retaining app settings. These are silent tests; no live wizard interaction was used.
- v0.3 hotkey tests use a fake Windows registration API: disabled defaults, virtual keys/modifiers, repeat-suppression flag, conflicts preserving an active shortcut, stale-message rejection, disable/dispose cleanup, and asynchronous toggle guards all pass. No real hotkey was registered and no keyboard input or visible UI was used for these tests. Live shortcut and game behavior need user verification.
- GitHub update selection and download verification have automated coverage; the final install-and-reopen interaction through the live UI still needs an authorized desktop test and a newer release.
- v0.2.0 release assets were uploaded from commit `8dd214e`. GitHub-reported SHA-256 digests matched all three local artifacts. A background probe using the actual updater successfully checked the private repository through the existing GitHub CLI login, downloaded the 98,815,781-byte installer through its authenticated asset API, and passed size/SHA-256 verification. The probe's temporary download was removed. The standalone copied updater also starts successfully without other files beside it.
- GUI visually inspected on a 3840×2160 desktop; settings, guidance, controls, and log are visible.
- Revised duplicate-status display visually confirmed with a bounded `MozaUiSmoke.exe` child: amber text showed the existing PID and Start was disabled before a click.
- Tray/startup behavior is implemented and builds successfully. Live tray interaction and an actual Windows sign-in cycle have not been exercised; desktop automation was stopped at the user's request. Future desktop interaction requires asking first.
- Follow-up dropdown/icon checks ran entirely in the background. Selector tests instantiate an unshown control with no form or tray icon; they verify FH5 default, Custom last, textbox visibility, preserved custom text, and old settings restoration. The new amber gauge ICO contains nine sizes from 16 to 256 pixels; application icon resources load at the actual tray/taskbar sizes. No mouse, keyboard, window-focus changes, or live desktop screenshots were used for this update.
- [Initial GitHub Actions run](https://github.com/d-b-c-e/moza-telemetry-helper/actions/runs/34731723193) was blocked before any steps ran. GitHub's annotation reports failed recent account payments or a spending limit that needs increasing. This is an account-side runner block, not a test/build failure; billing settings were not changed. Local build/test/package results above remain valid.

## 0.4.0 (2026-10-12)

- 104 passing tests, adding new-user close-to-tray defaults with saved choices kept, and single-instance behavior (a second launch is refused and asks the first to show; a quiet `--tray` second launch does not; the name frees after exit). Tests use unique object names, not the real app's.
- Release packaging passed the hidden process lifecycle smoke. The isolated installer test now asserts that a fresh install enables startup (`"<app>\MozaTelemetryHelper.exe" --tray`), that an upgrade after the user removed startup keeps it off, that explicit enable/disable and live-preference preservation still work, and that uninstall removes the entry.
- Not yet exercised: a real Windows sign-in cycle, and the window-shown path of a second launch on the desktop.

## Pit House / hardware

- **2026-10-12 recheck on Pit House 1.4.1.13:** a 20-second headless Process-only run (`ForzaHorizon5.exe` sentinel PID 44800, no telemetry) made Pit House bind `127.0.0.1:20055` within 4 seconds and release it after the helper exited (exit 0). Before the run Pit House owned only unrelated UDP ports, not 20055, 20044 or 8001. The FH5 destination port is unchanged.
- **The same run changed Pit House's saved Motor preset.** Pit House has `[IsAutoLoadPreset] Motor=true` in `Documents/MOZA Pit House/Presets/config.ini`. It rewrote that file at 14:53:30, during the run. The R12 last-used preset changed from the owner's custom *Arcade Xbox 270* (from a 2026-10-07 baseline snapshot) to the official *R12-Forza Horizon 5-Official2*, which is bound to FH5. Notable differences:
  - steering angle 135 → 450;
  - maximum torque 75 → 100;
  - game FFB 40 → 55;
  - damper / friction / spring 10 / 10 / 10 → 25 / 20 / 0.

  Nothing was written back when the helper exited. This is persisted-file evidence: the wheel base's actual state was not read back. Copies and hashes are kept outside the repository.

- Installed binary contains `ForzaHorizon5.exe`.
- Before starting a sentinel, Pit House was running and did not own UDP port 20055.
- **FH5 process gate confirmed on this installation.** An owned `ForzaHorizon5.exe` sentinel (PID 63764) caused Pit House (PID 42828) to bind `127.0.0.1:20055`. The helper ran for 20 seconds with no telemetry socket, ended normally, removed its child, and Pit House released port 20055. A subsequent demo run reactivated that listener. This is evidence that this build's FH5 telemetry-reader activation can be triggered by the helper.
- **Physical on-wheel display confirmed by the user** during the two-minute FH5 demo on 2026-09-12: “Yep it appears to be working!” The demo sent 4,057 packets without send errors and the helper stopped cleanly afterward. Per-field accuracy and different dashboards were not separately measured.
- Live DiRT / GRID / PCARS2 conversion: not yet verified. Synthetic fixtures establish codec behavior only.

Do not relabel a sent UDP packet or an open listener as a verified physical dashboard.

## Environment observations

- SimHub also recognized the sentinel as FH5. The process identity is visible system-wide to any game-detection software.
- During the demo, the user attempted a second start from the GUI. The duplicate guard correctly rejected it, but its presentation was confusing. The UI now polls process availability, shows matching PIDs in amber, disables duplicate Start, and reports cleanup only for resources actually started by that session.
- The automated screenshot tool initially failed because its automatic InputBridge launch did not bring up the pipe. Starting the installed InputBridge explicitly with `--mode mame --mame-dir <MAME directory>` restored the testing connection on this system without a vJoy driver. Full-desktop capture also worked with the Windows screen-capture API.
