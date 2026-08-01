using System.Diagnostics;
using DevSpaceStatusPet.Models;
using DevSpaceStatusPet.Services;

namespace DevSpaceStatusPet.UI;

public sealed class TrayApplicationContext : ApplicationContext
{
    private readonly SettingsStore _settingsStore = new();
    private readonly PositionStore _positionStore = new();
    private readonly Localizer _localizer;
    private readonly DevSpaceMonitor _monitor;
    private readonly NotifyIcon _notifyIcon;
    private readonly Dictionary<ActivityState, Icon> _icons;
    private readonly ContextMenuStrip _trayMenu = new();
    private readonly ToolStripMenuItem _statusItem = new() { Enabled = false };
    private readonly ToolStripMenuItem _projectItem = new() { Enabled = false };
    private readonly ToolStripMenuItem _operationItem = new() { Enabled = false };
    private readonly ToolStripMenuItem _elapsedItem = new() { Enabled = false };
    private readonly ToolStripMenuItem _refreshItem = new();
    private readonly ToolStripMenuItem _settingsItem = new();
    private readonly ToolStripMenuItem _openLogItem = new();
    private readonly ToolStripMenuItem _openFolderItem = new();
    private readonly ToolStripMenuItem _installItem = new();
    private readonly ToolStripMenuItem _exitItem = new();
    private readonly PetForm _petForm;
    private readonly SettingsForm _settingsForm;
    private readonly System.Windows.Forms.Timer _timer;

    private DevSpaceSnapshot _snapshot;
    private bool _refreshing;
    private bool _workSessionActive;
    private DateTimeOffset _workSessionStartedAt;
    private DateTimeOffset _lastSessionActivityAt;
    private DateTimeOffset? _lastSeenToolAt;
    private string _lastSessionProjectName = "DevSpace";
    private bool _stallNotificationShown;
    private ActivityState _previousState = ActivityState.Idle;

    public TrayApplicationContext(bool showSettingsOnStart = false)
    {
        _localizer = new Localizer(() => _settingsStore.Current);
        var configurationLoader = new DevSpaceConfigurationLoader();
        _monitor = new DevSpaceMonitor(
            configurationLoader,
            new NativeProcessInspector(),
            new DevSpaceLogReader(),
            () => _settingsStore.Current);
        var config = configurationLoader.Load();
        _snapshot = DevSpaceSnapshot.Initial(config.ConfigPath, config.LogPath, config.Port);

        _icons = Enum.GetValues<ActivityState>()
            .ToDictionary(state => state, IconFactory.Create);

        _notifyIcon = new NotifyIcon
        {
            Visible = true,
            Icon = _icons[ActivityState.Idle],
            Text = "DevSpace Status Pet",
            ContextMenuStrip = _trayMenu
        };
        _trayMenu.Items.AddRange([
            _statusItem,
            _projectItem,
            _operationItem,
            _elapsedItem,
            new ToolStripSeparator(),
            _refreshItem,
            _settingsItem,
            _openLogItem,
            _openFolderItem,
            new ToolStripSeparator(),
            _installItem,
            _exitItem
        ]);
        DarkUiTheme.ApplyMenu(_trayMenu);

        _petForm = new PetForm(_settingsStore, _positionStore, _localizer);
        _petForm.SettingsRequested += (_, _) => ShowSettings();
        _petForm.ExitRequested += (_, _) => ExitApplication();
        _settingsForm = new SettingsForm(_settingsStore, _localizer, _snapshot);

        _refreshItem.Click += async (_, _) => await RefreshAsync();
        _settingsItem.Click += (_, _) => ShowSettings();
        _openLogItem.Click += (_, _) => OpenLog();
        _openFolderItem.Click += (_, _) => OpenFolder();
        _installItem.Click += (_, _) => InstallOrUninstall();
        _exitItem.Click += (_, _) => ExitApplication();
        _notifyIcon.MouseClick += (_, eventArgs) =>
        {
            if (eventArgs.Button == MouseButtons.Left)
            {
                ShowCurrentStatusBalloon();
            }
        };

        _settingsStore.Changed += (_, _) =>
        {
            UpdateStaticText();
            _petForm.ApplySnapshot(_snapshot);
            _settingsForm.UpdateSnapshot(_snapshot);
        };

        _timer = new System.Windows.Forms.Timer { Interval = 1000 };
        _timer.Tick += async (_, _) => await RefreshAsync();

        UpdateStaticText();
        _petForm.Show();
        if (showSettingsOnStart)
        {
            ShowSettings();
        }
        _timer.Start();
        _ = RefreshAsync();
    }

    private async Task RefreshAsync()
    {
        if (_refreshing)
        {
            return;
        }

        _refreshing = true;
        try
        {
            var snapshot = await Task.Run(_monitor.Capture);
            ApplySnapshot(snapshot);
        }
        catch (Exception exception)
        {
            CrashLogger.Write(exception, "RefreshAsync");
        }
        finally
        {
            _refreshing = false;
        }
    }

    private void ApplySnapshot(DevSpaceSnapshot snapshot)
    {
        _snapshot = snapshot;
        _notifyIcon.Icon = _icons[snapshot.State];
        _petForm.ApplySnapshot(snapshot);
        _settingsForm.UpdateSnapshot(snapshot);
        UpdateTrayText(snapshot);
        UpdateNotifications(snapshot);
    }

    private void UpdateStaticText()
    {
        _refreshItem.Text = _localizer["Refresh"];
        _settingsItem.Text = _localizer["Settings"];
        _openLogItem.Text = _localizer["OpenLog"];
        _openFolderItem.Text = _localizer["OpenFolder"];
        _installItem.Text = SelfInstaller.IsRunningFromInstallDirectory
            ? _localizer["UninstallV2"]
            : _localizer["InstallUpdate"];
        _exitItem.Text = _localizer["Exit"];
        UpdateTrayText(_snapshot);
    }

    private void UpdateTrayText(DevSpaceSnapshot snapshot)
    {
        var primary = snapshot.Activities.FirstOrDefault();
        var project = primary?.ProjectName ?? "DevSpace";
        var operation = primary is null
            ? _localizer.Operation(snapshot.State == ActivityState.Stopped ? OperationKind.Stopped : OperationKind.Idle, null)
            : _localizer.Operation(primary.Operation, primary.Detail);
        var elapsed = primary?.Elapsed ?? TimeSpan.Zero;

        _statusItem.Text = _localizer.Get("Status", _localizer.State(snapshot.State));
        _projectItem.Text = _localizer.Get("Project", project);
        _operationItem.Text = _localizer.Get("Operation", operation);
        _elapsedItem.Text = _localizer.Get("Elapsed", FormatDuration(elapsed));

        var tooltip = $"DevSpace: {_localizer.State(snapshot.State)}";
        if (snapshot.State is ActivityState.Working or ActivityState.Stalled)
        {
            tooltip += $" / {project} / {FormatDuration(elapsed)}";
        }
        _notifyIcon.Text = tooltip.Length > 63 ? tooltip[..63] : tooltip;
    }

    private void UpdateNotifications(DevSpaceSnapshot snapshot)
    {
        var settings = _settingsStore.Current;
        if (!settings.NotificationsEnabled)
        {
            _previousState = snapshot.State;
            return;
        }

        var now = DateTimeOffset.Now;
        var newTool = snapshot.LastToolAt.HasValue &&
                      (!_lastSeenToolAt.HasValue || snapshot.LastToolAt.Value > _lastSeenToolAt.Value);
        if (newTool)
        {
            _lastSeenToolAt = snapshot.LastToolAt;
        }

        if (snapshot.State is ActivityState.Working or ActivityState.Stalled || newTool)
        {
            var primary = snapshot.Activities.FirstOrDefault();
            if (primary is not null)
            {
                _lastSessionProjectName = primary.ProjectName;
            }

            if (!_workSessionActive)
            {
                _workSessionActive = true;
                _workSessionStartedAt = now;
            }
            _lastSessionActivityAt = now;
        }
        else if (_workSessionActive && now - _lastSessionActivityAt >= TimeSpan.FromSeconds(settings.CompletionQuietSeconds))
        {
            var duration = _lastSessionActivityAt - _workSessionStartedAt;
            if (duration >= TimeSpan.FromSeconds(10))
            {
                var title = snapshot.LastToolSucceeded ? _localizer["WorkDoneTitle"] : _localizer["WorkFailedTitle"];
                var text = $"{_lastSessionProjectName}{Environment.NewLine}" +
                           $"{_localizer.Get("WorkTime", FormatDuration(duration))}";
                ShowBalloon(title, text, snapshot.LastToolSucceeded ? ToolTipIcon.Info : ToolTipIcon.Error);
            }
            _workSessionActive = false;
        }

        if (snapshot.State == ActivityState.Stalled && !_stallNotificationShown)
        {
            _stallNotificationShown = true;
            var primary = snapshot.Activities.FirstOrDefault();
            ShowBalloon(
                _localizer["StallTitle"],
                $"{primary?.ProjectName ?? "DevSpace"}{Environment.NewLine}{_localizer.Operation(primary?.Operation ?? OperationKind.LocalProcess, primary?.Detail)}",
                ToolTipIcon.Warning);
        }
        else if (snapshot.State != ActivityState.Stalled)
        {
            _stallNotificationShown = false;
        }

        if (_previousState != ActivityState.Stopped && snapshot.State == ActivityState.Stopped)
        {
            ShowBalloon(_localizer["StopTitle"], _localizer["StopText"], ToolTipIcon.Error);
        }
        _previousState = snapshot.State;
    }

    private void ShowCurrentStatusBalloon()
    {
        var primary = _snapshot.Activities.FirstOrDefault();
        var project = primary?.ProjectName ?? "DevSpace";
        var operation = primary is null
            ? _localizer.Operation(_snapshot.State == ActivityState.Stopped ? OperationKind.Stopped : OperationKind.Idle, null)
            : _localizer.Operation(primary.Operation, primary.Detail);
        ShowBalloon(
            $"DevSpace: {_localizer.State(_snapshot.State)}",
            $"{project}{Environment.NewLine}{operation}{Environment.NewLine}{_localizer.Get("Elapsed", FormatDuration(primary?.Elapsed ?? TimeSpan.Zero))}",
            _snapshot.State switch
            {
                ActivityState.Stopped or ActivityState.Failed => ToolTipIcon.Error,
                ActivityState.Stalled => ToolTipIcon.Warning,
                _ => ToolTipIcon.Info
            });
    }

    private void ShowBalloon(string title, string text, ToolTipIcon icon)
    {
        _notifyIcon.BalloonTipTitle = title;
        _notifyIcon.BalloonTipText = text;
        _notifyIcon.BalloonTipIcon = icon;
        _notifyIcon.ShowBalloonTip(6000);
    }

    private void ShowSettings()
    {
        _settingsForm.UpdateSnapshot(_snapshot);
        if (!_settingsForm.Visible)
        {
            _settingsForm.Show();
        }
        else
        {
            _settingsForm.Activate();
        }
    }

    private void OpenLog()
    {
        if (!File.Exists(_snapshot.LogPath))
        {
            MessageBox.Show(_localizer["NoLog"], _localizer["AppName"], MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        Process.Start(new ProcessStartInfo("notepad.exe", $"\"{_snapshot.LogPath}\"") { UseShellExecute = true });
    }

    private void OpenFolder()
    {
        var folder = Path.GetDirectoryName(_snapshot.LogPath) ?? AppPaths.DevSpaceDirectory;
        Directory.CreateDirectory(folder);
        Process.Start(new ProcessStartInfo("explorer.exe", folder) { UseShellExecute = true });
    }

    private void InstallOrUninstall()
    {
        if (SelfInstaller.IsRunningFromInstallDirectory)
        {
            var result = MessageBox.Show(
                _localizer["ConfirmUninstall"],
                _localizer["AppName"],
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);
            if (result != DialogResult.Yes)
            {
                return;
            }

            SelfInstaller.Uninstall(removeSettings: false, silent: true);
            ExitApplication();
            return;
        }

        SelfInstaller.Install(silent: false, launchAfterInstall: true);
        ExitApplication();
    }

    private void ExitApplication()
    {
        _timer.Stop();
        _notifyIcon.Visible = false;
        _petForm.Close();
        _settingsForm.Dispose();
        _notifyIcon.Dispose();
        _trayMenu.Dispose();
        foreach (var icon in _icons.Values)
        {
            icon.Dispose();
        }
        ExitThread();
    }

    protected override void ExitThreadCore()
    {
        _timer.Stop();
        base.ExitThreadCore();
    }

    private static string FormatDuration(TimeSpan duration) => duration.TotalHours >= 1
        ? $"{(int)duration.TotalHours}:{duration.Minutes:00}:{duration.Seconds:00}"
        : $"{duration.Minutes:00}:{duration.Seconds:00}";
}
