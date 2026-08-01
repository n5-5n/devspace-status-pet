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
Check(migrated.ResolvedBubbleTheme == BubbleColorTheme.Light, "v0.2 bubble theme migration default");
Check(Math.Abs(migrated.Scale - 1.15) < 0.001 && migrated.MaxBubbles == 4, "new settings defaults");

var darkBubbleSettings = new AppSettings { BubbleTheme = "Dark" };
darkBubbleSettings.Normalize();
Check(darkBubbleSettings.ResolvedBubbleTheme == BubbleColorTheme.Dark, "dark bubble setting normalization");
Check(darkBubbleSettings.Clone().ResolvedBubbleTheme == BubbleColorTheme.Dark, "dark bubble setting clone");
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

var current = new AppSettings { Language = "English" };
var localizer = new Localizer(() => current);
Check(localizer.State(ActivityState.Working) == "Working", "English localization");
current.Language = "Japanese";
Check(localizer.State(ActivityState.Working) == "作業中", "Japanese localization");

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
Check(defaultLayout.Width >= 340 && defaultLayout.Height >= 360, "larger default pet layout");
Check(largeLayout.Width > defaultLayout.Width && largeLayout.Height > defaultLayout.Height, "scale changes layout size");
Check(parallelLayout.Height > defaultLayout.Height + 150, "parallel bubbles expand window");

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
