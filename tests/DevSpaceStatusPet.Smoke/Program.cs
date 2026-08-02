using System.Drawing;
using System.IO.Compression;
using System.Net;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Windows.Forms;
using DevSpaceStatusPet.Models;
using DevSpaceStatusPet.Services;
using DevSpaceStatusPet.UI;

var failures = new List<string>();

void Check(bool condition, string name)
{
    if (condition)
    {
        Console.WriteLine($"[OK] {name}");
    }
    else
    {
        Console.WriteLine($"[FAIL] {name}");
        failures.Add(name);
    }
}

var migrated = JsonSerializer.Deserialize<AppSettings>(
    """
    {
      "Theme": "Neon",
      "ShowBubble": false,
      "Language": "English"
    }
    """,
    new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new AppSettings();
migrated.Normalize();
Check(migrated.ResolvedTheme == PetTheme.Neon, "v0.1 theme migration");
Check(!migrated.ShowBubble, "v0.1 bubble migration");
Check(migrated.LanguagePreference == UiLanguagePreference.English, "v0.1 language migration");
var chineseSettings = new AppSettings { Language = "ChineseSimplified" };
chineseSettings.Normalize();
Check(chineseSettings.LanguagePreference == UiLanguagePreference.ChineseSimplified, "simplified Chinese language setting");
Check(migrated.ResolvedBubbleTheme == BubbleColorTheme.Light, "legacy .NET bubble theme migration default");
Check(migrated.ResolvedBubbleStyle == BubbleVisualStyle.Speech, "bubble design migration default");
Check(Math.Abs(migrated.Scale - 1.15) < 0.001 && migrated.MaxBubbles == 4, "new settings defaults");
Check(migrated.CheckUpdatesOnStartup && !migrated.IncludePrereleaseUpdates, "update settings defaults");
Check(migrated.Clone().CheckUpdatesOnStartup, "update settings clone");

Check(SemanticVersion.TryParse("v0.1.2", out var stableVersion) && stableVersion.ToString() == "0.1.2", "stable version parsing");
Check(SemanticVersion.TryParse("0.1.3-beta.2", out var previewVersion) && previewVersion.IsPrerelease, "prerelease version parsing");
Check(stableVersion.CompareTo(new SemanticVersion(0, 1, 2, "beta.1")) > 0, "stable outranks prerelease");

using (var releasesDocument = JsonDocument.Parse("""
[
  {
    "tag_name": "v0.1.2",
    "name": "DevSpace Status Pet v0.1.2",
    "html_url": "https://example.test/v0.1.2",
    "body": "Stable notes",
    "draft": false,
    "prerelease": false,
    "published_at": "2026-08-01T00:00:00Z",
    "assets": [
      { "name": "DevSpace-Status-Pet-v0.1.2-win-x64.zip", "browser_download_url": "https://example.test/stable.zip", "size": 123 },
      { "name": "DevSpace-Status-Pet-v0.1.2-win-x64.zip.sha256", "browser_download_url": "https://example.test/stable.sha256", "size": 100 }
    ]
  },
  {
    "tag_name": "v0.1.3-beta.2",
    "name": "DevSpace Status Pet v0.1.3-beta.2",
    "html_url": "https://example.test/v0.1.3-beta.2",
    "body": "Preview notes",
    "draft": false,
    "prerelease": true,
    "published_at": "2026-08-02T00:00:00Z",
    "assets": [
      { "name": "DevSpace-Status-Pet-v0.1.3-beta.2-win-x64.zip", "browser_download_url": "https://example.test/preview.zip", "size": 456 },
      { "name": "DevSpace-Status-Pet-v0.1.3-beta.2-win-x64.zip.sha256", "browser_download_url": "https://example.test/preview.sha256", "size": 100 }
    ]
  }
]
"""))
{
    var stableRelease = UpdateService.SelectLatestRelease(
        releasesDocument.RootElement,
        new SemanticVersion(0, 1, 1, null),
        includePrereleases: false);
    var previewRelease = UpdateService.SelectLatestRelease(
        releasesDocument.RootElement,
        new SemanticVersion(0, 1, 1, null),
        includePrereleases: true);
    Check(stableRelease?.Version == "0.1.2", "stable update selection");
    Check(previewRelease?.Version == "0.1.3-beta.2", "prerelease update selection");
}

Check(UpdateService.ParseSha256($"{new string('a', 64)}  package.zip") == new string('a', 64), "SHA-256 text parsing");

var updatePackageRoot = Path.Combine(Path.GetTempPath(), $"DevSpaceStatusPet-UpdatePackage-{Environment.ProcessId}");
Directory.CreateDirectory(updatePackageRoot);
try
{
    var sourceExecutable = Environment.ProcessPath
        ?? throw new InvalidOperationException("The smoke-test executable path is unavailable.");
    var sourceProductVersion = System.Diagnostics.FileVersionInfo
        .GetVersionInfo(sourceExecutable)
        .ProductVersion ?? "1.0.0";
    var metadataIndex = sourceProductVersion.IndexOf('+');
    var packageVersion = metadataIndex >= 0
        ? sourceProductVersion[..metadataIndex]
        : sourceProductVersion;
    var zipPath = Path.Combine(updatePackageRoot, "update.zip");
    using (var archive = ZipFile.Open(zipPath, ZipArchiveMode.Create))
    {
        var entry = archive.CreateEntry("DevSpace-Status-Pet-test/DevSpaceStatusPet.exe");
        await using var target = entry.Open();
        await using var source = File.OpenRead(sourceExecutable);
        await source.CopyToAsync(target);
    }

    var zipBytes = await File.ReadAllBytesAsync(zipPath);
    var zipHash = Convert.ToHexString(SHA256.HashData(zipBytes)).ToLowerInvariant();
    var handler = new FakeHttpMessageHandler(new Dictionary<string, byte[]>
    {
        ["https://example.test/update.zip"] = zipBytes,
        ["https://example.test/update.sha256"] = Encoding.UTF8.GetBytes($"{zipHash}  update.zip\n")
    });
    using var client = new HttpClient(handler);
    using var updateService = new UpdateService("0.0.0", client);
    var release = new UpdateRelease(
        packageVersion,
        $"v{packageVersion}",
        "Test update",
        "https://example.test/release",
        "Test notes",
        false,
        DateTimeOffset.Now,
        "https://example.test/update.zip",
        "https://example.test/update.sha256",
        zipBytes.Length);
    var preparedInstaller = await updateService.PrepareInstallerAsync(release);
    Check(File.Exists(preparedInstaller), "verified update package extraction");
    Check(
        System.Diagnostics.FileVersionInfo.GetVersionInfo(preparedInstaller).ProductVersion?.StartsWith(packageVersion, StringComparison.OrdinalIgnoreCase) == true,
        "prepared installer version validation");

    var badHandler = new FakeHttpMessageHandler(new Dictionary<string, byte[]>
    {
        ["https://example.test/update.zip"] = zipBytes,
        ["https://example.test/update.sha256"] = Encoding.UTF8.GetBytes($"{new string('0', 64)}  update.zip\n")
    });
    using var badClient = new HttpClient(badHandler);
    using var badService = new UpdateService("0.0.0", badClient);
    var checksumRejected = false;
    try
    {
        _ = await badService.PrepareInstallerAsync(release);
    }
    catch (InvalidDataException)
    {
        checksumRejected = true;
    }
    Check(checksumRejected, "tampered update package rejection");

    var unsafeZip = Path.Combine(updatePackageRoot, "unsafe.zip");
    using (var archive = ZipFile.Open(unsafeZip, ZipArchiveMode.Create))
    {
        var entry = archive.CreateEntry("../outside.txt");
        await using var writer = new StreamWriter(entry.Open());
        await writer.WriteAsync("unsafe");
    }
    var unsafeRejected = false;
    try
    {
        UpdateService.ExtractZipSafely(unsafeZip, Path.Combine(updatePackageRoot, "unsafe-extract"));
    }
    catch (InvalidDataException)
    {
        unsafeRejected = true;
    }
    Check(unsafeRejected, "ZIP traversal rejection");
}
finally
{
    Directory.Delete(updatePackageRoot, true);
}

if (Environment.GetEnvironmentVariable("DEVSPACE_STATUS_PET_LIVE_UPDATE_TEST") == "1")
{
    using var liveUpdateService = new UpdateService("0.1.0");
    var liveRelease = await liveUpdateService.CheckAsync(includePrereleases: false);
    Check(liveRelease is not null, "live GitHub update discovery");
    if (liveRelease is not null)
    {
        var liveInstaller = await liveUpdateService.PrepareInstallerAsync(liveRelease);
        Check(File.Exists(liveInstaller), "live GitHub package verification");
        Check(
            System.Diagnostics.FileVersionInfo.GetVersionInfo(liveInstaller).ProductVersion?.StartsWith(liveRelease.Version, StringComparison.OrdinalIgnoreCase) == true,
            "live GitHub installer version");
    }
}

var darkBubbleSettings = new AppSettings { BubbleTheme = "Dark" };
darkBubbleSettings.Normalize();
Check(darkBubbleSettings.ResolvedBubbleTheme == BubbleColorTheme.Dark, "dark bubble setting normalization");
Check(darkBubbleSettings.Clone().ResolvedBubbleTheme == BubbleColorTheme.Dark, "dark bubble setting clone");
var monitorCardSettings = new AppSettings { BubbleStyle = "MonitorCard" };
monitorCardSettings.Normalize();
Check(monitorCardSettings.ResolvedBubbleStyle == BubbleVisualStyle.MonitorCardNeon, "legacy monitor-card migration");
Check(monitorCardSettings.BubbleStyle == nameof(BubbleVisualStyle.MonitorCardNeon), "legacy monitor-card normalization");
var cleanMonitorCardSettings = new AppSettings { BubbleStyle = "MonitorCardClean" };
cleanMonitorCardSettings.Normalize();
Check(cleanMonitorCardSettings.ResolvedBubbleStyle == BubbleVisualStyle.MonitorCardClean, "clean monitor-card setting normalization");
Check(cleanMonitorCardSettings.Clone().ResolvedBubbleStyle == BubbleVisualStyle.MonitorCardClean, "clean monitor-card setting clone");
var lightBubbleColors = PetForm.ResolveBubbleColors(BubbleColorTheme.Light);
var darkBubbleColors = PetForm.ResolveBubbleColors(BubbleColorTheme.Dark);
Check(lightBubbleColors.Background != darkBubbleColors.Background, "light and dark bubble palettes differ");
Check(darkBubbleColors.Text.GetBrightness() > darkBubbleColors.Background.GetBrightness(), "dark bubble text contrast");
Check(DarkUiTheme.Foreground.GetBrightness() > DarkUiTheme.WindowBackground.GetBrightness(), "dark settings contrast");
Check(DarkUiTheme.MenuSelection != DarkUiTheme.MenuBackground, "dark menu selection contrast");
using (var darkMenu = new ContextMenuStrip())
{
    var submenu = new ToolStripMenuItem("Parent");
    submenu.DropDownItems.Add(new ToolStripMenuItem("Child"));
    darkMenu.Items.Add(submenu);
    DarkUiTheme.ApplyMenu(darkMenu);
    Check(darkMenu.BackColor == DarkUiTheme.MenuBackground, "dark context menu background");
    Check(submenu.DropDown.BackColor == DarkUiTheme.MenuBackground, "dark submenu background");
}
using (var darkForm = new Form())
{
    var input = new ComboBox();
    var button = new Button();
    darkForm.Controls.Add(input);
    darkForm.Controls.Add(button);
    DarkUiTheme.ApplyWindow(darkForm);
    Check(darkForm.BackColor == DarkUiTheme.WindowBackground, "dark settings window background");
    Check(input.BackColor == DarkUiTheme.InputBackground, "dark settings input background");
    Check(button.BackColor == DarkUiTheme.ButtonBackground, "dark settings button background");
}

var updateUiSettings = new AppSettings { Language = "English" };
var updateUiStore = new SettingsStore(updateUiSettings);
var updateUiLocalizer = new Localizer(() => updateUiStore.Current);
var updateUiSnapshot = DevSpaceSnapshot.Initial("config.json", "serve.log", 7676);
using (var settingsForm = new SettingsForm(updateUiStore, updateUiLocalizer, updateUiSnapshot))
{
    settingsForm.SetUpdateStatus("0.1.2", updateUiLocalizer["UpToDate"]);
    Check(settingsForm.Controls.Find("CheckUpdatesButton", true).Length == 1, "settings update-check button");
    Check(settingsForm.Controls.Find("CheckUpdatesOnStartupInput", true).Length == 1, "settings startup update option");
    Check(settingsForm.Controls.Find("IncludePrereleaseUpdatesInput", true).Length == 1, "settings prerelease option");
    Check(settingsForm.Controls.Find("BubbleStyleInput", true).Length == 1, "settings bubble-design option");
    var languageInput = settingsForm.Controls.Find("LanguageInput", true).OfType<ComboBox>().Single();
    Check(languageInput.Items.Count == 4, "settings simplified Chinese option");
}
using (var updateUiService = new UpdateService("0.1.1"))
using (var updateForm = new UpdateForm(
           updateUiService,
           new UpdateRelease(
               "0.1.2",
               "v0.1.2",
               "DevSpace Status Pet v0.1.2",
               "https://example.test/v0.1.2",
               "Release notes",
               false,
               DateTimeOffset.Now,
               "https://example.test/update.zip",
               "https://example.test/update.sha256",
               123),
           updateUiLocalizer))
{
    Check(updateForm.BackColor == DarkUiTheme.WindowBackground, "dark update window background");
    Check(updateForm.Text == updateUiLocalizer["UpdateAvailableTitle"], "update window localization");
}

var current = new AppSettings { Language = "English" };
var localizer = new Localizer(() => current);
Check(localizer.State(ActivityState.Working) == "Working", "English localization");
current.Language = "Japanese";
Check(localizer.State(ActivityState.Working) == "作業中", "Japanese localization");
Check(localizer["ShowRecoverPet"] == "ペットを表示／復旧", "pet recovery menu localization");
current.Language = "ChineseSimplified";
Check(localizer.State(ActivityState.Working) == "工作中", "simplified Chinese localization");
Check(localizer["ShowRecoverPet"] == "显示／恢复宠物", "simplified Chinese recovery menu localization");
Check(localizer["CheckUpdates"] == "检查更新", "simplified Chinese updater localization");
Check(localizer.Get("InstalledMessage", "C:\\Test").Contains("已安装", StringComparison.Ordinal), "simplified Chinese installer localization");
Check(localizer.Get("ApplicationError", "错误", "crash.log").Contains("遇到错误", StringComparison.Ordinal), "simplified Chinese error localization");
Check(Localizer.HasCompleteCatalogs, "complete localization catalogs");
var chineseReadmePath = Path.Combine(Environment.CurrentDirectory, "README.zh-CN.md");
var chinesePackageReadmePath = Path.Combine(Environment.CurrentDirectory, "README.dotnet.zh-CN.md");
Check(
    File.Exists(chineseReadmePath) && File.ReadAllText(chineseReadmePath).Contains("简体中文", StringComparison.Ordinal),
    "GitHub simplified Chinese README");
Check(
    File.Exists(chinesePackageReadmePath) && File.ReadAllText(chinesePackageReadmePath).Contains("简体中文", StringComparison.Ordinal),
    "package simplified Chinese README");
var originalUiCulture = System.Globalization.CultureInfo.CurrentUICulture;
try
{
    System.Globalization.CultureInfo.CurrentUICulture = new System.Globalization.CultureInfo("zh-CN");
    Check(
        Localizer.Resolve(UiLanguagePreference.Auto) == UiLanguage.ChineseSimplified,
        "Chinese OS language auto detection");
}
finally
{
    System.Globalization.CultureInfo.CurrentUICulture = originalUiCulture;
}
using (var chineseMenuPet = new PetForm(
           new SettingsStore(new AppSettings { Language = "ChineseSimplified" }),
           new PositionStore(null),
           new Localizer(() => new AppSettings { Language = "ChineseSimplified" })))
{
    chineseMenuPet.ApplySnapshot(DevSpaceSnapshot.Initial("config.json", "serve.log", 7676));
    var hasChineseChoice = chineseMenuPet.ContextMenuStrip?.Items
        .OfType<ToolStripMenuItem>()
        .SelectMany(item => item.DropDownItems.OfType<ToolStripMenuItem>())
        .Any(item => item.Text == "简体中文") == true;
    Check(hasChineseChoice, "pet menu simplified Chinese option");
}
Check(AppPaths.RuntimeLogPath.EndsWith("runtime.log", StringComparison.OrdinalIgnoreCase), "runtime diagnostics path");
Check(!LayeredWindowRenderer.IsCloaked(IntPtr.Zero), "zero-handle cloak check");
Check(!LayeredWindowRenderer.IsTopMost(IntPtr.Zero), "zero-handle topmost check");
using (var topMostPet = new PetForm(
           new SettingsStore(new AppSettings()),
           new PositionStore(null),
           new Localizer(() => new AppSettings())))
{
    topMostPet.Show();
    Application.DoEvents();
    Check(LayeredWindowRenderer.IsTopMost(topMostPet.Handle), "native topmost initial state");
    NativeWindowTest.SetTopMost(topMostPet.Handle, enabled: false);
    Application.DoEvents();
    Check(topMostPet.TopMost && !LayeredWindowRenderer.IsTopMost(topMostPet.Handle), "native topmost loss simulation");
    topMostPet.VerifyVisibility("smoke-native-topmost");
    Application.DoEvents();
    Check(LayeredWindowRenderer.IsTopMost(topMostPet.Handle), "native topmost watchdog recovery");
    topMostPet.Close();
}

var tempRoot = Path.Combine(Path.GetTempPath(), $"DevSpaceStatusPet-Smoke-{Environment.ProcessId}");
Directory.CreateDirectory(tempRoot);
try
{
    var logPath = Path.Combine(tempRoot, "serve.log");
    var workspaceId = "ws-smoke";
    var timestamp = DateTimeOffset.Now.ToString("O");
    var lines = new[]
    {
        JsonSerializer.Serialize(new
        {
            @event = "tool_call",
            tool = "open_workspace",
            workspaceId,
            path = @"D:\Dev Work\Alpha Project",
            ts = timestamp,
            durationMs = 10,
            success = true
        }),
        JsonSerializer.Serialize(new
        {
            @event = "tool_call",
            tool = "read",
            workspaceId,
            path = @"src\Program.cs",
            ts = timestamp,
            durationMs = 5,
            success = true
        })
    };
    File.WriteAllLines(logPath, lines);

    var snapshot = new DevSpaceLogReader().Read(logPath, [@"D:\Dev Work"]);
    Check(snapshot.LastTool is not null, "log parsing");
    Check(snapshot.LastTool?.ProjectName == "Alpha Project", "portable project detection");
    Check(snapshot.LastTool?.Operation == OperationKind.Read, "tool operation mapping");
}
finally
{
    Directory.Delete(tempRoot, true);
}

var largeLogRoot = Path.Combine(Path.GetTempPath(), $"DevSpaceStatusPet-LargeLog-{Environment.ProcessId}");
Directory.CreateDirectory(largeLogRoot);
try
{
    var logPath = Path.Combine(largeLogRoot, "serve.log");
    var timestamp = DateTimeOffset.Now;
    using (var writer = new StreamWriter(logPath, false, new System.Text.UnicodeEncoding(false, true)))
    {
        writer.WriteLine(JsonSerializer.Serialize(new
        {
            @event = "tool_call",
            tool = "open_workspace",
            workspaceId = "ws-alpha",
            path = @"D:\Dev Work\Alpha Project",
            ts = timestamp.AddMinutes(-20).ToString("O"),
            success = true
        }));
        writer.WriteLine(JsonSerializer.Serialize(new
        {
            @event = "tool_call",
            tool = "open_workspace",
            workspaceId = "ws-beta",
            path = @"D:\Dev Work\Beta Project",
            ts = timestamp.AddMinutes(-20).ToString("O"),
            success = true
        }));
        var filler = new string('x', 1024);
        for (var index = 0; index < 4300; index++)
        {
            writer.WriteLine(filler);
        }
        writer.WriteLine(JsonSerializer.Serialize(new
        {
            @event = "tool_call",
            tool = "bash",
            workspaceId = "ws-alpha",
            workingDirectory = string.Empty,
            ts = timestamp.AddSeconds(-2).ToString("O"),
            success = true
        }));
        writer.WriteLine(JsonSerializer.Serialize(new
        {
            @event = "tool_call",
            tool = "bash",
            workspaceId = "ws-beta",
            workingDirectory = string.Empty,
            ts = timestamp.AddSeconds(-3).ToString("O"),
            success = true
        }));
    }

    var snapshot = new DevSpaceLogReader().Read(logPath, [@"D:\Dev Work"]);
    Check(snapshot.RecentTools.Count == 2, "UTF-16 large-log parallel workspace recovery");
    Check(snapshot.RecentTools.Any(tool => tool.ProjectName == "Alpha Project"), "old Alpha workspace identity recovery");
    Check(snapshot.RecentTools.Any(tool => tool.ProjectName == "Beta Project"), "old Beta workspace identity recovery");
    Check(snapshot.RecentTools.All(tool => !tool.ProjectName.Equals("Unknown", StringComparison.OrdinalIgnoreCase)), "no Unknown project labels");
}
finally
{
    Directory.Delete(largeLogRoot, true);
}

var fallbackRoot = Path.Combine(Path.GetTempPath(), $"DevSpaceStatusPet-Fallback-{Environment.ProcessId}");
Directory.CreateDirectory(fallbackRoot);
try
{
    var logPath = Path.Combine(fallbackRoot, "serve.log");
    File.WriteAllText(logPath, JsonSerializer.Serialize(new
    {
        @event = "tool_call",
        tool = "bash",
        workspaceId = "ws-1234567890",
        workingDirectory = string.Empty,
        ts = DateTimeOffset.Now.ToString("O"),
        success = true
    }));
    var snapshot = new DevSpaceLogReader().Read(logPath, Array.Empty<string>());
    Check(snapshot.LastTool?.ProjectName == "Workspace ws-12345", "workspace label fallback");
}
finally
{
    Directory.Delete(fallbackRoot, true);
}

var parallelNow = DateTimeOffset.Now;
var activeWorkspace = new DevSpaceActivity(
    "process:1",
    "SharedProject",
    ActivityState.Working,
    OperationKind.Command,
    null,
    parallelNow.AddSeconds(-5),
    TimeSpan.FromSeconds(5),
    false,
    true,
    "workspace-active");
var sameProjectTools = new[]
{
    new ToolEvent("workspace-active", "bash", "SharedProject", OperationKind.Command, null, parallelNow.AddSeconds(-2), true, 10),
    new ToolEvent("workspace-other", "read", "SharedProject", OperationKind.Read, "README.md", parallelNow.AddSeconds(-3), true, 5)
};
var parallelActivities = DevSpaceMonitor.BuildRecentActivities(
    sameProjectTools,
    [activeWorkspace],
    45,
    parallelNow);
Check(parallelActivities.Count == 1, "parallel workspace preservation");
Check(parallelActivities.SingleOrDefault()?.WorkspaceId == "workspace-other", "same-project workspace identity");

var waitingBeforeQuietThreshold = DevSpaceMonitor.BuildRecentActivities(
    [new ToolEvent("workspace-waiting", "read", "FinishedProject", OperationKind.Read, "README.md", parallelNow.AddSeconds(-44), true, 5)],
    Array.Empty<DevSpaceActivity>(),
    45,
    parallelNow);
var waitingAtQuietThreshold = DevSpaceMonitor.BuildRecentActivities(
    [new ToolEvent("workspace-waiting", "read", "FinishedProject", OperationKind.Read, "README.md", parallelNow.AddSeconds(-45), true, 5)],
    Array.Empty<DevSpaceActivity>(),
    45,
    parallelNow);
Check(waitingBeforeQuietThreshold.Count == 1, "waiting bubble remains before quiet threshold");
Check(waitingAtQuietThreshold.Count == 0, "waiting bubble expires at quiet threshold");

var defaultLayout = PetForm.CalculateClientSize(new AppSettings(), 1);
var largeLayout = PetForm.CalculateClientSize(new AppSettings { Scale = 2.0 }, 1);
var parallelLayout = PetForm.CalculateClientSize(new AppSettings(), 3);
var neonMonitorCardLayout = PetForm.CalculateClientSize(new AppSettings { BubbleStyle = "MonitorCardNeon" }, 3);
var cleanMonitorCardLayout = PetForm.CalculateClientSize(new AppSettings { BubbleStyle = "MonitorCardClean" }, 3);
Check(defaultLayout.Width >= 340 && defaultLayout.Height >= 360, "larger default pet layout");
Check(largeLayout.Width > defaultLayout.Width && largeLayout.Height > defaultLayout.Height, "scale changes layout size");
Check(parallelLayout.Height > defaultLayout.Height + 150, "parallel bubbles expand window");
Check(neonMonitorCardLayout.Height > parallelLayout.Height, "neon monitor cards use expanded information layout");
Check(cleanMonitorCardLayout == neonMonitorCardLayout, "clean monitor cards preserve monitor layout");
var workingAreas = new[]
{
    new Rectangle(0, 0, 1920, 1040),
    new Rectangle(1920, 200, 1280, 1024)
};
var recoveredPosition = PetForm.ClampPosition(new Rectangle(5000, 3000, 300, 400), workingAreas);
Check(recoveredPosition == new Point(1620, 640), "off-screen pet position recovery");
var secondaryPosition = PetForm.ClampPosition(new Rectangle(2200, 400, 300, 400), workingAreas);
Check(secondaryPosition == new Point(2200, 400), "visible secondary-monitor position preservation");
var cleanCardBackground = PetForm.ResolveCleanCardBackground(BubbleColorTheme.Dark);
var cleanCardBorder = PetForm.ResolveCleanCardBorder(BubbleColorTheme.Dark);
Check(cleanCardBackground != cleanCardBorder, "clean monitor-card single border contrast");
Check(cleanCardBorder.GetBrightness() > cleanCardBackground.GetBrightness(), "clean monitor-card border remains subtle and visible");

var previewSample = PreviewCapture.CreateSampleSnapshot();
var expectedPreviewProjects = new[] { "Aurora Desktop", "Orbit API", "Nimbus Docs" };
Check(
    previewSample.Activities.Select(activity => activity.ProjectName).SequenceEqual(expectedPreviewProjects),
    "public preview uses fictional sample projects");
var expectedPreviewDetails = new[] { "dotnet test", "StatusPanel.cs", "git push" };
Check(
    previewSample.Activities.Select(activity => activity.Detail).SequenceEqual(expectedPreviewDetails),
    "public preview uses only approved generic operation details");

var renderNow = DateTimeOffset.Now;
var renderActivities = new[]
{
    new DevSpaceActivity(
        "render-working",
        "Aurora Desktop",
        ActivityState.Working,
        OperationKind.Dotnet,
        "dotnet test",
        renderNow.AddMinutes(-2),
        TimeSpan.FromMinutes(2),
        WorkspaceId: "ws-render-working"),
    new DevSpaceActivity(
        "render-waiting",
        "Orbit API",
        ActivityState.Waiting,
        OperationKind.Edit,
        "StatusPanel.cs",
        renderNow.AddSeconds(-18),
        TimeSpan.FromSeconds(18),
        WorkspaceId: "ws-render-waiting")
};
var renderSnapshot = new DevSpaceSnapshot(
    ActivityState.Working,
    renderActivities,
    Environment.ProcessId,
    7676,
    "config.json",
    "serve.log",
    renderNow,
    renderNow,
    true);
var renderSettings = new AppSettings
{
    Language = "English",
    BubbleTheme = "Dark",
    BubbleStyle = "MonitorCardClean",
    Scale = 1.0,
    MaxBubbles = 4
};
var renderStore = new SettingsStore(renderSettings);
var renderLocalizer = new Localizer(() => renderStore.Current);
using (var renderPet = new PetForm(renderStore, new PositionStore(null), renderLocalizer))
{
    renderPet.ApplySnapshot(renderSnapshot);
    using var renderedCard = renderPet.RenderPreview(Color.FromArgb(18, 20, 26));
    var sampledColors = new HashSet<int>();
    for (var y = 0; y < renderedCard.Height; y += 8)
    {
        for (var x = 0; x < renderedCard.Width; x += 8)
        {
            sampledColors.Add(renderedCard.GetPixel(x, y).ToArgb());
        }
    }
    Check(sampledColors.Count >= 20, "clean monitor-card visual rendering");

    using var transparentLayer = renderPet.RenderTransparentPreview();
    var visiblePixels = 0;
    var antialiasedPixels = 0;
    var magentaFringePixels = 0;
    for (var y = 0; y < transparentLayer.Height; y++)
    {
        for (var x = 0; x < transparentLayer.Width; x++)
        {
            var pixel = transparentLayer.GetPixel(x, y);
            if (pixel.A == 0)
            {
                continue;
            }

            visiblePixels++;
            if (pixel.A < byte.MaxValue)
            {
                antialiasedPixels++;
            }
            if (pixel.R > 170 && pixel.B > 170 && pixel.G < 110)
            {
                magentaFringePixels++;
            }
        }
    }
    Check(visiblePixels > 1000, "per-pixel alpha visible surface");
    Check(antialiasedPixels > 100, "per-pixel alpha antialiased edges");
    Check(magentaFringePixels == 0, "no transparency-key magenta fringe");

    renderSettings.BubbleStyle = "MonitorCardNeon";
    renderStore.Save(renderSettings);
    using var renderedNeonCard = renderPet.RenderPreview(Color.FromArgb(18, 20, 26));
    var neonSampledColors = new HashSet<int>();
    for (var y = 0; y < renderedNeonCard.Height; y += 8)
    {
        for (var x = 0; x < renderedNeonCard.Width; x += 8)
        {
            neonSampledColors.Add(renderedNeonCard.GetPixel(x, y).ToArgb());
        }
    }
    Check(neonSampledColors.Count >= 20, "neon monitor-card visual rendering");
}

var boundarySettings = new AppSettings
{
    Theme = "invalid-theme",
    BubbleTheme = "invalid-bubble-theme",
    BubbleStyle = "invalid-bubble-style",
    Language = "invalid-language",
    Scale = -10,
    Opacity = 5,
    CompletionQuietSeconds = 1,
    StallMinutes = 999,
    MaxBubbles = 99,
    LastNotifiedUpdateVersion = "  0.1.4  "
};
boundarySettings.Normalize();
Check(boundarySettings.ResolvedTheme == PetTheme.Classic, "invalid pet theme fallback");
Check(boundarySettings.ResolvedBubbleTheme == BubbleColorTheme.Light, "invalid bubble theme fallback");
Check(boundarySettings.ResolvedBubbleStyle == BubbleVisualStyle.Speech, "invalid bubble style fallback");
Check(boundarySettings.LanguagePreference == UiLanguagePreference.Auto, "invalid language fallback");
Check(Math.Abs(boundarySettings.Scale - 0.6) < 0.001, "minimum scale clamp");
Check(Math.Abs(boundarySettings.Opacity - 1.0) < 0.001, "maximum opacity clamp");
Check(boundarySettings.CompletionQuietSeconds == 10, "minimum quiet-time clamp");
Check(boundarySettings.StallMinutes == 240, "maximum stall-time clamp");
Check(boundarySettings.MaxBubbles == 8, "maximum bubble-count clamp");
Check(boundarySettings.LastNotifiedUpdateVersion == "0.1.4", "update-version trimming");

var renderMatrixFailures = new List<string>();
var renderMatrixCount = 0;
foreach (var theme in Enum.GetValues<PetTheme>())
{
    foreach (var bubbleTheme in Enum.GetValues<BubbleColorTheme>())
    {
        foreach (var bubbleStyle in Enum.GetValues<BubbleVisualStyle>())
        {
            foreach (var scale in new[] { 0.6, 1.15, 2.5 })
            {
                renderMatrixCount++;
                try
                {
                    var matrixSettings = new AppSettings
                    {
                        Theme = theme.ToString(),
                        BubbleTheme = bubbleTheme.ToString(),
                        BubbleStyle = bubbleStyle.ToString(),
                        Language = UiLanguagePreference.English.ToString(),
                        Scale = scale,
                        MaxBubbles = 8
                    };
                    var matrixStore = new SettingsStore(matrixSettings);
                    using var matrixPet = new PetForm(
                        matrixStore,
                        new PositionStore(null),
                        new Localizer(() => matrixStore.Current));
                    matrixPet.ApplySnapshot(renderSnapshot);
                    using var matrixBitmap = matrixPet.RenderTransparentPreview();
                    var expectedSize = PetForm.CalculateFittedClientSize(
                        matrixSettings,
                        renderActivities.Length,
                        Screen.FromRectangle(matrixPet.Bounds).WorkingArea.Size);
                    if (matrixBitmap.Size != expectedSize)
                    {
                        renderMatrixFailures.Add($"{theme}/{bubbleTheme}/{bubbleStyle}/{scale}: size {matrixBitmap.Size} != {expectedSize}");
                        continue;
                    }

                    var hasVisibleSample = false;
                    for (var y = 0; y < matrixBitmap.Height && !hasVisibleSample; y += Math.Max(1, matrixBitmap.Height / 24))
                    {
                        for (var x = 0; x < matrixBitmap.Width; x += Math.Max(1, matrixBitmap.Width / 24))
                        {
                            if (matrixBitmap.GetPixel(x, y).A > 0)
                            {
                                hasVisibleSample = true;
                                break;
                            }
                        }
                    }
                    if (!hasVisibleSample)
                    {
                        renderMatrixFailures.Add($"{theme}/{bubbleTheme}/{bubbleStyle}/{scale}: no visible sample");
                    }
                }
                catch (Exception exception)
                {
                    renderMatrixFailures.Add($"{theme}/{bubbleTheme}/{bubbleStyle}/{scale}: {exception.Message}");
                }
            }
        }
    }
}
foreach (var matrixFailure in renderMatrixFailures)
{
    Console.WriteLine($"[INFO] render matrix failure: {matrixFailure}");
}
Check(renderMatrixCount == 36 && renderMatrixFailures.Count == 0, "36-combination rendering matrix");

var lowResolutionSettings = new AppSettings
{
    Scale = 2.5,
    BubbleStyle = BubbleVisualStyle.MonitorCardClean.ToString(),
    MaxBubbles = 8
};
var lowResolutionRawSize = PetForm.CalculateClientSize(lowResolutionSettings, renderActivities.Length);
var lowResolutionFittedSize = PetForm.CalculateFittedClientSize(
    lowResolutionSettings,
    renderActivities.Length,
    new Size(1024, 768));
Check(
    lowResolutionFittedSize.Width <= 1008 && lowResolutionFittedSize.Height <= 752,
    "low-resolution layout fits working area");
Check(
    lowResolutionFittedSize.Width < lowResolutionRawSize.Width ||
    lowResolutionFittedSize.Height < lowResolutionRawSize.Height,
    "oversized layout reduces effective scale");
Check(
    Math.Abs(PetForm.CalculateEffectiveScale(
        new AppSettings { Scale = 1.15 },
        1,
        new Size(1920, 1080)) - 1.15) < 0.001,
    "normal layout preserves requested scale");

var inspector = new NativeProcessInspector();
Check(inspector.FindListeningProcessId(65534) is null, "unused port lookup");

var liveConfigurationLoader = new DevSpaceConfigurationLoader();
var liveConfiguration = liveConfigurationLoader.Load();
var liveLogFile = new FileInfo(liveConfiguration.LogPath);
Console.WriteLine($"[INFO] live log path={liveConfiguration.LogPath}, exists={liveLogFile.Exists}, bytes={(liveLogFile.Exists ? liveLogFile.Length : 0)}, roots={string.Join(" | ", liveConfiguration.AllowedRoots)}");
var liveLog = new DevSpaceLogReader().Read(liveConfiguration.LogPath, liveConfiguration.AllowedRoots);
Console.WriteLine($"[INFO] live log workspaces={liveLog.RecentTools.Count}: {string.Join(" | ", liveLog.RecentTools.Select(tool => $"{tool.WorkspaceId}={tool.ProjectName}@{tool.Timestamp:HH:mm:ss}"))}");
var monitor = new DevSpaceMonitor(
    liveConfigurationLoader,
    inspector,
    new DevSpaceLogReader(),
    () => new AppSettings());
var liveSnapshot = monitor.Capture();
Check(liveSnapshot.Port is > 0 and <= 65535, "live configuration capture");
Console.WriteLine($"[INFO] live state={liveSnapshot.State}, activities={liveSnapshot.Activities.Count}, port={liveSnapshot.Port}: {string.Join(" | ", liveSnapshot.Activities.Select(activity => $"{activity.Id}={activity.ProjectName}/{activity.State}/{activity.WorkspaceId}"))}");

if (failures.Count > 0)
{
    Console.Error.WriteLine($"Smoke test failed: {string.Join(", ", failures)}");
    return 1;
}

Console.WriteLine("[OK] DevSpace Status Pet .NET smoke test");
return 0;

internal static class NativeWindowTest
{
    private static readonly IntPtr TopMost = new(-1);
    private static readonly IntPtr NotTopMost = new(-2);
    private const uint NoMoveNoSizeNoActivate = 0x0001 | 0x0002 | 0x0010;

    public static void SetTopMost(IntPtr windowHandle, bool enabled)
    {
        if (!SetWindowPos(
                windowHandle,
                enabled ? TopMost : NotTopMost,
                0,
                0,
                0,
                0,
                NoMoveNoSizeNoActivate))
        {
            throw new InvalidOperationException("Could not change the test window topmost state.");
        }
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetWindowPos(
        IntPtr windowHandle,
        IntPtr insertAfter,
        int x,
        int y,
        int width,
        int height,
        uint flags);
}

internal sealed class FakeHttpMessageHandler : HttpMessageHandler
{
    private readonly IReadOnlyDictionary<string, byte[]> _responses;

    public FakeHttpMessageHandler(IReadOnlyDictionary<string, byte[]> responses)
    {
        _responses = responses;
    }

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var url = request.RequestUri?.ToString() ?? string.Empty;
        if (!_responses.TryGetValue(url, out var bytes))
        {
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
        }

        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new ByteArrayContent(bytes)
        });
    }
}
