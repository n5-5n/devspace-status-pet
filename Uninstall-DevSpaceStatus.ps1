[CmdletBinding()]
param(
    [string]$InstallDirectory = "$env:LOCALAPPDATA\DevSpaceStatusPet",
    [switch]$RemoveSettings,
    [switch]$Interactive,
    [string]$SettingsPath = "$env:USERPROFILE\.devspace\devspace-pet-settings.json",
    [string]$PositionPath = "$env:USERPROFILE\.devspace\devspace-pet-position.json",
    [string]$StatePath = "$env:USERPROFILE\.devspace\devspace-status.json"
)

Set-StrictMode -Version 2.0
$ErrorActionPreference = 'Stop'

$localizationPath = Join-Path $InstallDirectory 'DevSpaceLocalization.ps1'
if (-not (Test-Path -LiteralPath $localizationPath)) {
    $localizationPath = Join-Path $PSScriptRoot 'DevSpaceLocalization.ps1'
}
if (Test-Path -LiteralPath $localizationPath) {
    . $localizationPath
    $settings = Read-DevSpaceSharedSettings -Path $SettingsPath
    $language = Resolve-DevSpaceLanguage -Preference ([string]$settings.Language)
    function U {
        param([string]$Key, [object[]]$Arguments = @())
        return Get-DevSpaceText -Language $language -Key $Key -Arguments $Arguments
    }
}
else {
    function U { param([string]$Key, [object[]]$Arguments = @()) return $Key }
}

if ($Interactive) {
    Add-Type -AssemblyName System.Windows.Forms
    $answer = [System.Windows.Forms.MessageBox]::Show(
        (U 'UninstallPrompt'),
        'DevSpace Status Pet',
        [System.Windows.Forms.MessageBoxButtons]::YesNo,
        [System.Windows.Forms.MessageBoxIcon]::Question
    )
    $RemoveSettings = $answer -eq [System.Windows.Forms.DialogResult]::Yes
}

$stopPath = Join-Path $InstallDirectory 'Stop-DevSpaceStatusPet.ps1'
if (Test-Path -LiteralPath $stopPath) {
    & $stopPath -InstallDirectory $InstallDirectory | Out-Null
}
Start-Sleep -Milliseconds 500

$desktopPath = [Environment]::GetFolderPath('Desktop')
$startupPath = [Environment]::GetFolderPath('Startup')
foreach ($shortcutPath in @(
    (Join-Path $desktopPath 'DevSpace Status Pet.lnk'),
    (Join-Path $desktopPath 'DevSpace Status Pet Settings.lnk'),
    (Join-Path $desktopPath 'DevSpace 状態.lnk'),
    (Join-Path $startupPath 'DevSpace Status Pet.lnk'),
    (Join-Path $startupPath 'DevSpace Status.lnk')
)) {
    if (Test-Path -LiteralPath $shortcutPath) {
        Remove-Item -LiteralPath $shortcutPath -Force -ErrorAction SilentlyContinue
    }
}

foreach ($transientPath in @($StatePath, "$StatePath.tmp.*")) {
    Remove-Item -Path $transientPath -Force -ErrorAction SilentlyContinue
}

if ($RemoveSettings) {
    foreach ($userPath in @($SettingsPath, $PositionPath)) {
        if (Test-Path -LiteralPath $userPath) {
            Remove-Item -LiteralPath $userPath -Force -ErrorAction SilentlyContinue
        }
    }
}

$normalizedInstall = [System.IO.Path]::GetFullPath($InstallDirectory).TrimEnd('\', '/')
Set-Location $env:TEMP
if (Test-Path -LiteralPath $normalizedInstall) {
    try {
        Remove-Item -LiteralPath $normalizedInstall -Recurse -Force -ErrorAction Stop
    }
    catch {
        $escaped = $normalizedInstall.Replace('"', '""')
        $deleteCommand = 'ping 127.0.0.1 -n 3 >nul & rmdir /s /q "{0}"' -f $escaped
        Start-Process cmd.exe -WindowStyle Hidden -ArgumentList @('/c', $deleteCommand)
    }
}

Write-Host (U 'UninstallDone') -ForegroundColor Green
if ($RemoveSettings) {
    Write-Host (U 'UninstallSettingsRemoved')
}
else {
    Write-Host (U 'UninstallSettingsKept')
}
