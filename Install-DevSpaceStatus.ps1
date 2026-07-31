[CmdletBinding()]
param(
    [switch]$StartWithWindows = $true
)

$ErrorActionPreference = 'Stop'

$projectDirectory = Split-Path -Parent $MyInvocation.MyCommand.Path
$launcherPath = Join-Path $projectDirectory 'Start-DevSpaceStatus.cmd'

if (-not (Test-Path -LiteralPath $launcherPath)) {
    throw "起動ファイルが見つかりません: $launcherPath"
}

$wshShell = New-Object -ComObject WScript.Shell
$desktopPath = [Environment]::GetFolderPath('Desktop')
$desktopShortcutPath = Join-Path $desktopPath 'DevSpace 状態.lnk'

$desktopShortcut = $wshShell.CreateShortcut($desktopShortcutPath)
$desktopShortcut.TargetPath = $launcherPath
$desktopShortcut.WorkingDirectory = $projectDirectory
$desktopShortcut.Description = 'DevSpaceの状態監視とデスクトップペットを起動します'
$desktopShortcut.IconLocation = "$env:SystemRoot\System32\shell32.dll,167"
$desktopShortcut.Save()

if ($StartWithWindows) {
    $startupPath = [Environment]::GetFolderPath('Startup')
    $startupShortcutPath = Join-Path $startupPath 'DevSpace Status.lnk'
    $startupShortcut = $wshShell.CreateShortcut($startupShortcutPath)
    $startupShortcut.TargetPath = $launcherPath
    $startupShortcut.WorkingDirectory = $projectDirectory
    $startupShortcut.Description = 'Windowsログイン時にDevSpace状態監視とペットを開始します'
    $startupShortcut.IconLocation = "$env:SystemRoot\System32\shell32.dll,167"
    $startupShortcut.Save()
}

Start-Process -FilePath $launcherPath

Write-Host 'DevSpace状態監視をインストールしました。' -ForegroundColor Green
Write-Host "デスクトップ: $desktopShortcutPath"
if ($StartWithWindows) {
    Write-Host 'Windowsログイン時の自動起動: 有効'
}
Write-Host 'タスクトレイの丸いアイコンと、デスクトップ右下のペットで状態を確認できます。'
