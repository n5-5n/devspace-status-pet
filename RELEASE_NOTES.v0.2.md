# DevSpace Status Pet v0.2.0-alpha.3

This alpha adds independent Light and Dark appearance themes for speech bubbles.

## Added

- Light speech bubbles with a bright background and dark text.
- Dark speech bubbles with a charcoal background and bright text.
- Independent combinations with either Classic or Neon robot themes.
- Bubble theme switching from the pet right-click menu.
- Bubble theme switching from the settings window.
- Immediate preview and persistence without pressing Save.
- Japanese and English labels for bubble appearance.

## Included from alpha.2

- Larger high-DPI-safe pet and speech-bubble layout.
- Separate bubbles for parallel workspaces, including multiple chats using the same project.
- Immediate live preview for size, opacity, robot theme, language, bubble visibility, bubble count, and timing settings.
- One self-contained `DevSpaceStatusPet.exe` containing the tray monitor, desktop pet, and settings window.
- No separate .NET Runtime installation or PowerShell execution-policy change required.
- Existing v0.1 and v0.2 settings are preserved automatically.

## Install or update

1. Download `DevSpace-Status-Pet-v0.2.0-alpha.3-win-x64.zip`.
2. Extract the ZIP.
3. Run `DevSpaceStatusPet.exe`, then select **Install / update v0.2**, or run:

```text
DevSpaceStatusPet.exe --install
```

Existing settings are preserved. Older settings without a bubble-theme value default to Light.

## Important

- This is a prerelease. v0.1.0 remains available as the stable rollback version.
- The alpha installs separately under `%LOCALAPPDATA%\DevSpaceStatusPetV2`.
