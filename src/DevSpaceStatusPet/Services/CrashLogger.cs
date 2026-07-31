namespace DevSpaceStatusPet.Services;

public static class CrashLogger
{
    public static void Write(Exception exception, string source)
    {
        try
        {
            Directory.CreateDirectory(AppPaths.LogsDirectory);
            var text = $"[{DateTimeOffset.Now:O}] {source}{Environment.NewLine}{exception}{Environment.NewLine}{new string('-', 80)}{Environment.NewLine}";
            File.AppendAllText(AppPaths.CrashLogPath, text, new System.Text.UTF8Encoding(false));
        }
        catch
        {
            // Crash logging must never cause a secondary crash.
        }
    }
}
