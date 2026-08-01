using System.Globalization;
using DevSpaceStatusPet.Models;

namespace DevSpaceStatusPet.Services;

public enum UiLanguage
{
    Japanese,
    English
}

public sealed class Localizer
{
    private readonly Func<AppSettings> _settingsProvider;

    public Localizer(Func<AppSettings> settingsProvider)
    {
        _settingsProvider = settingsProvider;
    }

    public UiLanguage Language => Resolve(_settingsProvider().LanguagePreference);

    public string this[string key] => Get(key);

    public string Get(string key, params object[] args)
    {
        var catalog = Language == UiLanguage.Japanese ? Japanese : English;
        var text = catalog.TryGetValue(key, out var value)
            ? value
            : English.TryGetValue(key, out var fallback) ? fallback : key;
        return args.Length == 0
            ? text
            : string.Format(CultureInfo.CurrentCulture, text, args);
    }

    public string State(ActivityState state) => state switch
    {
        ActivityState.Working => Get("Working"),
        ActivityState.Waiting => Get("Waiting"),
        ActivityState.Failed => Get("Failed"),
        ActivityState.Stalled => Get("Stalled"),
        ActivityState.Stopped => Get("Stopped"),
        _ => Get("Idle")
    };

    public string Operation(OperationKind kind, string? detail) => kind switch
    {
        OperationKind.Read => string.IsNullOrWhiteSpace(detail) ? Get("ReadFile") : Get("ReadTarget", detail),
        OperationKind.Edit => string.IsNullOrWhiteSpace(detail) ? Get("EditFile") : Get("EditTarget", detail),
        OperationKind.Write => string.IsNullOrWhiteSpace(detail) ? Get("WriteFile") : Get("WriteTarget", detail),
        OperationKind.Command => string.IsNullOrWhiteSpace(detail) ? Get("RunCommand") : Get("CommandTarget", detail),
        OperationKind.OpenWorkspace => Get("OpenWorkspace"),
        OperationKind.Dotnet => string.IsNullOrWhiteSpace(detail) ? "dotnet" : detail,
        OperationKind.Git => string.IsNullOrWhiteSpace(detail) ? "git" : detail,
        OperationKind.Ffmpeg => string.IsNullOrWhiteSpace(detail) ? "ffmpeg" : detail,
        OperationKind.PowerShell => "PowerShell",
        OperationKind.Python => "Python",
        OperationKind.LocalProcess => string.IsNullOrWhiteSpace(detail) ? Get("LocalProcess") : detail,
        OperationKind.Stopped => Get("Stopped"),
        OperationKind.Idle => Get("Idle"),
        _ => string.IsNullOrWhiteSpace(detail) ? Get("Unknown") : detail
    };

    public static UiLanguage Resolve(UiLanguagePreference preference) => preference switch
    {
        UiLanguagePreference.Japanese => UiLanguage.Japanese,
        UiLanguagePreference.English => UiLanguage.English,
        _ => CultureInfo.CurrentUICulture.TwoLetterISOLanguageName.Equals("ja", StringComparison.OrdinalIgnoreCase)
            ? UiLanguage.Japanese
            : UiLanguage.English
    };

    private static readonly IReadOnlyDictionary<string, string> Japanese = new Dictionary<string, string>
    {
        ["AppName"] = "DevSpace Status Pet",
        ["Working"] = "作業中",
        ["Waiting"] = "次の処理待ち",
        ["Failed"] = "処理失敗",
        ["Stalled"] = "停滞の疑い",
        ["Stopped"] = "停止中",
        ["Idle"] = "待機中",
        ["Unknown"] = "不明",
        ["ReadFile"] = "ファイル読取",
        ["ReadTarget"] = "読取: {0}",
        ["EditFile"] = "ファイル編集",
        ["EditTarget"] = "編集: {0}",
        ["WriteFile"] = "ファイル作成",
        ["WriteTarget"] = "作成: {0}",
        ["RunCommand"] = "コマンド実行",
        ["CommandTarget"] = "コマンド: {0}",
        ["OpenWorkspace"] = "ワークスペースを開く",
        ["LocalProcess"] = "ローカル処理",
        ["Project"] = "プロジェクト: {0}",
        ["Operation"] = "処理: {0}",
        ["Elapsed"] = "経過時間: {0}",
        ["Refresh"] = "今すぐ再確認",
        ["Settings"] = "設定",
        ["OpenLog"] = "ログを開く",
        ["OpenFolder"] = ".devspaceフォルダーを開く",
        ["Exit"] = "終了",
        ["ShowBubble"] = "吹き出しを常時表示",
        ["Theme"] = "ロボットテーマ",
        ["Classic"] = "クラシック（状態色）",
        ["Neon"] = "ネオン（紫・黄）",
        ["BubbleTheme"] = "吹き出しテーマ",
        ["BubbleLight"] = "ライト",
        ["BubbleDark"] = "ダーク",
        ["BubbleStyle"] = "吹き出しデザイン",
        ["BubbleSpeech"] = "標準吹き出し",
        ["BubbleMonitorCard"] = "モニターカード",
        ["Language"] = "言語 / Language",
        ["Auto"] = "自動（OS言語）",
        ["Japanese"] = "日本語",
        ["English"] = "English",
        ["ResetPosition"] = "位置を右下へ戻す",
        ["ParallelMore"] = "+{0}件",
        ["OtherTasks"] = "ほかの処理",
        ["WorkDoneTitle"] = "DevSpace 作業区切り完了",
        ["WorkFailedTitle"] = "DevSpace 作業失敗",
        ["WorkTime"] = "作業時間: {0}",
        ["StopTitle"] = "DevSpace 停止",
        ["StopText"] = "DevSpaceサーバーが停止しました。",
        ["StallTitle"] = "DevSpace 停滞の疑い",
        ["Status"] = "状態: {0}",
        ["Port"] = "ポート: {0}",
        ["Config"] = "設定ファイル: {0}",
        ["Log"] = "ログ: {0}",
        ["General"] = "一般",
        ["Appearance"] = "表示",
        ["Notification"] = "通知",
        ["Scale"] = "サイズ",
        ["Opacity"] = "透明度",
        ["QuietSeconds"] = "完了通知までの待機秒数",
        ["StallMinutes"] = "停滞判定（分）",
        ["MaxBubbles"] = "最大吹き出し数",
        ["NotificationsEnabled"] = "Windows通知を有効にする",
        ["StartWithWindows"] = "Windowsログイン時に起動",
        ["Save"] = "保存",
        ["Close"] = "閉じる",
        ["OpenLogFolder"] = "ログフォルダーを開く",
        ["InstallUpdate"] = "インストール／更新",
        ["UninstallV2"] = "アンインストール",
        ["ConfirmUninstall"] = "DevSpace Status Petをアンインストールしますか？\n設定は保持されます。",
        ["Version"] = "バージョン: {0}",
        ["Saved"] = "設定を保存しました。",
        ["NoLog"] = "ログファイルが見つかりません。",
        ["CheckUpdates"] = "更新を確認",
        ["CheckingUpdates"] = "更新を確認中…",
        ["UpdateAvailableMenu"] = "更新あり: v{0}",
        ["UpdateAvailableTitle"] = "新しいバージョンがあります",
        ["UpdateAvailableText"] = "DevSpace Status Pet v{0}を利用できます。",
        ["UpdateVersionLine"] = "現在: v{0}    最新: v{1}",
        ["UpdatePublished"] = "公開日時: {0}",
        ["NoReleaseNotes"] = "リリースノートはありません。",
        ["UpdateReady"] = "内容を確認してから更新してください。",
        ["InstallUpdateNow"] = "更新する",
        ["OpenReleasePage"] = "GitHubで見る",
        ["Cancel"] = "キャンセル",
        ["DownloadingChecksum"] = "チェックサムを取得しています…",
        ["DownloadingUpdate"] = "更新をダウンロードしています… {0}",
        ["VerifyingUpdate"] = "SHA-256を検証しています…",
        ["ExtractingUpdate"] = "更新を展開しています…",
        ["StartingInstaller"] = "更新版を起動しています…",
        ["UpdateCancelled"] = "更新をキャンセルしました。",
        ["UpdateFailed"] = "更新に失敗しました。",
        ["UpdateFailedDetail"] = "更新に失敗しました。現在のバージョンは変更されていません。\n\n{0}",
        ["UpToDate"] = "最新バージョンです。",
        ["UpdateCheckFailed"] = "更新確認に失敗しました。",
        ["UpdateCheckFailedDetail"] = "更新確認に失敗しました。\n\n{0}",
        ["LatestVersion"] = "最新バージョン",
        ["UpdateStatus"] = "更新状態",
        ["CheckUpdatesOnStartup"] = "起動時に更新を確認",
        ["IncludePrereleaseUpdates"] = "開発版（Prerelease）も確認",
        ["NotChecked"] = "未確認",
        ["None"] = "なし"
    };

    private static readonly IReadOnlyDictionary<string, string> English = new Dictionary<string, string>
    {
        ["AppName"] = "DevSpace Status Pet",
        ["Working"] = "Working",
        ["Waiting"] = "Waiting for next step",
        ["Failed"] = "Process failed",
        ["Stalled"] = "Possibly stalled",
        ["Stopped"] = "Stopped",
        ["Idle"] = "Idle",
        ["Unknown"] = "Unknown",
        ["ReadFile"] = "Read file",
        ["ReadTarget"] = "Read: {0}",
        ["EditFile"] = "Edit file",
        ["EditTarget"] = "Edit: {0}",
        ["WriteFile"] = "Write file",
        ["WriteTarget"] = "Write: {0}",
        ["RunCommand"] = "Run command",
        ["CommandTarget"] = "Command: {0}",
        ["OpenWorkspace"] = "Open workspace",
        ["LocalProcess"] = "Local process",
        ["Project"] = "Project: {0}",
        ["Operation"] = "Operation: {0}",
        ["Elapsed"] = "Elapsed: {0}",
        ["Refresh"] = "Refresh now",
        ["Settings"] = "Settings",
        ["OpenLog"] = "Open log",
        ["OpenFolder"] = "Open .devspace folder",
        ["Exit"] = "Exit",
        ["ShowBubble"] = "Always show bubbles",
        ["Theme"] = "Robot theme",
        ["Classic"] = "Classic (status colors)",
        ["Neon"] = "Neon (purple and yellow)",
        ["BubbleTheme"] = "Bubble theme",
        ["BubbleLight"] = "Light",
        ["BubbleDark"] = "Dark",
        ["BubbleStyle"] = "Bubble design",
        ["BubbleSpeech"] = "Standard speech bubble",
        ["BubbleMonitorCard"] = "Monitor card",
        ["Language"] = "Language / 言語",
        ["Auto"] = "Auto (OS language)",
        ["Japanese"] = "日本語",
        ["English"] = "English",
        ["ResetPosition"] = "Reset position to bottom-right",
        ["ParallelMore"] = "+{0} more",
        ["OtherTasks"] = "Other tasks",
        ["WorkDoneTitle"] = "DevSpace work segment finished",
        ["WorkFailedTitle"] = "DevSpace work failed",
        ["WorkTime"] = "Work time: {0}",
        ["StopTitle"] = "DevSpace stopped",
        ["StopText"] = "The DevSpace server has stopped.",
        ["StallTitle"] = "DevSpace may be stalled",
        ["Status"] = "Status: {0}",
        ["Port"] = "Port: {0}",
        ["Config"] = "Config: {0}",
        ["Log"] = "Log: {0}",
        ["General"] = "General",
        ["Appearance"] = "Appearance",
        ["Notification"] = "Notifications",
        ["Scale"] = "Size",
        ["Opacity"] = "Opacity",
        ["QuietSeconds"] = "Quiet seconds before completion notification",
        ["StallMinutes"] = "Stall threshold (minutes)",
        ["MaxBubbles"] = "Maximum bubbles",
        ["NotificationsEnabled"] = "Enable Windows notifications",
        ["StartWithWindows"] = "Start with Windows",
        ["Save"] = "Save",
        ["Close"] = "Close",
        ["OpenLogFolder"] = "Open log folder",
        ["InstallUpdate"] = "Install / update",
        ["UninstallV2"] = "Uninstall",
        ["ConfirmUninstall"] = "Uninstall DevSpace Status Pet?\nSettings will be kept.",
        ["Version"] = "Version: {0}",
        ["Saved"] = "Settings saved.",
        ["NoLog"] = "The log file was not found.",
        ["CheckUpdates"] = "Check for updates",
        ["CheckingUpdates"] = "Checking for updates…",
        ["UpdateAvailableMenu"] = "Update available: v{0}",
        ["UpdateAvailableTitle"] = "A new version is available",
        ["UpdateAvailableText"] = "DevSpace Status Pet v{0} is available.",
        ["UpdateVersionLine"] = "Current: v{0}    Latest: v{1}",
        ["UpdatePublished"] = "Published: {0}",
        ["NoReleaseNotes"] = "No release notes are available.",
        ["UpdateReady"] = "Review the release notes before updating.",
        ["InstallUpdateNow"] = "Update now",
        ["OpenReleasePage"] = "View on GitHub",
        ["Cancel"] = "Cancel",
        ["DownloadingChecksum"] = "Downloading checksum…",
        ["DownloadingUpdate"] = "Downloading update… {0}",
        ["VerifyingUpdate"] = "Verifying SHA-256…",
        ["ExtractingUpdate"] = "Extracting update…",
        ["StartingInstaller"] = "Starting the updated version…",
        ["UpdateCancelled"] = "Update cancelled.",
        ["UpdateFailed"] = "Update failed.",
        ["UpdateFailedDetail"] = "The update failed. Your current version was not changed.\n\n{0}",
        ["UpToDate"] = "You are using the latest version.",
        ["UpdateCheckFailed"] = "Update check failed.",
        ["UpdateCheckFailedDetail"] = "Could not check for updates.\n\n{0}",
        ["LatestVersion"] = "Latest version",
        ["UpdateStatus"] = "Update status",
        ["CheckUpdatesOnStartup"] = "Check for updates at startup",
        ["IncludePrereleaseUpdates"] = "Include prerelease builds",
        ["NotChecked"] = "Not checked",
        ["None"] = "None"
    };
}
