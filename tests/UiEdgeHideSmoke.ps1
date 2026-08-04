[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$ExecutablePath,
    [int]$TimeoutSeconds = 12
)

Set-StrictMode -Version 2.0
$ErrorActionPreference = 'Stop'

Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
Add-Type -Namespace DevSpaceStatusPetSmoke -Name NativeInput -MemberDefinition @'
[System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true)]
public static extern bool PostMessage(System.IntPtr windowHandle, uint message, System.IntPtr wParam, System.IntPtr lParam);
'@

function Assert-Condition {
    param([bool]$Condition, [string]$Message)
    if (-not $Condition) { throw $Message }
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
        if ($window.Current.Name -eq 'DevSpace Status Pet') { return $window }
    }
    return $null
}

function Wait-PetWindow {
    param([int]$ProcessId)

    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    do {
        $window = Get-PetWindow -ProcessId $ProcessId
        if ($null -ne $window) { return $window }
        Start-Sleep -Milliseconds 200
    } while ((Get-Date) -lt $deadline)
    throw 'Timed out waiting for the pet window.'
}

function Find-MenuItem {
    param([int]$ProcessId)

    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    $japaneseName = [string]::Concat([char[]]@(0x753B, 0x9762, 0x7AEF, 0x306B, 0x96A0, 0x3059))
    $chineseName = [string]::Concat([char[]]@(0x9690, 0x85CF, 0x5230, 0x5C4F, 0x5E55, 0x8FB9, 0x7F18))
    $names = @($japaneseName, 'Hide at screen edge', $chineseName)
    do {
        $processCondition = New-Object System.Windows.Automation.PropertyCondition(
            [System.Windows.Automation.AutomationElement]::ProcessIdProperty,
            $ProcessId)
        $elements = [System.Windows.Automation.AutomationElement]::RootElement.FindAll(
            [System.Windows.Automation.TreeScope]::Descendants,
            $processCondition)
        foreach ($element in $elements) {
            if ($names -contains $element.Current.Name) { return $element }
        }
        Start-Sleep -Milliseconds 150
    } while ((Get-Date) -lt $deadline)
    throw 'Could not find the screen-edge hide menu item.'
}

function Post-WindowMessage {
    param(
        [IntPtr]$WindowHandle,
        [uint32]$Message,
        [int]$X = 0,
        [int]$Y = 0,
        [int]$WParam = 0
    )

    $packed = (($Y -band 0xFFFF) -shl 16) -bor ($X -band 0xFFFF)
    if (-not [DevSpaceStatusPetSmoke.NativeInput]::PostMessage(
            $WindowHandle,
            $Message,
            [IntPtr]$WParam,
            [IntPtr]$packed)) {
        throw "Could not post window message 0x$($Message.ToString('X'))."
    }
}

function Open-PetContextMenu {
    param([IntPtr]$WindowHandle)

    if (-not [DevSpaceStatusPetSmoke.NativeInput]::PostMessage(
            $WindowHandle,
            0x007B,
            $WindowHandle,
            [IntPtr](-1))) {
        throw 'Could not open the pet context menu.'
    }
}

function Get-EdgeHiddenRecord {
    param(
        [int]$ProcessId,
        [string]$RuntimeLogPath
    )

    if (-not (Test-Path -LiteralPath $RuntimeLogPath)) { return $null }
    $pattern = "pid=$ProcessId \| pet-edge-hidden \| side=(?<side>Left|Right); normal=\{X=(?<nx>-?\d+),Y=(?<ny>-?\d+)\}; hiddenBounds=\{X=(?<hx>-?\d+),Y=(?<hy>-?\d+),Width=(?<hw>\d+),Height=(?<hh>\d+)\}; area=\{X=(?<ax>-?\d+),Y=(?<ay>-?\d+),Width=(?<aw>\d+),Height=(?<ah>\d+)\}"
    $line = Get-Content -LiteralPath $RuntimeLogPath -Tail 80 -ErrorAction SilentlyContinue |
        Where-Object { $_ -match $pattern } |
        Select-Object -Last 1
    if ($null -eq $line -or $line -notmatch $pattern) { return $null }

    [pscustomobject]@{
        Side = $Matches.side
        NormalX = [int]$Matches.nx
        NormalY = [int]$Matches.ny
        HiddenX = [int]$Matches.hx
        HiddenY = [int]$Matches.hy
        HiddenWidth = [int]$Matches.hw
        HiddenHeight = [int]$Matches.hh
        AreaX = [int]$Matches.ax
        AreaY = [int]$Matches.ay
        AreaWidth = [int]$Matches.aw
        AreaHeight = [int]$Matches.ah
    }
}

function Get-EdgeRestoredRecord {
    param(
        [int]$ProcessId,
        [string]$RuntimeLogPath
    )

    if (-not (Test-Path -LiteralPath $RuntimeLogPath)) { return $null }
    $pattern = "pid=$ProcessId \| pet-edge-restored \| reason=(?<reason>[^;]+); before=\{X=-?\d+,Y=-?\d+,Width=\d+,Height=\d+\}; after=\{X=(?<x>-?\d+),Y=(?<y>-?\d+),Width=(?<w>\d+),Height=(?<h>\d+)\}"
    $line = Get-Content -LiteralPath $RuntimeLogPath -Tail 80 -ErrorAction SilentlyContinue |
        Where-Object { $_ -match $pattern } |
        Select-Object -Last 1
    if ($null -eq $line -or $line -notmatch $pattern) { return $null }

    [pscustomobject]@{
        Reason = $Matches.reason
        X = [int]$Matches.x
        Y = [int]$Matches.y
        Width = [int]$Matches.w
        Height = [int]$Matches.h
    }
}

if (-not (Test-Path -LiteralPath $ExecutablePath)) {
    throw "Executable not found: $ExecutablePath"
}
$ExecutablePath = (Resolve-Path -LiteralPath $ExecutablePath).Path
$runtimeLogPath = Join-Path $env:LOCALAPPDATA 'DevSpaceStatusPet\logs\runtime.log'

Get-Process DevSpaceStatusPet -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Milliseconds 700
$process = Start-Process -FilePath $ExecutablePath -PassThru
$petWindow = Wait-PetWindow -ProcessId $process.Id
$initial = $petWindow.Current.BoundingRectangle
$windowHandle = [IntPtr]$petWindow.Current.NativeWindowHandle

Open-PetContextMenu -WindowHandle $windowHandle
$hideItem = Find-MenuItem -ProcessId $process.Id
$invoke = $hideItem.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern)
$invoke.Invoke()

$deadline = (Get-Date).AddSeconds($TimeoutSeconds)
$hiddenRecord = $null
do {
    Start-Sleep -Milliseconds 200
    $hiddenRecord = Get-EdgeHiddenRecord -ProcessId $process.Id -RuntimeLogPath $runtimeLogPath
} while ($null -eq $hiddenRecord -and (Get-Date) -lt $deadline)
Assert-Condition ($null -ne $hiddenRecord) 'The application did not record an edge-hidden state.'

$visibleWidth = if ($hiddenRecord.Side -eq 'Left') {
    ($hiddenRecord.HiddenX + $hiddenRecord.HiddenWidth) - $hiddenRecord.AreaX
}
else {
    ($hiddenRecord.AreaX + $hiddenRecord.AreaWidth) - $hiddenRecord.HiddenX
}
Assert-Condition ($visibleWidth -ge 30 -and $visibleWidth -le 38) "Unexpected visible edge width: $visibleWidth"

Start-Sleep -Seconds 3
$restoredTooEarly = Get-EdgeRestoredRecord -ProcessId $process.Id -RuntimeLogPath $runtimeLogPath
Assert-Condition ($null -eq $restoredTooEarly) 'The visibility watchdog undid intentional edge hiding.'
Assert-Condition ((Get-Process -Id $process.Id).Responding) 'The pet stopped responding while hidden.'

$windowHandle = [IntPtr](Wait-PetWindow -ProcessId $process.Id).Current.NativeWindowHandle
$handleX = if ($hiddenRecord.Side -eq 'Left') {
    $hiddenRecord.HiddenWidth - [Math]::Min(8, $visibleWidth - 1)
}
else {
    [Math]::Min(8, $visibleWidth - 1)
}
$handleY = [int]($hiddenRecord.HiddenHeight / 2)
Post-WindowMessage -WindowHandle $windowHandle -Message 0x0200 -X $handleX -Y $handleY

$deadline = (Get-Date).AddSeconds($TimeoutSeconds)
$restoredRecord = $null
do {
    Start-Sleep -Milliseconds 200
    $restoredRecord = Get-EdgeRestoredRecord -ProcessId $process.Id -RuntimeLogPath $runtimeLogPath
} while ($null -eq $restoredRecord -and (Get-Date) -lt $deadline)

if ($null -eq $restoredRecord) {
    Post-WindowMessage -WindowHandle $windowHandle -Message 0x0201 -X $handleX -Y $handleY -WParam 1
    Post-WindowMessage -WindowHandle $windowHandle -Message 0x0202 -X $handleX -Y $handleY
    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    do {
        Start-Sleep -Milliseconds 200
        $restoredRecord = Get-EdgeRestoredRecord -ProcessId $process.Id -RuntimeLogPath $runtimeLogPath
    } while ($null -eq $restoredRecord -and (Get-Date) -lt $deadline)
}

Assert-Condition ($null -ne $restoredRecord) 'Hover/click did not restore the pet from the screen edge.'
Assert-Condition ($restoredRecord.X -eq $hiddenRecord.NormalX) 'The pet did not return to its previous horizontal position.'
Assert-Condition ($restoredRecord.Y -eq $hiddenRecord.NormalY) 'The pet did not return to its previous vertical position.'
Assert-Condition ((Get-Process -Id $process.Id).Responding) 'The pet stopped responding during edge-hide testing.'

[pscustomobject]@{
    InitialBounds = $initial
    HiddenBounds = "$($hiddenRecord.HiddenX),$($hiddenRecord.HiddenY),$($hiddenRecord.HiddenWidth),$($hiddenRecord.HiddenHeight)"
    HiddenSide = $hiddenRecord.Side
    VisibleHandleWidth = $visibleWidth
    RestoredBounds = "$($restoredRecord.X),$($restoredRecord.Y),$($restoredRecord.Width),$($restoredRecord.Height)"
    RestoreReason = $restoredRecord.Reason
    Responding = $true
} | Format-List

Write-Host '[OK] Live screen-edge hide and restore smoke test' -ForegroundColor Green
