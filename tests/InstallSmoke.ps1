[CmdletBinding()]
param(
    [string]$PackageDirectory = '',
    [string]$ZipPath = ''
)

Set-StrictMode -Version 2.0
$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$version = [System.IO.File]::ReadAllText((Join-Path $root 'VERSION'), [System.Text.Encoding]::UTF8).Trim()
$packageName = "DevSpace-Status-Pet-v$version"
$testRoot = Join-Path ([System.IO.Path]::GetTempPath()) "DevSpaceStatusPet-Smoke-$PID"
$installDirectory = Join-Path $testRoot 'Installed App With Spaces'
$settingsPath = Join-Path $testRoot 'settings.json'
$positionPath = Join-Path $testRoot 'position.json'
$statePath = Join-Path $testRoot 'state.json'
[void](New-Item -ItemType Directory -Path $testRoot -Force)

try {
    if ([string]::IsNullOrWhiteSpace($PackageDirectory)) {
        if ([string]::IsNullOrWhiteSpace($ZipPath)) {
            $ZipPath = Join-Path $root "artifacts\$packageName.zip"
        }
        if (-not (Test-Path -LiteralPath $ZipPath)) {
            throw "Release ZIP was not found: $ZipPath"
        }
        $extractRoot = Join-Path $testRoot 'Extracted Release'
        Expand-Archive -LiteralPath $ZipPath -DestinationPath $extractRoot -Force
        $PackageDirectory = Join-Path $extractRoot $packageName
    }

    if (-not (Test-Path -LiteralPath $PackageDirectory)) {
        throw "Package directory was not found: $PackageDirectory"
    }

    & (Join-Path $PackageDirectory 'Install-DevSpaceStatus.ps1') `
        -InstallDirectory $installDirectory `
        -SettingsPath $settingsPath `
        -StartWithWindows:$false `
        -NoStart `
        -NoShortcuts `
        -NoStopExisting

    foreach ($required in @('VERSION', 'Install.cmd', 'Settings.cmd', 'Uninstall.cmd', 'DevSpaceStatus.ps1', 'DevSpacePet.ps1', 'docs\classic-preview.svg', 'docs\neon-preview.svg')) {
        if (-not (Test-Path -LiteralPath (Join-Path $installDirectory $required))) {
            throw "Installed file is missing: $required"
        }
    }

    $installedVersion = [System.IO.File]::ReadAllText((Join-Path $installDirectory 'VERSION'), [System.Text.Encoding]::UTF8).Trim()
    if ($installedVersion -ne $version) {
        throw "Installed version mismatch: $installedVersion"
    }

    & (Join-Path $installDirectory 'Uninstall-DevSpaceStatus.ps1') `
        -InstallDirectory $installDirectory `
        -SettingsPath $settingsPath `
        -PositionPath $positionPath `
        -StatePath $statePath `
        -RemoveSettings

    Start-Sleep -Milliseconds 500
    if (Test-Path -LiteralPath $installDirectory) {
        throw 'The isolated installation directory still exists after uninstall.'
    }

    Write-Host '[OK] Release ZIP install and uninstall smoke test'
}
finally {
    if (Test-Path -LiteralPath $testRoot) {
        Remove-Item -LiteralPath $testRoot -Recurse -Force -ErrorAction SilentlyContinue
    }
}
