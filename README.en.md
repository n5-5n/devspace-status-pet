# DevSpace Status Pet

**[日本語](README.md) | [English](README.en.md)**

A lightweight Windows monitor that shows DevSpace activity through a system-tray indicator and an animated desktop pet.

- Shows whether DevSpace is actually running a local process
- Displays a safe summary of the project, operation, and elapsed time
- Shows parallel chats and workspaces as multiple speech bubbles
- Notifies you about work-segment completion, failures, stalls, and server shutdowns
- Includes Classic and Neon visual themes
- Supports Japanese, English, and automatic OS-language selection
- Stays off the taskbar and floats near the bottom-right of the desktop

## Compatibility

### Supported

- Windows 10 or Windows 11
- Windows PowerShell 5.1
- `@waishnav/devspace`
- DevSpace and this monitor running on the same Windows PC
- Projects stored on any local drive or in any folder
- Paths containing spaces or non-ASCII characters
- Local paths and UNC paths

The monitor automatically reads the following DevSpace configuration file:

```text
%USERPROFILE%\.devspace\config.json
```

It uses:

- `port`
- `allowedRoots`

By default, the monitor reads `serve.log` from the same `.devspace` directory.

### Limitations

- macOS and Linux are not currently supported.
- Running DevSpace on another PC while showing only the pet locally is not supported.
- If your DevSpace launcher writes logs somewhere else, pass `-LogPath` explicitly.
- A major future change to the DevSpace JSON log format may require an update.
- “Work segment finished” is a heuristic based on a quiet period with no new DevSpace activity.

## Installation and startup

1. Place the repository in any folder.
2. Run `Install-DevSpaceStatus.cmd` once.
3. The monitor and pet will start automatically when you sign in to Windows.

The installer creates:

- Desktop shortcut: `DevSpace Status Pet.lnk`
- Startup shortcut: `DevSpace Status Pet.lnk`

Use `Start-DevSpaceStatus.cmd` for manual startup and `Check-DevSpaceStatus.cmd` for a one-time console status check.

A mutex prevents duplicate tray monitors or pets from running.

## Language

Right-click the pet and open **Language / 言語**.

- **Auto (OS language)**: Japanese on a Japanese Windows UI; English otherwise
- **日本語**
- **English**

The selection is stored here and shared by the pet, tray menu, notifications, and details dialog:

```text
%USERPROFILE%\.devspace\devspace-pet-settings.json
```

## Tray states

| Color | Meaning |
|---|---|
| Green | A local DevSpace process is running |
| Blue | DevSpace is running but idle |
| Yellow | The previous process finished and the session is waiting for the next step |
| Orange | The previous process failed |
| Purple | CPU and log activity have been quiet long enough to suggest a stall |
| Red | The DevSpace server is stopped |

Yellow does **not** mean the entire assistant task is finished.

By default, a Windows completion notification appears only after 45 seconds with no new DevSpace activity. Individual `read`, `edit`, and `bash` completions do not each produce a notification.

## Desktop pet

The pet changes its animation with the current state:

- Idle: slowly floats
- Working: moves its arms and legs
- Waiting for the next step: jumps
- Failed: shows X-shaped eyes
- Possibly stalled: displays `Z`
- DevSpace stopped: appears powered off

### Themes

Right-click the pet and choose:

- **Classic (status colors)**: blue, green, yellow, red, and purple based on state
- **Neon (purple and yellow)**: dark body, purple neon outline, yellow eyes and lights

### Parallel work

When multiple DevSpace workspaces or processes are active, the pet displays up to four project-specific speech bubbles.

```text
VideoShrink
Run-BatchGuiSmoke
Working  03:21

personal-hub
Edit file
Waiting for next step  00:08
```

If more than four activities exist, the final bubble summarizes the remainder.

### Controls

- Left-drag: move the pet
- Left-click: toggle persistent speech bubbles
- Right-click: change theme or language, toggle bubbles, reset position, or exit

## State sharing and safety

The tray monitor writes the pet state here:

```text
%USERPROFILE%\.devspace\devspace-status.json
```

Only safe summaries are written:

- State
- Project name
- Short operation names such as `dotnet test`
- Elapsed time
- Success or failure

Full command lines, credentials, and environment variables are not written to the pet state file.

## Main options

The port and allowed roots are detected from `config.json`, but they can also be overridden.

```powershell
.\DevSpaceStatus.ps1 `
  -RefreshSeconds 3 `
  -CompletionQuietSeconds 45 `
  -StallMinutes 30 `
  -Port 7676 `
  -LogPath "$env:USERPROFILE\.devspace\serve.log"
```

```powershell
.\DevSpacePet.ps1 -StateRefreshMilliseconds 750
```

## Validation

```powershell
.\tests\ParseScripts.ps1
```

The test suite checks:

- Windows PowerShell 5.1 parsing
- Workspaces on another drive
- Project paths containing spaces
- UNC paths
- Japanese and English localization

The same validation runs in GitHub Actions.

## Files

- `DevSpaceLocalization.ps1`: Japanese and English strings plus shared settings loading
- `DevSpaceStatus.ps1`: status detection, parallel activity aggregation, tray UI, and notifications
- `DevSpacePet.ps1`: themes, language selection, multiple bubbles, and animation
- `Start-DevSpaceStatus.cmd`: starts the monitor and pet
- `Check-DevSpaceStatus.cmd`: performs a one-time status check
- `Install-DevSpaceStatus.ps1`: creates shortcuts and startup registration
- `Install-DevSpaceStatus.cmd`: launches the installer

## License

MIT License
