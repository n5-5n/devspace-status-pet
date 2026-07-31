# DevSpace Status Pet

Windows上で動くDevSpaceの状態を、タスクトレイとデスクトップペットで確認する軽量モニターです。

- DevSpaceが実際にローカル処理を実行しているかを表示
- プロジェクト名、処理内容、経過時間を安全な要約で表示
- 複数チャット／ワークスペースの並列作業を複数の吹き出しで表示
- 作業の区切り、失敗、停滞、DevSpace停止を通知
- タスクバーには出さず、画面右下へ常駐

## 必要環境

- Windows 10またはWindows 11
- Windows PowerShell 5.1
- [`@waishnav/devspace`](https://www.npmjs.com/package/@waishnav/devspace)
- DevSpaceが既定の `127.0.0.1:7676` で起動していること

ポートやログパスは `DevSpaceStatus.ps1` の引数で変更できます。

## 起動

`Start-DevSpaceStatus.cmd` をダブルクリックすると、次の2つが起動します。

1. タスクトレイ監視
2. デスクトップペット

多重起動防止があるため、同じものが複数起動することはありません。

Windowsログイン時の自動起動とデスクトップショートカットは、`Install-DevSpaceStatus.cmd` を1回実行して設定します。

状態だけをコンソールで確認する場合は、`Check-DevSpaceStatus.cmd` を使用します。

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

Windowsの完了通知は、最後のDevSpace操作から既定で45秒間、新しい処理がない場合だけ「作業区切り完了」として1回表示します。個々の `read`、`edit`、`bash` の完了ごとには通知しません。

## デスクトップペット

ペットは状態に合わせて動きます。

- 待機中：ゆっくり上下する
- 作業中：手足を動かす
- 次の処理待ち：ジャンプする
- 失敗：目が×印になる
- 停滞：`Z`を表示する
- DevSpace停止：電源が切れたような表示になる

### テーマ

ペットを右クリックして、次のテーマを選択できます。

- **クラシック（状態色）**：青、緑、黄、赤、紫を状態に合わせて使用
- **ネオン（紫・黄）**：黒い筐体、紫のネオン、黄色の目とランプ

選択したテーマは `%USERPROFILE%\.devspace\devspace-pet-settings.json` に保存されます。

### 並列作業

複数のDevSpaceワークスペースや処理が並行して動いている場合、プロジェクトごとの吹き出しを最大4段表示します。

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
- 右クリック：テーマ変更、吹き出し表示、位置リセット、終了

位置は `%USERPROFILE%\.devspace\devspace-pet-position.json` に保存されます。

## 状態連携と安全性

タスクトレイ監視は、ペット用の状態を次へ書き出します。

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

```powershell
.\DevSpaceStatus.ps1 `
  -RefreshSeconds 3 `
  -CompletionQuietSeconds 45 `
  -StallMinutes 30 `
  -Port 7676
```

```powershell
.\DevSpacePet.ps1 -StateRefreshMilliseconds 750
```

## ファイル

- `DevSpaceStatus.ps1`：状態判定、並列活動の集約、タスクトレイ、通知
- `DevSpacePet.ps1`：テーマ切替、複数吹き出し、アニメーション
- `Start-DevSpaceStatus.cmd`：監視とペットを起動
- `Check-DevSpaceStatus.cmd`：状態を1回確認
- `Install-DevSpaceStatus.ps1`：ショートカットと自動起動を設定
- `Install-DevSpaceStatus.cmd`：インストーラー起動

## License

MIT License
