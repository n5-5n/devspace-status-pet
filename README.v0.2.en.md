# DevSpace Status Pet v0.2 (.NET edition)

**[日本語](README.v0.2.md) | [English](README.v0.2.en.md)**

v0.2 is the next-generation C# / .NET 8 edition that consolidates the PowerShell v0.1 features into one executable. v0.1.0 remains the stable release while v0.2 is currently an alpha build.

## Implemented

- Tray monitor, desktop pet, and settings window in one process
- One self-contained `DevSpaceStatusPet.exe`
- No .NET Runtime installation or PowerShell execution-policy changes required
- Automatic DevSpace port, log, and child-process detection
- Up to eight parallel workspace bubbles
- Classic and Neon robot themes
- Independent Light and Dark speech-bubble themes
- Japanese, English, and automatic OS-language selection
- Instant GUI controls for size, opacity, themes, completion delay, stall threshold, and bubble count
- Automatic migration of the v0.1 settings JSON
- One completion notification after a quiet period instead of notifications for every tool call
- Crash log at `%LOCALAPPDATA%\DevSpaceStatusPet\logs\crash.log`
- Self-installation, Windows startup registration, and self-uninstallation

## Portable use

Run `DevSpaceStatusPet.exe` directly.

Open the settings window immediately:

```text
DevSpaceStatusPet.exe --settings
```

## Install

```text
DevSpaceStatusPet.exe --install
```

The executable is copied to:

```text
%LOCALAPPDATA%\DevSpaceStatusPetV2\DevSpaceStatusPet.exe
```

A desktop shortcut and Windows startup entry are also created.

## Uninstall

Keep settings:

```text
DevSpaceStatusPet.exe --uninstall
```

Remove settings too:

```text
DevSpaceStatusPet.exe --uninstall --remove-settings
```

## Development and validation

```powershell
dotnet build .\src\DevSpaceStatusPet\DevSpaceStatusPet.csproj -c Release -warnaserror
dotnet run --project .\tests\DevSpaceStatusPet.Smoke\DevSpaceStatusPet.Smoke.csproj -c Release
dotnet publish .\src\DevSpaceStatusPet\DevSpaceStatusPet.csproj -c Release -r win-x64 --self-contained true
```

## Relationship to v0.1

v0.2 reads the existing settings without conversion steps:

```text
%USERPROFILE%\.devspace\devspace-pet-settings.json
%USERPROFILE%\.devspace\devspace-pet-position.json
```

During the alpha period, v0.2 does not remove v0.1 automatically and uses a separate install directory and mutex.
