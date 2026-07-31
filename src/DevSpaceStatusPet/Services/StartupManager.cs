using Microsoft.Win32;

namespace DevSpaceStatusPet.Services;

public static class StartupManager
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private static string ValueName =>
        Environment.GetEnvironmentVariable("DEVSPACE_STATUS_PET_RUN_VALUE") ?? "DevSpaceStatusPet";

    public static bool IsEnabled()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, false);
            var value = key?.GetValue(ValueName) as string;
            return !string.IsNullOrWhiteSpace(value);
        }
        catch
        {
            return false;
        }
    }

    public static void SetEnabled(bool enabled, string? executablePath = null)
    {
        using var key = Registry.CurrentUser.CreateSubKey(RunKeyPath, true);
        if (enabled)
        {
            executablePath ??= AppPaths.ExecutablePath;
            key.SetValue(ValueName, $"\"{executablePath}\" --startup", RegistryValueKind.String);
        }
        else
        {
            key.DeleteValue(ValueName, false);
        }
    }
}
