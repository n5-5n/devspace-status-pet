[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$files = @(
    'DevSpaceStatus.ps1',
    'DevSpacePet.ps1',
    'Install-DevSpaceStatus.ps1'
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
