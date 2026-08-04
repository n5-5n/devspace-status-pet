# DevSpace Status Pet v0.1.6-alpha.3

**[日本語](README.dotnet.md) | [English](README.dotnet.en.md) | [简体中文](README.dotnet.zh-CN.md)**

DevSpaceの作業状況を、タスクトレイとデスクトップペットで表示するWindows向けモニターです。v0.1.6-alpha.3は、全プロセス監視で毎秒数百件のアクセス拒否例外が発生し、長時間後にメモリが数GBへ再膨張する問題を修正したHotfix Prereleaseです。画面端への収納機能も引き続き利用できます。

## 必要環境

- Windows 10またはWindows 11（x64）
- `@waishnav/devspace`を同じPCで実行

.NET Runtimeの追加インストールは不要です。

## インストール／更新

1. ZIPを展開
2. `DevSpaceStatusPet.exe`を実行
3. 右クリックメニューから**インストール／更新**を選択

または次を実行します。

```text
DevSpaceStatusPet.exe --install
```

インストール先：

```text
%LOCALAPPDATA%\DevSpaceStatusPetV2\DevSpaceStatusPet.exe
```

PowerShell版と.NET版の既存設定は自動的に引き継がれます。

## 主な機能

- プロジェクト名、処理内容、経過時間の表示
- 複数ワークスペースの並列吹き出し
- UTF-8／UTF-16／UTF-32ログ対応
- 完了、失敗、停滞、DevSpace停止通知
- 完了後の待機吹き出し自動削除
- クラシック／ネオンのロボットテーマ
- ライト／ダークの吹き出しテーマ
- ダーク化された右クリックメニューと設定画面
- 日本語／English／简体中文／OS言語自動選択
- サイズ、透明度、通知時間、吹き出し数の即時変更
- 右クリックで近い画面端へ収納し、取っ手へのホバー／クリックで復帰
- 大きな倍率でも現在のモニター内へ自動フィット
- GitHub Releasesの更新確認とSHA-256検証付き手動更新
- Stable／Prerelease更新チャンネル
- モニター復帰、画面構成変更、画面外移動からの表示自己復旧
- トレイの「ペットを表示／復旧」
- Windows自動起動、自己アンインストール、通常診断ログ、クラッシュログ

## アプリ内更新

タスクトレイまたは設定画面から**更新を確認**を選択します。ZIP、SHA-256、EXEバージョンを検証し、失敗した場合は旧EXEへ復旧します。既定ではStable Releaseのみを確認し、Prereleaseは設定で選択できます。

## ポータブル実行

インストールせず、展開したEXEを直接実行することもできます。

```text
DevSpaceStatusPet.exe --settings
```

## アンインストール

設定を保持：

```text
DevSpaceStatusPet.exe --uninstall
```

設定も削除：

```text
DevSpaceStatusPet.exe --uninstall --remove-settings
```

DevSpace本体やプロジェクトは変更しません。

## 設定保存先

```text
%USERPROFILE%\.devspace\devspace-pet-settings.json
%USERPROFILE%\.devspace\devspace-pet-position.json
```

通常診断ログ：

```text
%LOCALAPPDATA%\DevSpaceStatusPet\logs\runtime.log
```

クラッシュログ：

```text
%LOCALAPPDATA%\DevSpaceStatusPet\logs\crash.log
```

## 旧PowerShell版

PowerShell製v0.1.0はロールバック用としてGitHub Releasesに残しています。
