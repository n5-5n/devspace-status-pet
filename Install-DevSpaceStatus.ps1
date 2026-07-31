[CmdletBinding()]
param(
    [switch]$StartWithWindows = $true,
    [string]$SettingsPath = "$env:USERPROFILE\.devspace\devspace-pet-settings.json"
)

Set-StrictMode -Version 2.0
$ErrorActionPreference = 'Stop'

$projectDirectory = $PSScriptRoot
$localizationPath = Join-Path $projectDirectory 'DevSpaceLocalization.ps1'
if (-not (Test-Path -LiteralPath $localizationPath)) {
    throw "Missing localization file: $localizationPath"
}
. $localizationPath

$settings = Read-DevSpaceSharedSettings -Path $SettingsPath
$language = Resolve-DevSpaceLanguage -Preference ([string]$settings.Language)
function I {
    param(
        [string]$Key,
        [object[]]$Arguments = @()
    )
    return Get-DevSpaceText -Language $language -Key $Key -Arguments $Arguments
}

$launcherPath = Join-Path $projectDirectory 'Start-DevSpaceStatus.cmd'
if (-not (Test-Path -LiteralPath $launcherPath)) {
    throw "Launcher not found: $launcherPath"
}

$wshShell = New-Object -ComObject WScript.Shell
$desktopPath = [Environment]::GetFolderPath('Desktop')
$desktopShortcutPath = Join-Path $desktopPath 'DevSpace Status Pet.lnk'

$desktopShortcut = $wshShell.CreateShortcut($desktopShortcutPath)
$desktopShortcut.TargetPath = $launcherPath
$desktopShortcut.WorkingDirectory = $projectDirectory
$desktopShortcut.Description = 'DevSpace Status Pet'
$desktopShortcut.IconLocation = "$env:SystemRoot\System32\shell32.dll,167"
$desktopShortcut.Save()

$legacyDesktopShortcut = Join-Path $desktopPath 'DevSpace 状態.lnk'
if (Test-Path -LiteralPath $legacyDesktopShortcut) {
    Remove-Item -LiteralPath $legacyDesktopShortcut -Force -ErrorAction SilentlyContinue
}

if ($StartWithWindows) {
    $startupPath = [Environment]::GetFolderPath('Startup')
    $startupShortcutPath = Join-Path $startupPath 'DevSpace Status Pet.lnk'
    $startupShortcut = $wshShell.CreateShortcut($startupShortcutPath)
    $startupShortcut.TargetPath = $launcherPath
    $startupShortcut.WorkingDirectory = $projectDirectory
    $startupShortcut.Description = 'DevSpace Status Pet'
    $startupShortcut.IconLocation = "$env:SystemRoot\System32\shell32.dll,167"
    $startupShortcut.Save()

    $legacyStartupShortcut = Join-Path $startupPath 'DevSpace Status.lnk'
    if (Test-Path -LiteralPath $legacyStartupShortcut) {
        Remove-Item -LiteralPath $legacyStartupShortcut -Force -ErrorAction SilentlyContinue
    }
}

Start-Process -FilePath $launcherPath

Write-Host (I 'InstallerDone') -ForegroundColor Green
Write-Host (I 'DesktopShortcut' @($desktopShortcutPath))
if ($StartWithWindows) {
    Write-Host (I 'StartupEnabled')
}
Write-Host (I 'InstallerHint')
