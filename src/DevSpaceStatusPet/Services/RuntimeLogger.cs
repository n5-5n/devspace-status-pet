using System.Text;

namespace DevSpaceStatusPet.Services;

public static class RuntimeLogger
{
    private const long MaximumLogBytes = 1024 * 1024;
    private static readonly object SyncRoot = new();
    private static readonly UTF8Encoding Utf8WithoutBom = new(false);

    public static void Write(string eventName, string? detail = null)
    {
        try
        {
            lock (SyncRoot)
            {
                Directory.CreateDirectory(AppPaths.LogsDirectory);
                RotateIfNeeded();
                var normalizedDetail = string.IsNullOrWhiteSpace(detail)
                    ? string.Empty
                    : " | " + detail.Replace('\r', ' ').Replace('\n', ' ');
                var line = $"[{DateTimeOffset.Now:O}] pid={Environment.ProcessId} | {eventName}{normalizedDetail}{Environment.NewLine}";
                File.AppendAllText(AppPaths.RuntimeLogPath, line, Utf8WithoutBom);
            }
        }
        catch
        {
            // Diagnostics must never interfere with the application.
        }
    }

    public static void Write(Exception exception, string source)
    {
        Write(source, exception.ToString());
    }

    private static void RotateIfNeeded()
    {
        if (!File.Exists(AppPaths.RuntimeLogPath) ||
            new FileInfo(AppPaths.RuntimeLogPath).Length < MaximumLogBytes)
        {
            return;
        }

        File.Delete(AppPaths.PreviousRuntimeLogPath);
        File.Move(AppPaths.RuntimeLogPath, AppPaths.PreviousRuntimeLogPath);
    }
}
