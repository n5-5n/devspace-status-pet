# DevSpace Status Pet v0.1.6-alpha.2

`v0.1.6-alpha.1`で確認された、長時間稼働時にメモリ使用量が増え続ける重大な不具合を修正したHotfix Prereleaseです。

## 修正内容

- 80msごとの描画で透過BitmapとHBITMAPを毎回作成していた処理を廃止
- 透過32-bit DIB、メモリDC、描画用Graphicsをウィンドウサイズが変わるまで再利用
- ウィンドウサイズ変更時だけ描画サーフェスを安全に再作成
- 終了時に再利用サーフェスを確実に解放
- 見た目、透明度、アンチエイリアス、アニメーション、端収納機能は変更なし

## 発生していた症状

実機では`v0.1.6-alpha.1`が約40分の稼働で以下まで増加しました。

- Working Set：約6.0GB
- Private Bytes：約8.36GB
- Peak Working Set：約6.58GB

GDI／USERオブジェクト数は横ばいだったため、GDIハンドル数の漏れではなく、毎フレーム作成していたネイティブ描画メモリの蓄積が原因でした。

## 修正後の実測

4分間の連続稼働：

- Working Set：156.2MB → 156.6MB
- Private Bytes：67.4MB → 66.5MB
- GDI objects：52 → 52
- USER objects：41 → 40

追加した60秒の自動回帰試験：

- Working Set平均：155.8MB → 161.3MB
- Private Bytes平均：69.7MB → 73.2MB
- GDI objects：52～53
- USER objects：42～44
- 応答停止なし

## 再発防止

- 500回連続描画で同じDIBが再利用されることを検証
- 透明背景とピクセル単位アルファを直接検証
- GDI／USERオブジェクトが増えないことを検証
- 60秒間のPrivate Bytes／Working Set傾向をCIで監視
- 既存の108通り描画、言語、低解像度、端収納、インストール、表示復旧試験を再実行

`v0.1.6-alpha.1`は長時間稼働させず、この版へ更新してください。既存の設定、位置、自動起動登録、端収納機能はそのまま引き継がれます。
