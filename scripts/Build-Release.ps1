[CmdletBinding()]
param(
    [string]$OutputDirectory = '',
    [switch]$SkipTests
)

Set-StrictMode -Version 2.0
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.IO.Compression.FileSystem

$root = Split-Path -Parent $PSScriptRoot
if ([string]::IsNullOrWhiteSpace($OutputDirectory)) {
    $OutputDirectory = Join-Path $root 'artifacts'
}
$versionPath = Join-Path $root 'VERSION'
if (-not (Test-Path -LiteralPath $versionPath)) {
    throw 'VERSION file is missing.'
}
$version = [System.IO.File]::ReadAllText($versionPath, [System.Text.Encoding]::UTF8).Trim()
if ($version -notmatch '^\d+\.\d+\.\d+(?:-[0-9A-Za-z.-]+)?$') {
    throw "Invalid semantic version: $version"
}

if (-not $SkipTests) {
    & (Join-Path $root 'tests\ParseScripts.ps1')
}

$packageName = "DevSpace-Status-Pet-v$version"
$stageDirectory = Join-Path $OutputDirectory $packageName
$zipPath = Join-Path $OutputDirectory "$packageName.zip"
$hashPath = "$zipPath.sha256"

if (Test-Path -LiteralPath $stageDirectory) {
    Remove-Item -LiteralPath $stageDirectory -Recurse -Force
}
foreach ($path in @($zipPath, $hashPath)) {
    if (Test-Path -LiteralPath $path) {
        Remove-Item -LiteralPath $path -Force
    }
}
[void](New-Item -ItemType Directory -Path $stageDirectory -Force)

$releaseFiles = @(
    'VERSION',
    'LICENSE',
    'README.md',
    'README.en.md',
    'CHANGELOG.md',
    'docs\classic-preview.svg',
    'docs\neon-preview.svg',
    'DevSpaceLocalization.ps1',
    'DevSpaceStatus.ps1',
    'DevSpacePet.ps1',
    'Open-DevSpaceStatusSettings.ps1',
    'Stop-DevSpaceStatusPet.ps1',
    'Install-DevSpaceStatus.ps1',
    'Uninstall-DevSpaceStatus.ps1',
    'Start-DevSpaceStatus.cmd',
    'Check-DevSpaceStatus.cmd',
    'Install-DevSpaceStatus.cmd',
    'Install.cmd',
    'Settings.cmd',
    'Uninstall.cmd'
)

foreach ($file in $releaseFiles) {
    $source = Join-Path $root $file
    if (-not (Test-Path -LiteralPath $source)) {
        throw "Release file is missing: $file"
    }
    $destination = Join-Path $stageDirectory $file
    $destinationParent = Split-Path -Parent $destination
    if (-not (Test-Path -LiteralPath $destinationParent)) {
        [void](New-Item -ItemType Directory -Path $destinationParent -Force)
    }
    Copy-Item -LiteralPath $source -Destination $destination -Force
}

Compress-Archive -LiteralPath $stageDirectory -DestinationPath $zipPath -CompressionLevel Optimal
$hash = Get-FileHash -LiteralPath $zipPath -Algorithm SHA256
[System.IO.File]::WriteAllText($hashPath, "$($hash.Hash.ToLowerInvariant())  $([System.IO.Path]::GetFileName($zipPath))`r`n", [System.Text.Encoding]::ASCII)

$archive = [System.IO.Compression.ZipFile]::OpenRead($zipPath)
try {
    $entryNames = @($archive.Entries | ForEach-Object { $_.FullName.Replace('\', '/') })
    foreach ($required in @("$packageName/Install.cmd", "$packageName/Uninstall.cmd", "$packageName/Settings.cmd", "$packageName/VERSION")) {
        if ($entryNames -notcontains $required) {
            throw "Release archive is missing: $required"
        }
    }
}
finally {
    $archive.Dispose()
}

[pscustomobject]@{
    Version        = $version
    PackageName    = $packageName
    StageDirectory = $stageDirectory
    ZipPath        = $zipPath
    Sha256Path     = $hashPath
    Sha256         = $hash.Hash.ToLowerInvariant()
}
