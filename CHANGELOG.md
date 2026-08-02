# Changelog

All notable changes to DevSpace Status Pet are documented here.

## [0.1.4-alpha.2] - 2026-08-03

### Fixed

- Detects and recovers native TopMost loss even when the WinForms `TopMost` property remains enabled.
- Reapplies native TopMost immediately when the pet window is first shown.
- Automatically reduces the effective render scale when the requested layout would exceed the current monitor working area.
- Refits the pet after monitor moves and visibility recovery while preserving the user's requested scale setting.
- Records both managed and native TopMost state in visibility-recovery diagnostics.

### Tested

- Added live fault injection for off-screen movement in both directions, minimize, hide, native TopMost loss, and duplicate launches.
- Added a 36-combination rendering matrix across robot themes, bubble themes, bubble designs, and scale limits.
- Added a 1024x768 regression test for a requested 250% scale.
- Added malformed settings, runtime-log rotation, GDI/USER resource, package privacy, Defender, and vulnerable-package checks.
- Preserves and restores an already-running installed pet around isolated installer smoke tests.

## [0.1.4-alpha.1] - 2026-08-01

### Added

- Added a rotating `runtime.log` for startup, shutdown, power/display events, rendering failures, and self-recovery.
- Added a tray command to show and recover the pet window.
- Added display-settings and power-resume recovery handlers with a delayed second recovery pass.
- Added a five-second visibility watchdog and an animation-level render watchdog.
- Replaced the remaining legacy SVG preview labels with fictional sample projects.

### Fixed

- Recovers a layered pet window that becomes hidden, loses TopMost, is minimized, or moves outside all monitor working areas.
- Retries rendering after `UpdateLayeredWindow` failures by recreating the window handle.

### Tested

- Added off-screen recovery, secondary-monitor preservation, and recovery-menu localization regression tests.

## [0.1.3] - 2026-08-01

### Stable release

- Promoted the tested Monitor Card, Clean Card, and per-pixel alpha-rendering work from the v0.1.3 alpha series.
- Replaced all public preview project names and operation details with fictional sample data.
- Regenerated the Classic, Neon, Monitor Card, context-menu, settings, and updater previews from the sanitized sample snapshot.
- Added a regression test that locks future public previews to an approved fictional sample set.
- Preserved existing settings, startup registration, and the installed application path.

## [0.1.3-alpha.3] - 2026-08-01

### Fixed

- Replaced magenta color-key transparency with per-pixel alpha rendering.
- Removed purple and magenta edge contamination that appeared only in the live desktop window.
- Unified the live pet and GitHub preview rendering paths.
- Switched transparent text rendering from ClearType to alpha-safe antialiasing.

### Changed

- Normalized historical release numbering to patch-by-patch updates: `v0.1.1`, `v0.1.2`, and `v0.1.3` families.
- Added a documented policy that increments the patch number for each independent update while keeping alpha iterations on the same base version.

### Tested

- Added transparent-layer pixel checks for visible content, antialiased edges, and zero magenta fringe pixels.

## [0.1.3-alpha.2] - 2026-08-01

### Added

- Added a Clean Monitor Card design with a single neutral border, no inner highlight, and no colored outer glow.
- Added separate Neon and Clean Monitor Card choices to the settings window and pet context menu.
- Added dedicated generated previews for both Monitor Card variants.

### Changed

- Preserved the alpha.1 Monitor Card appearance as Monitor Card (Neon).
- Automatically migrates the legacy `MonitorCard` setting to `MonitorCardNeon`.
- Uses the Clean Monitor Card in settings and context-menu preview images.

### Tested

- Added migration, clean-border contrast, layout parity, rendering, and live three-style switching tests.

## [0.1.3-alpha.1] - 2026-08-01

### Added

- Added the optional Monitor Card bubble design inspired by compact AI usage-monitor widgets.
- Emphasized elapsed time with a large right-aligned value, status badge, accent rail, and animated activity meter.
- Added immediate bubble-design switching in both the settings window and pet context menu.
- Added a generated GitHub preview dedicated to the Monitor Card design.

### Changed

- Kept the existing speech bubble as the migration-safe default for current users.
- Made installer, uninstaller, and desktop-shortcut labels version-neutral for future versions.
- Extended the release workflow to accept future v0.x tags.

## [0.1.2] - 2026-08-01

### Added

- Stable and optional prerelease update discovery through GitHub Releases.
- Startup and manual update checks from the tray menu and settings window.
- Dark release-notes and update-progress window.
- ZIP download progress, SHA-256 verification, safe extraction, and executable-version validation.
- Backup, rollback, and restored-version relaunch when replacement fails.
- One update notification per released version.
- Reproducible in-app preview capture for the pet, menus, settings, and updater.

### Changed

- Refreshed the Japanese and English GitHub previews to match the current .NET UI.
- Added update-channel and startup-check settings while preserving existing configuration files.

### Tested

- Added stable/prerelease selection, checksum tampering, ZIP traversal, verified extraction, and live GitHub release tests.

## [0.1.1] - 2026-08-01

### Stable release

- Promoted the tested .NET 8 single-executable implementation to the default stable release.
- Consolidated the tray monitor, desktop pet, settings window, notifications, and diagnostics into one process.
- Included all fixes from alpha.1 through alpha.6: high-DPI layout, parallel workspace bubbles, log encoding detection, workspace identity recovery, waiting-bubble expiration, live settings, independent robot/bubble themes, and the dark application UI.
- Updated the GitHub release workflow to publish stable tags without the prerelease flag while retaining prerelease support for suffixed versions.
- Updated Japanese and English documentation so the .NET build is the primary release and PowerShell v0.1.0 is the legacy fallback.

## [0.1.1-alpha.6] - 2026-08-01

### Added

- Unified dark styling for the tray context menu and pet context menu, including nested menus, check marks, selection states, and separators.
- Dark settings window styling for labels, panels, combo boxes, numeric inputs, check boxes, and buttons.
- Immersive dark title-bar support on compatible Windows 10 and Windows 11 builds.
- Automated contrast and control-theme smoke tests for the new application UI palette.

## [0.1.1-alpha.5] - 2026-08-01

### Fixed

- Removed completed `Waiting for next step` bubbles when the configured completion quiet period expires.
- Reused the completion-notification delay as the waiting-bubble lifetime so both finish at the same time.
- Kept bubbles for genuinely active child processes while expiring only inactive workspace entries.

## [0.1.1-alpha.4] - 2026-08-01

### Fixed

- Correctly read UTF-16 LE `serve.log` files after they grow beyond the 4 MB tail window.
- Restored project names from historical `open_workspace` entries instead of showing `Unknown` after an app restart.
- Preserved separate bubbles for concurrently active workspaces in large logs.
- Extended parallel-workspace retention from two minutes to five minutes.
- Replaced unresolved project names with a stable workspace label instead of `Unknown`.

## [0.1.1-alpha.3] - 2026-08-01

### Added

- Independent Light and Dark themes for speech bubbles.
- Bubble theme switching from both the pet context menu and settings window.
- Immediate bubble theme preview and persistence without pressing Save.
- Japanese and English labels for the new bubble appearance setting.

## [0.1.1-alpha.2] - 2026-08-01

### Fixed

- Rebuilt the pet layout for high-DPI displays so bubble text no longer overlaps.
- Increased the default pet and bubble size while keeping user-adjustable scaling.
- Applied scale, opacity, theme, language, bubble count, and other settings immediately.
- Preserved separate bubbles for parallel workspaces even when they use the same project.
- Kept recent workspace bubbles visible longer so parallel sessions are easier to follow.

## [0.1.1-alpha.1] - 2026-08-01

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
- v0.1.1 uses a separate .NET install directory and mutex during the alpha period.
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
