# DevSpace Status Pet v0.1.6-alpha.3

**[日本語](README.dotnet.md) | [English](README.dotnet.en.md) | [简体中文](README.dotnet.zh-CN.md)**

一个面向 Windows 的 DevSpace 状态监视器，通过系统托盘图标和桌面宠物显示工作状态。v0.1.6-alpha.3 是修复进程监视异常风暴的 Hotfix Prerelease；该问题会让长时间运行后的内存再次增长到数 GB。隐藏到屏幕边缘的功能仍然可用。

## 系统要求

- Windows 10 或 Windows 11（x64）
- `@waishnav/devspace` 运行在同一台电脑上

无需另行安装 .NET Runtime。

## 安装／更新

1. 解压 ZIP
2. 运行 `DevSpaceStatusPet.exe`
3. 在右键菜单中选择**安装／更新**

也可以运行：

```text
DevSpaceStatusPet.exe --install
```

安装位置：

```text
%LOCALAPPDATA%\DevSpaceStatusPetV2\DevSpaceStatusPet.exe
```

现有 PowerShell 版和 .NET 版设置会自动继承。

## 主要功能

- 显示项目名称、当前操作和已用时间
- 为多个工作区分别显示气泡
- 支持 UTF-8、UTF-16 和 UTF-32 DevSpace 日志
- 工作完成、失败、停滞和 DevSpace 停止通知
- 自动移除已完成的等待气泡
- 经典／霓虹机器人主题
- 浅色／深色气泡主题
- 深色右键菜单和设置窗口
- 日本語／English／简体中文／自动选择系统语言
- 即时调整大小、透明度、通知时间和气泡数量
- 可从右键菜单隐藏到最近的屏幕边缘，并通过悬停或点击可见把手恢复
- 自动将较大布局适配到当前显示器
- 从 GitHub Releases 检查更新，并通过 SHA-256 验证后手动更新
- Stable／Prerelease 更新通道
- 从显示器恢复、显示配置变化和窗口移出屏幕等状态中自动恢复
- 系统托盘中的“显示／恢复宠物”命令
- Windows 自动启动、自卸载、运行诊断和崩溃日志

## 应用内更新

从系统托盘或设置窗口选择**检查更新**。更新程序会验证 ZIP、SHA-256 和 EXE 版本；如果替换失败，会恢复旧 EXE。默认只检查 Stable Release，也可以在设置中选择 Prerelease。

## 便携运行

无需安装，直接运行解压后的 EXE 也可以使用。

```text
DevSpaceStatusPet.exe --settings
```

## 卸载

保留设置：

```text
DevSpaceStatusPet.exe --uninstall
```

同时删除设置：

```text
DevSpaceStatusPet.exe --uninstall --remove-settings
```

不会修改 DevSpace 本体或任何项目。

## 设置保存位置

```text
%USERPROFILE%\.devspace\devspace-pet-settings.json
%USERPROFILE%\.devspace\devspace-pet-position.json
```

运行诊断日志：

```text
%LOCALAPPDATA%\DevSpaceStatusPet\logs\runtime.log
```

崩溃日志：

```text
%LOCALAPPDATA%\DevSpaceStatusPet\logs\crash.log
```

## 旧版 PowerShell

PowerShell v0.1.0 仍保留在 GitHub Releases 中作为回退版本。
