[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$ExecutablePath,
    [int]$WarmupSeconds = 15,
    [int]$SampleSeconds = 60,
    [double]$MaxPrivateGrowthMB = 40,
    [double]$MaxWorkingSetGrowthMB = 60
)

Set-StrictMode -Version 2.0
$ErrorActionPreference = 'Stop'

Add-Type -Namespace DevSpaceStatusPetSmoke -Name MemoryNative -MemberDefinition @'
[System.Runtime.InteropServices.DllImport("user32.dll")]
public static extern uint GetGuiResources(System.IntPtr processHandle, uint flags);
'@

function Assert-Condition {
    param([bool]$Condition, [string]$Message)
    if (-not $Condition) {
        throw $Message
    }
}

if (-not (Test-Path -LiteralPath $ExecutablePath)) {
    throw "Executable not found: $ExecutablePath"
}
$ExecutablePath = (Resolve-Path -LiteralPath $ExecutablePath).Path

Get-Process DevSpaceStatusPet -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Milliseconds 700
$process = Start-Process -FilePath $ExecutablePath -PassThru

try {
    Start-Sleep -Seconds ([Math]::Max(1, $WarmupSeconds))
    $samples = @()
    $sampleCount = [Math]::Max(20, $SampleSeconds)
    $cpuStart = (Get-Process -Id $process.Id -ErrorAction Stop).TotalProcessorTime.TotalSeconds

    1..$sampleCount | ForEach-Object {
        Start-Sleep -Seconds 1
        $sample = Get-Process -Id $process.Id -ErrorAction Stop
        Assert-Condition $sample.Responding 'The pet stopped responding during memory stability testing.'
        $samples += [pscustomobject]@{
            WorkingSetMB = $sample.WorkingSet64 / 1MB
            PrivateMB = $sample.PrivateMemorySize64 / 1MB
            Handles = $sample.HandleCount
            Threads = $sample.Threads.Count
            GdiObjects = [DevSpaceStatusPetSmoke.MemoryNative]::GetGuiResources($sample.Handle, 0)
            UserObjects = [DevSpaceStatusPetSmoke.MemoryNative]::GetGuiResources($sample.Handle, 1)
        }
    }

    $windowSize = [Math]::Min(10, [Math]::Max(3, [int]($samples.Count / 6)))
    $first = @($samples | Select-Object -First $windowSize)
    $last = @($samples | Select-Object -Last $windowSize)
    $privateStart = ($first.PrivateMB | Measure-Object -Average).Average
    $privateEnd = ($last.PrivateMB | Measure-Object -Average).Average
    $workingSetStart = ($first.WorkingSetMB | Measure-Object -Average).Average
    $workingSetEnd = ($last.WorkingSetMB | Measure-Object -Average).Average
    $privateGrowth = $privateEnd - $privateStart
    $workingSetGrowth = $workingSetEnd - $workingSetStart
    $finalProcess = Get-Process -Id $process.Id -ErrorAction Stop

    Assert-Condition ($privateGrowth -le $MaxPrivateGrowthMB) (
        "Private memory grew by {0:N1} MB; limit is {1:N1} MB." -f $privateGrowth, $MaxPrivateGrowthMB)
    Assert-Condition ($workingSetGrowth -le $MaxWorkingSetGrowthMB) (
        "Working set grew by {0:N1} MB; limit is {1:N1} MB." -f $workingSetGrowth, $MaxWorkingSetGrowthMB)
    Assert-Condition (($samples.GdiObjects | Measure-Object -Maximum).Maximum - ($samples.GdiObjects | Measure-Object -Minimum).Minimum -le 5) (
        'GDI object count was not stable during memory testing.')
    Assert-Condition (($samples.UserObjects | Measure-Object -Maximum).Maximum - ($samples.UserObjects | Measure-Object -Minimum).Minimum -le 5) (
        'USER object count was not stable during memory testing.')

    [pscustomobject]@{
        Version = $finalProcess.MainModule.FileVersionInfo.ProductVersion
        SampleSeconds = $sampleCount
        WorkingSetStartMB = [Math]::Round($workingSetStart, 1)
        WorkingSetEndMB = [Math]::Round($workingSetEnd, 1)
        WorkingSetGrowthMB = [Math]::Round($workingSetGrowth, 1)
        PrivateStartMB = [Math]::Round($privateStart, 1)
        PrivateEndMB = [Math]::Round($privateEnd, 1)
        PrivateGrowthMB = [Math]::Round($privateGrowth, 1)
        PrivateMinMB = [Math]::Round((($samples.PrivateMB | Measure-Object -Minimum).Minimum), 1)
        PrivateMaxMB = [Math]::Round((($samples.PrivateMB | Measure-Object -Maximum).Maximum), 1)
        GdiObjectMin = ($samples.GdiObjects | Measure-Object -Minimum).Minimum
        GdiObjectMax = ($samples.GdiObjects | Measure-Object -Maximum).Maximum
        UserObjectMin = ($samples.UserObjects | Measure-Object -Minimum).Minimum
        UserObjectMax = ($samples.UserObjects | Measure-Object -Maximum).Maximum
        HandleMin = ($samples.Handles | Measure-Object -Minimum).Minimum
        HandleMax = ($samples.Handles | Measure-Object -Maximum).Maximum
        ThreadMin = ($samples.Threads | Measure-Object -Minimum).Minimum
        ThreadMax = ($samples.Threads | Measure-Object -Maximum).Maximum
        CpuSeconds = [Math]::Round($finalProcess.TotalProcessorTime.TotalSeconds - $cpuStart, 2)
        Responding = $finalProcess.Responding
    } | Format-List

    Write-Host '[OK] Live memory stability smoke test' -ForegroundColor Green
}
finally {
    Get-Process -Id $process.Id -ErrorAction SilentlyContinue | Stop-Process -Force
}
