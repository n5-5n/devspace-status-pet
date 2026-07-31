namespace DevSpaceStatusPet.Services;

public static class AppPaths
{
    public static string UserProfile { get; } =
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

    public static string DevSpaceDirectory { get; } =
        Path.Combine(UserProfile, ".devspace");

    public static string ConfigPath { get; } =
        Path.Combine(DevSpaceDirectory, "config.json");

    public static string ServeLogPath { get; } =
        Path.Combine(DevSpaceDirectory, "serve.log");

    public static string SettingsPath { get; } =
        Path.Combine(DevSpaceDirectory, "devspace-pet-settings.json");

    public static string PositionPath { get; } =
        Path.Combine(DevSpaceDirectory, "devspace-pet-position.json");

    public static string LocalDataDirectory { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "DevSpaceStatusPet");

    public static string LogsDirectory { get; } =
        Path.Combine(LocalDataDirectory, "logs");

    public static string CrashLogPath { get; } =
        Path.Combine(LogsDirectory, "crash.log");

    public static string ExecutablePath { get; } =
        Environment.ProcessPath ?? Path.Combine(AppContext.BaseDirectory, "DevSpaceStatusPet.exe");
}
