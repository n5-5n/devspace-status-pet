[CmdletBinding()]
param(
    [switch]$StartWithWindows = $true,
    [switch]$NoStart,
    [switch]$NoShortcuts,
    [switch]$NoStopExisting,
    [string]$InstallDirectory = "$env:LOCALAPPDATA\DevSpaceStatusPet",
    [string]$SettingsPath = "$env:USERPROFILE\.devspace\devspace-pet-settings.json"
)

Set-StrictMode -Version 2.0
$ErrorActionPreference = 'Stop'

$sourceDirectory = $PSScriptRoot
$localizationPath = Join-Path $sourceDirectory 'DevSpaceLocalization.ps1'
if (-not (Test-Path -LiteralPath $localizationPath)) {
    throw "Missing localization file: $localizationPath"
}
. $localizationPath

$settings = Read-DevSpaceSharedSettings -Path $SettingsPath
$language = Resolve-DevSpaceLanguage -Preference ([string]$settings.Language)
function I {
    param([string]$Key, [object[]]$Arguments = @())
    return Get-DevSpaceText -Language $language -Key $Key -Arguments $Arguments
}

function Normalize-InstallPath {
    param([string]$Path)
    return ([System.IO.Path]::GetFullPath($Path)).TrimEnd('\', '/')
}

function Stop-ExistingCopies {
    $scriptNames = @('DevSpaceStatus.ps1', 'DevSpacePet.ps1')
    try {
        foreach ($process in @(Get-CimInstance Win32_Process -ErrorAction Stop |
            Where-Object {
                $_.ProcessId -ne $PID -and
                $_.Name -match '^(powershell|pwsh)(\.exe)?$' -and
                -not [string]::IsNullOrWhiteSpace([string]$_.CommandLine)
            })) {
            $commandLine = [string]$process.CommandLine
            if ($scriptNames | Where-Object { $commandLine -match [regex]::Escape($_) }) {
                Stop-Process -Id ([int]$process.ProcessId) -Force -ErrorAction SilentlyContinue
            }
        }
    }
    catch {
        Write-Warning $_.Exception.Message
    }
}

$sourceDirectory = Normalize-InstallPath -Path $sourceDirectory
$InstallDirectory = Normalize-InstallPath -Path $InstallDirectory
$runtimeFiles = @(
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
    'Start-DevSpaceStatus.cmd',
    'Check-DevSpaceStatus.cmd',
    'Open-DevSpaceStatusSettings.ps1',
    'Stop-DevSpaceStatusPet.ps1',
    'Uninstall-DevSpaceStatus.ps1',
    'Install-DevSpaceStatus.ps1',
    'Install-DevSpaceStatus.cmd',
    'Install.cmd',
    'Settings.cmd',
    'Uninstall.cmd'
)

foreach ($file in $runtimeFiles) {
    if (-not (Test-Path -LiteralPath (Join-Path $sourceDirectory $file))) {
        throw "Required release file is missing: $file"
    }
}

if (-not $NoStopExisting) {
    Stop-ExistingCopies
    Start-Sleep -Milliseconds 500
}

if ($sourceDirectory -ne $InstallDirectory) {
    [void](New-Item -ItemType Directory -Path $InstallDirectory -Force)
    foreach ($file in $runtimeFiles) {
        $destination = Join-Path $InstallDirectory $file
        $destinationParent = Split-Path -Parent $destination
        if (-not (Test-Path -LiteralPath $destinationParent)) {
            [void](New-Item -ItemType Directory -Path $destinationParent -Force)
        }
        Copy-Item -LiteralPath (Join-Path $sourceDirectory $file) -Destination $destination -Force
    }
}

$launcherPath = Join-Path $InstallDirectory 'Start-DevSpaceStatus.cmd'
$settingsLauncherPath = Join-Path $InstallDirectory 'Settings.cmd'
$wshShell = New-Object -ComObject WScript.Shell
$desktopPath = [Environment]::GetFolderPath('Desktop')
$startupPath = [Environment]::GetFolderPath('Startup')
$desktopShortcutPath = Join-Path $desktopPath 'DevSpace Status Pet.lnk'
$settingsShortcutPath = Join-Path $desktopPath 'DevSpace Status Pet Settings.lnk'
$startupShortcutPath = Join-Path $startupPath 'DevSpace Status Pet.lnk'

if (-not $NoShortcuts) {
    $desktopShortcut = $wshShell.CreateShortcut($desktopShortcutPath)
    $desktopShortcut.TargetPath = $launcherPath
    $desktopShortcut.WorkingDirectory = $InstallDirectory
    $desktopShortcut.Description = 'DevSpace Status Pet'
    $desktopShortcut.IconLocation = "$env:SystemRoot\System32\shell32.dll,167"
    $desktopShortcut.Save()

    $settingsShortcut = $wshShell.CreateShortcut($settingsShortcutPath)
    $settingsShortcut.TargetPath = $settingsLauncherPath
    $settingsShortcut.WorkingDirectory = $InstallDirectory
    $settingsShortcut.Description = 'DevSpace Status Pet Settings'
    $settingsShortcut.IconLocation = "$env:SystemRoot\System32\shell32.dll,167"
    $settingsShortcut.Save()

    if ($StartWithWindows) {
        $startupShortcut = $wshShell.CreateShortcut($startupShortcutPath)
        $startupShortcut.TargetPath = $launcherPath
        $startupShortcut.WorkingDirectory = $InstallDirectory
        $startupShortcut.Description = 'DevSpace Status Pet'
        $startupShortcut.IconLocation = "$env:SystemRoot\System32\shell32.dll,167"
        $startupShortcut.Save()
    }
    elseif (Test-Path -LiteralPath $startupShortcutPath) {
        Remove-Item -LiteralPath $startupShortcutPath -Force
    }

    foreach ($legacyPath in @(
        (Join-Path $desktopPath 'DevSpace 状態.lnk'),
        (Join-Path $startupPath 'DevSpace Status.lnk')
    )) {
        if (Test-Path -LiteralPath $legacyPath) {
            Remove-Item -LiteralPath $legacyPath -Force -ErrorAction SilentlyContinue
        }
    }
}

$configPath = Join-Path $env:USERPROFILE '.devspace\config.json'
$devSpaceCommand = Get-Command devspace.cmd -ErrorAction SilentlyContinue
$devSpaceRunning = $false
try {
    $devSpaceRunning = $null -ne (Get-CimInstance Win32_Process -ErrorAction Stop |
        Where-Object { $_.Name -eq 'node.exe' -and $_.CommandLine -match '@waishnav[\\/]devspace' -and $_.CommandLine -match '\bserve\b' } |
        Select-Object -First 1)
}
catch {
    $devSpaceRunning = $false
}
$devSpaceDetected = (Test-Path -LiteralPath $configPath) -or $null -ne $devSpaceCommand -or $devSpaceRunning

if (-not $NoStart) {
    Start-Process -FilePath $launcherPath
}

Write-Host (I 'InstallerDone') -ForegroundColor Green
Write-Host (I 'InstalledTo' @($InstallDirectory))
if (-not $NoShortcuts) {
    Write-Host (I 'DesktopShortcut' @($desktopShortcutPath))
    if ($StartWithWindows) {
        Write-Host (I 'StartupEnabled')
    }
}
if ($devSpaceDetected) {
    Write-Host (I 'DevSpaceDetected') -ForegroundColor Green
}
else {
    Write-Warning (I 'DevSpaceNotDetected')
}
Write-Host (I 'InstallerHint')
