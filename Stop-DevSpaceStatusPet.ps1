[CmdletBinding()]
param(
    [string]$InstallDirectory = '',
    [int]$ExcludeProcessId = $PID
)

Set-StrictMode -Version 2.0
$ErrorActionPreference = 'Stop'
if ([string]::IsNullOrWhiteSpace($InstallDirectory)) {
    $InstallDirectory = $PSScriptRoot
}

function Normalize-CommandPath {
    param([string]$Path)

    if ([string]::IsNullOrWhiteSpace($Path)) {
        return ''
    }
    try {
        $Path = [System.IO.Path]::GetFullPath($Path)
    }
    catch {
        # Keep the supplied path.
    }
    return $Path.Replace('\', '/').TrimEnd('/').ToLowerInvariant()
}

$targets = @(
    (Join-Path $InstallDirectory 'DevSpaceStatus.ps1'),
    (Join-Path $InstallDirectory 'DevSpacePet.ps1')
) | ForEach-Object { Normalize-CommandPath -Path $_ }

$stopped = New-Object System.Collections.ArrayList
try {
    $processes = @(Get-CimInstance Win32_Process -ErrorAction Stop |
        Where-Object {
            $_.ProcessId -ne $ExcludeProcessId -and
            $_.Name -match '^(powershell|pwsh)(\.exe)?$' -and
            -not [string]::IsNullOrWhiteSpace([string]$_.CommandLine)
        })

    foreach ($process in $processes) {
        $commandLine = ([string]$process.CommandLine).Replace('\', '/').ToLowerInvariant()
        $matchesTarget = $false
        foreach ($target in $targets) {
            if ($target -and $commandLine.Contains($target)) {
                $matchesTarget = $true
                break
            }
        }

        if ($matchesTarget) {
            Stop-Process -Id ([int]$process.ProcessId) -Force -ErrorAction SilentlyContinue
            [void]$stopped.Add([int]$process.ProcessId)
        }
    }
}
catch {
    Write-Warning $_.Exception.Message
}

[pscustomobject]@{
    InstallDirectory = $InstallDirectory
    StoppedCount      = $stopped.Count
    ProcessIds        = @($stopped)
}
