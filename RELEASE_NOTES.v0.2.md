# DevSpace Status Pet v0.2.0-alpha.4

This alpha fixes workspace identification and missing parallel bubbles on real DevSpace logs.

## Fixed

- Correctly detects UTF-16 LE DevSpace logs even after `serve.log` grows beyond the 4 MB tail window.
- Restores workspace-to-project mappings from historical `open_workspace` entries at startup.
- Keeps known project names when later `bash` calls contain no path information.
- Shows separate bubbles for concurrent workspaces instead of collapsing to one local process.
- Keeps recent parallel workspace bubbles for up to five minutes.
- Uses a stable `Workspace xxxxxxxx` label instead of displaying `Unknown` when no project path can be recovered.

## Included from earlier v0.2 alphas

- One self-contained `DevSpaceStatusPet.exe` containing the tray monitor, desktop pet, and settings window.
- Larger high-DPI-safe pet and speech-bubble layout.
- Immediate settings preview and persistence.
- Separate Light and Dark speech-bubble themes.
- Classic and Neon robot themes.
- Japanese and English UI.
- Portable use, self-installation, startup registration, and self-uninstallation.

## Install or update

1. Download `DevSpace-Status-Pet-v0.2.0-alpha.4-win-x64.zip`.
2. Extract the ZIP.
3. Run `DevSpaceStatusPet.exe`, then select **Install / update v0.2**, or run:

```text
DevSpaceStatusPet.exe --install
```

Existing v0.2 settings are preserved.

## Important

- This is a prerelease. v0.1.0 remains available as the stable rollback version.
- The alpha installs separately under `%LOCALAPPDATA%\DevSpaceStatusPetV2`.
