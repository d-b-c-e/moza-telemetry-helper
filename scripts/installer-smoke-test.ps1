param([Parameter(Mandatory)][string]$BuildDirectory, [Parameter(Mandatory)][string]$InnoCompiler)
$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path $PSScriptRoot -Parent
$version = ([xml](Get-Content -LiteralPath (Join-Path $repoRoot 'Directory.Build.props') -Raw)).Project.PropertyGroup.Version
$testRoot = Join-Path $repoRoot ('artifacts\installer-test-' + [guid]::NewGuid().ToString('N'))
$installDir = Join-Path $testRoot 'installed app'
$testRegistry = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\{C9084109-5268-4C2D-AE57-A4E36C79574B}_is1'
if (Test-Path -LiteralPath $testRegistry) { throw 'A previous packaging test installation exists; inspect it before running another test.' }
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
try {
    if ((Run-Silent $installer ($common + @("/DIR=$installDir", "/LOG=$testRoot\install.log"))) -ne 0) { throw 'Silent installation failed.' }
    if (!(Test-Path -LiteralPath $testRegistry)) { throw 'Per-user uninstall registration missing.' }
    foreach ($file in @('MozaTelemetryHelper.exe', 'sentinel\MozaTelemetry.Sentinel.exe', 'updater\MozaTelemetryUpdater.exe', 'docs\installation-and-updates.md')) {
        if (!(Test-Path -LiteralPath (Join-Path $installDir $file))) { throw "Missing installed file: $file" }
    }
    Write-Host 'PASS: isolated per-user silent installation and complete payload'
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
    if ((Run-Silent $installer ($common + @("/DIR=$installDir", "/LOG=$testRoot\upgrade.log"))) -ne 0) { throw 'Silent upgrade failed.' }
    if ([IO.File]::ReadAllText($marker) -ne 'preserve me') { throw 'Upgrade modified unrelated user data.' }
    & (Join-Path $PSScriptRoot 'smoke-test.ps1') -BuildDirectory $installDir
    Write-Host 'PASS: upgrade preserves user data; installed app passes hidden process lifecycle checks'
} finally {
    if (Test-Path -LiteralPath $uninstaller) {
        if ((Run-Silent $uninstaller ($common + @("/LOG=$testRoot\uninstall.log"))) -ne 0) { throw 'Test uninstall failed.' }
    }
}
if (Test-Path -LiteralPath $testRegistry) { throw 'Uninstall left its registration behind.' }
if (Test-Path -LiteralPath (Join-Path $installDir 'MozaTelemetryHelper.exe')) { throw 'Uninstall left the application executable behind.' }
Write-Host "PASS: silent uninstall; test logs retained at $testRoot"
