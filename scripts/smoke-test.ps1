param([string]$BuildDirectory)
$ErrorActionPreference = 'Stop'
if (!$BuildDirectory) { $BuildDirectory = Join-Path (Split-Path $PSScriptRoot -Parent) 'artifacts\win-x64' }
$exe = Join-Path $BuildDirectory 'MozaTelemetryHelper.exe'
if (!(Test-Path -LiteralPath $exe)) { throw 'Run scripts/publish.ps1 first.' }
$testName = 'MozaTelemetrySmoke-' + [guid]::NewGuid().ToString('N')
$report = Join-Path ([IO.Path]::GetTempPath()) ($testName + '.json')
$duplicateReport = Join-Path ([IO.Path]::GetTempPath()) ($testName + '-duplicate.json')
$ownedParent = $null
$ownedChild = $null

function Read-ReadyReport([string]$Path) {
    $deadline = [DateTime]::UtcNow.AddSeconds(15)
    while ([DateTime]::UtcNow -lt $deadline) {
        if (Test-Path -LiteralPath $Path) {
            try {
                $result = Get-Content -LiteralPath $Path -Raw | ConvertFrom-Json
                if ($result.State -eq 'Error') { throw [InvalidOperationException]::new($result.Error) }
                if ($result.ChildId) { return $result }
            } catch [InvalidOperationException] { throw } catch { }
        }
        Start-Sleep -Milliseconds 100
    }
    throw 'Timed out waiting for process helper readiness.'
}

try {
    # Custom names make this safe to run alongside Pit House or real games.
    $ownedParent = Start-Process -FilePath $exe -ArgumentList @('--headless', '--process', $testName, '--seconds', '5', '--report', ('"' + $report + '"')) -WindowStyle Hidden -PassThru
    $ready = Read-ReadyReport $report
    $ownedChild = Get-Process -Id $ready.ChildId
    if ($ownedChild.ProcessName -ne $testName) { throw 'Renamed Windows process identity did not match.' }
    $childDirectory = Split-Path $ownedChild.Path -Parent

    $duplicate = Start-Process -FilePath $exe -ArgumentList @('--headless', '--process', $testName, '--seconds', '1', '--report', ('"' + $duplicateReport + '"')) -WindowStyle Hidden -PassThru
    if (!$duplicate.WaitForExit(10000)) { $duplicate.Kill(); throw 'Duplicate guard timed out.' }
    $failure = Get-Content -LiteralPath $duplicateReport -Raw | ConvertFrom-Json
    if ($failure.State -ne 'Error' -or $failure.Error -notmatch 'already running') { throw 'Duplicate guard failed.' }
    if ($ownedChild.HasExited) { throw 'Duplicate request stopped the existing child.' }
    $duplicate.Dispose()
    if (!$ownedParent.WaitForExit(15000)) { throw 'Normal stop timed out.' }
    if (!$ownedChild.WaitForExit(4000)) { throw 'Child survived a normal stop.' }
    if (Test-Path -LiteralPath $childDirectory) { throw 'Normal stop did not remove session directory.' }
    $finished = Get-Content -LiteralPath $report -Raw | ConvertFrom-Json
    if ($finished.State -ne 'Stopped' -or $finished.UnexpectedExit) { throw 'Normal run failed.' }
    Write-Host 'PASS: custom process name, duplicate guard, normal stop, session cleanup.'
    $ownedParent.Dispose(); $ownedChild.Dispose(); $ownedChild = $null

    Remove-Item -LiteralPath $report
    $ownedParent = Start-Process -FilePath $exe -ArgumentList @('--headless', '--process', $testName, '--seconds', '60', '--report', ('"' + $report + '"')) -WindowStyle Hidden -PassThru
    $ready = Read-ReadyReport $report
    $ownedChild = Get-Process -Id $ready.ChildId
    $crashDirectory = Split-Path $ownedChild.Path -Parent
    $ownedParent.Kill()
    if (!$ownedChild.WaitForExit(5000)) { throw 'Child survived parent termination.' }
    Write-Host 'PASS: sentinel exits when its parent crashes.'
    # A crash leaves an inert directory. Only remove the observed test session within our exact cache root.
    $cacheRoot = [IO.Path]::GetFullPath((Join-Path $env:LOCALAPPDATA 'MozaTelemetryHelper\sessions')) + [IO.Path]::DirectorySeparatorChar
    $resolvedCrashDirectory = [IO.Path]::GetFullPath($crashDirectory)
    if (!$resolvedCrashDirectory.StartsWith($cacheRoot, [StringComparison]::OrdinalIgnoreCase)) { throw 'Unexpected session cleanup path.' }
    Remove-Item -LiteralPath $resolvedCrashDirectory -Recurse
} finally {
    foreach ($process in @($ownedParent, $ownedChild)) {
        if ($process) { if (!$process.HasExited) { $process.Kill(); $process.WaitForExit(5000) | Out-Null }; $process.Dispose() }
    }
    foreach ($path in @($report, $duplicateReport)) { if (Test-Path -LiteralPath $path) { Remove-Item -LiteralPath $path } }
}
