using System.Diagnostics;
using System.Reflection;
using DevSpaceStatusPet.Models;
using DevSpaceStatusPet.Services;

namespace DevSpaceStatusPet.UI;

public sealed class SettingsForm : Form
{
    private readonly SettingsStore _settingsStore;
    private readonly Localizer _localizer;
    private readonly ComboBox _languageBox = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly ComboBox _themeBox = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly CheckBox _showBubble = new();
    private readonly CheckBox _notifications = new();
    private readonly CheckBox _startWithWindows = new();
    private readonly NumericUpDown _scale = new() { Minimum = 60, Maximum = 180, Increment = 5 };
    private readonly NumericUpDown _opacity = new() { Minimum = 50, Maximum = 100, Increment = 5 };
    private readonly NumericUpDown _quietSeconds = new() { Minimum = 10, Maximum = 300 };
    private readonly NumericUpDown _stallMinutes = new() { Minimum = 1, Maximum = 240 };
    private readonly NumericUpDown _maxBubbles = new() { Minimum = 1, Maximum = 8 };
    private readonly Label _statusLabel = new() { AutoSize = true };
    private readonly Label _portLabel = new() { AutoSize = true };
    private readonly Label _configLabel = new() { AutoSize = true };
    private readonly Label _logLabel = new() { AutoSize = true };
    private readonly Label _languageLabel = new() { AutoSize = true };
    private readonly Label _themeLabel = new() { AutoSize = true };
    private readonly Label _bubbleLabel = new() { AutoSize = true };
    private readonly Label _scaleLabel = new() { AutoSize = true };
    private readonly Label _opacityLabel = new() { AutoSize = true };
    private readonly Label _quietLabel = new() { AutoSize = true };
    private readonly Label _stallLabel = new() { AutoSize = true };
    private readonly Label _maxBubblesLabel = new() { AutoSize = true };
    private readonly Label _notificationsLabel = new() { AutoSize = true };
    private readonly Label _startupLabel = new() { AutoSize = true };
    private readonly Label _versionLabel = new() { AutoSize = true };
    private readonly Label _statusValue = new() { AutoSize = true };
    private readonly Label _portValue = new() { AutoSize = true };
    private readonly Label _configValue = new() { AutoSize = true, MaximumSize = new Size(430, 0) };
    private readonly Label _logValue = new() { AutoSize = true, MaximumSize = new Size(430, 0) };
    private readonly Label _versionValue = new() { AutoSize = true };
    private readonly Button _saveButton = new();
    private readonly Button _closeButton = new();
    private readonly Button _openLogsButton = new();
    private DevSpaceSnapshot _snapshot;

    public SettingsForm(SettingsStore settingsStore, Localizer localizer, DevSpaceSnapshot snapshot)
    {
        _settingsStore = settingsStore;
        _localizer = localizer;
        _snapshot = snapshot;

        AutoScaleMode = AutoScaleMode.Dpi;
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = true;
        ClientSize = new Size(600, 590);
        Font = new Font("Segoe UI", 9f);

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(16),
            ColumnCount = 2,
            RowCount = 17,
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
        AddRow(root, 6, _bubbleLabel, _showBubble);
        AddRow(root, 7, _scaleLabel, _scale);
        AddRow(root, 8, _opacityLabel, _opacity);
        AddRow(root, 9, _quietLabel, _quietSeconds);
        AddRow(root, 10, _stallLabel, _stallMinutes);
        AddRow(root, 11, _maxBubblesLabel, _maxBubbles);
        AddRow(root, 12, _notificationsLabel, _notifications);
        AddRow(root, 13, _startupLabel, _startWithWindows);
        AddRow(root, 14, _versionLabel, _versionValue);

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            AutoSize = true,
            WrapContents = false
        };
        buttons.Controls.AddRange([_closeButton, _saveButton, _openLogsButton]);
        root.Controls.Add(buttons, 0, 16);
        root.SetColumnSpan(buttons, 2);

        _saveButton.Click += (_, _) => SaveSettings();
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
    }

    public void UpdateSnapshot(DevSpaceSnapshot snapshot)
    {
        _snapshot = snapshot;
        RefreshText();
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        Reload();
        Activate();
    }

    private void Reload()
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
        RefreshText();
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
        _bubbleLabel.Text = _localizer["ShowBubble"];
        _scaleLabel.Text = _localizer["Scale"];
        _opacityLabel.Text = _localizer["Opacity"];
        _quietLabel.Text = _localizer["QuietSeconds"];
        _stallLabel.Text = _localizer["StallMinutes"];
        _maxBubblesLabel.Text = _localizer["MaxBubbles"];
        _notificationsLabel.Text = _localizer["NotificationsEnabled"];
        _startupLabel.Text = _localizer["StartWithWindows"];
        _versionLabel.Text = _localizer.Get("Version", string.Empty).TrimEnd(' ', ':', '：');

        _showBubble.Text = string.Empty;
        _notifications.Text = string.Empty;
        _startWithWindows.Text = string.Empty;
        _saveButton.Text = _localizer["Save"];
        _closeButton.Text = _localizer["Close"];
        _openLogsButton.Text = _localizer["OpenLogFolder"];

        _statusValue.Text = _localizer.State(_snapshot.State);
        _portValue.Text = _snapshot.Port.ToString();
        _configValue.Text = _snapshot.ConfigPath;
        _logValue.Text = _snapshot.LogPath;
        _versionValue.Text = Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "0.2.0";
    }

    private void SaveSettings()
    {
        var settings = _settingsStore.Current;
        settings.Language = (_languageBox.SelectedItem is Choice<UiLanguagePreference> language
            ? language.Value
            : UiLanguagePreference.Auto).ToString();
        settings.Theme = (_themeBox.SelectedItem is Choice<PetTheme> theme
            ? theme.Value
            : PetTheme.Classic).ToString();
        settings.ShowBubble = _showBubble.Checked;
        settings.Scale = (double)_scale.Value / 100d;
        settings.Opacity = (double)_opacity.Value / 100d;
        settings.CompletionQuietSeconds = (int)_quietSeconds.Value;
        settings.StallMinutes = (int)_stallMinutes.Value;
        settings.MaxBubbles = (int)_maxBubbles.Value;
        settings.NotificationsEnabled = _notifications.Checked;
        _settingsStore.Save(settings);
        StartupManager.SetEnabled(_startWithWindows.Checked);
        RefreshText();
        MessageBox.Show(this, _localizer["Saved"], _localizer["AppName"], MessageBoxButtons.OK, MessageBoxIcon.Information);
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
