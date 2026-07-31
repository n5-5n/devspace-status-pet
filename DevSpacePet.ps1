[CmdletBinding()]
param(
    [ValidateRange(100, 5000)]
    [int]$StateRefreshMilliseconds = 750,
    [string]$StatePath = "$env:USERPROFILE\.devspace\devspace-status.json",
    [string]$PositionPath = "$env:USERPROFILE\.devspace\devspace-pet-position.json",
    [string]$SettingsPath = "$env:USERPROFILE\.devspace\devspace-pet-settings.json"
)

Set-StrictMode -Version 2.0
$ErrorActionPreference = 'Stop'

Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing

$createdNew = $false
$mutex = New-Object System.Threading.Mutex($true, 'Local\DevSpaceDesktopPet', [ref]$createdNew)
if (-not $createdNew) {
    $mutex.Dispose()
    exit 0
}

function Format-PetDuration {
    param([double]$Seconds)

    $safeSeconds = [Math]::Max(0, [int][Math]::Floor($Seconds))
    $span = [TimeSpan]::FromSeconds($safeSeconds)
    if ($span.TotalHours -ge 1) {
        return '{0}:{1:00}:{2:00}' -f [int]$span.TotalHours, $span.Minutes, $span.Seconds
    }
    return '{0:00}:{1:00}' -f $span.Minutes, $span.Seconds
}

function Limit-PetText {
    param(
        [string]$Text,
        [int]$MaximumLength
    )

    if ([string]::IsNullOrWhiteSpace($Text)) {
        return '-'
    }
    if ($Text.Length -le $MaximumLength) {
        return $Text
    }
    return $Text.Substring(0, [Math]::Max(1, $MaximumLength - 1)) + '…'
}

function New-RoundedRectanglePath {
    param(
        [System.Drawing.RectangleF]$Rectangle,
        [float]$Radius
    )

    $path = New-Object System.Drawing.Drawing2D.GraphicsPath
    $diameter = $Radius * 2
    if ($diameter -le 0) {
        $path.AddRectangle($Rectangle)
        return $path
    }

    $arc = [System.Drawing.RectangleF]::new([float]$Rectangle.X, [float]$Rectangle.Y, [float]$diameter, [float]$diameter)
    $path.AddArc($arc, 180, 90)
    $arc.X = $Rectangle.Right - $diameter
    $path.AddArc($arc, 270, 90)
    $arc.Y = $Rectangle.Bottom - $diameter
    $path.AddArc($arc, 0, 90)
    $arc.X = $Rectangle.Left
    $path.AddArc($arc, 90, 90)
    $path.CloseFigure()
    return $path
}

function Read-PetState {
    try {
        if (-not (Test-Path -LiteralPath $StatePath)) {
            return $null
        }
        $stateFile = Get-Item -LiteralPath $StatePath -ErrorAction Stop
        if ($stateFile.LastWriteTimeUtc -eq $script:lastStateWriteUtc) {
            return $null
        }
        $json = [System.IO.File]::ReadAllText($StatePath, [System.Text.Encoding]::UTF8)
        $state = $json | ConvertFrom-Json -ErrorAction Stop
        $script:lastStateWriteUtc = $stateFile.LastWriteTimeUtc
        return $state
    }
    catch {
        return $null
    }
}

function Read-PetSettings {
    $settings = [pscustomobject]@{
        Theme      = 'Classic'
        ShowBubble = $true
    }

    try {
        if (-not (Test-Path -LiteralPath $SettingsPath)) {
            return $settings
        }
        $json = [System.IO.File]::ReadAllText($SettingsPath, [System.Text.Encoding]::UTF8)
        $saved = $json | ConvertFrom-Json -ErrorAction Stop
        if ($saved.PSObject.Properties.Name -contains 'Theme' -and [string]$saved.Theme -in @('Classic', 'Neon')) {
            $settings.Theme = [string]$saved.Theme
        }
        if ($saved.PSObject.Properties.Name -contains 'ShowBubble') {
            $settings.ShowBubble = [bool]$saved.ShowBubble
        }
    }
    catch {
        # Defaults are safe.
    }

    return $settings
}

function Save-PetSettings {
    try {
        $directory = Split-Path -Parent $SettingsPath
        if (-not (Test-Path -LiteralPath $directory)) {
            [void](New-Item -ItemType Directory -Path $directory -Force)
        }
        $payload = [ordered]@{
            Theme      = $script:theme
            ShowBubble = $script:forceBubble
        } | ConvertTo-Json
        [System.IO.File]::WriteAllText($SettingsPath, $payload, (New-Object System.Text.UTF8Encoding($false)))
    }
    catch {
        # Settings persistence is optional.
    }
}

function Save-PetPosition {
    try {
        $directory = Split-Path -Parent $PositionPath
        if (-not (Test-Path -LiteralPath $directory)) {
            [void](New-Item -ItemType Directory -Path $directory -Force)
        }
        $payload = [ordered]@{
            X = $form.Left
            Y = $form.Top
        } | ConvertTo-Json
        [System.IO.File]::WriteAllText($PositionPath, $payload, (New-Object System.Text.UTF8Encoding($false)))
    }
    catch {
        # Position persistence is optional.
    }
}

function Move-PetToBottomRight {
    $screen = [System.Windows.Forms.Screen]::FromControl($form)
    if ($null -eq $screen) {
        $screen = [System.Windows.Forms.Screen]::PrimaryScreen
    }
    $area = $screen.WorkingArea
    $form.Left = $area.Right - $form.Width - 20
    $form.Top = $area.Bottom - $form.Height - 12
}

function Restore-PetPosition {
    try {
        if (Test-Path -LiteralPath $PositionPath) {
            $json = [System.IO.File]::ReadAllText($PositionPath, [System.Text.Encoding]::UTF8)
            $position = $json | ConvertFrom-Json -ErrorAction Stop
            $candidate = [System.Drawing.Rectangle]::new([int]$position.X, [int]$position.Y, [int]$form.Width, [int]$form.Height)
            foreach ($screen in [System.Windows.Forms.Screen]::AllScreens) {
                if ($screen.WorkingArea.IntersectsWith($candidate)) {
                    $area = $screen.WorkingArea
                    $form.Left = [Math]::Max($area.Left, [Math]::Min([int]$position.X, $area.Right - $form.Width))
                    $form.Top = [Math]::Max($area.Top, [Math]::Min([int]$position.Y, $area.Bottom - $form.Height))
                    return
                }
            }
        }
    }
    catch {
        # Fall through to the default position.
    }
    Move-PetToBottomRight
}

function Get-StateAccentColor {
    param([string]$State)

    switch ($State) {
        'Working'      { return [System.Drawing.Color]::FromArgb(255, 75, 225, 130) }
        'Waiting'      { return [System.Drawing.Color]::FromArgb(255, 255, 203, 58) }
        'JustFinished' { return [System.Drawing.Color]::FromArgb(255, 255, 203, 58) }
        'Failed'       { return [System.Drawing.Color]::FromArgb(255, 255, 83, 74) }
        'Stalled'      { return [System.Drawing.Color]::FromArgb(255, 177, 108, 255) }
        'Stopped'      { return [System.Drawing.Color]::FromArgb(255, 130, 135, 145) }
        default        { return [System.Drawing.Color]::FromArgb(255, 68, 160, 255) }
    }
}

function Get-PetThemePalette {
    param(
        [string]$State,
        [string]$Theme
    )

    $stateAccent = Get-StateAccentColor -State $State
    if ($Theme -eq 'Neon') {
        $outline = [System.Drawing.Color]::FromArgb(255, 222, 0, 238)
        $signal = [System.Drawing.Color]::FromArgb(255, 255, 198, 53)
        if ($State -eq 'Stopped') {
            $outline = [System.Drawing.Color]::FromArgb(255, 105, 108, 118)
            $signal = [System.Drawing.Color]::FromArgb(255, 145, 148, 157)
        }
        return [pscustomobject]@{
            StateAccent     = $stateAccent
            Outline         = $outline
            Signal          = $signal
            BubbleBack      = [System.Drawing.Color]::FromArgb(255, 20, 23, 31)
            BubbleText      = [System.Drawing.Color]::FromArgb(255, 246, 247, 251)
            BubbleMuted     = [System.Drawing.Color]::FromArgb(255, 176, 182, 196)
            GlowAlpha       = 48
            MidAlpha        = 120
            ShadowAlpha     = 92
            GlowWidth       = 10
            MidWidth        = 5
        }
    }

    return [pscustomobject]@{
        StateAccent     = $stateAccent
        Outline         = $stateAccent
        Signal          = $stateAccent
        BubbleBack      = [System.Drawing.Color]::FromArgb(248, 250, 252, 255)
        BubbleText      = [System.Drawing.Color]::FromArgb(255, 25, 31, 42)
        BubbleMuted     = [System.Drawing.Color]::FromArgb(255, 76, 88, 107)
        GlowAlpha       = 20
        MidAlpha        = 62
        ShadowAlpha     = 45
        GlowWidth       = 6
        MidWidth        = 3
    }
}

function Get-PetActivities {
    $rawActivities = @()
    if ($null -ne $script:petState -and $script:petState.PSObject.Properties.Name -contains 'Activities' -and $null -ne $script:petState.Activities) {
        $rawActivities = @($script:petState.Activities)
    }

    if ($rawActivities.Count -eq 0) {
        return @([pscustomobject]@{
            Id             = 'primary'
            State          = [string]$script:petState.State
            Label          = [string]$script:petState.Label
            ProjectName    = [string]$script:petState.ProjectName
            Operation      = [string]$script:petState.Operation
            ElapsedSeconds = [double]$script:petState.ElapsedSeconds
        })
    }

    if ($rawActivities.Count -le 4) {
        return $rawActivities
    }

    return @(
        $rawActivities[0],
        $rawActivities[1],
        $rawActivities[2],
        [pscustomobject]@{
            Id             = 'more'
            State          = [string]$script:petState.State
            Label          = '並列実行中'
            ProjectName    = "+$($rawActivities.Count - 3)件"
            Operation      = 'ほかの処理'
            ElapsedSeconds = 0
        }
    )
}

function Resize-PetForActivities {
    $count = [Math]::Max(1, @(Get-PetActivities).Count)
    $extraLogical = [Math]::Max(0, $count - 1) * 58
    $targetHeight = [int][Math]::Ceiling(246 + ($extraLogical * 1.15))
    if ($form.Height -eq $targetHeight) {
        return
    }

    $bottom = $form.Top + $form.Height
    $form.Height = $targetHeight
    $form.Top = $bottom - $targetHeight

    $screen = [System.Windows.Forms.Screen]::FromControl($form)
    if ($null -ne $screen) {
        $area = $screen.WorkingArea
        $form.Top = [Math]::Max($area.Top, [Math]::Min($form.Top, $area.Bottom - $form.Height))
    }
}

function Update-PetState {
    $newState = Read-PetState
    if ($null -eq $newState) {
        return
    }

    $oldStateName = [string]$script:petState.State
    $newStateName = [string]$newState.State
    $script:petState = $newState
    Resize-PetForActivities

    if ($newStateName -ne $oldStateName) {
        $script:stateChangedAt = Get-Date
        $script:bubbleUntil = (Get-Date).AddSeconds(20)
    }
    elseif ($newStateName -eq 'Working') {
        $script:bubbleUntil = (Get-Date).AddSeconds(8)
    }
}

$settings = Read-PetSettings
$script:lastStateWriteUtc = [DateTime]::MinValue
$script:frame = 0
$script:stateChangedAt = Get-Date
$script:bubbleUntil = (Get-Date).AddSeconds(20)
$script:theme = [string]$settings.Theme
$script:forceBubble = [bool]$settings.ShowBubble
$script:dragging = $false
$script:dragOffset = [System.Drawing.Point]::Empty
$script:dragMoved = $false
$script:petState = [pscustomobject]@{
    State          = 'Idle'
    Label          = '待機中'
    Summary        = 'DevSpaceの状態を確認しています'
    ProjectName    = 'DevSpace'
    Operation      = '確認中'
    ElapsedSeconds = 0
    UpdatedAt      = (Get-Date).ToString('o')
    Activities     = @()
    LastTool       = $null
}

$form = New-Object System.Windows.Forms.Form
$form.Text = 'DevSpace Pet'
$form.Width = 212
$form.Height = 246
$form.FormBorderStyle = [System.Windows.Forms.FormBorderStyle]::None
$form.StartPosition = [System.Windows.Forms.FormStartPosition]::Manual
$form.ShowInTaskbar = $false
$form.TopMost = $true
$form.BackColor = [System.Drawing.Color]::Fuchsia
$form.TransparencyKey = [System.Drawing.Color]::Fuchsia
$form.Opacity = 1.0
$form.KeyPreview = $true

$doubleBufferedProperty = $form.GetType().GetProperty('DoubleBuffered', [System.Reflection.BindingFlags]'Instance,NonPublic')
if ($null -ne $doubleBufferedProperty) {
    $doubleBufferedProperty.SetValue($form, $true, $null)
}

$bubbleFont = New-Object System.Drawing.Font('Segoe UI', 8.1, [System.Drawing.FontStyle]::Regular)
$bubbleBoldFont = New-Object System.Drawing.Font('Segoe UI Semibold', 9.0, [System.Drawing.FontStyle]::Bold)
$smallFont = New-Object System.Drawing.Font('Segoe UI', 7.2, [System.Drawing.FontStyle]::Regular)
$symbolFont = New-Object System.Drawing.Font('Segoe UI Symbol', 11, [System.Drawing.FontStyle]::Bold)

$contextMenu = New-Object System.Windows.Forms.ContextMenuStrip
$toggleBubbleItem = New-Object System.Windows.Forms.ToolStripMenuItem
$toggleBubbleItem.Text = '吹き出しを常時表示'
$toggleBubbleItem.Checked = $script:forceBubble
[void]$contextMenu.Items.Add($toggleBubbleItem)

$themeMenuItem = New-Object System.Windows.Forms.ToolStripMenuItem
$themeMenuItem.Text = 'テーマ'
$classicThemeItem = New-Object System.Windows.Forms.ToolStripMenuItem
$classicThemeItem.Text = 'クラシック（状態色）'
$neonThemeItem = New-Object System.Windows.Forms.ToolStripMenuItem
$neonThemeItem.Text = 'ネオン（紫・黄）'
[void]$themeMenuItem.DropDownItems.Add($classicThemeItem)
[void]$themeMenuItem.DropDownItems.Add($neonThemeItem)
[void]$contextMenu.Items.Add($themeMenuItem)

$resetPositionItem = New-Object System.Windows.Forms.ToolStripMenuItem
$resetPositionItem.Text = '位置を右下へ戻す'
[void]$contextMenu.Items.Add($resetPositionItem)

[void]$contextMenu.Items.Add((New-Object System.Windows.Forms.ToolStripSeparator))
$exitItem = New-Object System.Windows.Forms.ToolStripMenuItem
$exitItem.Text = 'ペットを終了'
[void]$contextMenu.Items.Add($exitItem)
$form.ContextMenuStrip = $contextMenu

function Update-ThemeMenuChecks {
    $classicThemeItem.Checked = $script:theme -eq 'Classic'
    $neonThemeItem.Checked = $script:theme -eq 'Neon'
}

$toggleBubbleItem.Add_Click({
    $script:forceBubble = -not $script:forceBubble
    $toggleBubbleItem.Checked = $script:forceBubble
    Save-PetSettings
    $form.Invalidate()
})
$classicThemeItem.Add_Click({
    $script:theme = 'Classic'
    Update-ThemeMenuChecks
    Save-PetSettings
    $form.Invalidate()
})
$neonThemeItem.Add_Click({
    $script:theme = 'Neon'
    Update-ThemeMenuChecks
    Save-PetSettings
    $form.Invalidate()
})
$resetPositionItem.Add_Click({
    Move-PetToBottomRight
    Save-PetPosition
})
$exitItem.Add_Click({ $form.Close() })
Update-ThemeMenuChecks

$form.Add_MouseDown({
    param($sender, $eventArgs)
    if ($eventArgs.Button -eq [System.Windows.Forms.MouseButtons]::Left) {
        $script:dragging = $true
        $script:dragMoved = $false
        $script:dragOffset = [System.Drawing.Point]::new([int]$eventArgs.X, [int]$eventArgs.Y)
    }
})
$form.Add_MouseMove({
    param($sender, $eventArgs)
    if ($script:dragging) {
        $cursor = [System.Windows.Forms.Cursor]::Position
        $newLeft = $cursor.X - $script:dragOffset.X
        $newTop = $cursor.Y - $script:dragOffset.Y
        if ([Math]::Abs($form.Left - $newLeft) -gt 2 -or [Math]::Abs($form.Top - $newTop) -gt 2) {
            $script:dragMoved = $true
        }
        $form.Left = $newLeft
        $form.Top = $newTop
    }
})
$form.Add_MouseUp({
    param($sender, $eventArgs)
    if ($eventArgs.Button -eq [System.Windows.Forms.MouseButtons]::Left) {
        $script:dragging = $false
        if ($script:dragMoved) {
            Save-PetPosition
        }
        else {
            $script:forceBubble = -not $script:forceBubble
            $toggleBubbleItem.Checked = $script:forceBubble
            Save-PetSettings
        }
        $form.Invalidate()
    }
})

$form.Add_Paint({
    param($sender, $eventArgs)

    $graphics = $eventArgs.Graphics
    $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $graphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $graphics.TextRenderingHint = [System.Drawing.Text.TextRenderingHint]::ClearTypeGridFit
    $graphics.ScaleTransform(1.15, 1.15)

    $stateName = [string]$script:petState.State
    $palette = Get-PetThemePalette -State $stateName -Theme $script:theme
    $stateAccent = $palette.StateAccent
    $outlineColor = $palette.Outline
    $signalColor = $palette.Signal
    $activities = @(Get-PetActivities)
    $bubbleCount = [Math]::Max(1, $activities.Count)
    $extraBubbleHeight = [Math]::Max(0, $bubbleCount - 1) * 58

    $phase = $script:frame / 5.0
    $bob = 0.0
    $legSwing = 0.0
    $armSwing = 0.0

    switch ($stateName) {
        'Working' {
            $bob = [Math]::Abs([Math]::Sin($phase * 1.8)) * -4
            $legSwing = [Math]::Sin($phase * 2.6) * 7
            $armSwing = [Math]::Sin($phase * 2.6 + 1.2) * 9
        }
        'JustFinished' {
            $bob = [Math]::Abs([Math]::Sin($phase * 1.5)) * -13
            $armSwing = -13
        }
        'Failed' {
            $bob = 4
            $armSwing = 8
        }
        'Stalled' {
            $bob = [Math]::Sin($phase * 0.35) * 1.5
            $armSwing = 5
        }
        'Stopped' {
            $bob = 6
            $armSwing = 7
        }
        default {
            $bob = [Math]::Sin($phase * 0.65) * 2.5
            $armSwing = [Math]::Sin($phase * 0.45) * 3
        }
    }

    $showBubble = $script:forceBubble -or ((Get-Date) -lt $script:bubbleUntil) -or $stateName -in @('Working', 'Stalled', 'Failed')
    if ($showBubble) {
        for ($index = 0; $index -lt $activities.Count; $index++) {
            $activity = $activities[$index]
            $activityState = if ($activity.PSObject.Properties.Name -contains 'State') { [string]$activity.State } else { $stateName }
            $activityPalette = Get-PetThemePalette -State $activityState -Theme $script:theme
            $bubbleY = 4 + ($index * 58)
            $bubbleRect = [System.Drawing.RectangleF]::new(4, [float]$bubbleY, 176, 52)
            $bubblePath = New-RoundedRectanglePath -Rectangle $bubbleRect -Radius 10
            $bubbleBrush = New-Object System.Drawing.SolidBrush($activityPalette.BubbleBack)
            $bubbleGlow = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb([int]$activityPalette.GlowAlpha, $activityPalette.Outline), [float]$activityPalette.GlowWidth)
            $bubbleBorder = New-Object System.Drawing.Pen($activityPalette.Outline, 2)
            $graphics.DrawPath($bubbleGlow, $bubblePath)
            $graphics.FillPath($bubbleBrush, $bubblePath)
            $graphics.DrawPath($bubbleBorder, $bubblePath)

            $projectText = Limit-PetText -Text ([string]$activity.ProjectName) -MaximumLength 24
            $operationText = Limit-PetText -Text ([string]$activity.Operation) -MaximumLength 25
            $elapsedText = Format-PetDuration -Seconds ([double]$activity.ElapsedSeconds)
            $textBrush = New-Object System.Drawing.SolidBrush($activityPalette.BubbleText)
            $mutedBrush = New-Object System.Drawing.SolidBrush($activityPalette.BubbleMuted)
            $activityAccentBrush = New-Object System.Drawing.SolidBrush($activityPalette.StateAccent)
            $graphics.DrawString($projectText, $bubbleBoldFont, $textBrush, 13, $bubbleY + 6)
            $graphics.DrawString($operationText, $bubbleFont, $mutedBrush, 13, $bubbleY + 23)
            $graphics.FillEllipse($activityAccentBrush, 13, $bubbleY + 41, 7, 7)
            $graphics.DrawString("$($activity.Label)  $elapsedText", $smallFont, $mutedBrush, 24, $bubbleY + 37)

            $activityAccentBrush.Dispose()
            $mutedBrush.Dispose()
            $textBrush.Dispose()
            $bubbleBorder.Dispose()
            $bubbleGlow.Dispose()
            $bubbleBrush.Dispose()
            $bubblePath.Dispose()
        }

        $lastBubbleY = 4 + (($activities.Count - 1) * 58)
        $tailPalette = Get-PetThemePalette -State ([string]$activities[$activities.Count - 1].State) -Theme $script:theme
        $tailBrush = New-Object System.Drawing.SolidBrush($tailPalette.BubbleBack)
        $tailPoints = [System.Drawing.PointF[]]@(
            ([System.Drawing.PointF]::new(120, [float]($lastBubbleY + 51))),
            ([System.Drawing.PointF]::new(138, [float]($lastBubbleY + 51))),
            ([System.Drawing.PointF]::new(129, [float]($lastBubbleY + 65)))
        )
        $graphics.FillPolygon($tailBrush, $tailPoints)
        $tailBrush.Dispose()
    }

    $baseY = 94 + $extraBubbleHeight + [float]$bob
    $shadowY = 190 + $extraBubbleHeight

    $shadowOuter = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb([Math]::Max(12, [int]($palette.ShadowAlpha / 3)), $outlineColor))
    $shadowInner = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb([int]$palette.ShadowAlpha, $outlineColor))
    $graphics.FillEllipse($shadowOuter, 42, $shadowY, 104, 20)
    $graphics.FillEllipse($shadowInner, 49, $shadowY + 4, 90, 12)
    $shadowInner.Dispose()
    $shadowOuter.Dispose()

    $dark = [System.Drawing.Color]::FromArgb(255, 29, 33, 43)
    $dark2 = [System.Drawing.Color]::FromArgb(255, 43, 49, 61)
    $panel = [System.Drawing.Color]::FromArgb(255, 12, 16, 23)
    if ($stateName -eq 'Stopped') {
        $dark = [System.Drawing.Color]::FromArgb(255, 66, 69, 77)
        $dark2 = [System.Drawing.Color]::FromArgb(255, 83, 87, 96)
        $panel = [System.Drawing.Color]::FromArgb(255, 40, 43, 49)
    }

    $bodyBrush = New-Object System.Drawing.SolidBrush($dark)
    $panelBrush = New-Object System.Drawing.SolidBrush($panel)
    $signalBrush = New-Object System.Drawing.SolidBrush($signalColor)
    $outlineGlowPen = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb([int]$palette.GlowAlpha, $outlineColor), [float]$palette.GlowWidth)
    $outlineMidPen = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb([int]$palette.MidAlpha, $outlineColor), [float]$palette.MidWidth)
    $outlinePen = New-Object System.Drawing.Pen($outlineColor, 2)
    $jointGlowPen = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb([int]$palette.GlowAlpha, $outlineColor), [float]$palette.GlowWidth)
    $jointPen = New-Object System.Drawing.Pen($signalColor, 5)
    foreach ($pen in @($outlineGlowPen, $outlineMidPen, $outlinePen, $jointGlowPen, $jointPen)) {
        $pen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
        $pen.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
        $pen.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Round
    }

    $leftFootX = 66 + [float]$legSwing
    $rightFootX = 107 - [float]$legSwing
    $legY = $baseY + 82
    $graphics.DrawLine($jointGlowPen, 76, $baseY + 73, $leftFootX, $legY + 12)
    $graphics.DrawLine($jointGlowPen, 104, $baseY + 73, $rightFootX, $legY + 12)
    $graphics.DrawLine($jointPen, 76, $baseY + 73, $leftFootX, $legY + 12)
    $graphics.DrawLine($jointPen, 104, $baseY + 73, $rightFootX, $legY + 12)
    $graphics.DrawEllipse($outlineGlowPen, $leftFootX - 11, $legY + 6, 26, 13)
    $graphics.DrawEllipse($outlineGlowPen, $rightFootX - 11, $legY + 6, 26, 13)
    $graphics.FillEllipse($bodyBrush, $leftFootX - 11, $legY + 6, 26, 13)
    $graphics.FillEllipse($bodyBrush, $rightFootX - 11, $legY + 6, 26, 13)
    $graphics.DrawEllipse($outlinePen, $leftFootX - 11, $legY + 6, 26, 13)
    $graphics.DrawEllipse($outlinePen, $rightFootX - 11, $legY + 6, 26, 13)

    $leftArmY = $baseY + 49 + [float]$armSwing
    $rightArmY = $baseY + 49 - [float]$armSwing
    $graphics.DrawLine($jointGlowPen, 61, $baseY + 42, 43, $leftArmY)
    $graphics.DrawLine($jointGlowPen, 119, $baseY + 42, 137, $rightArmY)
    $graphics.DrawLine($jointPen, 61, $baseY + 42, 43, $leftArmY)
    $graphics.DrawLine($jointPen, 119, $baseY + 42, 137, $rightArmY)
    $graphics.DrawEllipse($outlineGlowPen, 35, $leftArmY - 7, 16, 16)
    $graphics.DrawEllipse($outlineGlowPen, 129, $rightArmY - 7, 16, 16)
    $graphics.FillEllipse($bodyBrush, 35, $leftArmY - 7, 16, 16)
    $graphics.FillEllipse($bodyBrush, 129, $rightArmY - 7, 16, 16)
    $graphics.DrawEllipse($outlinePen, 35, $leftArmY - 7, 16, 16)
    $graphics.DrawEllipse($outlinePen, 129, $rightArmY - 7, 16, 16)

    $bodyRect = [System.Drawing.RectangleF]::new(57, [float]($baseY + 34), 66, 56)
    $bodyPath = New-RoundedRectanglePath -Rectangle $bodyRect -Radius 14
    $graphics.DrawPath($outlineGlowPen, $bodyPath)
    $graphics.DrawPath($outlineMidPen, $bodyPath)
    $graphics.FillPath($bodyBrush, $bodyPath)
    $graphics.DrawPath($outlinePen, $bodyPath)
    $graphics.DrawEllipse($outlineGlowPen, 81, $baseY + 55, 18, 18)
    $graphics.FillEllipse($signalBrush, 82, $baseY + 56, 16, 16)
    $graphics.DrawEllipse($outlinePen, 82, $baseY + 56, 16, 16)

    $headRect = [System.Drawing.RectangleF]::new(47, [float]$baseY, 86, 56)
    $headPath = New-RoundedRectanglePath -Rectangle $headRect -Radius 18
    $graphics.DrawPath($outlineGlowPen, $headPath)
    $graphics.DrawPath($outlineMidPen, $headPath)
    $graphics.FillPath($bodyBrush, $headPath)
    $graphics.DrawPath($outlinePen, $headPath)

    $faceRect = [System.Drawing.RectangleF]::new(56, [float]($baseY + 9), 68, 34)
    $facePath = New-RoundedRectanglePath -Rectangle $faceRect -Radius 11
    $graphics.FillPath($panelBrush, $facePath)

    $antennaX = 90 + [Math]::Sin($phase) * 5
    $graphics.DrawLine($jointGlowPen, 90, $baseY, $antennaX, $baseY - 14)
    $graphics.DrawLine($jointPen, 90, $baseY, $antennaX, $baseY - 14)
    $graphics.DrawEllipse($outlineGlowPen, $antennaX - 5, $baseY - 20, 11, 11)
    $graphics.FillEllipse($signalBrush, $antennaX - 5, $baseY - 20, 11, 11)
    $graphics.DrawEllipse($outlinePen, $antennaX - 5, $baseY - 20, 11, 11)

    $blink = (($script:frame % 110) -gt 102) -and $stateName -notin @('Failed', 'Stopped')
    if ($blink) {
        $eyePen = New-Object System.Drawing.Pen($signalColor, 3)
        $graphics.DrawLine($eyePen, 69, $baseY + 25, 80, $baseY + 25)
        $graphics.DrawLine($eyePen, 100, $baseY + 25, 111, $baseY + 25)
        $eyePen.Dispose()
    }
    elseif ($stateName -eq 'Failed') {
        $errorPen = New-Object System.Drawing.Pen($stateAccent, 3)
        $graphics.DrawLine($errorPen, 69, $baseY + 20, 80, $baseY + 29)
        $graphics.DrawLine($errorPen, 80, $baseY + 20, 69, $baseY + 29)
        $graphics.DrawLine($errorPen, 100, $baseY + 20, 111, $baseY + 29)
        $graphics.DrawLine($errorPen, 111, $baseY + 20, 100, $baseY + 29)
        $errorPen.Dispose()
    }
    elseif ($stateName -eq 'Stopped') {
        $offPen = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(255, 130, 135, 145), 3)
        $graphics.DrawLine($offPen, 69, $baseY + 25, 80, $baseY + 25)
        $graphics.DrawLine($offPen, 100, $baseY + 25, 111, $baseY + 25)
        $offPen.Dispose()
    }
    else {
        $eyeGlow = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb([Math]::Max(25, [int]$palette.GlowAlpha), $signalColor))
        $graphics.FillEllipse($eyeGlow, 67, $baseY + 17, 15, 17)
        $graphics.FillEllipse($eyeGlow, 98, $baseY + 17, 15, 17)
        $graphics.FillEllipse($signalBrush, 69, $baseY + 19, 11, 13)
        $graphics.FillEllipse($signalBrush, 100, $baseY + 19, 11, 13)
        $eyeGlow.Dispose()
    }

    if ($stateName -notin @('Failed', 'Stopped', 'Stalled')) {
        $starGlowBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb([Math]::Max(30, [int]$palette.MidAlpha), $outlineColor))
        $starBrush = New-Object System.Drawing.SolidBrush($signalColor)
        $starLift = [Math]::Sin($phase * 0.8) * 2
        $graphics.DrawString('★', $symbolFont, $starGlowBrush, 25, $baseY - 6 + $starLift)
        $graphics.DrawString('★', $symbolFont, $starBrush, 26, $baseY - 7 + $starLift)
        $graphics.DrawString('✦', $symbolFont, $starGlowBrush, 143, $baseY + 5 - $starLift)
        $graphics.DrawString('✦', $symbolFont, $starBrush, 144, $baseY + 4 - $starLift)
        $starBrush.Dispose()
        $starGlowBrush.Dispose()
    }

    if ($stateName -eq 'Stalled') {
        $textBrush = New-Object System.Drawing.SolidBrush($stateAccent)
        $graphics.DrawString('Z', $symbolFont, $textBrush, 137, $baseY - 15)
        $graphics.DrawString('z', $bubbleBoldFont, $textBrush, 151, $baseY - 28)
        $textBrush.Dispose()
    }
    elseif ($stateName -eq 'Working') {
        $sparkBrush = New-Object System.Drawing.SolidBrush($signalColor)
        $graphics.DrawString('›', $symbolFont, $sparkBrush, 147, $baseY + 33)
        $graphics.DrawString('›', $symbolFont, $sparkBrush, 155, $baseY + 33)
        $sparkBrush.Dispose()
    }

    $facePath.Dispose()
    $headPath.Dispose()
    $bodyPath.Dispose()
    $jointPen.Dispose()
    $jointGlowPen.Dispose()
    $outlinePen.Dispose()
    $outlineMidPen.Dispose()
    $outlineGlowPen.Dispose()
    $signalBrush.Dispose()
    $panelBrush.Dispose()
    $bodyBrush.Dispose()
})

$animationTimer = New-Object System.Windows.Forms.Timer
$animationTimer.Interval = 80
$animationTimer.Add_Tick({
    $script:frame++
    $form.Invalidate()
})

$stateTimer = New-Object System.Windows.Forms.Timer
$stateTimer.Interval = $StateRefreshMilliseconds
$stateTimer.Add_Tick({ Update-PetState })

$form.Add_Shown({
    Restore-PetPosition
    Update-PetState
    Resize-PetForActivities
    Save-PetSettings
    $animationTimer.Start()
    $stateTimer.Start()
})

$form.Add_FormClosed({
    Save-PetPosition
    Save-PetSettings
    $animationTimer.Stop()
    $stateTimer.Stop()
})

try {
    [System.Windows.Forms.Application]::Run($form)
}
finally {
    $animationTimer.Dispose()
    $stateTimer.Dispose()
    $contextMenu.Dispose()
    $bubbleFont.Dispose()
    $bubbleBoldFont.Dispose()
    $smallFont.Dispose()
    $symbolFont.Dispose()
    $form.Dispose()
    $mutex.ReleaseMutex()
    $mutex.Dispose()
}
