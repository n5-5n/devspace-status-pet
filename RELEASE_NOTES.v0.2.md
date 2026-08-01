# DevSpace Status Pet v0.2.0-alpha.2

This alpha fixes the first .NET pet layout and settings issues reported on real high-DPI desktops.

## Fixed

- Larger default pet and speech bubbles.
- High-DPI-safe text layout with fixed line regions and ellipsis instead of overlapping text.
- Immediate live preview for scale, opacity, theme, language, bubble visibility, bubble count, and timing settings.
- Separate bubbles for parallel workspaces that use the same project.
- Longer recent-workspace retention so parallel sessions remain visible while waiting for their next local tool call.

## Included from alpha.1

- One self-contained `DevSpaceStatusPet.exe` containing the tray monitor, desktop pet, and settings window.
- No separate .NET Runtime installation or PowerShell execution-policy change required.
- Existing v0.1 theme, language, bubble, and position settings are reused automatically.
- Native DevSpace port, log, workspace, and child-process inspection.
- Classic and Neon themes, Japanese and English, notifications, and stall detection.
- Portable use, self-installation, startup registration, and self-uninstallation.

## Install or update

1. Download `DevSpace-Status-Pet-v0.2.0-alpha.2-win-x64.zip`.
2. Extract the ZIP.
3. Run `DevSpaceStatusPet.exe`, then select **Install / update v0.2**, or run:

```text
DevSpaceStatusPet.exe --install
```

Existing v0.2 settings are preserved.

## Important

- This is a prerelease. v0.1.0 remains available as the stable rollback version.
- The alpha installs separately under `%LOCALAPPDATA%\DevSpaceStatusPetV2`.
