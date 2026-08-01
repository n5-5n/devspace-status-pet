namespace DevSpaceStatusPet.Models;

public enum ActivityState
{
    Idle,
    Working,
    Waiting,
    Failed,
    Stalled,
    Stopped
}

public enum OperationKind
{
    Unknown,
    Read,
    Edit,
    Write,
    Command,
    OpenWorkspace,
    Dotnet,
    Git,
    Ffmpeg,
    PowerShell,
    Python,
    LocalProcess,
    Idle,
    Stopped
}

public sealed record DevSpaceActivity(
    string Id,
    string ProjectName,
    ActivityState State,
    OperationKind Operation,
    string? Detail,
    DateTimeOffset StartedAt,
    TimeSpan Elapsed,
    bool ProjectEstimated = false,
    bool Success = true,
    string? WorkspaceId = null);

public sealed record DevSpaceSnapshot(
    ActivityState State,
    IReadOnlyList<DevSpaceActivity> Activities,
    int? ServerProcessId,
    int Port,
    string ConfigPath,
    string LogPath,
    DateTimeOffset UpdatedAt,
    DateTimeOffset? LastToolAt = null,
    bool LastToolSucceeded = true)
{
    public static DevSpaceSnapshot Initial(string configPath, string logPath, int port) =>
        new(
            ActivityState.Idle,
            Array.Empty<DevSpaceActivity>(),
            null,
            port,
            configPath,
            logPath,
            DateTimeOffset.Now);
}
