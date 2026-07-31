using System.Text;
using System.Text.Json;
using DevSpaceStatusPet.Models;

namespace DevSpaceStatusPet.Services;

public sealed record ToolEvent(
    string WorkspaceId,
    string Tool,
    string ProjectName,
    OperationKind Operation,
    string? Detail,
    DateTimeOffset Timestamp,
    bool Success,
    long DurationMilliseconds);

public sealed record LogSnapshot(
    DateTime LastWriteTimeUtc,
    ToolEvent? LastTool,
    IReadOnlyList<ToolEvent> RecentTools);

public sealed class DevSpaceLogReader
{
    private const int MaximumTailBytes = 4 * 1024 * 1024;
    private readonly object _sync = new();
    private readonly Dictionary<string, string> _workspacePaths = new(StringComparer.OrdinalIgnoreCase);
    private DateTime _cachedWriteTimeUtc = DateTime.MinValue;
    private LogSnapshot _cached = new(DateTime.MinValue, null, Array.Empty<ToolEvent>());

    public LogSnapshot Read(string path, IReadOnlyList<string> allowedRoots)
    {
        lock (_sync)
        {
            try
            {
                if (!File.Exists(path))
                {
                    return new LogSnapshot(DateTime.MinValue, null, Array.Empty<ToolEvent>());
                }

                var fileInfo = new FileInfo(path);
                if (fileInfo.LastWriteTimeUtc == _cachedWriteTimeUtc)
                {
                    return _cached;
                }

                var latestByWorkspace = new Dictionary<string, ToolEvent>(StringComparer.OrdinalIgnoreCase);
                ToolEvent? lastTool = null;

                foreach (var line in ReadTailLines(path))
                {
                    if (string.IsNullOrWhiteSpace(line) || !line.Contains("\"event\"", StringComparison.Ordinal))
                    {
                        continue;
                    }

                    try
                    {
                        using var document = JsonDocument.Parse(line);
                        var root = document.RootElement;
                        if (!TryGetString(root, "event", out var eventName) ||
                            !eventName.Equals("tool_call", StringComparison.OrdinalIgnoreCase))
                        {
                            continue;
                        }

                        TryGetString(root, "tool", out var tool);
                        TryGetString(root, "workspaceId", out var workspaceId);
                        TryGetString(root, "path", out var entryPath);
                        TryGetString(root, "workingDirectory", out var workingDirectory);

                        if (tool.Equals("open_workspace", StringComparison.OrdinalIgnoreCase) &&
                            !string.IsNullOrWhiteSpace(workspaceId) &&
                            !string.IsNullOrWhiteSpace(entryPath))
                        {
                            _workspacePaths[workspaceId] = entryPath;
                        }

                        var workspacePath = !string.IsNullOrWhiteSpace(workspaceId) &&
                                            _workspacePaths.TryGetValue(workspaceId, out var knownPath)
                            ? knownPath
                            : string.Empty;

                        var timestamp = DateTimeOffset.Now;
                        if (TryGetString(root, "ts", out var timestampText))
                        {
                            DateTimeOffset.TryParse(timestampText, out timestamp);
                            if (timestamp == default)
                            {
                                timestamp = DateTimeOffset.Now;
                            }
                        }

                        var duration = TryGetInt64(root, "durationMs", out var durationMs) ? durationMs : 0;
                        var success = !root.TryGetProperty("success", out var successElement) ||
                                      successElement.ValueKind != JsonValueKind.False;

                        var projectName = ResolveProjectName(
                            workspacePath,
                            entryPath,
                            workingDirectory,
                            allowedRoots);
                        var (operation, detail) = ResolveOperation(tool, entryPath, workingDirectory);

                        var record = new ToolEvent(
                            workspaceId,
                            tool,
                            projectName,
                            operation,
                            detail,
                            timestamp.ToLocalTime(),
                            success,
                            duration);
                        lastTool = record;

                        if (!string.IsNullOrWhiteSpace(workspaceId))
                        {
                            latestByWorkspace[workspaceId] = record;
                        }
                    }
                    catch (JsonException)
                    {
                        // The log may be observed while the final line is still being written.
                    }
                }

                _cachedWriteTimeUtc = fileInfo.LastWriteTimeUtc;
                _cached = new LogSnapshot(
                    fileInfo.LastWriteTimeUtc,
                    lastTool,
                    latestByWorkspace.Values
                        .OrderByDescending(item => item.Timestamp)
                        .ToArray());
                return _cached;
            }
            catch
            {
                return _cached;
            }
        }
    }

    private static IEnumerable<string> ReadTailLines(string path)
    {
        using var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete);
        var start = Math.Max(0, stream.Length - MaximumTailBytes);
        stream.Seek(start, SeekOrigin.Begin);
        using var reader = new StreamReader(stream, new UTF8Encoding(false, false), true, 4096, false);

        if (start > 0)
        {
            _ = reader.ReadLine();
        }

        while (reader.ReadLine() is { } line)
        {
            yield return line;
        }
    }

    private static string ResolveProjectName(
        string workspacePath,
        string entryPath,
        string workingDirectory,
        IReadOnlyList<string> allowedRoots)
    {
        if (!string.IsNullOrWhiteSpace(workspacePath))
        {
            var normalizedWorkspace = Normalize(workspacePath);
            var matchingRoot = allowedRoots.FirstOrDefault(root =>
                Normalize(root).Equals(normalizedWorkspace, StringComparison.OrdinalIgnoreCase));
            if (matchingRoot is null)
            {
                return Path.GetFileName(workspacePath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            }

            var first = FirstRelativeSegment(!string.IsNullOrWhiteSpace(entryPath) ? entryPath : workingDirectory);
            if (!string.IsNullOrWhiteSpace(first))
            {
                return first;
            }
        }

        foreach (var candidate in new[] { entryPath, workingDirectory })
        {
            if (string.IsNullOrWhiteSpace(candidate))
            {
                continue;
            }

            if (Path.IsPathRooted(candidate))
            {
                var normalizedCandidate = Normalize(candidate);
                foreach (var root in allowedRoots.OrderByDescending(root => root.Length))
                {
                    var normalizedRoot = Normalize(root).TrimEnd('/') + "/";
                    if (!normalizedCandidate.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    var remainder = normalizedCandidate[normalizedRoot.Length..];
                    return remainder.Split('/', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "Unknown";
                }
            }
            else
            {
                var first = FirstRelativeSegment(candidate);
                if (!string.IsNullOrWhiteSpace(first))
                {
                    return first;
                }
            }
        }

        return "Unknown";
    }

    private static (OperationKind Kind, string? Detail) ResolveOperation(
        string tool,
        string path,
        string workingDirectory) => tool.ToLowerInvariant() switch
        {
            "read" => (OperationKind.Read, path),
            "edit" => (OperationKind.Edit, path),
            "write" => (OperationKind.Write, path),
            "bash" => (OperationKind.Command,
                !string.IsNullOrWhiteSpace(workingDirectory) && workingDirectory != "."
                    ? workingDirectory
                    : null),
            "open_workspace" => (OperationKind.OpenWorkspace, null),
            _ => (OperationKind.Unknown, tool)
        };

    private static string FirstRelativeSegment(string value)
    {
        var normalized = value.Trim().TrimStart('.', '\\', '/');
        return normalized.Split(new[] { '\\', '/' }, StringSplitOptions.RemoveEmptyEntries)
            .FirstOrDefault() ?? string.Empty;
    }

    private static string Normalize(string value)
    {
        try
        {
            value = Path.GetFullPath(value);
        }
        catch
        {
            // Keep the original path when it cannot be canonicalized.
        }

        return value.Replace('\\', '/').TrimEnd('/');
    }

    private static bool TryGetString(JsonElement element, string propertyName, out string value)
    {
        value = string.Empty;
        if (!element.TryGetProperty(propertyName, out var property) || property.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        value = property.GetString() ?? string.Empty;
        return true;
    }

    private static bool TryGetInt64(JsonElement element, string propertyName, out long value)
    {
        value = 0;
        return element.TryGetProperty(propertyName, out var property) && property.TryGetInt64(out value);
    }
}
