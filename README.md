# DevSpace Status Pet

**[日本語](README.md) | [English](README.en.md)**

Windows上で動くDevSpaceの状態を、タスクトレイとデスクトップペットで確認する軽量モニターです。

- DevSpaceが実際にローカル処理を実行しているかを表示
- プロジェクト名、処理内容、経過時間を安全な要約で表示
- 複数チャット／ワークスペースの並列作業を複数の吹き出しで表示
- 作業の区切り、失敗、停滞、DevSpace停止を通知
- クラシック／ネオンのテーマ切替
- 日本語／英語／OS言語への自動追従
- タスクバーには出さず、画面右下へ常駐

## 対応環境

### 対応

- Windows 10またはWindows 11
- Windows PowerShell 5.1
- `@waishnav/devspace`
- DevSpaceとこのモニターを同じWindows PCで実行する構成
- DevSpaceプロジェクトを任意のドライブや任意のフォルダーへ配置する構成
- スペース、日本語などを含むパス
- ローカルドライブおよびUNCパス

DevSpaceの次の設定を自動で読み取ります。

```text
%USERPROFILE%\.devspace\config.json
```

- `port`
- `allowedRoots`

ログは既定で、同じ`.devspace`フォルダー内の`serve.log`を使用します。

### 制限

- macOSとLinuxには未対応です。
- DevSpaceを別PCで実行し、このペットだけを手元のPCで動かす構成には未対応です。
- DevSpaceのログ保存場所を独自変更している場合は、`-LogPath`を指定してください。
- DevSpaceのログ形式が将来大きく変わった場合は、追従修正が必要になる可能性があります。
- 「作業区切り完了」は、一定時間DevSpace操作がないことを利用した推定です。

## インストールと起動

1. リポジトリを任意のフォルダーへ配置します。
2. `Install-DevSpaceStatus.cmd`を1回実行します。
3. 以後はWindowsログイン時に自動起動します。

インストーラーは次を作成します。

- デスクトップ：`DevSpace Status Pet.lnk`
- スタートアップ：`DevSpace Status Pet.lnk`

手動起動は`Start-DevSpaceStatus.cmd`、状態の1回確認は`Check-DevSpaceStatus.cmd`です。

多重起動防止があるため、同じ監視やペットが複数起動することはありません。

## 言語

ペットを右クリックし、**言語 / Language**から選択します。

- **自動（OS言語）**：WindowsのUI言語が日本語なら日本語、それ以外は英語
- **日本語**
- **English**

設定は次へ保存され、ペット、タスクトレイ、通知、詳細画面へまとめて反映されます。

```text
%USERPROFILE%\.devspace\devspace-pet-settings.json
```

## タスクトレイの状態

| 色 | 状態 |
|---|---|
| 緑 | ローカル処理を実行中 |
| 青 | DevSpace起動済み・待機中 |
| 黄 | 直前の処理が終了し、次の操作待ち |
| オレンジ | 直前の処理が失敗 |
| 紫 | 長時間、CPUとログの更新がなく停滞の疑い |
| 赤 | DevSpace停止中 |

黄色は作業全体の完了を意味しません。

Windowsの完了通知は、最後のDevSpace操作から既定で45秒間、新しい処理がない場合だけ「作業区切り完了」として1回表示します。個々の`read`、`edit`、`bash`の完了ごとには通知しません。

## デスクトップペット

ペットは状態に合わせて動きます。

- 待機中：ゆっくり上下する
- 作業中：手足を動かす
- 次の処理待ち：ジャンプする
- 失敗：目が×印になる
- 停滞：`Z`を表示する
- DevSpace停止：電源が切れたような表示になる

### テーマ

ペットを右クリックして選択します。

- **クラシック（状態色）**：青、緑、黄、赤、紫を状態に合わせて使用
- **ネオン（紫・黄）**：黒い筐体、紫のネオン、黄色の目とランプ

### 並列作業

複数のDevSpaceワークスペースや処理が並行している場合、プロジェクトごとの吹き出しを最大4段表示します。

```text
VideoShrink
Run-BatchGuiSmoke
作業中  03:21

personal-hub
ファイル編集
次の処理待ち  00:08
```

5件以上ある場合は、最後の吹き出しへ残り件数をまとめます。

### 操作

- 左ドラッグ：移動
- 左クリック：吹き出しの常時表示を切り替え
- 右クリック：テーマ、言語、吹き出し表示、位置リセット、終了

## 状態連携と安全性

監視は、ペット用の状態を次へ書き出します。

```text
%USERPROFILE%\.devspace\devspace-status.json
```

ペットへ渡すのは次の安全な要約だけです。

- 状態
- プロジェクト名
- `dotnet test`などの短い処理名
- 経過時間
- 成否

コマンド全文、認証情報、環境変数は状態JSONへ書き出しません。

## 主な引数

DevSpaceのポートとルートは`config.json`から自動取得します。手動指定も可能です。

```powershell
.\DevSpaceStatus.ps1 `
  -RefreshSeconds 3 `
  -CompletionQuietSeconds 45 `
  -StallMinutes 30 `
  -Port 7676 `
  -LogPath "$env:USERPROFILE\.devspace\serve.log"
```

```powershell
.\DevSpacePet.ps1 -StateRefreshMilliseconds 750
```

## 検証

```powershell
.\tests\ParseScripts.ps1
```

次を検証します。

- Windows PowerShell 5.1の構文
- 別ドライブのワークスペース
- スペース入りプロジェクト名
- UNCパス
- 日本語と英語のローカライズ

GitHub Actionsでも同じ検証を実行します。

## ファイル

- `DevSpaceLocalization.ps1`：日本語・英語辞書と共通設定読込
- `DevSpaceStatus.ps1`：状態判定、並列活動の集約、タスクトレイ、通知
- `DevSpacePet.ps1`：テーマ・言語切替、複数吹き出し、アニメーション
- `Start-DevSpaceStatus.cmd`：監視とペットを起動
- `Check-DevSpaceStatus.cmd`：状態を1回確認
- `Install-DevSpaceStatus.ps1`：ショートカットと自動起動を設定
- `Install-DevSpaceStatus.cmd`：インストーラー起動

## License

MIT License
