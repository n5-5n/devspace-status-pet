# DevSpace Status Pet v0.2.0

The first stable C# / .NET 8 release of DevSpace Status Pet.

## Highlights

- One self-contained `DevSpaceStatusPet.exe` containing the tray monitor, desktop pet, and settings window.
- Multiple speech bubbles for parallel DevSpace workspaces, including workspaces that use the same project.
- Reliable project-name recovery from UTF-8, UTF-16, and UTF-32 DevSpace logs.
- Completed waiting bubbles expire with the configured completion quiet period.
- Classic and Neon robot themes.
- Independent Light and Dark speech-bubble themes.
- Dark tray menu, pet menu, submenus, and settings window.
- Japanese, English, and automatic OS-language selection.
- Immediate settings preview for size, opacity, themes, notifications, timing, and bubble count.
- Quiet-period completion notifications instead of one notification per tool call.
- Stall detection, failure notifications, DevSpace stopped state, and crash logging.
- Portable use, self-installation, Windows startup registration, and self-uninstallation.
- Automatic reuse of existing v0.1 and v0.2 settings.

## Install or update

1. Download `DevSpace-Status-Pet-v0.2.0-win-x64.zip`.
2. Extract the ZIP.
3. Run `DevSpaceStatusPet.exe` and select **Install / update v0.2**, or run:

```text
DevSpaceStatusPet.exe --install
```

Existing alpha and v0.1 settings are preserved. The application remains installed under:

```text
%LOCALAPPDATA%\DevSpaceStatusPetV2
```

## Requirements

- Windows 10 or Windows 11, x64
- `@waishnav/devspace` running on the same computer

No separate .NET Runtime installation or PowerShell execution-policy change is required.

## Legacy release

v0.1.0 remains available as the legacy PowerShell release and rollback option.
