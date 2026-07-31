# DevSpace Status Pet v0.2.0-alpha.1

This is the first public alpha of the C# / .NET 8 rewrite.

## Highlights

- Tray monitor, desktop pet, and settings window are now one Windows process.
- Distributed as one self-contained `DevSpaceStatusPet.exe`.
- No separate .NET Runtime installation or PowerShell execution-policy change is required.
- Existing v0.1 theme, language, bubble, and position settings are reused automatically.
- DevSpace port, log, workspaces, and child processes are detected natively.
- Classic and Neon themes, Japanese and English, parallel bubbles, quiet-period notifications, and stall detection are included.
- Size, opacity, bubble count, notification delay, and startup behavior are configurable in the GUI.
- The executable supports portable use, self-installation, and self-uninstallation.

## Install

1. Download `DevSpace-Status-Pet-v0.2.0-alpha.1-win-x64.zip`.
2. Extract the ZIP.
3. Run `DevSpaceStatusPet.exe` for portable use, or run:

```text
DevSpaceStatusPet.exe --install
```

## Important

- This is a prerelease. v0.1.0 remains the recommended stable version.
- The alpha installs separately under `%LOCALAPPDATA%\DevSpaceStatusPetV2` and does not remove v0.1 automatically.
- Automatic update checking is planned for a later alpha.
