using System.Text.Json;
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
    120,
    parallelNow);
Check(parallelActivities.Count == 1, "parallel workspace preservation");
Check(parallelActivities.SingleOrDefault()?.WorkspaceId == "workspace-other", "same-project workspace identity");

var defaultLayout = PetForm.CalculateClientSize(new AppSettings(), 1);
var largeLayout = PetForm.CalculateClientSize(new AppSettings { Scale = 2.0 }, 1);
var parallelLayout = PetForm.CalculateClientSize(new AppSettings(), 3);
Check(defaultLayout.Width >= 340 && defaultLayout.Height >= 360, "larger default pet layout");
Check(largeLayout.Width > defaultLayout.Width && largeLayout.Height > defaultLayout.Height, "scale changes layout size");
Check(parallelLayout.Height > defaultLayout.Height + 150, "parallel bubbles expand window");

var inspector = new NativeProcessInspector();
Check(inspector.FindListeningProcessId(65534) is null, "unused port lookup");

var monitor = new DevSpaceMonitor(
    new DevSpaceConfigurationLoader(),
    inspector,
    new DevSpaceLogReader(),
    () => new AppSettings());
var liveSnapshot = monitor.Capture();
Check(liveSnapshot.Port is > 0 and <= 65535, "live configuration capture");
Console.WriteLine($"[INFO] live state={liveSnapshot.State}, activities={liveSnapshot.Activities.Count}, port={liveSnapshot.Port}");

if (failures.Count > 0)
{
    Console.Error.WriteLine($"Smoke test failed: {string.Join(", ", failures)}");
    return 1;
}

Console.WriteLine("[OK] DevSpace Status Pet .NET smoke test");
return 0;
