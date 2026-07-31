[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$ExecutablePath
)

Set-StrictMode -Version 2.0
$ErrorActionPreference = 'Stop'

$executable = (Resolve-Path -LiteralPath $ExecutablePath).Path
$tempRoot = Join-Path $env:TEMP "DevSpaceStatusPetV2-Smoke-$PID"
$installDirectory = Join-Path $tempRoot 'Installed App With Spaces'
$shortcutPath = Join-Path $tempRoot 'DevSpace Status Pet Test.lnk'
$runValue = "DevSpaceStatusPetTest$PID"
$installedExecutable = Join-Path $installDirectory 'DevSpaceStatusPet.exe'

$env:DEVSPACE_STATUS_PET_INSTALL_DIR = $installDirectory
$env:DEVSPACE_STATUS_PET_SHORTCUT_PATH = $shortcutPath
$env:DEVSPACE_STATUS_PET_RUN_VALUE = $runValue

try {
    New-Item -ItemType Directory -Path $tempRoot -Force | Out-Null
    $installProcess = Start-Process `
        -FilePath $executable `
        -ArgumentList @('--install', '--silent', '--no-launch') `
        -Wait `
        -PassThru
    if ($installProcess.ExitCode -ne 0) {
        throw "Installer exited with code $($installProcess.ExitCode)"
    }

    if (-not (Test-Path -LiteralPath $installedExecutable)) {
        throw "Installed executable was not created: $installedExecutable"
    }
    if (-not (Test-Path -LiteralPath $shortcutPath)) {
        throw "Shortcut was not created: $shortcutPath"
    }

    $runValueData = (Get-ItemProperty `
        'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run' `
        -Name $runValue `
        -ErrorAction Stop).$runValue
    if ([string]$runValueData -notmatch [regex]::Escape($installedExecutable)) {
        throw "Unexpected Run value: $runValueData"
    }

    $uninstallProcess = Start-Process `
        -FilePath $installedExecutable `
        -ArgumentList @('--uninstall', '--silent') `
        -Wait `
        -PassThru
    if ($uninstallProcess.ExitCode -ne 0) {
        throw "Uninstaller exited with code $($uninstallProcess.ExitCode)"
    }

    $deadline = (Get-Date).AddSeconds(12)
    while ((Test-Path -LiteralPath $installDirectory) -and (Get-Date) -lt $deadline) {
        Start-Sleep -Milliseconds 500
    }

    if (Test-Path -LiteralPath $installDirectory) {
        throw "Install directory was not removed: $installDirectory"
    }
    if (Test-Path -LiteralPath $shortcutPath) {
        throw "Shortcut was not removed: $shortcutPath"
    }
    $remainingRunValue = Get-ItemProperty `
        'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run' `
        -Name $runValue `
        -ErrorAction SilentlyContinue
    if ($null -ne $remainingRunValue) {
        throw "Run value was not removed: $runValue"
    }

    Write-Host '[OK] Isolated .NET self-install and self-uninstall smoke test' -ForegroundColor Green
}
finally {
    Remove-ItemProperty `
        'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run' `
        -Name $runValue `
        -ErrorAction SilentlyContinue
    Remove-Item -LiteralPath $tempRoot -Recurse -Force -ErrorAction SilentlyContinue
    Remove-Item Env:DEVSPACE_STATUS_PET_INSTALL_DIR -ErrorAction SilentlyContinue
    Remove-Item Env:DEVSPACE_STATUS_PET_SHORTCUT_PATH -ErrorAction SilentlyContinue
    Remove-Item Env:DEVSPACE_STATUS_PET_RUN_VALUE -ErrorAction SilentlyContinue
}
