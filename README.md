# DevSpace Status Pet

**[日本語](README.md) | [English](README.en.md)**

Windows上のDevSpace作業を、タスクトレイとアニメーションするデスクトップペットで確認するモニターです。

> **安定版：v0.1.4（C# / .NET 8・表示自己復旧・診断ログ）**<br>
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
- 標準吹き出し／ネオンカード／クリーンカードを即時切替
- ピクセル単位のアルファ透過で実機とGitHubプレビューを同じ描画に統一
- ダーク化されたタスクトレイメニュー、ペットメニュー、設定画面
- 日本語／English／OS言語自動選択
- サイズ、透明度、通知時間、停滞判定、吹き出し数を即時変更
- 大きな倍率でも現在のモニター内へ自動フィット
- Windowsログイン時の自動起動
- モニター復帰・画面構成変更・画面外移動からの表示自己復旧
- タスクトレイからの**ペットを表示／復旧**
- ローテーション付き通常診断ログ
- GitHub Releasesからの更新確認とSHA-256検証付き手動更新
- Stable／Prerelease更新チャンネルの選択
- クラッシュログと診断情報

## プレビュー

| 標準・クラシック | 標準・ネオン |
|---|---|
| ![Classic parallel workspace preview](docs/preview-classic.png) | ![Neon parallel workspace preview](docs/preview-neon.png) |

| モニターカード（ネオン） | モニターカード（クリーン） |
|---|---|
| ![Neon monitor card preview](docs/preview-monitor-card-neon.png) | ![Clean monitor card preview](docs/preview-monitor-card-clean.png) |

| ダーク設定画面 | 安全な更新画面 |
|---|---|
| ![Dark settings preview](docs/preview-settings.png) | ![Safe updater preview](docs/preview-updater.png) |

![Dark pet context menu](docs/preview-menu.png)

## 必要環境

- Windows 10またはWindows 11（x64）
- [`@waishnav/devspace`](https://www.npmjs.com/package/@waishnav/devspace)
- DevSpaceと本ツールを同じPCで実行

.NET Runtimeの別途インストールや、PowerShell実行ポリシーの変更は不要です。macOSとLinuxには対応していません。

## インストール

1. [GitHub Releases](https://github.com/n5-5n/devspace-status-pet/releases/latest)から`DevSpace-Status-Pet-v0.1.4-win-x64.zip`をダウンロード
2. ZIPを展開
3. `DevSpaceStatusPet.exe`を実行
4. 右クリックメニューから**インストール／更新**を選択

コマンドでもインストールできます。

```text
DevSpaceStatusPet.exe --install
```

インストール先：

```text
%LOCALAPPDATA%\DevSpaceStatusPetV2\DevSpaceStatusPet.exe
```

PowerShell版と.NET版の既存設定は自動的に引き継がれます。Prereleaseも同じ設定ファイルを使用します。

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
Aurora Desktop
テスト実行
作業中  03:21

Aurora Desktop
ファイル編集
次の処理待ち  00:08
```

最大表示数は設定画面から1～8件で変更できます。

## 設定

ペットまたはタスクトレイを右クリックして**設定**を開きます。変更は保存ボタンを押さなくても即時反映されます。

- 表示言語：自動／日本語／English
- ロボットテーマ：クラシック／ネオン
- 吹き出しテーマ：ライト／ダーク
- 吹き出しデザイン：標準吹き出し／モニターカード（ネオン）／モニターカード（クリーン）
- ペットサイズ、透明度
- 吹き出し表示と最大件数
- 完了通知までの待機秒数
- 停滞判定時間
- 通知の有効／無効
- Windows自動起動
- 起動時の更新確認
- Prerelease版も更新対象に含めるか

設定ファイル：

```text
%USERPROFILE%\.devspace\devspace-pet-settings.json
%USERPROFILE%\.devspace\devspace-pet-position.json
```

## 更新

タスクトレイまたは設定画面の**更新を確認**から、GitHub Releasesの最新版を確認できます。

更新時は次の順序で検証します。

1. ZIPと`.sha256`をGitHubから取得
2. SHA-256が一致することを確認
3. ZIP内の危険なパスを拒否して展開
4. EXEのバージョンがReleaseと一致することを確認
5. 現在のEXEをバックアップして入れ替え
6. 起動に失敗した場合は旧EXEへ復旧

既定ではStable Releaseだけを確認します。設定画面でPrereleaseも対象にできます。自動で勝手に更新はせず、リリースノートを確認して**更新する**を押した場合だけ入れ替えます。

通常診断ログ：

```text
%LOCALAPPDATA%\DevSpaceStatusPet\logs\runtime.log
```

起動、終了、モニター／電源復帰、表示自己復旧、描画失敗を記録します。1MBを超えると`runtime.previous.log`へローテーションします。

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
.\src\DevSpaceStatusPet\bin\Release\net8.0-windows10.0.17763.0\win-x64\DevSpaceStatusPet.exe --capture-previews docs
```

タグ`v0.x.x`をpushすると、GitHub ActionsがWindows上でビルド、スモークテスト、自己インストール／アンインストール試験を行い、ZIPとSHA-256をGitHub Releasesへ公開します。ハイフン付きバージョンはPrerelease、通常バージョンはStable Releaseとして公開されます。

## バージョン運用

独立した更新を公開するたびにパッチ番号を1つ上げます。例：`v0.1.1 → v0.1.2 → v0.1.3`。

同じ更新内容を試験している間は基準番号を変えず、`alpha.1 → alpha.2 → alpha.3`のようにPrerelease番号だけを上げます。正式公開時は同じ基準番号からalpha表記を外します。

過去の公開番号は、`v0.2.0系 → v0.1.1系`、`v0.2.1 → v0.1.2`、`v0.3.0-alpha系 → v0.1.3-alpha系`へ遡及して正規化しました。対応表と詳細は[`VERSIONING.md`](VERSIONING.md)にあります。

## 旧PowerShell版

PowerShell製v0.1.0はGitHub Releasesに残しています。現在の.NET版は、そのテーマ、言語、吹き出し、位置設定をそのまま読み取ります。

## License

[MIT License](LICENSE)
