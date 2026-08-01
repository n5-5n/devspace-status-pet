[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$SourceDirectory,

    [Parameter(Mandatory = $true)]
    [string]$Version,

    [string]$DotNetPath = 'dotnet',
    [string]$OutputDirectory = ''
)

Set-StrictMode -Version 2.0
$ErrorActionPreference = 'Stop'

$sourceRoot = (Resolve-Path -LiteralPath $SourceDirectory).Path
$project = Join-Path $sourceRoot 'src\DevSpaceStatusPet\DevSpaceStatusPet.csproj'
if (-not (Test-Path -LiteralPath $project)) {
    throw "Historical .NET project was not found: $project"
}

$match = [regex]::Match($Version, '^(\d+)\.(\d+)\.(\d+)(?:-([0-9A-Za-z.-]+))?$')
if (-not $match.Success) {
    throw "Unsupported semantic version: $Version"
}

$major = [int]$match.Groups[1].Value
$minor = [int]$match.Groups[2].Value
$patch = [int]$match.Groups[3].Value
$prerelease = $match.Groups[4].Value
$revision = 0
if ($prerelease -match '(\d+)$') {
    $revision = [int]$Matches[1]
}

$fileVersion = "$major.$minor.$patch.$revision"
$assemblyVersion = "$major.$minor.0.0"
if ([string]::IsNullOrWhiteSpace($OutputDirectory)) {
    $OutputDirectory = Join-Path (Split-Path -Parent $PSScriptRoot) 'artifacts\normalized-history'
}

$packageName = "DevSpace-Status-Pet-v$Version-win-x64"
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

& $DotNetPath publish $project `
    -c Release `
    -r win-x64 `
    --self-contained true `
    -o $publishDirectory `
    -p:Version=$Version `
    -p:InformationalVersion=$Version `
    -p:FileVersion=$fileVersion `
    -p:AssemblyVersion=$assemblyVersion `
    -p:IncludeSourceRevisionInInformationalVersion=false
if ($LASTEXITCODE -ne 0) {
    throw "Historical publish failed for $Version with exit code $LASTEXITCODE"
}

$publishedFiles = @(Get-ChildItem -LiteralPath $publishDirectory -File)
if ($publishedFiles.Count -ne 1 -or $publishedFiles[0].Name -ne 'DevSpaceStatusPet.exe') {
    throw "Expected one DevSpaceStatusPet.exe, found: $($publishedFiles.Name -join ', ')"
}

$actualProductVersion = [System.Diagnostics.FileVersionInfo]::GetVersionInfo($publishedFiles[0].FullName).ProductVersion
if ([string]$actualProductVersion -ne $Version) {
    throw "ProductVersion mismatch. Expected $Version, got $actualProductVersion"
}

New-Item -ItemType Directory -Path $stageDirectory -Force | Out-Null
Copy-Item -LiteralPath $publishedFiles[0].FullName -Destination (Join-Path $stageDirectory 'DevSpaceStatusPet.exe')

$replacementRules = @(
    @{ Pattern = 'v0\.3\.0-alpha\.(\d+)'; Replacement = 'v0.1.3-alpha.$1' },
    @{ Pattern = '(?<!v)0\.3\.0-alpha\.(\d+)'; Replacement = '0.1.3-alpha.$1' },
    @{ Pattern = 'v0\.2\.0-alpha\.(\d+)'; Replacement = 'v0.1.1-alpha.$1' },
    @{ Pattern = '(?<!v)0\.2\.0-alpha\.(\d+)'; Replacement = '0.1.1-alpha.$1' },
    @{ Pattern = 'v0\.2\.1'; Replacement = 'v0.1.2' },
    @{ Pattern = '(?<!v)0\.2\.1'; Replacement = '0.1.2' },
    @{ Pattern = 'v0\.2\.0'; Replacement = 'v0.1.1' },
    @{ Pattern = '(?<!v)0\.2\.0'; Replacement = '0.1.1' }
)

foreach ($name in @('README.md', 'README.en.md', 'RELEASE_NOTES.md', 'CHANGELOG.md')) {
    $source = Join-Path $sourceRoot $name
    if (-not (Test-Path -LiteralPath $source)) {
        continue
    }

    $text = [System.IO.File]::ReadAllText($source)
    foreach ($rule in $replacementRules) {
        $text = [regex]::Replace($text, $rule.Pattern, $rule.Replacement)
    }
    [System.IO.File]::WriteAllText(
        (Join-Path $stageDirectory $name),
        $text,
        (New-Object System.Text.UTF8Encoding($false)))
}

$license = Join-Path $sourceRoot 'LICENSE'
if (Test-Path -LiteralPath $license) {
    Copy-Item -LiteralPath $license -Destination (Join-Path $stageDirectory 'LICENSE')
}

[System.IO.File]::WriteAllText(
    (Join-Path $stageDirectory 'DOTNET_VERSION'),
    "$Version`n",
    (New-Object System.Text.UTF8Encoding($false)))
[System.IO.File]::WriteAllText(
    (Join-Path $stageDirectory 'VERSIONING-NOTE.txt'),
    "This package was rebuilt from the original source revision with the normalized version v$Version.`nSee VERSIONING.md in the repository for the historical mapping.`n",
    (New-Object System.Text.UTF8Encoding($false)))

Compress-Archive -Path $stageDirectory -DestinationPath $zipPath -CompressionLevel Optimal
$hash = (Get-FileHash -LiteralPath $zipPath -Algorithm SHA256).Hash.ToLowerInvariant()
[System.IO.File]::WriteAllText(
    $shaPath,
    "$hash  $([System.IO.Path]::GetFileName($zipPath))`n",
    (New-Object System.Text.UTF8Encoding($false)))

[pscustomobject]@{
    Version = $Version
    ProductVersion = $actualProductVersion
    FileVersion = [System.Diagnostics.FileVersionInfo]::GetVersionInfo($publishedFiles[0].FullName).FileVersion
    SourceDirectory = $sourceRoot
    ZipPath = $zipPath
    Sha256Path = $shaPath
    Sha256 = $hash
}
