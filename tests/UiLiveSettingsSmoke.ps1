[CmdletBinding()]
param(
    [string]$ExecutablePath = "$env:LOCALAPPDATA\DevSpaceStatusPetV2\DevSpaceStatusPet.exe"
)

Set-StrictMode -Version 2.0
$ErrorActionPreference = 'Stop'

Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
Add-Type -AssemblyName System.Windows.Forms

function Get-AppWindow {
    param(
        [int]$ProcessId,
        [scriptblock]$Predicate
    )

    $root = [System.Windows.Automation.AutomationElement]::RootElement
    $processCondition = New-Object System.Windows.Automation.PropertyCondition(
        [System.Windows.Automation.AutomationElement]::ProcessIdProperty,
        $ProcessId)
    $windows = $root.FindAll(
        [System.Windows.Automation.TreeScope]::Children,
        $processCondition)

    foreach ($window in $windows) {
        if (& $Predicate $window) {
            return $window
        }
    }
    return $null
}

function Wait-AppWindow {
    param(
        [int]$ProcessId,
        [scriptblock]$Predicate,
        [int]$TimeoutSeconds = 15
    )

    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    do {
        $window = Get-AppWindow -ProcessId $ProcessId -Predicate $Predicate
        if ($null -ne $window) {
            return $window
        }
        Start-Sleep -Milliseconds 250
    } while ((Get-Date) -lt $deadline)

    throw 'Timed out waiting for the application window.'
}

function Resolve-NumericAutomationElement {
    param([System.Windows.Automation.AutomationElement]$Element)

    foreach ($candidate in @($Element)) {
        foreach ($pattern in @(
            [System.Windows.Automation.RangeValuePattern]::Pattern,
            [System.Windows.Automation.ValuePattern]::Pattern)) {
            try {
                [void]$candidate.GetCurrentPattern($pattern)
                return $candidate
            }
            catch {
            }
        }
    }

    $editCondition = New-Object System.Windows.Automation.PropertyCondition(
        [System.Windows.Automation.AutomationElement]::ControlTypeProperty,
        [System.Windows.Automation.ControlType]::Edit)
    $edit = $Element.FindFirst(
        [System.Windows.Automation.TreeScope]::Descendants,
        $editCondition)
    if ($null -eq $edit) {
        throw 'No editable child was found for the numeric control.'
    }
    return $edit
}

function Get-AutomationNumericValue {
    param([System.Windows.Automation.AutomationElement]$Element)

    $target = Resolve-NumericAutomationElement -Element $Element
    try {
        $range = $target.GetCurrentPattern([System.Windows.Automation.RangeValuePattern]::Pattern)
        return [double]$range.Current.Value
    }
    catch {
        $value = $target.GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern)
        return [double]$value.Current.Value
    }
}

function Set-AutomationNumericValue {
    param(
        [System.Windows.Automation.AutomationElement]$Element,
        [double]$Value
    )

    $target = Resolve-NumericAutomationElement -Element $Element
    try {
        $range = $target.GetCurrentPattern([System.Windows.Automation.RangeValuePattern]::Pattern)
        $range.SetValue($Value)
        return
    }
    catch {
        $valuePattern = $target.GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern)
        $target.SetFocus()
        $valuePattern.SetValue([string][int]$Value)
        [System.Windows.Forms.SendKeys]::SendWait('{ENTER}')
    }
}

if (-not (Test-Path -LiteralPath $ExecutablePath)) {
    throw "Executable not found: $ExecutablePath"
}

Get-Process DevSpaceStatusPet -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Milliseconds 700
$process = Start-Process -FilePath $ExecutablePath -ArgumentList '--settings' -PassThru

try {
    $settingsWindow = Wait-AppWindow -ProcessId $process.Id -Predicate {
        param($window)
        $window.Current.Name.StartsWith('DevSpace Status Pet') -and $window.Current.Name -ne 'DevSpace Status Pet'
    }
    $petWindow = Wait-AppWindow -ProcessId $process.Id -Predicate {
        param($window)
        $window.Current.Name -eq 'DevSpace Status Pet'
    }

    $scaleCondition = New-Object System.Windows.Automation.PropertyCondition(
        [System.Windows.Automation.AutomationElement]::AutomationIdProperty,
        'ScaleInput')
    $scaleInput = $settingsWindow.FindFirst(
        [System.Windows.Automation.TreeScope]::Descendants,
        $scaleCondition)
    if ($null -eq $scaleInput) {
        throw 'ScaleInput was not found through UI Automation.'
    }

    $original = Get-AutomationNumericValue -Element $scaleInput
    $before = $petWindow.Current.BoundingRectangle
    $testValue = if ($original -lt 160) { 160 } else { 120 }

    Set-AutomationNumericValue -Element $scaleInput -Value $testValue
    Start-Sleep -Seconds 2

    $after = $petWindow.Current.BoundingRectangle
    $settingsPath = Join-Path $env:USERPROFILE '.devspace\devspace-pet-settings.json'
    $saved = [System.IO.File]::ReadAllText($settingsPath) | ConvertFrom-Json
    $expectedScale = $testValue / 100.0

    if ($saved.PSObject.Properties.Name -notcontains 'Scale') {
        throw 'Scale was not written to the settings file after the UI change.'
    }
    if ([Math]::Abs([double]$saved.Scale - $expectedScale) -gt 0.001) {
        throw "Scale was not saved immediately. Expected $expectedScale, got $($saved.Scale)."
    }
    if ([Math]::Abs($after.Width - $before.Width) -lt 20) {
        throw "Pet width did not change immediately. Before=$($before.Width), After=$($after.Width)."
    }

    Set-AutomationNumericValue -Element $scaleInput -Value $original
    Start-Sleep -Seconds 2
    $restored = $petWindow.Current.BoundingRectangle

    $bubbleThemeCondition = New-Object System.Windows.Automation.PropertyCondition(
        [System.Windows.Automation.AutomationElement]::AutomationIdProperty,
        'BubbleThemeInput')
    $bubbleThemeInput = $settingsWindow.FindFirst(
        [System.Windows.Automation.TreeScope]::Descendants,
        $bubbleThemeCondition)
    if ($null -eq $bubbleThemeInput) {
        throw 'BubbleThemeInput was not found through UI Automation.'
    }

    $currentSettings = [System.IO.File]::ReadAllText($settingsPath) | ConvertFrom-Json
    $originalBubbleTheme = if ($currentSettings.PSObject.Properties.Name -contains 'BubbleTheme') {
        [string]$currentSettings.BubbleTheme
    }
    else {
        'Light'
    }
    $testBubbleTheme = if ($originalBubbleTheme -eq 'Dark') { 'Light' } else { 'Dark' }
    $bubbleThemeInput.SetFocus()
    [System.Windows.Forms.SendKeys]::SendWait($(if ($testBubbleTheme -eq 'Dark') { '{END}' } else { '{HOME}' }))
    Start-Sleep -Seconds 2
    $changedSettings = [System.IO.File]::ReadAllText($settingsPath) | ConvertFrom-Json
    if ([string]$changedSettings.BubbleTheme -ne $testBubbleTheme) {
        throw "Bubble theme was not saved immediately. Expected $testBubbleTheme, got $($changedSettings.BubbleTheme)."
    }

    $bubbleThemeInput.SetFocus()
    [System.Windows.Forms.SendKeys]::SendWait($(if ($originalBubbleTheme -eq 'Dark') { '{END}' } else { '{HOME}' }))
    Start-Sleep -Seconds 2
    $restoredSettings = [System.IO.File]::ReadAllText($settingsPath) | ConvertFrom-Json
    if ([string]$restoredSettings.BubbleTheme -ne $originalBubbleTheme) {
        throw "Bubble theme was not restored. Expected $originalBubbleTheme, got $($restoredSettings.BubbleTheme)."
    }

    $bubbleStyleCondition = New-Object System.Windows.Automation.PropertyCondition(
        [System.Windows.Automation.AutomationElement]::AutomationIdProperty,
        'BubbleStyleInput')
    $bubbleStyleInput = $settingsWindow.FindFirst(
        [System.Windows.Automation.TreeScope]::Descendants,
        $bubbleStyleCondition)
    if ($null -eq $bubbleStyleInput) {
        throw 'BubbleStyleInput was not found through UI Automation.'
    }

    $styleSettings = [System.IO.File]::ReadAllText($settingsPath) | ConvertFrom-Json
    $originalBubbleStyle = if ($styleSettings.PSObject.Properties.Name -contains 'BubbleStyle') {
        [string]$styleSettings.BubbleStyle
    }
    else {
        'Speech'
    }
    $styleBefore = $petWindow.Current.BoundingRectangle

    # The clean card is the last item in the three-style list.
    $bubbleStyleInput.SetFocus()
    [System.Windows.Forms.SendKeys]::SendWait('{END}')
    Start-Sleep -Seconds 2
    $cleanStyleBounds = $petWindow.Current.BoundingRectangle
    $cleanStyleSettings = [System.IO.File]::ReadAllText($settingsPath) | ConvertFrom-Json
    if ([string]$cleanStyleSettings.BubbleStyle -ne 'MonitorCardClean') {
        throw "Clean monitor-card design was not saved immediately. Got $($cleanStyleSettings.BubbleStyle)."
    }

    # Speech is the first item and must use a visibly shorter layout.
    $bubbleStyleInput.SetFocus()
    [System.Windows.Forms.SendKeys]::SendWait('{HOME}')
    Start-Sleep -Seconds 2
    $speechStyleBounds = $petWindow.Current.BoundingRectangle
    $speechStyleSettings = [System.IO.File]::ReadAllText($settingsPath) | ConvertFrom-Json
    if ([string]$speechStyleSettings.BubbleStyle -ne 'Speech') {
        throw "Speech-bubble design was not saved immediately. Got $($speechStyleSettings.BubbleStyle)."
    }
    $styleHeightDifference = $cleanStyleBounds.Height - $speechStyleBounds.Height
    if ($styleHeightDifference -lt 8) {
        throw "Pet height did not change as expected between clean card and speech styles. Clean=$($cleanStyleBounds.Height), Speech=$($speechStyleBounds.Height), Difference=$styleHeightDifference."
    }

    $restoreKeys = switch ($originalBubbleStyle) {
        'MonitorCardClean' { '{END}' }
        'MonitorCardNeon' { '{HOME}{DOWN}' }
        'MonitorCard' { '{HOME}{DOWN}' }
        default { '{HOME}' }
    }
    $expectedRestoredStyle = if ($originalBubbleStyle -eq 'MonitorCard') { 'MonitorCardNeon' } else { $originalBubbleStyle }
    $bubbleStyleInput.SetFocus()
    [System.Windows.Forms.SendKeys]::SendWait($restoreKeys)
    Start-Sleep -Seconds 2
    $restoredStyleSettings = [System.IO.File]::ReadAllText($settingsPath) | ConvertFrom-Json
    if ([string]$restoredStyleSettings.BubbleStyle -ne $expectedRestoredStyle) {
        throw "Bubble design was not restored. Expected $expectedRestoredStyle, got $($restoredStyleSettings.BubbleStyle)."
    }

    [pscustomobject]@{
        OriginalScale = $original
        TestScale = $testValue
        WidthBefore = [Math]::Round($before.Width, 1)
        HeightBefore = [Math]::Round($before.Height, 1)
        WidthAfter = [Math]::Round($after.Width, 1)
        HeightAfter = [Math]::Round($after.Height, 1)
        WidthRestored = [Math]::Round($restored.Width, 1)
        HeightRestored = [Math]::Round($restored.Height, 1)
        SavedImmediately = $true
        BubbleThemeBefore = $originalBubbleTheme
        BubbleThemeTest = $testBubbleTheme
        BubbleThemeRestored = [string]$restoredSettings.BubbleTheme
        BubbleStyleBefore = $originalBubbleStyle
        BubbleStyleTest = 'MonitorCardClean'
        BubbleStyleHeightBefore = [Math]::Round($styleBefore.Height, 1)
        BubbleStyleCleanHeight = [Math]::Round($cleanStyleBounds.Height, 1)
        BubbleStyleSpeechHeight = [Math]::Round($speechStyleBounds.Height, 1)
        BubbleStyleRestored = [string]$restoredStyleSettings.BubbleStyle
    } | Format-List

    Write-Host '[OK] Live settings UI smoke test' -ForegroundColor Green
}
finally {
    # Keep the tested tray application running after the local UI smoke test.
}
