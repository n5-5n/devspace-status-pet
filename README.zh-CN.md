# DevSpace Status Pet

**[日本語](README.md) | [English](README.en.md) | [简体中文](README.zh-CN.md)**

一个面向 Windows 的 DevSpace 状态监视器，通过系统托盘图标和动画桌面宠物显示本地工作状态。

> **稳定版：v0.1.5（简体中文界面、中文文档和设置窗口布局修复）**<br>
> **开发版：v0.1.6-alpha.3（修复进程监视异常风暴和再次出现的内存增长）**<br>
> 旧版 PowerShell v0.1.0 仍保留在 GitHub Releases 中，可用于回退。

## 主要功能

- 快速检测 DevSpace 的实际本地处理
- 显示项目名称、当前操作和已用时间
- 为多个聊天／工作区分别显示气泡
- 自动识别 UTF-8、UTF-16 和 UTF-32 DevSpace 日志
- 从历史 `open_workspace` 记录恢复项目名称
- 在工作阶段完成、失败、停滞或 DevSpace 停止时发送通知
- 在设定时间后自动移除已完成的“等待下一步”气泡
- 经典／霓虹机器人主题
- 浅色／深色气泡主题
- 标准对话气泡／霓虹监视卡片／简洁监视卡片即时切换
- 使用逐像素 Alpha 透明，使实际窗口与 GitHub 预览保持一致
- 深色系统托盘菜单、宠物菜单和设置窗口
- 日本語／English／简体中文／自动选择系统语言
- 即时调整大小、透明度、通知时间、停滞阈值和气泡数量
- 即使选择较大倍率，也会自动适配当前显示器
- 随 Windows 登录自动启动
- 可从右键菜单隐藏到最近的屏幕边缘，并通过悬停或点击可见把手恢复
- 从显示器恢复、显示配置变化和窗口移出屏幕等状态中自动恢复
- 可从系统托盘执行**显示／恢复宠物**
- 带轮换功能的运行诊断日志
- 从 GitHub Releases 检查更新，并通过 SHA-256 验证后手动更新
- 可选择 Stable／Prerelease 更新通道
- 崩溃日志和诊断信息

## 预览

| 标准・经典 | 标准・霓虹 |
|---|---|
| ![Classic parallel workspace preview](docs/preview-classic.png) | ![Neon parallel workspace preview](docs/preview-neon.png) |

| 监视卡片（霓虹） | 监视卡片（简洁） |
|---|---|
| ![Neon monitor card preview](docs/preview-monitor-card-neon.png) | ![Clean monitor card preview](docs/preview-monitor-card-clean.png) |

| 深色设置窗口 | 安全更新窗口 |
|---|---|
| ![Dark settings preview](docs/preview-settings.png) | ![Safe updater preview](docs/preview-updater.png) |

![Dark pet context menu](docs/preview-menu.png)

## 系统要求

- Windows 10 或 Windows 11（x64）
- [`@waishnav/devspace`](https://www.npmjs.com/package/@waishnav/devspace)
- DevSpace 和本工具运行在同一台电脑上

无需另行安装 .NET Runtime，也不需要修改 PowerShell 执行策略。不支持 macOS 和 Linux。

## 安装

1. 从 [GitHub Releases](https://github.com/n5-5n/devspace-status-pet/releases/latest) 下载 `DevSpace-Status-Pet-v0.1.5-win-x64.zip`
2. 解压 ZIP
3. 运行 `DevSpaceStatusPet.exe`
4. 右键菜单中选择**安装／更新**

也可以通过命令安装：

```text
DevSpaceStatusPet.exe --install
```

安装位置：

```text
%LOCALAPPDATA%\DevSpaceStatusPetV2\DevSpaceStatusPet.exe
```

现有 PowerShell 版和 .NET 版设置会自动继承。Prerelease 也使用相同的设置文件。

## 便携运行

无需安装，直接运行解压后的 `DevSpaceStatusPet.exe` 也可以使用。

直接打开设置窗口：

```text
DevSpaceStatusPet.exe --settings
```

## 状态显示

| 颜色 | 状态 |
|---|---|
| 绿色 | 正在执行本地处理 |
| 蓝色 | DevSpace 已启动并处于空闲状态 |
| 黄色 | 上一个操作已结束，正在等待下一步 |
| 橙色 | 上一个操作失败 |
| 紫色 | CPU 和日志长时间没有更新，可能已停滞 |
| 红色 | DevSpace 已停止 |

黄色并不代表整个工作已经立即完成。默认情况下，如果最后一次 DevSpace 操作后 45 秒内没有新处理，应用会发送一次完成通知，并同时移除等待气泡。

## 并行工作

活动按工作区 ID 分离，因此即使多个聊天正在操作同一个项目，也会显示为不同气泡。

```text
Aurora Desktop
运行测试
工作中  03:21

Aurora Desktop
编辑文件
等待下一步  00:08
```

可在设置窗口中将最大显示数量调整为 1～8 个。

## 设置

右键单击宠物或系统托盘图标，然后打开**设置**。更改会立即生效，无需按保存按钮。

- 显示语言：自动／日本語／English／简体中文
- 机器人主题：经典／霓虹
- 气泡主题：浅色／深色
- 气泡样式：标准对话气泡／监视卡片（霓虹）／监视卡片（简洁）
- 宠物大小和透明度
- 气泡显示和最大数量
- 完成通知前的等待秒数
- 停滞判定时间
- 启用／禁用通知
- 随 Windows 自动启动
- 启动时检查更新
- 是否包含 Prerelease 版本

设置文件：

```text
%USERPROFILE%\.devspace\devspace-pet-settings.json
%USERPROFILE%\.devspace\devspace-pet-position.json
```

## 更新

可从系统托盘或设置窗口中的**检查更新**查看 GitHub Releases 最新版本。

更新时按以下顺序验证：

1. 从 GitHub 获取 ZIP 和 `.sha256`
2. 验证 SHA-256 是否一致
3. 拒绝 ZIP 中的危险路径后再解压
4. 验证 EXE 版本是否与 Release 一致
5. 备份当前 EXE 后进行替换
6. 如果新版本启动失败，则恢复旧 EXE

默认只检查 Stable Release。可在设置中选择同时检查 Prerelease。应用不会自动替换版本，只有在查看发行说明并按下**立即更新**后才会更新。

运行诊断日志：

```text
%LOCALAPPDATA%\DevSpaceStatusPet\logs\runtime.log
```

记录启动、退出、显示器／电源恢复、显示自动恢复和绘制失败。超过 1 MB 后会轮换为 `runtime.previous.log`。

崩溃日志：

```text
%LOCALAPPDATA%\DevSpaceStatusPet\logs\crash.log
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

卸载程序不会修改 DevSpace 本体或任何 DevSpace 项目。

## 开发与验证

```powershell
dotnet build .\src\DevSpaceStatusPet\DevSpaceStatusPet.csproj -c Release -warnaserror
dotnet run --project .\tests\DevSpaceStatusPet.Smoke\DevSpaceStatusPet.Smoke.csproj -c Release
.\scripts\Build-DotNetRelease.ps1
.\src\DevSpaceStatusPet\bin\Release\net8.0-windows10.0.17763.0\win-x64\DevSpaceStatusPet.exe --capture-previews docs
```

推送 `v0.x.x` 标签后，GitHub Actions 会在 Windows 上执行构建、冒烟测试和隔离的自安装／自卸载验证，然后将 ZIP 和 SHA-256 校验文件发布到 GitHub Releases。带后缀的版本作为 Prerelease 发布，普通版本作为 Stable Release 发布。

## 旧版 PowerShell

PowerShell v0.1.0 仍保留在 GitHub Releases 中作为回退版本。当前 .NET 版本会自动读取其现有主题、语言、气泡和位置设置。

## License

[MIT License](LICENSE)
