# DevSpace Status Pet

**[日本語](README.md) | [English](README.en.md)**

Windows上のDevSpace作業を、タスクトレイとアニメーションするデスクトップペットで確認するモニターです。

- 実際のローカル処理を素早く検出
- プロジェクト名、処理内容、経過時間を表示
- 複数チャット／ワークスペースの並列作業を複数吹き出しで表示
- 作業区切り、失敗、停滞、DevSpace停止を通知
- クラシック／ネオンの2テーマ
- 日本語／英語／OS言語自動選択
- タスクバーには表示せず、画面上へ常駐

## プレビュー

| クラシック | ネオン |
|---|---|
| ![Classic theme](docs/classic-preview.svg) | ![Neon theme](docs/neon-preview.svg) |

## 必要環境

- Windows 10またはWindows 11
- Windows PowerShell 5.1
- [`@waishnav/devspace`](https://www.npmjs.com/package/@waishnav/devspace)
- DevSpaceと本ツールを同じPCで実行

macOSとLinuxには対応していません。

## 最短インストール

1. [GitHub Releases](https://github.com/n5-5n/devspace-status-pet/releases/latest)から`DevSpace-Status-Pet-vX.Y.Z.zip`をダウンロード
2. ZIPを展開
3. `Install.cmd`を実行

インストーラーは次へコピーします。

```text
%LOCALAPPDATA%\DevSpaceStatusPet
```

作成されるもの：

- デスクトップ：`DevSpace Status Pet`
- デスクトップ：`DevSpace Status Pet Settings`
- Windowsログイン時の自動起動

DevSpaceを検出できない場合もインストール自体は完了し、必要な起動手順を案内します。

## 設定画面

ペットまたはタスクトレイを右クリックし、**設定を開く**を選択します。デスクトップの`DevSpace Status Pet Settings`からも開けます。

確認・変更できる項目：

- DevSpaceの起動状態
- 検出したポート
- `config.json`の位置
- `serve.log`の位置
- 表示言語：自動／日本語／English
- テーマ：クラシック／ネオン
- 吹き出しの常時表示
- Windowsログイン時の自動起動
- バージョン

保存すると、監視とペットを安全に再起動します。

## 状態表示

| 色 | 状態 |
|---|---|
| 緑 | ローカル処理を実行中 |
| 青 | DevSpace起動済み・待機中 |
| 黄 | 直前の処理が終了し、次の操作待ち |
| オレンジ | 直前の処理が失敗 |
| 紫 | CPUとログ更新が長時間なく、停滞の疑い |
| 赤 | DevSpace停止中 |

黄色は作業全体の完了を意味しません。完了通知は、最後のDevSpace操作から既定で45秒間、新しい処理がない場合に1回だけ表示します。個々の`read`、`edit`、`bash`の完了ごとには通知しません。

## 並列作業

複数のワークスペースやプロセスが動いている場合、最大4段の吹き出しを表示します。

```text
VideoShrink
dotnet test
作業中  03:21

personal-hub
ファイル編集
次の処理待ち  00:08
```

5件以上は残り件数へまとめます。

## ペット操作

- 左ドラッグ：移動
- 左クリック：吹き出しの常時表示を切替
- 右クリック：設定、言語、テーマ、位置リセット、終了

設定は次へ保存されます。

```text
%USERPROFILE%\.devspace\devspace-pet-settings.json
%USERPROFILE%\.devspace\devspace-pet-position.json
```

## アンインストール

インストール先または展開したZIP内の`Uninstall.cmd`を実行します。

アンインストール時に、テーマ・言語・ペット位置も削除するか選択できます。DevSpace本体やDevSpaceのプロジェクトには触れません。

## 自動検出と移植性

本ツールは各PCの次の情報を自動検出します。

- `%USERPROFILE%\.devspace\config.json`
- DevSpaceのポート
- `allowedRoots`
- 実際に開かれたワークスペース
- `serve.log`

別ドライブ、スペース入りパス、日本語パス、UNCパスをセルフテストしています。ログ位置を独自変更していて自動検出できない場合は、`DevSpaceStatus.ps1 -LogPath ...`で指定できます。

## 安全性

ペットへ渡す状態JSONには、次の安全な要約だけを書き出します。

- 状態
- プロジェクト名
- `dotnet test`などの短い処理名
- 経過時間
- 成否

コマンド全文、環境変数、認証情報は書き出しません。

## ソースから実行

```powershell
.\tests\ParseScripts.ps1
.\Start-DevSpaceStatus.cmd
```

リリースZIPを生成する場合：

```powershell
.\scripts\Build-Release.ps1
```

生成物：

```text
artifacts\DevSpace-Status-Pet-vX.Y.Z.zip
artifacts\DevSpace-Status-Pet-vX.Y.Z.zip.sha256
```

`vX.Y.Z`タグをpushすると、GitHub Actionsが検証、ZIP生成、GitHub Release公開を自動実行します。

## 変更履歴

[CHANGELOG.md](CHANGELOG.md)

## License

MIT License
