# DevSpace Status Pet v0.1.5-alpha.2

**[日本語](README.dotnet.md) | [English](README.dotnet.en.md) | [简体中文](README.dotnet.zh-CN.md)**

A Windows monitor that shows DevSpace activity through a tray icon and an animated desktop pet. v0.1.5-alpha.2 is a prerelease that adds a complete Simplified Chinese UI, automatic Chinese OS detection, Chinese documentation, and a fix for clipped settings actions.

## Requirements

- Windows 10 or Windows 11, x64
- `@waishnav/devspace` running on the same computer

No separate .NET Runtime installation is required.

## Install or update

1. Extract the ZIP
2. Run `DevSpaceStatusPet.exe`
3. Select **Install / update** from the context menu

Or run:

```text
DevSpaceStatusPet.exe --install
```

Install location:

```text
%LOCALAPPDATA%\DevSpaceStatusPetV2\DevSpaceStatusPet.exe
```

Existing PowerShell and .NET settings are reused automatically.

## Main features

- Project name, operation, and elapsed-time display
- Separate bubbles for parallel workspaces
- UTF-8, UTF-16, and UTF-32 DevSpace log support
- Completion, failure, stall, and DevSpace stopped notifications
- Automatic removal of completed waiting bubbles
- Classic and Neon robot themes
- Light and Dark speech-bubble themes
- Dark context menus and settings window
- Japanese, English, Simplified Chinese, and automatic OS-language selection
- Immediate size, opacity, timing, and bubble-count changes
- Automatically fits oversized layouts inside the current monitor
- GitHub Release discovery and manual updates with SHA-256 verification
- Stable or optional prerelease update channels
- Display self-recovery after monitor resume, display changes, or off-screen movement
- Tray command to show and recover the pet
- Windows startup, self-uninstallation, runtime diagnostics, and crash logging

## In-app updates

Choose **Check for updates** from the tray menu or Settings. The updater verifies the ZIP, SHA-256, and executable version, and restores the previous executable if replacement fails. Stable releases are checked by default; prereleases are optional.

## Portable use

You can run the extracted executable without installing it.

```text
DevSpaceStatusPet.exe --settings
```

## Uninstall

Keep settings:

```text
DevSpaceStatusPet.exe --uninstall
```

Remove settings too:

```text
DevSpaceStatusPet.exe --uninstall --remove-settings
```

The uninstaller does not modify DevSpace itself or any project.

## Settings locations

```text
%USERPROFILE%\.devspace\devspace-pet-settings.json
%USERPROFILE%\.devspace\devspace-pet-position.json
```

Runtime diagnostics log:

```text
%LOCALAPPDATA%\DevSpaceStatusPet\logs\runtime.log
```

Crash log:

```text
%LOCALAPPDATA%\DevSpaceStatusPet\logs\crash.log
```

## Legacy PowerShell release

The PowerShell v0.1.0 release remains available on GitHub Releases as a rollback option.
