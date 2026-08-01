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
    private readonly Dictionary<string, string> _workspaceProjects = new(StringComparer.OrdinalIgnoreCase);
    private string _identitySourcePath = string.Empty;
    private long _identitySourceLength;
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
                EnsureWorkspaceIdentityCache(path, allowedRoots, fileInfo.Length);
                if (fileInfo.LastWriteTimeUtc == _cachedWriteTimeUtc)
                {
                    return _cached;
                }

                var latestByWorkspace = new Dictionary<string, ToolEvent>(StringComparer.OrdinalIgnoreCase);
                ToolEvent? lastTool = null;

                foreach (var line in ReadTailLines(path))
                {
                    if (!TryParseToolCall(line, out var document))
                    {
                        continue;
                    }

                    using (document)
                    {
                        var root = document.RootElement;
                        TryGetString(root, "tool", out var tool);
                        TryGetString(root, "workspaceId", out var workspaceId);
                        TryGetString(root, "path", out var entryPath);
                        TryGetString(root, "workingDirectory", out var workingDirectory);

                        if (tool.Equals("open_workspace", StringComparison.OrdinalIgnoreCase) &&
                            !string.IsNullOrWhiteSpace(workspaceId) &&
                            !string.IsNullOrWhiteSpace(entryPath))
                        {
                            RememberWorkspace(workspaceId, entryPath, allowedRoots);
                        }

                        var workspacePath = !string.IsNullOrWhiteSpace(workspaceId) &&
                                            _workspacePaths.TryGetValue(workspaceId, out var knownPath)
                            ? knownPath
                            : string.Empty;

                        var projectName = ResolveProjectName(
                            workspacePath,
                            entryPath,
                            workingDirectory,
                            allowedRoots);
                        if (string.IsNullOrWhiteSpace(projectName) &&
                            !string.IsNullOrWhiteSpace(workspaceId) &&
                            _workspaceProjects.TryGetValue(workspaceId, out var knownProject))
                        {
                            projectName = knownProject;
                        }
                        if (string.IsNullOrWhiteSpace(projectName))
                        {
                            projectName = WorkspaceLabel(workspaceId);
                        }
                        else if (!string.IsNullOrWhiteSpace(workspaceId))
                        {
                            _workspaceProjects[workspaceId] = projectName;
                        }

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
            catch (Exception exception)
            {
                CrashLogger.Write(exception, "DevSpaceLogReader.Read");
                return _cached;
            }
        }
    }

    private void EnsureWorkspaceIdentityCache(
        string path,
        IReadOnlyList<string> allowedRoots,
        long currentLength)
    {
        var normalizedPath = Path.GetFullPath(path);
        var sameFile = normalizedPath.Equals(_identitySourcePath, StringComparison.OrdinalIgnoreCase);
        if (sameFile && currentLength >= _identitySourceLength)
        {
            return;
        }

        _workspacePaths.Clear();
        _workspaceProjects.Clear();

        foreach (var line in ReadAllLinesShared(path))
        {
            if (!line.Contains("open_workspace", StringComparison.OrdinalIgnoreCase) ||
                !TryParseToolCall(line, out var document))
            {
                continue;
            }

            using (document)
            {
                var root = document.RootElement;
                TryGetString(root, "tool", out var tool);
                TryGetString(root, "workspaceId", out var workspaceId);
                TryGetString(root, "path", out var workspacePath);
                if (tool.Equals("open_workspace", StringComparison.OrdinalIgnoreCase) &&
                    !string.IsNullOrWhiteSpace(workspaceId) &&
                    !string.IsNullOrWhiteSpace(workspacePath))
                {
                    RememberWorkspace(workspaceId, workspacePath, allowedRoots);
                }
            }
        }

        _identitySourcePath = normalizedPath;
        _identitySourceLength = currentLength;
    }

    private void RememberWorkspace(
        string workspaceId,
        string workspacePath,
        IReadOnlyList<string> allowedRoots)
    {
        _workspacePaths[workspaceId] = workspacePath;
        var project = ResolveProjectName(workspacePath, string.Empty, string.Empty, allowedRoots);
        if (string.IsNullOrWhiteSpace(project))
        {
            project = Path.GetFileName(workspacePath.TrimEnd(
                Path.DirectorySeparatorChar,
                Path.AltDirectorySeparatorChar));
        }
        if (!string.IsNullOrWhiteSpace(project))
        {
            _workspaceProjects[workspaceId] = project;
        }
    }

    private static bool TryParseToolCall(string line, out JsonDocument document)
    {
        document = null!;
        if (string.IsNullOrWhiteSpace(line) ||
            !line.Contains("tool_call", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        try
        {
            document = JsonDocument.Parse(line);
            var root = document.RootElement;
            if (!TryGetString(root, "event", out var eventName) ||
                !eventName.Equals("tool_call", StringComparison.OrdinalIgnoreCase))
            {
                document.Dispose();
                document = null!;
                return false;
            }
            return true;
        }
        catch (JsonException)
        {
            // The log may be observed while the final line is still being written.
            return false;
        }
    }

    private static IEnumerable<string> ReadAllLinesShared(string path)
    {
        var format = DetectLogEncoding(path);
        using var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete);
        stream.Seek(format.PreambleLength, SeekOrigin.Begin);
        using var reader = new StreamReader(stream, format.Encoding, false, 4096, false);
        while (reader.ReadLine() is { } line)
        {
            yield return line;
        }
    }

    private static IEnumerable<string> ReadTailLines(string path)
    {
        var format = DetectLogEncoding(path);
        using var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete);
        var dataStart = format.PreambleLength;
        var start = Math.Max(dataStart, stream.Length - MaximumTailBytes);
        if (format.CodeUnitSize > 1)
        {
            start -= (start - dataStart) % format.CodeUnitSize;
        }
        stream.Seek(start, SeekOrigin.Begin);
        using var reader = new StreamReader(stream, format.Encoding, false, 4096, false);

        if (start > dataStart)
        {
            _ = reader.ReadLine();
        }

        while (reader.ReadLine() is { } line)
        {
            yield return line;
        }
    }

    private static LogEncoding DetectLogEncoding(string path)
    {
        Span<byte> prefix = stackalloc byte[4];
        using var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete);
        var read = stream.Read(prefix);

        if (read >= 4 && prefix[0] == 0xFF && prefix[1] == 0xFE && prefix[2] == 0x00 && prefix[3] == 0x00)
        {
            return new LogEncoding(new UTF32Encoding(false, true, false), 4, 4);
        }
        if (read >= 4 && prefix[0] == 0x00 && prefix[1] == 0x00 && prefix[2] == 0xFE && prefix[3] == 0xFF)
        {
            return new LogEncoding(new UTF32Encoding(true, true, false), 4, 4);
        }
        if (read >= 3 && prefix[0] == 0xEF && prefix[1] == 0xBB && prefix[2] == 0xBF)
        {
            return new LogEncoding(new UTF8Encoding(true, false), 3, 1);
        }
        if (read >= 2 && prefix[0] == 0xFF && prefix[1] == 0xFE)
        {
            return new LogEncoding(new UnicodeEncoding(false, true, false), 2, 2);
        }
        if (read >= 2 && prefix[0] == 0xFE && prefix[1] == 0xFF)
        {
            return new LogEncoding(new UnicodeEncoding(true, true, false), 2, 2);
        }

        return new LogEncoding(new UTF8Encoding(false, false), 0, 1);
    }

    private readonly record struct LogEncoding(Encoding Encoding, int PreambleLength, int CodeUnitSize);

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
                return Path.GetFileName(workspacePath.TrimEnd(
                    Path.DirectorySeparatorChar,
                    Path.AltDirectorySeparatorChar));
            }

            var first = FirstRelativeSegment(!string.IsNullOrWhiteSpace(entryPath) ? entryPath : workingDirectory);
            if (!string.IsNullOrWhiteSpace(first))
            {
                return first;
            }
        }

        foreach (var candidate in new[] { entryPath, workingDirectory })
        {
            if (string.IsNullOrWhiteSpace(candidate) || !Path.IsPathRooted(candidate))
            {
                continue;
            }

            var normalizedCandidate = Normalize(candidate);
            foreach (var root in allowedRoots.OrderByDescending(root => root.Length))
            {
                var normalizedRoot = Normalize(root).TrimEnd('/') + "/";
                if (!normalizedCandidate.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var remainder = normalizedCandidate[normalizedRoot.Length..];
                return remainder.Split('/', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? string.Empty;
            }
        }

        return string.Empty;
    }

    private static string WorkspaceLabel(string workspaceId)
    {
        if (string.IsNullOrWhiteSpace(workspaceId))
        {
            return "DevSpace";
        }

        var compact = workspaceId.StartsWith("ws_", StringComparison.OrdinalIgnoreCase)
            ? workspaceId[3..]
            : workspaceId;
        compact = compact.Length > 8 ? compact[..8] : compact;
        return $"Workspace {compact}";
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
