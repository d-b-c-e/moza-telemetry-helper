param([string]$InnoCompiler)
$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path $PSScriptRoot -Parent
$version = ([xml](Get-Content -LiteralPath (Join-Path $repoRoot 'Directory.Build.props') -Raw)).Project.PropertyGroup.Version
if ($version -notmatch '^\d+\.\d+\.\d+$') { throw 'Release version must be major.minor.patch.' }
if (!$InnoCompiler) {
    $candidates = @(
        (Join-Path $env:LOCALAPPDATA 'Programs\Inno Setup 6\ISCC.exe'),
        (Join-Path ${env:ProgramFiles(x86)} 'Inno Setup 6\ISCC.exe'),
        (Join-Path $env:ProgramFiles 'Inno Setup 6\ISCC.exe')
    )
    $command = Get-Command ISCC.exe -ErrorAction SilentlyContinue
    if ($command) { $candidates += $command.Source }
    $InnoCompiler = $candidates | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
}
if (!$InnoCompiler -or !(Test-Path -LiteralPath $InnoCompiler)) { throw 'Install Inno Setup 6 or pass -InnoCompiler with the path to ISCC.exe.' }
# Unique staging avoids mixing old publish files into a release. No recursive deletion required.
$buildRoot = Join-Path $repoRoot ('artifacts\releases\' + $version + '-' + [guid]::NewGuid().ToString('N'))
$publish = Join-Path $buildRoot 'app'
$dist = Join-Path $buildRoot 'dist'
New-Item -ItemType Directory -Path $dist -Force | Out-Null
& (Join-Path $PSScriptRoot 'publish.ps1') -OutputDirectory $publish
& (Join-Path $PSScriptRoot 'smoke-test.ps1') -BuildDirectory $publish
& $InnoCompiler "/DMyAppVersion=$version" "/DPublishDir=$publish" "/DReleaseDir=$dist" (Join-Path $repoRoot 'installer\MozaTelemetryHelper.iss')
if ($LASTEXITCODE -ne 0) { throw 'Installer compilation failed.' }
Add-Type -AssemblyName System.IO.Compression.FileSystem
$archive = Join-Path $dist "MozaTelemetryHelper-$version-win-x64.zip"
[IO.Compression.ZipFile]::CreateFromDirectory($publish, $archive)
$assets = Get-ChildItem -LiteralPath $dist -File | Sort-Object Name
$hashes = foreach ($asset in $assets) { (Get-FileHash -LiteralPath $asset.FullName -Algorithm SHA256).Hash.ToLowerInvariant() + '  ' + $asset.Name }
[IO.File]::WriteAllLines((Join-Path $dist 'SHA256SUMS.txt'), [string[]]$hashes)
Write-Host "Release assets: $dist"
