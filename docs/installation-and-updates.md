# Installation and GitHub updates

## Distribution

Releases include a self-contained Windows x64 Setup EXE, a ZIP containing the complete application, and `SHA256SUMS.txt`.

Setup uses Inno Setup with `PrivilegesRequired=lowest`. It installs for the current user, defaults to `%LocalAppData%\Programs\MozaTelemetryHelper`, adds a Start menu shortcut and an Apps uninstall entry, and offers an optional desktop shortcut. There are no drivers, services, machine-wide registry settings, or .NET prerequisites to install. It does not enable Windows startup automatically. The app's existing **Start app with Windows** option uses HKCU and also needs no elevation.

Exit the app from its tray menu before manually upgrading or uninstalling. Setup checks a mutex held by GUI and headless instances and refuses to replace a running app. It never force-closes games or telemetry sessions. It preserves user settings under `%LocalAppData%\MozaTelemetryHelper`; uninstall leaves those settings available for reinstall. An enabled startup entry is repointed to the installed executable during installation and removed on uninstall if it still points there.

The current release is unsigned. Windows may show an unknown publisher / SmartScreen reputation prompt when opening a downloaded installer. Code signing is future release work.

## Updates

**Automatically check GitHub for updates** defaults on, including when migrating earlier settings. Checks occur at launch and every six hours; disable the checkbox to use only **Check now**. The window and tray both offer update controls. Checks run without dialogs or changing window focus.

The stable update channel selects the highest newer `vMAJOR.MINOR.PATCH` release with the exact expected Setup filename and GitHub SHA-256 digest. Drafts, prereleases, malformed tags, older versions, and incomplete assets are ignored.

For an installed copy, stop the helper and choose **Install update**. The app downloads and verifies the installer, saves preferences, and exits. A separate updater waits for that exact app process to exit, verifies the installer again, runs Setup silently in the same directory, and reopens the app in the window or tray it came from. It does not restart telemetry. Another running instance blocks installation. Failures are reported in the app log on reopening; installer logs and cached downloads are under `%LocalAppData%\MozaTelemetryHelper\updates`.

Automatic checks do not automatically install or restart the app while a game is running. Downloads start only when the user chooses Install update. ZIP and development copies instead offer **Download Setup**, opening the release page; they are never overwritten by the installer updater.

### Private repository now, public later

The updater tries the public GitHub API first. While the repository is private, it falls back to an installed **GitHub CLI** using that user's existing login (`gh auth login`) and repository access. MOZA Telemetry Helper does not retrieve, store, or ship a GitHub token. Signing into GitHub in a browser alone does not authenticate the CLI. A failed login/network check is shown in the log, with manual releases available through the browser.

Once the repository becomes public, the same build can check and download updates without GitHub CLI or authentication. Changing visibility remains the owner's choice.

## Building a release

Install the .NET 10 SDK and [Inno Setup 6](https://jrsoftware.org/isinfo.php), then run:

```powershell
dotnet test MozaTelemetry.slnx -c Release
./scripts/release.ps1
```

The script discovers ISCC in common per-user/system locations or accepts `-InnoCompiler`. It publishes into a new staging directory, runs the process lifecycle smoke test with unique custom process names, compiles Setup, and creates the ZIP and SHA-256 file under `artifacts/releases/<version>-<unique-id>/dist`. This builds artifacts; it does not install or open the app.

Update `Directory.Build.props`, commit, and tag the matching version, e.g. `v0.2.0`. `.github/workflows/release.yml` builds and uploads a **draft** GitHub release on version tags so all assets can be checked before publication. GitHub's asset digest must be present before publishing for the updater to accept the release. Do not put credentials into artifacts or source.

Hosted GitHub Actions currently cannot start because of the account billing/spending-limit block documented in [verification](verification.md). Local packaging and `gh release create ... --verify-tag --notes-file ...` provide the release path until that is resolved.

References: [Inno per-user installation](https://jrsoftware.org/ishelp/topic_setup_privilegesrequired.htm), [Inno application mutex](https://jrsoftware.org/ishelp/topic_setup_appmutex.htm), [Inno command-line options](https://jrsoftware.org/ishelp/topic_setupcmdline.htm), [GitHub release assets, authentication and digests](https://docs.github.com/en/rest/releases/assets).
