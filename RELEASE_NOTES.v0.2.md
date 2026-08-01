# DevSpace Status Pet v0.2.0-alpha.6

This alpha gives the application controls and menus a consistent dark appearance.

## Added

- Dark styling for the Windows tray context menu.
- Dark styling for the pet context menu and all nested theme/language submenus.
- Dark menu selection, pressed, checked, disabled, border, and separator states.
- Dark settings window background, labels, panels, combo boxes, numeric inputs, check boxes, and buttons.
- Dark title-bar support on compatible Windows 10 and Windows 11 builds.
- Shared UI palette and renderer so the tray menu, pet menu, and settings window use the same colors.
- Automated smoke tests for dark-menu colors, nested menus, settings controls, and text contrast.

## Included from earlier v0.2 alphas

- Completed waiting bubbles expire with the configured completion quiet period.
- Correct UTF-8, UTF-16, and UTF-32 DevSpace log detection.
- Historical workspace-to-project recovery and separate parallel workspace bubbles.
- One self-contained `DevSpaceStatusPet.exe` containing the tray monitor, desktop pet, and settings window.
- Larger high-DPI-safe pet and speech-bubble layout.
- Immediate settings preview and persistence.
- Separate Light and Dark speech-bubble themes.
- Classic and Neon robot themes.
- Japanese and English UI.
- Portable use, self-installation, startup registration, and self-uninstallation.

## Install or update

1. Download `DevSpace-Status-Pet-v0.2.0-alpha.6-win-x64.zip`.
2. Extract the ZIP.
3. Run `DevSpaceStatusPet.exe`, then select **Install / update v0.2**, or run:

```text
DevSpaceStatusPet.exe --install
```

Existing v0.2 settings are preserved.

## Important

- This is a prerelease. v0.1.0 remains available as the stable rollback version.
- The alpha installs separately under `%LOCALAPPDATA%\DevSpaceStatusPetV2`.
