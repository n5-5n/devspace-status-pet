using DevSpaceStatusPet.Models;

namespace DevSpaceStatusPet.Services;

public sealed class DevSpaceMonitor
{
    private readonly DevSpaceConfigurationLoader _configurationLoader;
    private readonly NativeProcessInspector _processInspector;
    private readonly DevSpaceLogReader _logReader;
    private readonly Func<AppSettings> _settingsProvider;
    private readonly object _sync = new();

    private ActivityState _previousState = ActivityState.Idle;
    private TimeSpan _previousCpu = TimeSpan.Zero;
    private DateTime _previousLogWriteUtc = DateTime.MinValue;
    private DateTimeOffset _lastProgressAt = DateTimeOffset.Now;
    private DateTimeOffset _workingStartedAt = DateTimeOffset.Now;

    public DevSpaceMonitor(
        DevSpaceConfigurationLoader configurationLoader,
        NativeProcessInspector processInspector,
        DevSpaceLogReader logReader,
        Func<AppSettings> settingsProvider)
    {
        _configurationLoader = configurationLoader;
        _processInspector = processInspector;
        _logReader = logReader;
        _settingsProvider = settingsProvider;
    }

    public DevSpaceSnapshot Capture()
    {
        lock (_sync)
        {
            var now = DateTimeOffset.Now;
            var settings = _settingsProvider();
            var configuration = _configurationLoader.Load();
            var log = _logReader.Read(configuration.LogPath, configuration.AllowedRoots);
            var serverPid = _processInspector.FindListeningProcessId(configuration.Port);

            if (serverPid is null)
            {
                ResetProgress(now);
                _previousState = ActivityState.Stopped;
                return new DevSpaceSnapshot(
                    ActivityState.Stopped,
                    Array.Empty<DevSpaceActivity>(),
                    null,
                    configuration.Port,
                    configuration.ConfigPath,
                    configuration.LogPath,
                    now,
                    log.LastTool?.Timestamp,
                    log.LastTool?.Success ?? true);
            }

            var groups = _processInspector.GetDescendantGroups(serverPid.Value);
            var active = BuildActiveActivities(groups, log.RecentTools, configuration.AllowedRoots, now);
            var recent = BuildRecentActivities(
                log.RecentTools,
                active,
                Math.Max(settings.CompletionQuietSeconds, 120),
                now);
            var activities = active.Concat(recent)
                .OrderByDescending(activity => activity.State == ActivityState.Working)
                .ThenByDescending(activity => activity.StartedAt)
                .ToArray();

            var state = DetermineOverallState(active, log.LastTool, now);
            if (state == ActivityState.Working)
            {
                state = ApplyStallDetection(groups, log.LastWriteTimeUtc, settings.StallMinutes, now);
                if (state == ActivityState.Stalled)
                {
                    activities = activities
                        .Select(activity => activity.State == ActivityState.Working
                            ? activity with { State = ActivityState.Stalled }
                            : activity)
                        .ToArray();
                }
            }
            else
            {
                ResetProgress(now);
            }

            _previousState = state;
            return new DevSpaceSnapshot(
                state,
                activities,
                serverPid,
                configuration.Port,
                configuration.ConfigPath,
                configuration.LogPath,
                now,
                log.LastTool?.Timestamp,
                log.LastTool?.Success ?? true);
        }
    }

    private static IReadOnlyList<DevSpaceActivity> BuildActiveActivities(
        IReadOnlyList<ProcessGroup> groups,
        IReadOnlyList<ToolEvent> recentTools,
        IReadOnlyList<string> allowedRoots,
        DateTimeOffset now)
    {
        var unusedTools = new Queue<ToolEvent>(recentTools.OrderByDescending(item => item.Timestamp));
        var result = new List<DevSpaceActivity>();

        foreach (var group in groups)
        {
            ToolEvent? fallback = unusedTools.Count > 0 ? unusedTools.Dequeue() : null;
            var project = InferProjectFromProcesses(group.Processes, allowedRoots) ?? fallback?.ProjectName ?? "Unknown";
            var (operation, detail) = ClassifyOperation(group.Processes);
            var startedAt = group.Processes
                .Where(process => process.StartedAt.HasValue)
                .Select(process => process.StartedAt!.Value)
                .DefaultIfEmpty(now)
                .Min();

            result.Add(new DevSpaceActivity(
                $"process:{group.RootProcessId}",
                project,
                ActivityState.Working,
                operation,
                detail,
                startedAt,
                now - startedAt,
                project.Equals("Unknown", StringComparison.OrdinalIgnoreCase),
                true,
                fallback?.WorkspaceId));
        }

        return result;
    }

    internal static IReadOnlyList<DevSpaceActivity> BuildRecentActivities(
        IReadOnlyList<ToolEvent> recentTools,
        IReadOnlyList<DevSpaceActivity> active,
        int windowSeconds,
        DateTimeOffset now)
    {
        var activeWorkspaceIds = active
            .Select(item => item.WorkspaceId)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var unmatchedActiveProjectCounts = active
            .Where(item => string.IsNullOrWhiteSpace(item.WorkspaceId))
            .GroupBy(item => item.ProjectName, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.OrdinalIgnoreCase);
        var seenWorkspaceIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var result = new List<DevSpaceActivity>();

        foreach (var tool in recentTools.OrderByDescending(item => item.Timestamp))
        {
            var age = now - tool.Timestamp;
            if (age < TimeSpan.Zero || age > TimeSpan.FromSeconds(windowSeconds))
            {
                continue;
            }

            if (!string.IsNullOrWhiteSpace(tool.WorkspaceId))
            {
                if (!seenWorkspaceIds.Add(tool.WorkspaceId))
                {
                    continue;
                }
                if (activeWorkspaceIds.Contains(tool.WorkspaceId))
                {
                    continue;
                }
            }
            else if (unmatchedActiveProjectCounts.TryGetValue(tool.ProjectName, out var count) && count > 0)
            {
                unmatchedActiveProjectCounts[tool.ProjectName] = count - 1;
                continue;
            }

            result.Add(new DevSpaceActivity(
                $"workspace:{tool.WorkspaceId}",
                tool.ProjectName,
                tool.Success ? ActivityState.Waiting : ActivityState.Failed,
                tool.Operation,
                tool.Detail,
                tool.Timestamp,
                age,
                false,
                tool.Success,
                tool.WorkspaceId));
        }

        return result;
    }

    private static ActivityState DetermineOverallState(
        IReadOnlyList<DevSpaceActivity> active,
        ToolEvent? lastTool,
        DateTimeOffset now)
    {
        if (active.Count > 0)
        {
            return ActivityState.Working;
        }

        if (lastTool is not null && now - lastTool.Timestamp < TimeSpan.FromSeconds(12))
        {
            return lastTool.Success ? ActivityState.Waiting : ActivityState.Failed;
        }

        return ActivityState.Idle;
    }

    private ActivityState ApplyStallDetection(
        IReadOnlyList<ProcessGroup> groups,
        DateTime logWriteTimeUtc,
        int stallMinutes,
        DateTimeOffset now)
    {
        var totalCpu = groups
            .SelectMany(group => group.Processes)
            .Aggregate(TimeSpan.Zero, (total, process) => total + process.CpuTime);

        if (_previousState != ActivityState.Working && _previousState != ActivityState.Stalled)
        {
            _workingStartedAt = now;
            _lastProgressAt = now;
            _previousCpu = totalCpu;
            _previousLogWriteUtc = logWriteTimeUtc;
            return ActivityState.Working;
        }

        if (totalCpu - _previousCpu > TimeSpan.FromMilliseconds(50) || logWriteTimeUtc != _previousLogWriteUtc)
        {
            _lastProgressAt = now;
        }

        _previousCpu = totalCpu;
        _previousLogWriteUtc = logWriteTimeUtc;
        var threshold = TimeSpan.FromMinutes(stallMinutes);
        return now - _workingStartedAt >= threshold && now - _lastProgressAt >= threshold
            ? ActivityState.Stalled
            : ActivityState.Working;
    }

    private void ResetProgress(DateTimeOffset now)
    {
        _workingStartedAt = now;
        _lastProgressAt = now;
        _previousCpu = TimeSpan.Zero;
        _previousLogWriteUtc = DateTime.MinValue;
    }

    private static (OperationKind Kind, string? Detail) ClassifyOperation(IReadOnlyList<ProcessEntry> processes)
    {
        var names = processes.Select(process => process.Name).ToArray();
        string? Match(params string[] candidates) => names.FirstOrDefault(name =>
            candidates.Any(candidate => name.Contains(candidate, StringComparison.OrdinalIgnoreCase)));

        if (Match("ffmpeg", "ffprobe") is { } ffmpeg)
        {
            return (OperationKind.Ffmpeg, ffmpeg);
        }
        if (Match("dotnet", "msbuild") is { } dotnet)
        {
            return (OperationKind.Dotnet, dotnet.Equals("msbuild", StringComparison.OrdinalIgnoreCase) ? "MSBuild" : "dotnet");
        }
        if (Match("git") is not null)
        {
            return (OperationKind.Git, "git");
        }
        if (Match("python", "py") is not null)
        {
            return (OperationKind.Python, "Python");
        }
        if (Match("powershell", "pwsh") is not null)
        {
            return (OperationKind.PowerShell, "PowerShell");
        }
        if (Match("cmd", "bash", "sh") is not null)
        {
            return (OperationKind.Command, null);
        }

        var candidate = processes
            .Select(process => process.Name)
            .FirstOrDefault(name => !string.IsNullOrWhiteSpace(name));
        return (OperationKind.LocalProcess, candidate);
    }

    private static string? InferProjectFromProcesses(
        IReadOnlyList<ProcessEntry> processes,
        IReadOnlyList<string> allowedRoots)
    {
        foreach (var process in processes)
        {
            if (string.IsNullOrWhiteSpace(process.ExecutablePath))
            {
                continue;
            }

            var executablePath = Normalize(process.ExecutablePath);
            foreach (var root in allowedRoots.OrderByDescending(root => root.Length))
            {
                var normalizedRoot = Normalize(root).TrimEnd('/') + "/";
                if (!executablePath.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var remainder = executablePath[normalizedRoot.Length..];
                return remainder.Split('/', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
            }
        }

        return null;
    }

    private static string Normalize(string value)
    {
        try { value = Path.GetFullPath(value); } catch { }
        return value.Replace('\\', '/');
    }
}
