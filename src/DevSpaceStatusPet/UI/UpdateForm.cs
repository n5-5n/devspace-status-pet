using System.Diagnostics;
using DevSpaceStatusPet.Services;

namespace DevSpaceStatusPet.UI;

public sealed class UpdateForm : Form
{
    private readonly UpdateService _updateService;
    private readonly UpdateRelease _release;
    private readonly Localizer _localizer;
    private readonly Label _heading = new() { AutoSize = true, Font = new Font("Segoe UI Semibold", 15f) };
    private readonly Label _versionLabel = new() { AutoSize = true };
    private readonly Label _publishedLabel = new() { AutoSize = true };
    private readonly TextBox _notes = new()
    {
        Multiline = true,
        ReadOnly = true,
        ScrollBars = ScrollBars.Vertical,
        WordWrap = true,
        Dock = DockStyle.Fill
    };
    private readonly ProgressBar _progress = new() { Dock = DockStyle.Fill, Minimum = 0, Maximum = 100 };
    private readonly Label _status = new() { AutoSize = true };
    private readonly Button _updateButton = new();
    private readonly Button _openReleaseButton = new();
    private readonly Button _cancelButton = new();
    private readonly CancellationTokenSource _cancellation = new();
    private bool _installing;

    public UpdateForm(
        UpdateService updateService,
        UpdateRelease release,
        Localizer localizer)
    {
        _updateService = updateService;
        _release = release;
        _localizer = localizer;

        AutoScaleMode = AutoScaleMode.Dpi;
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = true;
        ClientSize = new Size(680, 560);
        Font = new Font("Segoe UI", 9f);
        BackColor = DarkUiTheme.WindowBackground;
        ForeColor = DarkUiTheme.Foreground;

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(20),
            ColumnCount = 1,
            RowCount = 8
        };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 12));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        Controls.Add(root);

        _heading.Margin = new Padding(0, 0, 0, 8);
        _versionLabel.Margin = new Padding(0, 0, 0, 4);
        _publishedLabel.Margin = new Padding(0, 0, 0, 4);
        _notes.Margin = new Padding(0);
        _progress.Margin = new Padding(0, 12, 0, 6);
        _status.Margin = new Padding(0, 0, 0, 10);

        root.Controls.Add(_heading, 0, 0);
        root.Controls.Add(_versionLabel, 0, 1);
        root.Controls.Add(_publishedLabel, 0, 2);
        root.Controls.Add(_notes, 0, 4);
        root.Controls.Add(_progress, 0, 5);
        root.Controls.Add(_status, 0, 6);

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false
        };
        buttons.Controls.AddRange([_cancelButton, _updateButton, _openReleaseButton]);
        root.Controls.Add(buttons, 0, 7);

        _updateButton.Click += async (_, _) => await InstallAsync();
        _openReleaseButton.Click += (_, _) => OpenReleasePage();
        _cancelButton.Click += (_, _) =>
        {
            if (_installing)
            {
                _cancellation.Cancel();
            }
            else
            {
                Close();
            }
        };
        FormClosing += (_, eventArgs) =>
        {
            if (_installing && !InstallerLaunched)
            {
                eventArgs.Cancel = true;
                _cancellation.Cancel();
            }
        };

        RefreshText();
        DarkUiTheme.ApplyWindow(this);
    }

    public bool InstallerLaunched { get; private set; }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        DarkUiTheme.ApplyImmersiveDarkTitleBar(this);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _cancellation.Dispose();
        }
        base.Dispose(disposing);
    }

    private void RefreshText()
    {
        Text = _localizer["UpdateAvailableTitle"];
        _heading.Text = _localizer["UpdateAvailableTitle"];
        _versionLabel.Text = _localizer.Get(
            "UpdateVersionLine",
            _updateService.CurrentVersion,
            _release.Version);
        _publishedLabel.Text = _release.PublishedAt.HasValue
            ? _localizer.Get("UpdatePublished", _release.PublishedAt.Value.ToLocalTime().ToString("yyyy-MM-dd HH:mm"))
            : string.Empty;
        _notes.Text = string.IsNullOrWhiteSpace(_release.Notes)
            ? _localizer["NoReleaseNotes"]
            : _release.Notes.Trim();
        _status.Text = _localizer["UpdateReady"];
        _updateButton.Text = _localizer["InstallUpdateNow"];
        _openReleaseButton.Text = _localizer["OpenReleasePage"];
        _cancelButton.Text = _localizer["Cancel"];
    }

    private async Task InstallAsync()
    {
        if (_installing)
        {
            return;
        }

        _installing = true;
        _updateButton.Enabled = false;
        _openReleaseButton.Enabled = false;
        _cancelButton.Text = _localizer["Cancel"];
        UseWaitCursor = true;

        var progress = new Progress<UpdateProgress>(value =>
        {
            _progress.Value = Math.Clamp(value.Percentage, 0, 100);
            _status.Text = LocalizeProgress(value);
        });

        try
        {
            var installer = await _updateService.PrepareInstallerAsync(
                _release,
                progress,
                _cancellation.Token);
            _status.Text = _localizer["StartingInstaller"];
            UpdateService.LaunchInstaller(installer);
            InstallerLaunched = true;
            _installing = false;
            DialogResult = DialogResult.OK;
            Close();
        }
        catch (OperationCanceledException)
        {
            _status.Text = _localizer["UpdateCancelled"];
        }
        catch (Exception exception)
        {
            CrashLogger.Write(exception, "UpdateForm.InstallAsync");
            _status.Text = _localizer["UpdateFailed"];
            MessageBox.Show(
                this,
                _localizer.Get("UpdateFailedDetail", exception.Message),
                _localizer["AppName"],
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
        finally
        {
            if (!InstallerLaunched)
            {
                _installing = false;
                _updateButton.Enabled = true;
                _openReleaseButton.Enabled = true;
                _cancelButton.Text = _localizer["Close"];
                UseWaitCursor = false;
            }
        }
    }

    private string LocalizeProgress(UpdateProgress progress) => progress.Stage switch
    {
        "checksum" => _localizer["DownloadingChecksum"],
        "download" => _localizer.Get("DownloadingUpdate", progress.Detail),
        "verify" => _localizer["VerifyingUpdate"],
        "extract" => _localizer["ExtractingUpdate"],
        "ready" => _localizer["UpdateReady"],
        _ => progress.Detail
    };

    private void OpenReleasePage()
    {
        if (string.IsNullOrWhiteSpace(_release.ReleaseUrl))
        {
            return;
        }

        Process.Start(new ProcessStartInfo(_release.ReleaseUrl)
        {
            UseShellExecute = true
        });
    }
}
