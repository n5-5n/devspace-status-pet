[CmdletBinding()]
param(
    [string]$DotNetPath = 'dotnet',
    [string]$OutputDirectory = '',
    [switch]$SkipTests
)

Set-StrictMode -Version 2.0
$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
if ([string]::IsNullOrWhiteSpace($OutputDirectory)) {
    $OutputDirectory = Join-Path $root 'artifacts'
}

$version = [System.IO.File]::ReadAllText((Join-Path $root 'DOTNET_VERSION')).Trim()
if ([string]::IsNullOrWhiteSpace($version)) {
    throw 'DOTNET_VERSION is empty.'
}

$project = Join-Path $root 'src\DevSpaceStatusPet\DevSpaceStatusPet.csproj'
$smokeProject = Join-Path $root 'tests\DevSpaceStatusPet.Smoke\DevSpaceStatusPet.Smoke.csproj'
$packageName = "DevSpace-Status-Pet-v$version-win-x64"
$publishDirectory = Join-Path $OutputDirectory "$packageName-publish"
$stageDirectory = Join-Path $OutputDirectory $packageName
$zipPath = Join-Path $OutputDirectory "$packageName.zip"
$shaPath = "$zipPath.sha256"

New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
foreach ($path in @($publishDirectory, $stageDirectory, $zipPath, $shaPath)) {
    if (Test-Path -LiteralPath $path) {
        Remove-Item -LiteralPath $path -Recurse -Force
    }
}

& $DotNetPath build $project -c Release -warnaserror
if ($LASTEXITCODE -ne 0) {
    throw 'The .NET release build failed.'
}

if (-not $SkipTests) {
    & $DotNetPath run --project $smokeProject -c Release
    if ($LASTEXITCODE -ne 0) {
        throw 'The .NET smoke test failed.'
    }
}

& $DotNetPath publish $project `
    -c Release `
    -r win-x64 `
    --self-contained true `
    --no-restore `
    -o $publishDirectory `
    -p:Version=$version
if ($LASTEXITCODE -ne 0) {
    throw 'The .NET single-file publish failed.'
}

$publishedFiles = @(Get-ChildItem -LiteralPath $publishDirectory -File)
if ($publishedFiles.Count -ne 1 -or $publishedFiles[0].Name -ne 'DevSpaceStatusPet.exe') {
    throw "Expected one DevSpaceStatusPet.exe, found: $($publishedFiles.Name -join ', ')"
}

New-Item -ItemType Directory -Path $stageDirectory -Force | Out-Null
Copy-Item -LiteralPath $publishedFiles[0].FullName -Destination (Join-Path $stageDirectory 'DevSpaceStatusPet.exe')
foreach ($file in @(
    'README.md',
    'README.en.md',
    'README.dotnet.md',
    'README.dotnet.en.md',
    'RELEASE_NOTES.md',
    'LICENSE',
    'CHANGELOG.md',
    'VERSIONING.md',
    'DOTNET_VERSION')) {
    Copy-Item -LiteralPath (Join-Path $root $file) -Destination (Join-Path $stageDirectory $file)
}

Compress-Archive -Path $stageDirectory -DestinationPath $zipPath -CompressionLevel Optimal
$hash = (Get-FileHash -LiteralPath $zipPath -Algorithm SHA256).Hash.ToLowerInvariant()
$line = "$hash  $([System.IO.Path]::GetFileName($zipPath))`n"
[System.IO.File]::WriteAllText($shaPath, $line, (New-Object System.Text.UTF8Encoding($false)))

[pscustomobject]@{
    Version = $version
    PackageName = $packageName
    Executable = $publishedFiles[0].FullName
    ZipPath = $zipPath
    Sha256Path = $shaPath
    Sha256 = $hash
    ExecutableSizeMB = [Math]::Round($publishedFiles[0].Length / 1MB, 1)
}
