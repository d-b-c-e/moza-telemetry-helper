# Verification

2026-09-12, Windows x64, .NET SDK 10.0.401. Installed Pit House: 1.4.0.30.

## Automated

- 45 passing tests: exact wire offsets, SI units, gear conversion, protocol rejection, input validation, real localhost UDP relay/conversion, bind conflict, stale-data reset, resume, cancellation, and quoting of Windows startup paths with spaces.
- Process lifecycle smoke passed: custom Windows process name, duplicate guard, normal stop, session-directory cleanup, and child exit following forced parent termination.
- Self-contained Windows x64 application published successfully.
- GUI visually inspected on a 3840×2160 desktop; settings, guidance, controls, and log are visible.
- Revised duplicate-status display visually confirmed with a bounded `MozaUiSmoke.exe` child: amber text showed the existing PID and Start was disabled before a click.
- Tray/startup behavior is implemented and builds successfully. Live tray interaction and an actual Windows sign-in cycle have not been exercised; desktop automation was stopped at the user's request. Future desktop interaction requires asking first.
- [Initial GitHub Actions run](https://github.com/d-b-c-e/moza-telemetry-helper/actions/runs/34731723193) was blocked before any steps ran. GitHub's annotation reports failed recent account payments or a spending limit that needs increasing. This is an account-side runner block, not a test/build failure; billing settings were not changed. Local build/test/package results above remain valid.

## Pit House / hardware

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
