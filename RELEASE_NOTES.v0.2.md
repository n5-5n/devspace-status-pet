# DevSpace Status Pet v0.2.0-alpha.5

This alpha removes completed waiting bubbles at the same time as the configured completion quiet period.

## Fixed

- `Waiting for next step` bubbles no longer remain for five minutes after work has finished.
- Inactive workspace bubbles now disappear when `CompletionQuietSeconds` expires.
- With the default setting, a waiting bubble remains for up to 45 seconds and then disappears.
- The completion notification and waiting-bubble removal now use the same threshold.
- Bubbles backed by a real active child process remain visible until that process finishes.
- Parallel workspace bubbles are still shown separately while they are active or within the quiet period.

## Included from earlier v0.2 alphas

- Correct UTF-8, UTF-16, and UTF-32 DevSpace log detection.
- Historical workspace-to-project recovery and stable project labels.
- One self-contained `DevSpaceStatusPet.exe` containing the tray monitor, desktop pet, and settings window.
- Larger high-DPI-safe pet and speech-bubble layout.
- Immediate settings preview and persistence.
- Separate Light and Dark speech-bubble themes.
- Classic and Neon robot themes.
- Japanese and English UI.
- Portable use, self-installation, startup registration, and self-uninstallation.

## Install or update

1. Download `DevSpace-Status-Pet-v0.2.0-alpha.5-win-x64.zip`.
2. Extract the ZIP.
3. Run `DevSpaceStatusPet.exe`, then select **Install / update v0.2**, or run:

```text
DevSpaceStatusPet.exe --install
```

Existing v0.2 settings are preserved.

## Important

- This is a prerelease. v0.1.0 remains available as the stable rollback version.
- The alpha installs separately under `%LOCALAPPDATA%\DevSpaceStatusPetV2`.
