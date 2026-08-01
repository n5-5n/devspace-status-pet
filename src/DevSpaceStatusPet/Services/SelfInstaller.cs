using System.Diagnostics;
using System.Globalization;

namespace DevSpaceStatusPet.Services;

public static class SelfInstaller
{
    public static string InstallDirectory { get; } =
        Environment.GetEnvironmentVariable("DEVSPACE_STATUS_PET_INSTALL_DIR") ??
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "DevSpaceStatusPetV2");

    public static string InstalledExecutablePath { get; } =
        Path.Combine(InstallDirectory, "DevSpaceStatusPet.exe");

    public static string DesktopShortcutPath { get; } =
        Environment.GetEnvironmentVariable("DEVSPACE_STATUS_PET_SHORTCUT_PATH") ??
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
            "DevSpace Status Pet v0.2.lnk");

    public static bool IsRunningFromInstallDirectory =>
        PathsEqual(AppPaths.ExecutablePath, InstalledExecutablePath);

    public static void Install(
        bool silent = false,
        bool launchAfterInstall = true,
        bool cleanupSource = false)
    {
        StopOtherInstances();
        Directory.CreateDirectory(InstallDirectory);
        var sourcePath = AppPaths.ExecutablePath;
        var backupPath = $"{InstalledExecutablePath}.backup";
        var replacementPath = Path.Combine(
            InstallDirectory,
            $"DevSpaceStatusPet.{Environment.ProcessId}.tmp.exe");

        try
        {
            if (!PathsEqual(sourcePath, InstalledExecutablePath))
            {
                TryDelete(backupPath);
                if (File.Exists(InstalledExecutablePath))
                {
                    File.Copy(InstalledExecutablePath, backupPath, true);
                }

                File.Copy(sourcePath, replacementPath, true);
                File.Move(replacementPath, InstalledExecutablePath, true);
            }

            CreateShortcut(DesktopShortcutPath, InstalledExecutablePath, "--settings");
            StartupManager.SetEnabled(true, InstalledExecutablePath);

            if (launchAfterInstall)
            {
                _ = Process.Start(new ProcessStartInfo(InstalledExecutablePath, "--settings")
                {
                    UseShellExecute = true,
                    WorkingDirectory = InstallDirectory
                }) ?? throw new InvalidOperationException("Could not start the installed application.");
            }

            TryDelete(backupPath);
            if (cleanupSource && !PathsEqual(sourcePath, InstalledExecutablePath))
            {
                ScheduleSourceCleanup(sourcePath);
            }
        }
        catch
        {
            TryDelete(replacementPath);
            if (File.Exists(backupPath))
            {
                File.Copy(backupPath, InstalledExecutablePath, true);
                TryDelete(backupPath);
                if (launchAfterInstall)
                {
                    TryLaunchInstalledApplication();
                }
            }
            throw;
        }

        if (!silent)
        {
            MessageBox.Show(
                IsJapanese()
                    ? $"DevSpace Status Pet v0.2をインストールしました。\n\n{InstallDirectory}"
                    : $"DevSpace Status Pet v0.2 has been installed.\n\n{InstallDirectory}",
                "DevSpace Status Pet",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }
    }

    public static void Uninstall(bool removeSettings, bool silent = false)
    {
        StopOtherInstances();
        StartupManager.SetEnabled(false);
        TryDelete(DesktopShortcutPath);

        if (removeSettings)
        {
            TryDelete(AppPaths.SettingsPath);
            TryDelete(AppPaths.PositionPath);
        }

        if (!silent)
        {
            MessageBox.Show(
                IsJapanese()
                    ? "DevSpace Status Pet v0.2をアンインストールします。"
                    : "DevSpace Status Pet v0.2 will be uninstalled.",
                "DevSpace Status Pet",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }

        if (Directory.Exists(InstallDirectory))
        {
            var command = $"ping 127.0.0.1 -n 3 >nul & rmdir /s /q \"{InstallDirectory}\"";
            Process.Start(new ProcessStartInfo("cmd.exe", $"/c {command}")
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden
            });
        }
    }

    private static void StopOtherInstances()
    {
        foreach (var process in Process.GetProcessesByName("DevSpaceStatusPet"))
        {
            using (process)
            {
                if (process.Id == Environment.ProcessId)
                {
                    continue;
                }

                try
                {
                    process.Kill(true);
                    process.WaitForExit(5000);
                }
                catch
                {
                    // The process may already have exited or belong to a protected session.
                }
            }
        }
    }

    private static void TryLaunchInstalledApplication()
    {
        try
        {
            _ = Process.Start(new ProcessStartInfo(InstalledExecutablePath, "--settings")
            {
                UseShellExecute = true,
                WorkingDirectory = InstallDirectory
            });
        }
        catch
        {
            // The original executable has been restored even if relaunching it is unavailable.
        }
    }

    private static void ScheduleSourceCleanup(string sourcePath)
    {
        var sourceDirectory = Path.GetDirectoryName(sourcePath);
        if (string.IsNullOrWhiteSpace(sourceDirectory) ||
            !sourceDirectory.StartsWith(Path.GetTempPath(), StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var command = $"ping 127.0.0.1 -n 4 >nul & rmdir /s /q \"{sourceDirectory}\"";
        Process.Start(new ProcessStartInfo("cmd.exe", $"/c {command}")
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            WindowStyle = ProcessWindowStyle.Hidden
        });
    }

    private static void CreateShortcut(string shortcutPath, string targetPath, string arguments)
    {
        var shellType = Type.GetTypeFromProgID("WScript.Shell")
                        ?? throw new InvalidOperationException("WScript.Shell is unavailable.");
        dynamic shell = Activator.CreateInstance(shellType)
                        ?? throw new InvalidOperationException("Could not create WScript.Shell.");
        dynamic shortcut = shell.CreateShortcut(shortcutPath);
        shortcut.TargetPath = targetPath;
        shortcut.Arguments = arguments;
        shortcut.WorkingDirectory = Path.GetDirectoryName(targetPath);
        shortcut.Description = "DevSpace Status Pet v0.2";
        shortcut.Save();
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
            // Uninstall should continue even if an optional file is already locked or removed.
        }
    }

    private static bool PathsEqual(string left, string right)
    {
        try
        {
            left = Path.GetFullPath(left);
            right = Path.GetFullPath(right);
        }
        catch
        {
            // Compare the original values if canonicalization fails.
        }
        return string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsJapanese() =>
        CultureInfo.CurrentUICulture.TwoLetterISOLanguageName.Equals("ja", StringComparison.OrdinalIgnoreCase);
}
