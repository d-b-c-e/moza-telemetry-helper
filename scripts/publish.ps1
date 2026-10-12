param([switch]$FrameworkDependent, [string]$OutputDirectory)
$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path $PSScriptRoot -Parent
# Resolve against the PowerShell location, not the process working directory, which can differ.
$output = if ($OutputDirectory) { $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($OutputDirectory) } else { Join-Path $repoRoot 'artifacts\win-x64' }
$sentinelOutput = Join-Path $output 'sentinel'
Push-Location $repoRoot
try {
    $selfContained = if ($FrameworkDependent) { 'false' } else { 'true' }
    # Native AOT regardless of -FrameworkDependent: the sentinel never needs a .NET runtime.
    # The AOT linker locates MSVC through vswhere.exe, which the VS installer does not put on PATH.
    $vsInstaller = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer'
    if ((Test-Path -LiteralPath (Join-Path $vsInstaller 'vswhere.exe')) -and ($env:PATH -notlike "*$vsInstaller*")) { $env:PATH = "$vsInstaller;$env:PATH" }
    dotnet publish src/MozaTelemetry.Sentinel -c Release -r win-x64 -o $sentinelOutput
    if ($LASTEXITCODE -ne 0) { throw 'Sentinel publish failed (Native AOT needs the Visual Studio C++ build tools).' }
    dotnet publish src/MozaTelemetry.Updater -c Release -r win-x64 --self-contained $selfContained -o (Join-Path $output 'updater') -p:PublishSingleFile=true -p:EnableCompressionInSingleFile=$selfContained -p:IncludeNativeLibrariesForSelfExtract=true
    if ($LASTEXITCODE -ne 0) { throw 'Updater publish failed.' }
    dotnet publish src/MozaTelemetry.App -c Release -r win-x64 --self-contained $selfContained -o $output
    if ($LASTEXITCODE -ne 0) { throw 'Application publish failed.' }
    Copy-Item -LiteralPath 'README.md' -Destination $output
    Copy-Item -LiteralPath 'docs' -Destination $output -Recurse -Force
    Write-Host "Ready: $output\MozaTelemetryHelper.exe"
} finally { Pop-Location }
