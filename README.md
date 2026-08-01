# DevSpace Status Pet

**[日本語](README.md) | [English](README.en.md)**

Windows上のDevSpace作業を、タスクトレイとアニメーションするデスクトップペットで確認するモニターです。

> **安定版：v0.2.0（C# / .NET 8・単一EXE）**<br>
> 旧PowerShell版のv0.1.0も、ロールバック用としてGitHub Releasesに残しています。

## 主な機能

- DevSpaceの実際のローカル処理を素早く検出
- プロジェクト名、処理内容、経過時間を表示
- 複数チャット／ワークスペースを別々の吹き出しで表示
- UTF-8／UTF-16／UTF-32のDevSpaceログを自動判定
- 過去の`open_workspace`履歴からプロジェクト名を復元
- 作業区切り、失敗、停滞、DevSpace停止を通知
- 完了後の「次の処理待ち」吹き出しを設定時間で自動削除
- クラシック／ネオンのロボットテーマ
- ライト／ダークの吹き出しテーマ
- ダーク化されたタスクトレイメニュー、ペットメニュー、設定画面
- 日本語／English／OS言語自動選択
- サイズ、透明度、通知時間、停滞判定、吹き出し数を即時変更
- Windowsログイン時の自動起動
- クラッシュログと診断情報

## プレビュー

| クラシック | ネオン |
|---|---|
| ![Classic theme](docs/classic-preview.svg) | ![Neon theme](docs/neon-preview.svg) |

## 必要環境

- Windows 10またはWindows 11（x64）
- [`@waishnav/devspace`](https://www.npmjs.com/package/@waishnav/devspace)
- DevSpaceと本ツールを同じPCで実行

.NET Runtimeの別途インストールや、PowerShell実行ポリシーの変更は不要です。macOSとLinuxには対応していません。

## インストール

1. [GitHub Releases](https://github.com/n5-5n/devspace-status-pet/releases/latest)から`DevSpace-Status-Pet-v0.2.0-win-x64.zip`をダウンロード
2. ZIPを展開
3. `DevSpaceStatusPet.exe`を実行
4. 右クリックメニューから**v0.2をインストール／更新**を選択

コマンドでもインストールできます。

```text
DevSpaceStatusPet.exe --install
```

インストール先：

```text
%LOCALAPPDATA%\DevSpaceStatusPetV2\DevSpaceStatusPet.exe
```

既存のv0.1／v0.2 alpha設定は自動的に引き継がれます。

## ポータブル実行

展開した`DevSpaceStatusPet.exe`をそのまま起動しても使用できます。

設定画面を直接開く場合：

```text
DevSpaceStatusPet.exe --settings
```

## 状態表示

| 色 | 状態 |
|---|---|
| 緑 | ローカル処理を実行中 |
| 青 | DevSpace起動済み・待機中 |
| 黄 | 直前の処理が終了し、次の操作待ち |
| オレンジ | 直前の処理が失敗 |
| 紫 | CPUとログ更新が長時間なく、停滞の疑い |
| 赤 | DevSpace停止中 |

黄色は作業全体の完了を即座に意味しません。既定では最後のDevSpace操作から45秒間、新しい処理がなければ完了通知を1回表示し、待機中の吹き出しも同時に消えます。

## 並列作業

ワークスペースIDごとに活動を分離するため、同じプロジェクトを複数チャットで操作していても別々の吹き出しとして表示されます。

```text
VideoShrink
テスト実行
作業中  03:21

VideoShrink
ファイル編集
次の処理待ち  00:08
```

最大表示数は設定画面から1～8件で変更できます。

## 設定

ペットまたはタスクトレイを右クリックして**設定**を開きます。変更は保存ボタンを押さなくても即時反映されます。

- 表示言語：自動／日本語／English
- ロボットテーマ：クラシック／ネオン
- 吹き出しテーマ：ライト／ダーク
- ペットサイズ、透明度
- 吹き出し表示と最大件数
- 完了通知までの待機秒数
- 停滞判定時間
- 通知の有効／無効
- Windows自動起動

設定ファイル：

```text
%USERPROFILE%\.devspace\devspace-pet-settings.json
%USERPROFILE%\.devspace\devspace-pet-position.json
```

クラッシュログ：

```text
%LOCALAPPDATA%\DevSpaceStatusPet\logs\crash.log
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

DevSpace本体やDevSpaceのプロジェクトには触れません。

## 開発・検証

```powershell
dotnet build .\src\DevSpaceStatusPet\DevSpaceStatusPet.csproj -c Release -warnaserror
dotnet run --project .\tests\DevSpaceStatusPet.Smoke\DevSpaceStatusPet.Smoke.csproj -c Release
.\scripts\Build-DotNetRelease.ps1
```

タグ`v0.2.x`をpushすると、GitHub ActionsがWindows上でビルド、スモークテスト、自己インストール／アンインストール試験を行い、ZIPとSHA-256をGitHub Releasesへ公開します。ハイフン付きバージョンはPrerelease、通常バージョンはStable Releaseとして公開されます。

## 旧v0.1版

PowerShell製v0.1.0はGitHub Releasesに残しています。v0.2はv0.1のテーマ、言語、吹き出し、位置設定をそのまま読み取ります。

## License

[MIT License](LICENSE)
