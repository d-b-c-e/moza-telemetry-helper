# MOZA Telemetry Helper 0.2.0

First packaged release: a per-user Windows installer and GitHub update support for the MOZA process helper and FH5 telemetry bridge.

## Downloads

- **MozaTelemetryHelper-0.2.0-Setup.exe** — recommended; installs for your Windows account, adds a Start menu shortcut, and supports in-app updates. No elevation or separate .NET installation required.
- **MozaTelemetryHelper-0.2.0-win-x64.zip** — extract the entire folder and run MozaTelemetryHelper.exe. ZIP builds link to the release page for updates.
- **SHA256SUMS.txt** — checksums for both downloads.

Windows 10 22H2 / Windows 11 x64. The installer is currently unsigned.

## Included

- Owned process helper with ForzaHorizon5.exe as the default, standard executable presets, and Custom name entry.
- Existing-process indicator and duplicate-start protection; shutdown only affects the owned helper.
- FH5 relay and demo telemetry; experimental Codemasters legacy and Project CARS 2 converters.
- Optional Windows startup, close to tray, tray Start/Stop/Exit, and a distinct amber gauge icon.
- Automatic GitHub update checks at launch and every six hours, manual checks, and Install update while stopped. Downloads are size/SHA-256 verified; Setup waits for app exit and reopens the app after installation without starting telemetry.
- Settings retained across upgrades and uninstall/reinstall.

While the repository is private, in-app updates require GitHub CLI signed into an account with access. Once public, the same updater uses anonymous GitHub access. No credentials are included in the application.

## Verification and limits

73 automated tests pass. Local hidden process tests and isolated silent install/upgrade/uninstall tests pass. The user confirmed the MOZA on-wheel display with demo telemetry and Pit House 1.4.0.30. Live per-game conversions and the new updater's full UI interaction remain to be verified.

Built locally because GitHub-hosted Actions are currently blocked by the account's billing/spending-limit condition.
