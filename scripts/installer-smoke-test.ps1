param([Parameter(Mandatory)][string]$BuildDirectory, [Parameter(Mandatory)][string]$InnoCompiler)
$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path $PSScriptRoot -Parent
$version = ([xml](Get-Content -LiteralPath (Join-Path $repoRoot 'Directory.Build.props') -Raw)).Project.PropertyGroup.Version
$testRoot = Join-Path $repoRoot ('artifacts\installer-test-' + [guid]::NewGuid().ToString('N'))
$installDir = Join-Path $testRoot 'installed app'
$settingsFile = Join-Path $installDir 'packaging-test-settings.json'
$testRegistry = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\{C9084109-5268-4C2D-AE57-A4E36C79574B}_is1'
if (Test-Path -LiteralPath $testRegistry) { throw 'A previous packaging test installation exists; inspect it before running another test.' }
$testRunSubkey = 'Software\MozaTelemetryHelper\PackagingTest\Run'
if (Test-Path -LiteralPath "HKCU:\$testRunSubkey") { throw 'A previous startup-option test exists; inspect it before running another test.' }
$realRun = [Microsoft.Win32.Registry]::CurrentUser.OpenSubKey('Software\Microsoft\Windows\CurrentVersion\Run')
$originalStartup = if ($realRun) { $realRun.GetValue('MozaTelemetryHelper'); $realRun.Dispose() } else { $null }
New-Item -ItemType Directory -Path $testRoot | Out-Null
$compilerLog = Join-Path $testRoot 'compile.log'
& $InnoCompiler '/DTestInstall=1' "/DMyAppVersion=$version" "/DPublishDir=$BuildDirectory" "/DReleaseDir=$testRoot" (Join-Path $repoRoot 'installer\MozaTelemetryHelper.iss') *> $compilerLog
if ($LASTEXITCODE -ne 0) { throw "Test installer compilation failed: $compilerLog" }
$installer = Join-Path $testRoot "MozaTelemetryHelper-$version-Setup.exe"
function Run-Silent([string]$Executable, [string[]]$Options) {
    $info = [Diagnostics.ProcessStartInfo]::new($Executable)
    $info.UseShellExecute = $false
    $info.CreateNoWindow = $true
    foreach ($option in $Options) { $info.ArgumentList.Add($option) }
    $process = [Diagnostics.Process]::Start($info)
    try {
        if (!$process.WaitForExit(60000)) { $process.Kill(); throw 'Silent installer timed out.' }
        return $process.ExitCode
    } finally { $process.Dispose() }
}
$common = @('/VERYSILENT', '/SUPPRESSMSGBOXES', '/NORESTART', '/SP-', '/NOCLOSEAPPLICATIONS', '/NORESTARTAPPLICATIONS')
$uninstaller = Join-Path $installDir 'unins000.exe'
function Assert-UpdatePreference([bool]$Expected) {
    $settings = Get-Content -LiteralPath $settingsFile -Raw | ConvertFrom-Json
    if ($settings.CheckForUpdates -ne $Expected) { throw "Update-check preference should be $Expected." }
}
$testRun = $null
try {
    if ((Run-Silent $installer ($common + @("/DIR=$installDir", "/LOG=$testRoot\install.log"))) -ne 0) { throw 'Silent installation failed.' }
    if (!(Test-Path -LiteralPath $testRegistry)) { throw 'Per-user uninstall registration missing.' }
    foreach ($file in @('MozaTelemetryHelper.exe', 'sentinel\MozaTelemetry.Sentinel.exe', 'updater\MozaTelemetryUpdater.exe', 'docs\installation-and-updates.md')) {
        if (!(Test-Path -LiteralPath (Join-Path $installDir $file))) { throw "Missing installed file: $file" }
    }
    Write-Host 'PASS: isolated per-user silent installation and complete payload'
    Assert-UpdatePreference $true
    $settings = Get-Content -LiteralPath $settingsFile -Raw | ConvertFrom-Json
    $settings | Add-Member -NotePropertyName UnrelatedSetting -NotePropertyValue 'preserve me'
    [IO.File]::WriteAllText($settingsFile, ($settings | ConvertTo-Json))
    $testRun = [Microsoft.Win32.Registry]::CurrentUser.CreateSubKey($testRunSubkey)
    if ($null -ne $testRun.GetValue('MozaTelemetryHelper')) { throw 'A fresh installation unexpectedly enabled startup.' }
    $testRun.SetValue('UnrelatedTestEntry', 'preserve me')
    $expectedStartup = '"' + (Join-Path $installDir 'MozaTelemetryHelper.exe') + '" --tray'
    if ((Run-Silent $installer ($common + @("/DIR=$installDir", '/MERGETASKS=startwithwindows', "/LOG=$testRoot\startup-on.log"))) -ne 0) { throw 'Enabling startup failed.' }
    if ($testRun.GetValue('MozaTelemetryHelper') -ne $expectedStartup) { throw 'The startup task did not write the quoted executable path and --tray.' }
    # Simulate disabling in the app after Setup remembered a checked task.
    $testRun.DeleteValue('MozaTelemetryHelper')
    $settings.CheckForUpdates = $false
    [IO.File]::WriteAllText($settingsFile, ($settings | ConvertTo-Json))
    if ((Run-Silent $installer ($common + @("/DIR=$installDir", "/LOG=$testRoot\preserve-off.log"))) -ne 0) { throw 'Upgrade with startup off failed.' }
    if ($null -ne $testRun.GetValue('MozaTelemetryHelper')) { throw 'Upgrade incorrectly restored a previously checked task after the app disabled startup.' }
    Assert-UpdatePreference $false
    # Simulate enabling in the app, including a stale executable path to repair on upgrade.
    $testRun.SetValue('MozaTelemetryHelper', '"C:\previous folder\MozaTelemetryHelper.exe" --tray')
    $settings.CheckForUpdates = $true
    [IO.File]::WriteAllText($settingsFile, ($settings | ConvertTo-Json))
    if ((Run-Silent $installer ($common + @("/DIR=$installDir", "/LOG=$testRoot\preserve-on.log"))) -ne 0) { throw 'Upgrade with startup on failed.' }
    if ($testRun.GetValue('MozaTelemetryHelper') -ne $expectedStartup) { throw 'Upgrade did not preserve enabled startup and repair its path.' }
    Assert-UpdatePreference $true
    if ((Run-Silent $installer ($common + @("/DIR=$installDir", '/TASKS=', "/LOG=$testRoot\startup-off.log"))) -ne 0) { throw 'Disabling startup failed.' }
    if ($null -ne $testRun.GetValue('MozaTelemetryHelper')) { throw 'Deselecting the task did not remove startup.' }
    Assert-UpdatePreference $false
    if ((Get-Content -LiteralPath $settingsFile -Raw | ConvertFrom-Json).UnrelatedSetting -ne 'preserve me') { throw 'Setup changed unrelated app settings.' }
    if ($testRun.GetValue('UnrelatedTestEntry') -ne 'preserve me') { throw 'Startup changes modified another registry value.' }
    Write-Host 'PASS: startup defaults off, enable/disable works, and upgrades preserve the live app preference'
    Write-Host 'PASS: update checks default on, can be disabled, and preserve live changes and unrelated settings on upgrade'
    $runnerCopy = Join-Path $testRoot 'MozaTelemetryUpdater.exe'
    Copy-Item -LiteralPath (Join-Path $installDir 'updater\MozaTelemetryUpdater.exe') -Destination $runnerCopy
    if ((Run-Silent $runnerCopy @()) -ne 2) { throw 'Standalone updater failed to run or did not reject missing arguments.' }
    Write-Host 'PASS: updater runs as a single copied executable outside the install directory'
    $marker = Join-Path $installDir 'user-test-data.txt'
    [IO.File]::WriteAllText($marker, 'preserve me')
    $mutex = [Threading.Mutex]::new($false, 'MozaTelemetryHelper.PackagingTest')
    try {
        if ((Run-Silent $installer ($common + @("/DIR=$installDir", "/LOG=$testRoot\blocked.log"))) -eq 0) { throw 'Installer ignored the running-app mutex.' }
    } finally { $mutex.Dispose() }
    Write-Host 'PASS: installer refuses upgrade while its application mutex exists'
    if ((Run-Silent $installer ($common + @("/DIR=$installDir", '/MERGETASKS=startwithwindows,checkforupdates', "/LOG=$testRoot\upgrade.log"))) -ne 0) { throw 'Silent upgrade failed.' }
    Assert-UpdatePreference $true
    if ([IO.File]::ReadAllText($marker) -ne 'preserve me') { throw 'Upgrade modified unrelated user data.' }
    & (Join-Path $PSScriptRoot 'smoke-test.ps1') -BuildDirectory $installDir
    Write-Host 'PASS: upgrade preserves user data; installed app passes hidden process lifecycle checks'
} finally {
    if (Test-Path -LiteralPath $uninstaller) {
        if ((Run-Silent $uninstaller ($common + @("/LOG=$testRoot\uninstall.log"))) -ne 0) { throw 'Test uninstall failed.' }
    }
    if ($testRun) {
        try {
            if ($null -ne $testRun.GetValue('MozaTelemetryHelper')) { throw 'Uninstall left its startup entry behind.' }
            if ($testRun.GetValue('UnrelatedTestEntry') -ne 'preserve me') { throw 'Uninstall removed another startup entry.' }
            $testRun.DeleteValue('UnrelatedTestEntry')
        } finally { $testRun.Dispose() }
        [Microsoft.Win32.Registry]::CurrentUser.DeleteSubKey($testRunSubkey)
    }
}
$realRun = [Microsoft.Win32.Registry]::CurrentUser.OpenSubKey('Software\Microsoft\Windows\CurrentVersion\Run')
$currentStartup = if ($realRun) { $realRun.GetValue('MozaTelemetryHelper'); $realRun.Dispose() } else { $null }
if ($originalStartup -ne $currentStartup) { throw 'The production startup preference changed during the isolated test.' }
if (Test-Path -LiteralPath $testRegistry) { throw 'Uninstall left its registration behind.' }
if (Test-Path -LiteralPath (Join-Path $installDir 'MozaTelemetryHelper.exe')) { throw 'Uninstall left the application executable behind.' }
Assert-UpdatePreference $true
Write-Host "PASS: silent uninstall; test logs retained at $testRoot"
