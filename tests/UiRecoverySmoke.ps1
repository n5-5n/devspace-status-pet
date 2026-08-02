[CmdletBinding()]
param(
    [string]$ExecutablePath = "$env:LOCALAPPDATA\DevSpaceStatusPetV2\DevSpaceStatusPet.exe",
    [int]$RecoveryTimeoutSeconds = 10,
    [int]$StabilitySeconds = 20
)

Set-StrictMode -Version 2.0
$ErrorActionPreference = 'Stop'

Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
Add-Type -AssemblyName System.Windows.Forms
Add-Type -Namespace DevSpaceStatusPetSmoke -Name NativeWindow -MemberDefinition @'
[System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true)]
public static extern bool SetWindowPos(
    System.IntPtr windowHandle,
    System.IntPtr insertAfter,
    int x,
    int y,
    int width,
    int height,
    uint flags);

[System.Runtime.InteropServices.DllImport("user32.dll")]
public static extern bool ShowWindow(System.IntPtr windowHandle, int command);

[System.Runtime.InteropServices.DllImport("user32.dll")]
public static extern bool IsWindowVisible(System.IntPtr windowHandle);

[System.Runtime.InteropServices.DllImport("user32.dll")]
public static extern bool IsIconic(System.IntPtr windowHandle);

[System.Runtime.InteropServices.DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
public static extern System.IntPtr GetWindowLongPtr(System.IntPtr windowHandle, int index);

[System.Runtime.InteropServices.DllImport("user32.dll")]
public static extern System.IntPtr MonitorFromWindow(System.IntPtr windowHandle, uint flags);

[System.Runtime.InteropServices.DllImport("user32.dll")]
public static extern uint GetGuiResources(System.IntPtr processHandle, uint flags);
'@

function Assert-Condition {
    param(
        [bool]$Condition,
        [string]$Message
    )

    if (-not $Condition) {
        throw $Message
    }
}

function Get-PetWindow {
    param([int]$ProcessId)

    $condition = New-Object System.Windows.Automation.PropertyCondition(
        [System.Windows.Automation.AutomationElement]::ProcessIdProperty,
        $ProcessId)
    $windows = [System.Windows.Automation.AutomationElement]::RootElement.FindAll(
        [System.Windows.Automation.TreeScope]::Children,
        $condition)

    foreach ($window in $windows) {
        if ($window.Current.Name -eq 'DevSpace Status Pet') {
            return $window
        }
    }
    return $null
}

function Wait-PetWindow {
    param(
        [int]$ProcessId,
        [int]$TimeoutSeconds = 15
    )

    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    do {
        $window = Get-PetWindow -ProcessId $ProcessId
        if ($null -ne $window) {
            return $window
        }
        Start-Sleep -Milliseconds 250
    } while ((Get-Date) -lt $deadline)

    throw 'Timed out waiting for the pet window.'
}

function Wait-Recovery {
    param(
        [int]$ProcessId,
        [scriptblock]$Predicate,
        [string]$FailureMessage
    )

    $deadline = (Get-Date).AddSeconds($RecoveryTimeoutSeconds)
    do {
        $window = Get-PetWindow -ProcessId $ProcessId
        if ($null -ne $window -and (& $Predicate $window)) {
            return $window
        }
        Start-Sleep -Milliseconds 250
    } while ((Get-Date) -lt $deadline)

    throw $FailureMessage
}

if (-not (Test-Path -LiteralPath $ExecutablePath)) {
    throw "Executable not found: $ExecutablePath"
}
$ExecutablePath = (Resolve-Path -LiteralPath $ExecutablePath).Path

Get-Process DevSpaceStatusPet -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Milliseconds 700
$process = Start-Process -FilePath $ExecutablePath -PassThru
$petWindow = Wait-PetWindow -ProcessId $process.Id
$initialBounds = $petWindow.Current.BoundingRectangle

1..5 | ForEach-Object { Start-Process -FilePath $ExecutablePath | Out-Null }
Start-Sleep -Seconds 2
$instanceCount = @(Get-Process DevSpaceStatusPet -ErrorAction Stop).Count
Assert-Condition ($instanceCount -eq 1) "Single-instance protection failed: $instanceCount processes"

$noSizeNoZOrderNoActivate = 0x0001 -bor 0x0004 -bor 0x0010
$noMoveNoSizeNoActivate = 0x0001 -bor 0x0002 -bor 0x0010
$notTopMost = [IntPtr](-2)
$extendedStyleIndex = -20
$topMostStyle = 0x00000008

$windowHandle = [IntPtr]$petWindow.Current.NativeWindowHandle
[void][DevSpaceStatusPetSmoke.NativeWindow]::SetWindowPos(
    $windowHandle,
    [IntPtr]::Zero,
    10000,
    10000,
    0,
    0,
    $noSizeNoZOrderNoActivate)
$positiveRecovery = Wait-Recovery -ProcessId $process.Id -FailureMessage 'Positive off-screen recovery failed.' -Predicate {
    param($window)
    [DevSpaceStatusPetSmoke.NativeWindow]::MonitorFromWindow(
        [IntPtr]$window.Current.NativeWindowHandle,
        0) -ne [IntPtr]::Zero
}
$positiveBounds = $positiveRecovery.Current.BoundingRectangle

$windowHandle = [IntPtr]$positiveRecovery.Current.NativeWindowHandle
[void][DevSpaceStatusPetSmoke.NativeWindow]::SetWindowPos(
    $windowHandle,
    [IntPtr]::Zero,
    -10000,
    -10000,
    0,
    0,
    $noSizeNoZOrderNoActivate)
$negativeRecovery = Wait-Recovery -ProcessId $process.Id -FailureMessage 'Negative off-screen recovery failed.' -Predicate {
    param($window)
    [DevSpaceStatusPetSmoke.NativeWindow]::MonitorFromWindow(
        [IntPtr]$window.Current.NativeWindowHandle,
        0) -ne [IntPtr]::Zero
}
$negativeBounds = $negativeRecovery.Current.BoundingRectangle

$windowHandle = [IntPtr]$negativeRecovery.Current.NativeWindowHandle
[void][DevSpaceStatusPetSmoke.NativeWindow]::ShowWindow($windowHandle, 6)
$minimizeRecovery = Wait-Recovery -ProcessId $process.Id -FailureMessage 'Minimized-window recovery failed.' -Predicate {
    param($window)
    -not [DevSpaceStatusPetSmoke.NativeWindow]::IsIconic([IntPtr]$window.Current.NativeWindowHandle)
}
$minimizeBounds = $minimizeRecovery.Current.BoundingRectangle

$windowHandle = [IntPtr]$minimizeRecovery.Current.NativeWindowHandle
[void][DevSpaceStatusPetSmoke.NativeWindow]::ShowWindow($windowHandle, 0)
$hiddenRecovery = Wait-Recovery -ProcessId $process.Id -FailureMessage 'Hidden-window recovery failed.' -Predicate {
    param($window)
    [DevSpaceStatusPetSmoke.NativeWindow]::IsWindowVisible([IntPtr]$window.Current.NativeWindowHandle)
}

$windowHandle = [IntPtr]$hiddenRecovery.Current.NativeWindowHandle
[void][DevSpaceStatusPetSmoke.NativeWindow]::SetWindowPos(
    $windowHandle,
    $notTopMost,
    0,
    0,
    0,
    0,
    $noMoveNoSizeNoActivate)
$topMostRecovery = Wait-Recovery -ProcessId $process.Id -FailureMessage 'Native TopMost recovery failed.' -Predicate {
    param($window)
    $style = [DevSpaceStatusPetSmoke.NativeWindow]::GetWindowLongPtr(
        [IntPtr]$window.Current.NativeWindowHandle,
        $extendedStyleIndex).ToInt64()
    ($style -band $topMostStyle) -ne 0
}

$samples = @()
$cpuStart = (Get-Process -Id $process.Id).TotalProcessorTime.TotalSeconds
$sampleCount = [Math]::Max(2, $StabilitySeconds * 2)
1..$sampleCount | ForEach-Object {
    Start-Sleep -Milliseconds 500
    $sample = Get-Process -Id $process.Id -ErrorAction Stop
    Assert-Condition $sample.Responding 'The pet process stopped responding during the stability sample.'
    $samples += [pscustomobject]@{
        WorkingSet = $sample.WorkingSet64
        PrivateMemory = $sample.PrivateMemorySize64
        Handles = $sample.HandleCount
        Threads = $sample.Threads.Count
        GdiObjects = [DevSpaceStatusPetSmoke.NativeWindow]::GetGuiResources($sample.Handle, 0)
        UserObjects = [DevSpaceStatusPetSmoke.NativeWindow]::GetGuiResources($sample.Handle, 1)
    }
}
$finalProcess = Get-Process -Id $process.Id -ErrorAction Stop

$result = [pscustomobject]@{
    ProcessId = $process.Id
    SingleInstanceCount = $instanceCount
    InitialBounds = $initialBounds
    PositiveRecoveryBounds = $positiveBounds
    NegativeRecoveryBounds = $negativeBounds
    MinimizeRecoveryBounds = $minimizeBounds
    HiddenRecovered = [DevSpaceStatusPetSmoke.NativeWindow]::IsWindowVisible(
        [IntPtr]$topMostRecovery.Current.NativeWindowHandle)
    TopMostRecovered = $true
    WorkingSetMinMB = [Math]::Round((($samples.WorkingSet | Measure-Object -Minimum).Minimum) / 1MB, 1)
    WorkingSetMaxMB = [Math]::Round((($samples.WorkingSet | Measure-Object -Maximum).Maximum) / 1MB, 1)
    PrivateMemoryMinMB = [Math]::Round((($samples.PrivateMemory | Measure-Object -Minimum).Minimum) / 1MB, 1)
    PrivateMemoryMaxMB = [Math]::Round((($samples.PrivateMemory | Measure-Object -Maximum).Maximum) / 1MB, 1)
    HandleMin = ($samples.Handles | Measure-Object -Minimum).Minimum
    HandleMax = ($samples.Handles | Measure-Object -Maximum).Maximum
    ThreadMin = ($samples.Threads | Measure-Object -Minimum).Minimum
    ThreadMax = ($samples.Threads | Measure-Object -Maximum).Maximum
    GdiObjectMin = ($samples.GdiObjects | Measure-Object -Minimum).Minimum
    GdiObjectMax = ($samples.GdiObjects | Measure-Object -Maximum).Maximum
    UserObjectMin = ($samples.UserObjects | Measure-Object -Minimum).Minimum
    UserObjectMax = ($samples.UserObjects | Measure-Object -Maximum).Maximum
    CpuSeconds = [Math]::Round($finalProcess.TotalProcessorTime.TotalSeconds - $cpuStart, 2)
    Responding = $finalProcess.Responding
}
$result | Format-List

Write-Host '[OK] Live pet recovery, single-instance, and stability smoke test' -ForegroundColor Green
