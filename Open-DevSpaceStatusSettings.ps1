[CmdletBinding()]
param(
    [string]$InstallDirectory = '',
    [string]$SettingsPath = "$env:USERPROFILE\.devspace\devspace-pet-settings.json"
)

Set-StrictMode -Version 2.0
$ErrorActionPreference = 'Stop'
if ([string]::IsNullOrWhiteSpace($InstallDirectory)) {
    $InstallDirectory = $PSScriptRoot
}

Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing

$localizationPath = Join-Path $InstallDirectory 'DevSpaceLocalization.ps1'
if (-not (Test-Path -LiteralPath $localizationPath)) {
    throw "Missing localization file: $localizationPath"
}
. $localizationPath

function Get-RuntimeInfo {
    $configPath = Join-Path $env:USERPROFILE '.devspace\config.json'
    $config = $null
    if (Test-Path -LiteralPath $configPath) {
        try {
            $config = [System.IO.File]::ReadAllText($configPath, [System.Text.Encoding]::UTF8) | ConvertFrom-Json -ErrorAction Stop
        }
        catch {
            $config = $null
        }
    }

    $port = 7676
    foreach ($name in @('port', 'listenPort', 'serverPort')) {
        if ($null -ne $config -and $config.PSObject.Properties.Name -contains $name) {
            $candidate = 0
            if ([int]::TryParse([string]$config.$name, [ref]$candidate) -and $candidate -gt 0) {
                $port = $candidate
                break
            }
        }
    }

    $logPath = Join-Path $env:USERPROFILE '.devspace\serve.log'
    foreach ($name in @('logPath', 'serveLogPath')) {
        if ($null -ne $config -and $config.PSObject.Properties.Name -contains $name -and -not [string]::IsNullOrWhiteSpace([string]$config.$name)) {
            $logPath = [string]$config.$name
            break
        }
    }

    $running = $false
    try {
        $running = $null -ne (Get-NetTCPConnection -LocalPort $port -State Listen -ErrorAction Stop | Select-Object -First 1)
    }
    catch {
        try {
            $running = $null -ne (Get-CimInstance Win32_Process -ErrorAction Stop |
                Where-Object { $_.Name -eq 'node.exe' -and $_.CommandLine -match '@waishnav[\\/]devspace' -and $_.CommandLine -match '\bserve\b' } |
                Select-Object -First 1)
        }
        catch {
            $running = $false
        }
    }

    return [pscustomobject]@{
        ConfigPath = $configPath
        LogPath    = $logPath
        Port       = $port
        Running    = $running
    }
}

function Write-SharedSettings {
    param($Value)

    $directory = Split-Path -Parent $SettingsPath
    if (-not (Test-Path -LiteralPath $directory)) {
        [void](New-Item -ItemType Directory -Path $directory -Force)
    }
    $json = $Value | ConvertTo-Json
    $tempPath = "$SettingsPath.tmp.$PID"
    [System.IO.File]::WriteAllText($tempPath, $json, (New-Object System.Text.UTF8Encoding($false)))
    if (Test-Path -LiteralPath $SettingsPath) {
        Remove-Item -LiteralPath $SettingsPath -Force
    }
    Move-Item -LiteralPath $tempPath -Destination $SettingsPath -Force
}

$settings = Read-DevSpaceSharedSettings -Path $SettingsPath
$language = Resolve-DevSpaceLanguage -Preference ([string]$settings.Language)
function S {
    param([string]$Key, [object[]]$Arguments = @())
    return Get-DevSpaceText -Language $language -Key $Key -Arguments $Arguments
}

$runtime = Get-RuntimeInfo
$version = Get-DevSpaceStatusPetVersion -BaseDirectory $InstallDirectory
$startupShortcutPath = Join-Path ([Environment]::GetFolderPath('Startup')) 'DevSpace Status Pet.lnk'

$form = New-Object System.Windows.Forms.Form
$form.Text = S 'SettingsTitle'
$form.Width = 610
$form.Height = 505
$form.FormBorderStyle = [System.Windows.Forms.FormBorderStyle]::FixedDialog
$form.MaximizeBox = $false
$form.MinimizeBox = $false
$form.StartPosition = [System.Windows.Forms.FormStartPosition]::CenterScreen
$form.Font = New-Object System.Drawing.Font('Segoe UI', 9)

$title = New-Object System.Windows.Forms.Label
$title.Left = 24
$title.Top = 20
$title.Width = 540
$title.Height = 30
$title.Font = New-Object System.Drawing.Font('Segoe UI Semibold', 15, [System.Drawing.FontStyle]::Bold)
$title.Text = S 'VersionFormat' @($version)
$form.Controls.Add($title)

$statusLabel = New-Object System.Windows.Forms.Label
$statusLabel.Left = 26
$statusLabel.Top = 58
$statusLabel.Width = 520
$statusLabel.Height = 24
$statusLabel.Font = New-Object System.Drawing.Font('Segoe UI Semibold', 10, [System.Drawing.FontStyle]::Bold)
$statusLabel.Text = if ($runtime.Running) { S 'SettingsStatusRunning' } else { S 'SettingsStatusStopped' }
$statusLabel.ForeColor = if ($runtime.Running) { [System.Drawing.Color]::ForestGreen } else { [System.Drawing.Color]::Crimson }
$form.Controls.Add($statusLabel)

function Add-FieldLabel {
    param([string]$Text, [int]$Top)
    $label = New-Object System.Windows.Forms.Label
    $label.Left = 26
    $label.Top = $Top
    $label.Width = 150
    $label.Height = 22
    $label.Text = $Text
    $form.Controls.Add($label)
}

Add-FieldLabel -Text (S 'SettingsPort') -Top 102
$portBox = New-Object System.Windows.Forms.TextBox
$portBox.Left = 180
$portBox.Top = 98
$portBox.Width = 120
$portBox.ReadOnly = $true
$portBox.Text = [string]$runtime.Port
$form.Controls.Add($portBox)

Add-FieldLabel -Text (S 'SettingsLogPath') -Top 140
$logBox = New-Object System.Windows.Forms.TextBox
$logBox.Left = 180
$logBox.Top = 136
$logBox.Width = 310
$logBox.ReadOnly = $true
$logBox.Text = [string]$runtime.LogPath
$form.Controls.Add($logBox)
$openLogButton = New-Object System.Windows.Forms.Button
$openLogButton.Left = 500
$openLogButton.Top = 134
$openLogButton.Width = 72
$openLogButton.Height = 27
$openLogButton.Text = S 'SettingsOpenLog'
$form.Controls.Add($openLogButton)

Add-FieldLabel -Text (S 'SettingsConfigPath') -Top 178
$configBox = New-Object System.Windows.Forms.TextBox
$configBox.Left = 180
$configBox.Top = 174
$configBox.Width = 392
$configBox.ReadOnly = $true
$configBox.Text = [string]$runtime.ConfigPath
$form.Controls.Add($configBox)

Add-FieldLabel -Text (S 'SettingsLanguage') -Top 224
$languageCombo = New-Object System.Windows.Forms.ComboBox
$languageCombo.Left = 180
$languageCombo.Top = 220
$languageCombo.Width = 220
$languageCombo.DropDownStyle = [System.Windows.Forms.ComboBoxStyle]::DropDownList
[void]$languageCombo.Items.Add((S 'LanguageAuto'))
[void]$languageCombo.Items.Add((S 'LanguageJapanese'))
[void]$languageCombo.Items.Add((S 'LanguageEnglish'))
$languageCombo.SelectedIndex = switch ([string]$settings.Language) { 'Japanese' { 1 } 'English' { 2 } default { 0 } }
$form.Controls.Add($languageCombo)

Add-FieldLabel -Text (S 'SettingsTheme') -Top 264
$themeCombo = New-Object System.Windows.Forms.ComboBox
$themeCombo.Left = 180
$themeCombo.Top = 260
$themeCombo.Width = 220
$themeCombo.DropDownStyle = [System.Windows.Forms.ComboBoxStyle]::DropDownList
[void]$themeCombo.Items.Add((S 'ThemeClassic'))
[void]$themeCombo.Items.Add((S 'ThemeNeon'))
$themeCombo.SelectedIndex = if ([string]$settings.Theme -eq 'Neon') { 1 } else { 0 }
$form.Controls.Add($themeCombo)

$showBubbleCheck = New-Object System.Windows.Forms.CheckBox
$showBubbleCheck.Left = 180
$showBubbleCheck.Top = 306
$showBubbleCheck.Width = 330
$showBubbleCheck.Text = S 'SettingsShowBubble'
$showBubbleCheck.Checked = [bool]$settings.ShowBubble
$form.Controls.Add($showBubbleCheck)

$startupCheck = New-Object System.Windows.Forms.CheckBox
$startupCheck.Left = 180
$startupCheck.Top = 338
$startupCheck.Width = 330
$startupCheck.Text = S 'SettingsStartWithWindows'
$startupCheck.Checked = Test-Path -LiteralPath $startupShortcutPath
$form.Controls.Add($startupCheck)

$devSpacePageButton = New-Object System.Windows.Forms.Button
$devSpacePageButton.Left = 26
$devSpacePageButton.Top = 382
$devSpacePageButton.Width = 210
$devSpacePageButton.Height = 30
$devSpacePageButton.Text = S 'SettingsInstallDevSpace'
$form.Controls.Add($devSpacePageButton)

$saveButton = New-Object System.Windows.Forms.Button
$saveButton.Left = 292
$saveButton.Top = 382
$saveButton.Width = 160
$saveButton.Height = 30
$saveButton.Text = S 'SettingsSaveRestart'
$form.Controls.Add($saveButton)

$closeButton = New-Object System.Windows.Forms.Button
$closeButton.Left = 462
$closeButton.Top = 382
$closeButton.Width = 110
$closeButton.Height = 30
$closeButton.Text = S 'SettingsClose'
$form.Controls.Add($closeButton)

$messageLabel = New-Object System.Windows.Forms.Label
$messageLabel.Left = 26
$messageLabel.Top = 425
$messageLabel.Width = 546
$messageLabel.Height = 40
$messageLabel.ForeColor = [System.Drawing.Color]::DimGray
$messageLabel.Text = if ($runtime.Running) { S 'DevSpaceDetected' } else { S 'DevSpaceNotDetected' }
$form.Controls.Add($messageLabel)

$openLogButton.Add_Click({
    if (Test-Path -LiteralPath $runtime.LogPath) {
        Start-Process notepad.exe -ArgumentList @($runtime.LogPath)
    }
    else {
        [System.Windows.Forms.MessageBox]::Show((S 'SettingsLogMissing'), (S 'SettingsTitle')) | Out-Null
    }
})

$devSpacePageButton.Add_Click({
    Start-Process 'https://www.npmjs.com/package/@waishnav/devspace'
})

$closeButton.Add_Click({ $form.Close() })

$saveButton.Add_Click({
    try {
        $languagePreference = @('Auto', 'Japanese', 'English')[$languageCombo.SelectedIndex]
        $theme = @('Classic', 'Neon')[$themeCombo.SelectedIndex]
        Write-SharedSettings -Value ([ordered]@{
            Theme      = $theme
            ShowBubble = [bool]$showBubbleCheck.Checked
            Language   = $languagePreference
        })

        $wshShell = New-Object -ComObject WScript.Shell
        if ($startupCheck.Checked) {
            $shortcut = $wshShell.CreateShortcut($startupShortcutPath)
            $shortcut.TargetPath = Join-Path $InstallDirectory 'Start-DevSpaceStatus.cmd'
            $shortcut.WorkingDirectory = $InstallDirectory
            $shortcut.Description = 'DevSpace Status Pet'
            $shortcut.IconLocation = "$env:SystemRoot\System32\shell32.dll,167"
            $shortcut.Save()
        }
        elseif (Test-Path -LiteralPath $startupShortcutPath) {
            Remove-Item -LiteralPath $startupShortcutPath -Force
        }

        $stopPath = Join-Path $InstallDirectory 'Stop-DevSpaceStatusPet.ps1'
        if (Test-Path -LiteralPath $stopPath) {
            & $stopPath -InstallDirectory $InstallDirectory | Out-Null
        }
        Start-Sleep -Milliseconds 500
        Start-Process -FilePath (Join-Path $InstallDirectory 'Start-DevSpaceStatus.cmd')
        [System.Windows.Forms.MessageBox]::Show((S 'SettingsSaved'), (S 'SettingsTitle')) | Out-Null
        $form.Close()
    }
    catch {
        [System.Windows.Forms.MessageBox]::Show($_.Exception.Message, (S 'SettingsTitle')) | Out-Null
    }
})

[void]$form.ShowDialog()
$form.Dispose()
