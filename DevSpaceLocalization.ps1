Set-StrictMode -Version 2.0

$script:DevSpaceTextCatalog = @{
    Japanese = @{
        Unknown = '不明'
        GenericWork = '作業'
        ReadTarget = '読取: {0}'
        FileRead = 'ファイル読取'
        EditTarget = '編集: {0}'
        FileEdit = 'ファイル編集'
        WriteTarget = '作成: {0}'
        FileWrite = 'ファイル作成'
        CommandTarget = 'コマンド: {0}'
        CommandRun = 'コマンド実行'
        OpenWorkspace = 'ワークスペースを開く'
        LocalProcessing = 'ローカル処理'
        Working = '作業中'
        Waiting = '次の処理待ち'
        Failed = '処理失敗'
        Stopped = '停止中'
        Idle = '待機中'
        Stalled = '停滞の疑い'
        StopOperation = '停止'
        IdleOperation = '待機'
        StoppedSummary = 'DevSpaceは停止しています'
        WorkingSummary = 'DevSpaceが作業中です ({0})'
        ParallelSummary = 'DevSpaceが{0}件を並列実行中です'
        ParallelOperation = '{0}件を並列実行'
        WaitingSummary = '直前の処理が終了し、次の操作を待っています'
        FailedSummary = '直前の処理が失敗しました'
        IdleSummary = 'DevSpaceは起動済みで、現在は待機中です'
        StalledSummary = '{0}分以上、CPU・ログ更新が確認できません'
        LastNone = '最終作業: 記録なし'
        Success = '成功'
        Failure = '失敗'
        LastFormat = '最終作業: {0} / {1} / {2} / {3}'
        ProjectPrefix = 'プロジェクト: {0}'
        OperationPrefix = '処理: {0}'
        ElapsedPrefix = '経過時間: {0}'
        RunningProcesses = '実行中プロセス:'
        WorkFailedTitle = 'DevSpace 作業失敗'
        WorkDoneTitle = 'DevSpace 作業区切り完了'
        WorkTime = '作業時間: {0}'
        Checking = '確認中'
        CheckingStatus = '状態を確認中...'
        EstimatedSuffix = '（推定）'
        RefreshNow = '今すぐ再確認'
        Details = '詳細を表示'
        OpenLog = 'ログを開く'
        OpenFolder = '.devspaceフォルダーを開く'
        Exit = '終了'
        StallTitle = 'DevSpace 停滞の疑い'
        StopTitle = 'DevSpace 停止'
        StopText = 'DevSpaceサーバーが停止しました。'
        StatusError = '状態確認エラー'
        None = 'なし'
        StatusPrefix = '状態: {0}'
        LogMissing = 'ログが見つかりません。'
        MoreParallel = '並列実行中'
        MoreCount = '+{0}件'
        OtherTasks = 'ほかの処理'
        PetCheckingSummary = 'DevSpaceの状態を確認しています'
        PetCheckingOperation = '確認中'
        ShowBubble = '吹き出しを常時表示'
        Theme = 'テーマ'
        ThemeClassic = 'クラシック（状態色）'
        ThemeNeon = 'ネオン（紫・黄）'
        Language = '言語 / Language'
        LanguageAuto = '自動（OS言語）'
        LanguageJapanese = '日本語'
        LanguageEnglish = 'English'
        ResetPosition = '位置を右下へ戻す'
        ExitPet = 'ペットを終了'
        InstallerDone = 'DevSpace状態監視をインストールしました。'
        DesktopShortcut = 'デスクトップ: {0}'
        StartupEnabled = 'Windowsログイン時の自動起動: 有効'
        InstallerHint = 'タスクトレイの丸いアイコンと、デスクトップ右下のペットで状態を確認できます。'
    }
    English = @{
        Unknown = 'Unknown'
        GenericWork = 'Work'
        ReadTarget = 'Read: {0}'
        FileRead = 'Read file'
        EditTarget = 'Edit: {0}'
        FileEdit = 'Edit file'
        WriteTarget = 'Write: {0}'
        FileWrite = 'Write file'
        CommandTarget = 'Command: {0}'
        CommandRun = 'Run command'
        OpenWorkspace = 'Open workspace'
        LocalProcessing = 'Local process'
        Working = 'Working'
        Waiting = 'Waiting for next step'
        Failed = 'Process failed'
        Stopped = 'Stopped'
        Idle = 'Idle'
        Stalled = 'Possibly stalled'
        StopOperation = 'Stopped'
        IdleOperation = 'Idle'
        StoppedSummary = 'DevSpace is stopped'
        WorkingSummary = 'DevSpace is working ({0})'
        ParallelSummary = 'DevSpace is running {0} tasks in parallel'
        ParallelOperation = '{0} parallel tasks'
        WaitingSummary = 'The previous process finished and is waiting for the next step'
        FailedSummary = 'The previous process failed'
        IdleSummary = 'DevSpace is running and currently idle'
        StalledSummary = 'No CPU or log activity has been detected for more than {0} minutes'
        LastNone = 'Last action: none'
        Success = 'Success'
        Failure = 'Failed'
        LastFormat = 'Last action: {0} / {1} / {2} / {3}'
        ProjectPrefix = 'Project: {0}'
        OperationPrefix = 'Operation: {0}'
        ElapsedPrefix = 'Elapsed: {0}'
        RunningProcesses = 'Running processes:'
        WorkFailedTitle = 'DevSpace work failed'
        WorkDoneTitle = 'DevSpace work segment finished'
        WorkTime = 'Work time: {0}'
        Checking = 'Checking'
        CheckingStatus = 'Checking status...'
        EstimatedSuffix = ' (estimated)'
        RefreshNow = 'Refresh now'
        Details = 'Show details'
        OpenLog = 'Open log'
        OpenFolder = 'Open .devspace folder'
        Exit = 'Exit'
        StallTitle = 'DevSpace may be stalled'
        StopTitle = 'DevSpace stopped'
        StopText = 'The DevSpace server has stopped.'
        StatusError = 'Status check error'
        None = 'None'
        StatusPrefix = 'Status: {0}'
        LogMissing = 'The log file was not found.'
        MoreParallel = 'Parallel tasks'
        MoreCount = '+{0} more'
        OtherTasks = 'Other tasks'
        PetCheckingSummary = 'Checking DevSpace status'
        PetCheckingOperation = 'Checking'
        ShowBubble = 'Always show bubbles'
        Theme = 'Theme'
        ThemeClassic = 'Classic (status colors)'
        ThemeNeon = 'Neon (purple and yellow)'
        Language = 'Language / 言語'
        LanguageAuto = 'Auto (OS language)'
        LanguageJapanese = '日本語'
        LanguageEnglish = 'English'
        ResetPosition = 'Reset position to bottom-right'
        ExitPet = 'Exit pet'
        InstallerDone = 'DevSpace Status Pet has been installed.'
        DesktopShortcut = 'Desktop shortcut: {0}'
        StartupEnabled = 'Start with Windows: enabled'
        InstallerHint = 'Use the tray icon and the desktop pet to view DevSpace activity.'
    }
}

function Resolve-DevSpaceLanguage {
    param([string]$Preference = 'Auto')

    switch -Regex ($Preference) {
        '^(Japanese|ja|日本語)$' { return 'Japanese' }
        '^(English|en)$' { return 'English' }
        default {
            if ([System.Globalization.CultureInfo]::CurrentUICulture.TwoLetterISOLanguageName -eq 'ja') {
                return 'Japanese'
            }
            return 'English'
        }
    }
}

function Get-DevSpaceText {
    param(
        [string]$Language,
        [string]$Key,
        [object[]]$Arguments = @()
    )

    $resolvedLanguage = Resolve-DevSpaceLanguage -Preference $Language
    $catalog = $script:DevSpaceTextCatalog[$resolvedLanguage]
    if (-not $catalog.ContainsKey($Key)) {
        $catalog = $script:DevSpaceTextCatalog['English']
    }
    if (-not $catalog.ContainsKey($Key)) {
        return $Key
    }

    $text = [string]$catalog[$Key]
    if ($Arguments.Count -gt 0) {
        return [string]::Format([System.Globalization.CultureInfo]::CurrentCulture, $text, $Arguments)
    }
    return $text
}

function Read-DevSpaceSharedSettings {
    param([string]$Path)

    $settings = [pscustomobject]@{
        Theme      = 'Classic'
        ShowBubble = $true
        Language   = 'Auto'
    }

    try {
        if (-not (Test-Path -LiteralPath $Path)) {
            return $settings
        }
        $json = [System.IO.File]::ReadAllText($Path, [System.Text.Encoding]::UTF8)
        $saved = $json | ConvertFrom-Json -ErrorAction Stop
        if ($saved.PSObject.Properties.Name -contains 'Theme' -and [string]$saved.Theme -in @('Classic', 'Neon')) {
            $settings.Theme = [string]$saved.Theme
        }
        if ($saved.PSObject.Properties.Name -contains 'ShowBubble') {
            $settings.ShowBubble = [bool]$saved.ShowBubble
        }
        if ($saved.PSObject.Properties.Name -contains 'Language' -and [string]$saved.Language -in @('Auto', 'Japanese', 'English')) {
            $settings.Language = [string]$saved.Language
        }
    }
    catch {
        # Defaults remain valid.
    }

    return $settings
}
