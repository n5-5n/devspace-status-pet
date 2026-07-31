# DevSpace Status Pet v0.2 (.NET版)

**[日本語](README.v0.2.md) | [English](README.v0.2.en.md)**

v0.2は、PowerShell製v0.1の機能をC# / .NET 8の単一EXEへ統合する次世代版です。v0.1.0は安定版として維持され、v0.2は現在アルファ版です。

## 現在実装済み

- タスクトレイ監視、デスクトップペット、設定画面を1プロセスへ統合
- 自己完結型の単一`DevSpaceStatusPet.exe`
- .NET RuntimeやPowerShell実行ポリシーが不要
- DevSpaceのポート、ログ、実行プロセスを自動検出
- 複数ワークスペースを最大8個の吹き出しで表示
- クラシック／ネオンテーマ
- 日本語／English／OS言語自動
- サイズ、透明度、通知待機時間、停滞判定、吹き出し数をGUIで変更
- v0.1の設定JSONを自動移行
- 完了通知を処理単位ではなく、静止期間後に1回だけ表示
- クラッシュログを`%LOCALAPPDATA%\DevSpaceStatusPet\logs\crash.log`へ保存
- EXE自身によるインストール、Windows自動起動登録、アンインストール

## ポータブル実行

`DevSpaceStatusPet.exe`をそのまま実行します。

設定画面を直接開く場合：

```text
DevSpaceStatusPet.exe --settings
```

## インストール

```text
DevSpaceStatusPet.exe --install
```

次へコピーされます。

```text
%LOCALAPPDATA%\DevSpaceStatusPetV2\DevSpaceStatusPet.exe
```

デスクトップショートカットとWindows自動起動も登録されます。

## アンインストール

設定を保持：

```text
DevSpaceStatusPet.exe --uninstall
```

設定も削除：

```text
DevSpaceStatusPet.exe --uninstall --remove-settings
```

## 開発・検証

```powershell
dotnet build .\src\DevSpaceStatusPet\DevSpaceStatusPet.csproj -c Release -warnaserror
dotnet run --project .\tests\DevSpaceStatusPet.Smoke\DevSpaceStatusPet.Smoke.csproj -c Release
dotnet publish .\src\DevSpaceStatusPet\DevSpaceStatusPet.csproj -c Release -r win-x64 --self-contained true
```

## v0.1との関係

v0.2は既存の次の設定をそのまま読みます。

```text
%USERPROFILE%\.devspace\devspace-pet-settings.json
%USERPROFILE%\.devspace\devspace-pet-position.json
```

アルファ期間中はv0.1を自動削除せず、別のインストール先とMutexを使用します。
