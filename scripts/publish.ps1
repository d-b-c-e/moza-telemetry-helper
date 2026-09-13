param([switch]$FrameworkDependent, [string]$OutputDirectory)
$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path $PSScriptRoot -Parent
$output = if ($OutputDirectory) { [IO.Path]::GetFullPath($OutputDirectory) } else { Join-Path $repoRoot 'artifacts\win-x64' }
$sentinelOutput = Join-Path $output 'sentinel'
Push-Location $repoRoot
try {
    $selfContained = if ($FrameworkDependent) { 'false' } else { 'true' }
    dotnet publish src/MozaTelemetry.Sentinel -c Release -r win-x64 --self-contained $selfContained -o $sentinelOutput -p:PublishSingleFile=true -p:EnableCompressionInSingleFile=$selfContained
    if ($LASTEXITCODE -ne 0) { throw 'Sentinel publish failed.' }
    dotnet publish src/MozaTelemetry.Updater -c Release -r win-x64 --self-contained $selfContained -o (Join-Path $output 'updater') -p:PublishSingleFile=true -p:EnableCompressionInSingleFile=$selfContained -p:IncludeNativeLibrariesForSelfExtract=true
    if ($LASTEXITCODE -ne 0) { throw 'Updater publish failed.' }
    dotnet publish src/MozaTelemetry.App -c Release -r win-x64 --self-contained $selfContained -o $output
    if ($LASTEXITCODE -ne 0) { throw 'Application publish failed.' }
    Copy-Item -LiteralPath 'README.md' -Destination $output
    Copy-Item -LiteralPath 'docs' -Destination $output -Recurse -Force
    Write-Host "Ready: $output\MozaTelemetryHelper.exe"
} finally { Pop-Location }
