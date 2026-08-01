using System.Diagnostics;
using System.Reflection;
using DevSpaceStatusPet.Models;
using DevSpaceStatusPet.Services;

namespace DevSpaceStatusPet.UI;

public sealed class SettingsForm : Form
{
    private readonly SettingsStore _settingsStore;
    private readonly Localizer _localizer;
    private readonly ComboBox _languageBox = new() { Name = "LanguageInput", DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly ComboBox _themeBox = new() { Name = "ThemeInput", DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly ComboBox _bubbleThemeBox = new() { Name = "BubbleThemeInput", DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly ComboBox _bubbleStyleBox = new() { Name = "BubbleStyleInput", DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly CheckBox _showBubble = new() { Name = "ShowBubbleInput" };
    private readonly CheckBox _notifications = new() { Name = "NotificationsInput" };
    private readonly CheckBox _startWithWindows = new() { Name = "StartWithWindowsInput" };
    private readonly CheckBox _checkUpdatesOnStartup = new() { Name = "CheckUpdatesOnStartupInput" };
    private readonly CheckBox _includePrereleases = new() { Name = "IncludePrereleaseUpdatesInput" };
    private readonly NumericUpDown _scale = new() { Name = "ScaleInput", Minimum = 60, Maximum = 250, Increment = 5 };
    private readonly NumericUpDown _opacity = new() { Name = "OpacityInput", Minimum = 50, Maximum = 100, Increment = 5 };
    private readonly NumericUpDown _quietSeconds = new() { Name = "QuietSecondsInput", Minimum = 10, Maximum = 300 };
    private readonly NumericUpDown _stallMinutes = new() { Name = "StallMinutesInput", Minimum = 1, Maximum = 240 };
    private readonly NumericUpDown _maxBubbles = new() { Name = "MaxBubblesInput", Minimum = 1, Maximum = 8 };
    private readonly Label _statusLabel = new() { AutoSize = true };
    private readonly Label _portLabel = new() { AutoSize = true };
    private readonly Label _configLabel = new() { AutoSize = true };
    private readonly Label _logLabel = new() { AutoSize = true };
    private readonly Label _languageLabel = new() { AutoSize = true };
    private readonly Label _themeLabel = new() { AutoSize = true };
    private readonly Label _bubbleThemeLabel = new() { AutoSize = true };
    private readonly Label _bubbleStyleLabel = new() { AutoSize = true };
    private readonly Label _bubbleLabel = new() { AutoSize = true };
    private readonly Label _scaleLabel = new() { AutoSize = true };
    private readonly Label _opacityLabel = new() { AutoSize = true };
    private readonly Label _quietLabel = new() { AutoSize = true };
    private readonly Label _stallLabel = new() { AutoSize = true };
    private readonly Label _maxBubblesLabel = new() { AutoSize = true };
    private readonly Label _notificationsLabel = new() { AutoSize = true };
    private readonly Label _startupLabel = new() { AutoSize = true };
    private readonly Label _versionLabel = new() { AutoSize = true };
    private readonly Label _latestVersionLabel = new() { AutoSize = true };
    private readonly Label _updateStatusLabel = new() { AutoSize = true };
    private readonly Label _statusValue = new() { AutoSize = true };
    private readonly Label _portValue = new() { AutoSize = true };
    private readonly Label _configValue = new() { AutoSize = true, MaximumSize = new Size(430, 0) };
    private readonly Label _logValue = new() { AutoSize = true, MaximumSize = new Size(430, 0) };
    private readonly Label _versionValue = new() { AutoSize = true };
    private readonly Label _latestVersionValue = new() { Name = "LatestVersionValue", AutoSize = true };
    private readonly Label _updateStatusValue = new() { Name = "UpdateStatusValue", AutoSize = true, MaximumSize = new Size(430, 0) };
    private readonly Button _saveButton = new();
    private readonly Button _closeButton = new();
    private readonly Button _openLogsButton = new();
    private readonly Button _checkUpdatesButton = new() { Name = "CheckUpdatesButton" };
    private DevSpaceSnapshot _snapshot;
    private bool _reloadingControls;
    private string _latestVersionText;
    private string _updateStatusText;

    public SettingsForm(SettingsStore settingsStore, Localizer localizer, DevSpaceSnapshot snapshot)
    {
        _settingsStore = settingsStore;
        _localizer = localizer;
        _snapshot = snapshot;
        _latestVersionText = GetCurrentVersion();
        _updateStatusText = _localizer["NotChecked"];

        AutoScaleMode = AutoScaleMode.Dpi;
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = true;
        ClientSize = new Size(620, 775);
        Font = new Font("Segoe UI", 9f);
        BackColor = DarkUiTheme.WindowBackground;
        ForeColor = DarkUiTheme.Foreground;

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(16),
            ColumnCount = 2,
            RowCount = 22,
            AutoSize = false
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 210));
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        Controls.Add(root);

        AddRow(root, 0, _statusLabel, _statusValue);
        AddRow(root, 1, _portLabel, _portValue);
        AddRow(root, 2, _configLabel, _configValue);
        AddRow(root, 3, _logLabel, _logValue);
        AddRow(root, 4, _languageLabel, _languageBox);
        AddRow(root, 5, _themeLabel, _themeBox);
        AddRow(root, 6, _bubbleThemeLabel, _bubbleThemeBox);
        AddRow(root, 7, _bubbleStyleLabel, _bubbleStyleBox);
        AddRow(root, 8, _bubbleLabel, _showBubble);
        AddRow(root, 9, _scaleLabel, _scale);
        AddRow(root, 10, _opacityLabel, _opacity);
        AddRow(root, 11, _quietLabel, _quietSeconds);
        AddRow(root, 12, _stallLabel, _stallMinutes);
        AddRow(root, 13, _maxBubblesLabel, _maxBubbles);
        AddRow(root, 14, _notificationsLabel, _notifications);
        AddRow(root, 15, _startupLabel, _startWithWindows);
        AddRow(root, 16, new Label { AutoSize = true }, _checkUpdatesOnStartup);
        AddRow(root, 17, new Label { AutoSize = true }, _includePrereleases);
        AddRow(root, 18, _versionLabel, _versionValue);
        AddRow(root, 19, _latestVersionLabel, _latestVersionValue);
        AddRow(root, 20, _updateStatusLabel, _updateStatusValue);

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            AutoSize = true,
            WrapContents = false
        };
        buttons.Controls.AddRange([_closeButton, _saveButton, _checkUpdatesButton, _openLogsButton]);
        root.Controls.Add(buttons, 0, 21);
        root.SetColumnSpan(buttons, 2);
        DarkUiTheme.ApplyWindow(this);

        _saveButton.Click += (_, _) => SaveSettings(showConfirmation: true);
        _checkUpdatesButton.Click += (_, _) => UpdateCheckRequested?.Invoke(this, EventArgs.Empty);
        _closeButton.Click += (_, _) => Hide();
        _openLogsButton.Click += (_, _) =>
        {
            Directory.CreateDirectory(AppPaths.LogsDirectory);
            Process.Start(new ProcessStartInfo("explorer.exe", AppPaths.LogsDirectory) { UseShellExecute = true });
        };
        FormClosing += (_, eventArgs) =>
        {
            if (eventArgs.CloseReason == CloseReason.UserClosing)
            {
                eventArgs.Cancel = true;
                Hide();
            }
        };
        _settingsStore.Changed += (_, _) => Reload();
        Reload();
        WireLivePreview();
    }

    public event EventHandler? UpdateCheckRequested;

    public void SetUpdateStatus(string latestVersion, string status)
    {
        _latestVersionText = string.IsNullOrWhiteSpace(latestVersion)
            ? GetCurrentVersion()
            : latestVersion;
        _updateStatusText = status;
        RefreshText();
    }

    public void SetUpdateCheckBusy(bool busy)
    {
        _checkUpdatesButton.Enabled = !busy;
        if (busy)
        {
            _updateStatusText = _localizer["CheckingUpdates"];
            RefreshText();
        }
    }

    public void UpdateSnapshot(DevSpaceSnapshot snapshot)
    {
        _snapshot = snapshot;
        RefreshText();
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        DarkUiTheme.ApplyImmersiveDarkTitleBar(this);
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        DarkUiTheme.ApplyWindow(this);
        DarkUiTheme.ApplyImmersiveDarkTitleBar(this);
        Reload();
        Activate();
    }

    private void Reload()
    {
        _reloadingControls = true;
        try
        {
            var settings = _settingsStore.Current;
            RefreshChoices(settings);
            _showBubble.Checked = settings.ShowBubble;
            _scale.Value = (decimal)(settings.Scale * 100);
            _opacity.Value = (decimal)(settings.Opacity * 100);
            _quietSeconds.Value = settings.CompletionQuietSeconds;
            _stallMinutes.Value = settings.StallMinutes;
            _maxBubbles.Value = settings.MaxBubbles;
            _notifications.Checked = settings.NotificationsEnabled;
            _startWithWindows.Checked = StartupManager.IsEnabled();
            _checkUpdatesOnStartup.Checked = settings.CheckUpdatesOnStartup;
            _includePrereleases.Checked = settings.IncludePrereleaseUpdates;
            RefreshText();
        }
        finally
        {
            _reloadingControls = false;
        }
    }

    private void RefreshText()
    {
        Text = $"{_localizer["AppName"]} - {_localizer["Settings"]}";
        _statusLabel.Text = _localizer.Get("Status", string.Empty).TrimEnd(' ', ':', '：');
        _portLabel.Text = _localizer.Get("Port", string.Empty).TrimEnd(' ', ':', '：');
        _configLabel.Text = _localizer.Get("Config", string.Empty).TrimEnd(' ', ':', '：');
        _logLabel.Text = _localizer.Get("Log", string.Empty).TrimEnd(' ', ':', '：');
        _languageLabel.Text = _localizer["Language"];
        _themeLabel.Text = _localizer["Theme"];
        _bubbleThemeLabel.Text = _localizer["BubbleTheme"];
        _bubbleStyleLabel.Text = _localizer["BubbleStyle"];
        _bubbleLabel.Text = _localizer["ShowBubble"];
        _scaleLabel.Text = _localizer["Scale"];
        _opacityLabel.Text = _localizer["Opacity"];
        _quietLabel.Text = _localizer["QuietSeconds"];
        _stallLabel.Text = _localizer["StallMinutes"];
        _maxBubblesLabel.Text = _localizer["MaxBubbles"];
        _notificationsLabel.Text = _localizer["NotificationsEnabled"];
        _startupLabel.Text = _localizer["StartWithWindows"];
        _versionLabel.Text = _localizer.Get("Version", string.Empty).TrimEnd(' ', ':', '：');
        _latestVersionLabel.Text = _localizer["LatestVersion"];
        _updateStatusLabel.Text = _localizer["UpdateStatus"];

        _showBubble.Text = string.Empty;
        _notifications.Text = string.Empty;
        _startWithWindows.Text = string.Empty;
        _checkUpdatesOnStartup.Text = _localizer["CheckUpdatesOnStartup"];
        _includePrereleases.Text = _localizer["IncludePrereleaseUpdates"];
        _saveButton.Text = _localizer["Save"];
        _closeButton.Text = _localizer["Close"];
        _openLogsButton.Text = _localizer["OpenLogFolder"];
        _checkUpdatesButton.Text = _localizer["CheckUpdates"];

        _statusValue.Text = _localizer.State(_snapshot.State);
        _portValue.Text = _snapshot.Port.ToString();
        _configValue.Text = _snapshot.ConfigPath;
        _logValue.Text = _snapshot.LogPath;
        _versionValue.Text = GetCurrentVersion();
        _latestVersionValue.Text = _latestVersionText;
        _updateStatusValue.Text = _updateStatusText;
    }

    private void SaveSettings(bool showConfirmation)
    {
        if (_reloadingControls)
        {
            return;
        }

        var settings = _settingsStore.Current;
        settings.Language = (_languageBox.SelectedItem is Choice<UiLanguagePreference> language
            ? language.Value
            : UiLanguagePreference.Auto).ToString();
        settings.Theme = (_themeBox.SelectedItem is Choice<PetTheme> theme
            ? theme.Value
            : PetTheme.Classic).ToString();
        settings.BubbleTheme = (_bubbleThemeBox.SelectedItem is Choice<BubbleColorTheme> bubbleTheme
            ? bubbleTheme.Value
            : BubbleColorTheme.Light).ToString();
        settings.BubbleStyle = (_bubbleStyleBox.SelectedItem is Choice<BubbleVisualStyle> bubbleStyle
            ? bubbleStyle.Value
            : BubbleVisualStyle.Speech).ToString();
        settings.ShowBubble = _showBubble.Checked;
        settings.Scale = (double)_scale.Value / 100d;
        settings.Opacity = (double)_opacity.Value / 100d;
        settings.CompletionQuietSeconds = (int)_quietSeconds.Value;
        settings.StallMinutes = (int)_stallMinutes.Value;
        settings.MaxBubbles = (int)_maxBubbles.Value;
        settings.NotificationsEnabled = _notifications.Checked;
        settings.CheckUpdatesOnStartup = _checkUpdatesOnStartup.Checked;
        settings.IncludePrereleaseUpdates = _includePrereleases.Checked;
        _settingsStore.Save(settings);
        StartupManager.SetEnabled(_startWithWindows.Checked);
        RefreshText();
        if (showConfirmation)
        {
            MessageBox.Show(this, _localizer["Saved"], _localizer["AppName"], MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }

    private void WireLivePreview()
    {
        _languageBox.SelectedIndexChanged += (_, _) => SaveSettings(showConfirmation: false);
        _themeBox.SelectedIndexChanged += (_, _) => SaveSettings(showConfirmation: false);
        _bubbleThemeBox.SelectedIndexChanged += (_, _) => SaveSettings(showConfirmation: false);
        _bubbleStyleBox.SelectedIndexChanged += (_, _) => SaveSettings(showConfirmation: false);
        _showBubble.CheckedChanged += (_, _) => SaveSettings(showConfirmation: false);
        _scale.ValueChanged += (_, _) => SaveSettings(showConfirmation: false);
        _opacity.ValueChanged += (_, _) => SaveSettings(showConfirmation: false);
        _quietSeconds.ValueChanged += (_, _) => SaveSettings(showConfirmation: false);
        _stallMinutes.ValueChanged += (_, _) => SaveSettings(showConfirmation: false);
        _maxBubbles.ValueChanged += (_, _) => SaveSettings(showConfirmation: false);
        _notifications.CheckedChanged += (_, _) => SaveSettings(showConfirmation: false);
        _startWithWindows.CheckedChanged += (_, _) => SaveSettings(showConfirmation: false);
        _checkUpdatesOnStartup.CheckedChanged += (_, _) => SaveSettings(showConfirmation: false);
        _includePrereleases.CheckedChanged += (_, _) => SaveSettings(showConfirmation: false);
    }

    private void RefreshChoices(AppSettings settings)
    {
        _languageBox.BeginUpdate();
        _languageBox.Items.Clear();
        _languageBox.Items.AddRange([
            new Choice<UiLanguagePreference>(UiLanguagePreference.Auto, _localizer["Auto"]),
            new Choice<UiLanguagePreference>(UiLanguagePreference.Japanese, _localizer["Japanese"]),
            new Choice<UiLanguagePreference>(UiLanguagePreference.English, _localizer["English"])
        ]);
        _languageBox.SelectedItem = _languageBox.Items
            .OfType<Choice<UiLanguagePreference>>()
            .First(choice => choice.Value == settings.LanguagePreference);
        _languageBox.EndUpdate();

        _themeBox.BeginUpdate();
        _themeBox.Items.Clear();
        _themeBox.Items.AddRange([
            new Choice<PetTheme>(PetTheme.Classic, _localizer["Classic"]),
            new Choice<PetTheme>(PetTheme.Neon, _localizer["Neon"])
        ]);
        _themeBox.SelectedItem = _themeBox.Items
            .OfType<Choice<PetTheme>>()
            .First(choice => choice.Value == settings.ResolvedTheme);
        _themeBox.EndUpdate();

        _bubbleThemeBox.BeginUpdate();
        _bubbleThemeBox.Items.Clear();
        _bubbleThemeBox.Items.AddRange([
            new Choice<BubbleColorTheme>(BubbleColorTheme.Light, _localizer["BubbleLight"]),
            new Choice<BubbleColorTheme>(BubbleColorTheme.Dark, _localizer["BubbleDark"])
        ]);
        _bubbleThemeBox.SelectedItem = _bubbleThemeBox.Items
            .OfType<Choice<BubbleColorTheme>>()
            .First(choice => choice.Value == settings.ResolvedBubbleTheme);
        _bubbleThemeBox.EndUpdate();

        _bubbleStyleBox.BeginUpdate();
        _bubbleStyleBox.Items.Clear();
        _bubbleStyleBox.Items.AddRange([
            new Choice<BubbleVisualStyle>(BubbleVisualStyle.Speech, _localizer["BubbleSpeech"]),
            new Choice<BubbleVisualStyle>(BubbleVisualStyle.MonitorCardNeon, _localizer["BubbleMonitorCardNeon"]),
            new Choice<BubbleVisualStyle>(BubbleVisualStyle.MonitorCardClean, _localizer["BubbleMonitorCardClean"])
        ]);
        _bubbleStyleBox.SelectedItem = _bubbleStyleBox.Items
            .OfType<Choice<BubbleVisualStyle>>()
            .First(choice => choice.Value == settings.ResolvedBubbleStyle);
        _bubbleStyleBox.EndUpdate();
    }

    private static string GetCurrentVersion()
    {
        var informational = Assembly.GetExecutingAssembly()
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion;
        if (!string.IsNullOrWhiteSpace(informational))
        {
            var metadata = informational.IndexOf('+');
            return metadata >= 0 ? informational[..metadata] : informational;
        }
        return Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "0.0.0";
    }

    private static void AddRow(TableLayoutPanel panel, int row, Control label, Control value)
    {
        panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        label.Margin = new Padding(3, 8, 8, 8);
        value.Margin = new Padding(3, 5, 3, 5);
        value.Anchor = AnchorStyles.Left | AnchorStyles.Right;
        panel.Controls.Add(label, 0, row);
        panel.Controls.Add(value, 1, row);
    }

    private sealed record Choice<T>(T Value, string Text)
    {
        public override string ToString() => Text;
    }
}
