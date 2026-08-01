# DevSpace Status Pet v0.2.1

v0.2.1 adds safe in-app update checking and refreshes the GitHub previews with the current dark UI and parallel-workspace layout.

## Added

- Checks GitHub Releases for a newer stable version at startup.
- Manual **Check for updates** actions in the tray menu and settings window.
- Optional prerelease update channel in settings.
- Dark update window showing current/latest versions, publication time, and release notes.
- Download progress for the ZIP package.
- SHA-256 verification before extracting or installing an update.
- Safe ZIP extraction that rejects path traversal entries.
- Executable-version validation before replacing the installed copy.
- Backup and automatic rollback if replacement or launch fails.
- One update notification per released version.
- Reproducible preview capture command for maintainers.

## Updated previews

- Parallel Classic and Neon pet previews.
- Dark pet context-menu preview.
- Current dark settings-window preview.
- New safe-updater preview.

## Validation

- Stable and prerelease semantic-version selection tests.
- Tampered-checksum rejection test.
- Unsafe ZIP-entry rejection test.
- Verified-package extraction and executable-version tests.
- Live GitHub test against the published v0.2.0 ZIP and SHA-256 assets.
- Existing log, parallel-workspace, high-DPI, settings, installation, and dark-UI regression tests.

## Install or update

Existing v0.2 users can open the tray menu or settings window and choose **Check for updates**.

For a manual installation:

1. Download `DevSpace-Status-Pet-v0.2.1-win-x64.zip`.
2. Extract the ZIP.
3. Run `DevSpaceStatusPet.exe` and select **Install / update v0.2**, or run:

```text
DevSpaceStatusPet.exe --install
```

Existing settings and the saved pet position are preserved.
