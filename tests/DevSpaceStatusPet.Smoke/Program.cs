using System.Text.Json;
using DevSpaceStatusPet.Models;
using DevSpaceStatusPet.Services;

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
Check(migrated.Scale == 1.0 && migrated.MaxBubbles == 4, "new settings defaults");

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
