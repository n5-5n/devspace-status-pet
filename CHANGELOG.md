# Changelog

All notable changes to DevSpace Status Pet are documented here.

## [0.2.0-alpha.5] - 2026-08-01

### Fixed

- Removed completed `Waiting for next step` bubbles when the configured completion quiet period expires.
- Reused the completion-notification delay as the waiting-bubble lifetime so both finish at the same time.
- Kept bubbles for genuinely active child processes while expiring only inactive workspace entries.

## [0.2.0-alpha.4] - 2026-08-01

### Fixed

- Correctly read UTF-16 LE `serve.log` files after they grow beyond the 4 MB tail window.
- Restored project names from historical `open_workspace` entries instead of showing `Unknown` after an app restart.
- Preserved separate bubbles for concurrently active workspaces in large logs.
- Extended parallel-workspace retention from two minutes to five minutes.
- Replaced unresolved project names with a stable workspace label instead of `Unknown`.

## [0.2.0-alpha.3] - 2026-08-01

### Added

- Independent Light and Dark themes for speech bubbles.
- Bubble theme switching from both the pet context menu and settings window.
- Immediate bubble theme preview and persistence without pressing Save.
- Japanese and English labels for the new bubble appearance setting.

## [0.2.0-alpha.2] - 2026-08-01

### Fixed

- Rebuilt the pet layout for high-DPI displays so bubble text no longer overlaps.
- Increased the default pet and bubble size while keeping user-adjustable scaling.
- Applied scale, opacity, theme, language, bubble count, and other settings immediately.
- Preserved separate bubbles for parallel workspaces even when they use the same project.
- Kept recent workspace bubbles visible longer so parallel sessions are easier to follow.

## [0.2.0-alpha.1] - 2026-08-01

### Added

- C# / .NET 8 rewrite in a single Windows process.
- One self-contained `DevSpaceStatusPet.exe` with no runtime installation required.
- Native TCP listener and child-process inspection without PowerShell subprocesses.
- WinForms tray icon, animated pet, and settings window.
- v0.1 settings and position migration.
- GUI controls for scale, opacity, completion delay, stall threshold, notifications, and bubble count.
- Built-in portable mode, self-installation, startup registration, and self-uninstallation.
- Crash logging and isolated install/uninstall smoke tests.

### Alpha notes

- v0.1.0 remains the stable release.
- v0.2 uses a separate install directory and mutex during the alpha period.
- Automatic update checking is not included yet.

## [0.1.0] - 2026-08-01

### Added

- Windows task-tray monitor for DevSpace activity.
- Animated desktop pet with Classic and Neon themes.
- Japanese, English, and automatic OS-language selection.
- Multiple speech bubbles for parallel workspaces and processes.
- Quiet-period completion notifications to avoid per-tool notification spam.
- Portable DevSpace path, port, log, and allowed-root detection.
- Settings window for language, theme, bubbles, startup, and runtime diagnostics.
- One-click installer and uninstaller.
- Reproducible ZIP packaging with SHA-256 checksum.
- GitHub Actions validation and tag-driven release publishing.

### Supported

- Windows 10 and Windows 11.
- Windows PowerShell 5.1.
- `@waishnav/devspace` running on the same computer.
