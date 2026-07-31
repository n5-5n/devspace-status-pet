[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$files = @(
    'DevSpaceLocalization.ps1',
    'DevSpaceStatus.ps1',
    'DevSpacePet.ps1',
    'Install-DevSpaceStatus.ps1',
    'Open-DevSpaceStatusSettings.ps1',
    'Stop-DevSpaceStatusPet.ps1',
    'Uninstall-DevSpaceStatus.ps1',
    'scripts\Build-Release.ps1',
    'scripts\Build-DotNetRelease.ps1',
    'tests\InstallSmoke.ps1',
    'tests\DotNetInstallSmoke.ps1'
)

$failed = $false
foreach ($file in $files) {
    $path = Join-Path $root $file
    $tokens = $null
    $errors = $null
    [System.Management.Automation.Language.Parser]::ParseFile($path, [ref]$tokens, [ref]$errors) | Out-Null

    if ($errors.Count -gt 0) {
        $failed = $true
        Write-Host "[FAIL] $file" -ForegroundColor Red
        foreach ($parseError in $errors) {
            Write-Host "  Line $($parseError.Extent.StartLineNumber): $($parseError.Message)" -ForegroundColor Red
        }
    }
    else {
        Write-Host "[OK] $file" -ForegroundColor Green
    }
}

if ($failed) {
    throw 'PowerShell parse validation failed.'
}

& powershell.exe -NoProfile -ExecutionPolicy Bypass -File (Join-Path $root 'DevSpaceStatus.ps1') -SelfTest
if ($LASTEXITCODE -ne 0) {
    throw 'Portability and localization self-test failed.'
}

& powershell.exe -NoProfile -ExecutionPolicy Bypass -File (Join-Path $root 'DevSpacePet.ps1') -SelfTest
if ($LASTEXITCODE -ne 0) {
    throw 'Pet localization self-test failed.'
}
