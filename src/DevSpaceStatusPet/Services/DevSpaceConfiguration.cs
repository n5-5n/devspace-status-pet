using System.Text.Json;

namespace DevSpaceStatusPet.Services;

public sealed record DevSpaceConfiguration(
    int Port,
    string ConfigPath,
    string LogPath,
    IReadOnlyList<string> AllowedRoots);

public sealed class DevSpaceConfigurationLoader
{
    public DevSpaceConfiguration Load()
    {
        var port = 7676;
        var roots = new List<string>();

        try
        {
            if (File.Exists(AppPaths.ConfigPath))
            {
                using var document = JsonDocument.Parse(File.ReadAllText(AppPaths.ConfigPath));
                var root = document.RootElement;

                if (root.TryGetProperty("port", out var portElement) &&
                    portElement.TryGetInt32(out var configuredPort) &&
                    configuredPort is > 0 and <= 65535)
                {
                    port = configuredPort;
                }

                if (root.TryGetProperty("allowedRoots", out var rootsElement) &&
                    rootsElement.ValueKind == JsonValueKind.Array)
                {
                    foreach (var item in rootsElement.EnumerateArray())
                    {
                        var value = item.GetString();
                        if (string.IsNullOrWhiteSpace(value))
                        {
                            continue;
                        }

                        try
                        {
                            value = Path.GetFullPath(Environment.ExpandEnvironmentVariables(value));
                        }
                        catch
                        {
                            // Keep the original value if canonicalization fails.
                        }

                        roots.Add(value.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
                    }
                }
            }
        }
        catch
        {
            // Defaults keep the monitor usable when config.json is temporarily being replaced.
        }

        return new DevSpaceConfiguration(
            port,
            AppPaths.ConfigPath,
            AppPaths.ServeLogPath,
            roots.Distinct(StringComparer.OrdinalIgnoreCase).ToArray());
    }
}
