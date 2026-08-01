[CmdletBinding()]
param(
    [string]$DotNetPath = 'dotnet',
    [string]$OutputDirectory = ''
)

Set-StrictMode -Version 2.0
$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
if ([string]::IsNullOrWhiteSpace($OutputDirectory)) {
    $OutputDirectory = Join-Path $root 'artifacts\normalized-history'
}

$mapping = @(
    @{ OldTag = 'v0.2.0-alpha.1'; NewVersion = '0.1.1-alpha.1' },
    @{ OldTag = 'v0.2.0-alpha.2'; NewVersion = '0.1.1-alpha.2' },
    @{ OldTag = 'v0.2.0-alpha.3'; NewVersion = '0.1.1-alpha.3' },
    @{ OldTag = 'v0.2.0-alpha.4'; NewVersion = '0.1.1-alpha.4' },
    @{ OldTag = 'v0.2.0-alpha.5'; NewVersion = '0.1.1-alpha.5' },
    @{ OldTag = 'v0.2.0-alpha.6'; NewVersion = '0.1.1-alpha.6' },
    @{ OldTag = 'v0.2.0'; NewVersion = '0.1.1' },
    @{ OldTag = 'v0.2.1'; NewVersion = '0.1.2' },
    @{ OldTag = 'v0.3.0-alpha.1'; NewVersion = '0.1.3-alpha.1' },
    @{ OldTag = 'v0.3.0-alpha.2'; NewVersion = '0.1.3-alpha.2' },
    @{ OldTag = 'v0.3.0-alpha.3'; NewVersion = '0.1.3-alpha.3' }
)

$worktree = Join-Path (Split-Path -Parent $root) 'devspace-status-normalized-history-worktree'
$results = @()

foreach ($entry in $mapping) {
    if (Test-Path -LiteralPath $worktree) {
        & git -C $root worktree remove --force $worktree
        if ($LASTEXITCODE -ne 0) {
            throw "Could not remove temporary worktree: $worktree"
        }
    }

    & git -C $root worktree add --detach $worktree $entry.OldTag
    if ($LASTEXITCODE -ne 0) {
        throw "Could not create worktree for $($entry.OldTag)"
    }

    try {
        $result = & (Join-Path $PSScriptRoot 'Rebuild-HistoricalDotNetRelease.ps1') `
            -SourceDirectory $worktree `
            -Version $entry.NewVersion `
            -DotNetPath $DotNetPath `
            -OutputDirectory $OutputDirectory
        $results += $result
    }
    finally {
        & git -C $root worktree remove --force $worktree
        if ($LASTEXITCODE -ne 0) {
            throw "Could not remove temporary worktree after $($entry.OldTag)"
        }
    }
}

$results | Format-Table Version, ProductVersion, FileVersion, Sha256 -AutoSize
