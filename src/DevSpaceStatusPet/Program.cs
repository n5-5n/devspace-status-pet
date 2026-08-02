using DevSpaceStatusPet.Services;
using DevSpaceStatusPet.UI;

namespace DevSpaceStatusPet;

internal static class Program
{
    private const string MutexName = "Local\\DevSpaceStatusPetV2";

    [STAThread]
    private static void Main(string[] args)
    {
        var silent = args.Any(argument => argument.Equals("--silent", StringComparison.OrdinalIgnoreCase));
        if (args.Any(argument => argument.Equals("--install", StringComparison.OrdinalIgnoreCase)))
        {
            var launchAfterInstall = !args.Any(argument =>
                argument.Equals("--no-launch", StringComparison.OrdinalIgnoreCase));
            SelfInstaller.Install(
                silent,
                launchAfterInstall,
                args.Any(argument => argument.Equals("--cleanup-source", StringComparison.OrdinalIgnoreCase)));
            return;
        }
        if (args.Any(argument => argument.Equals("--uninstall", StringComparison.OrdinalIgnoreCase)))
        {
            SelfInstaller.Uninstall(
                args.Any(argument => argument.Equals("--remove-settings", StringComparison.OrdinalIgnoreCase)),
                silent);
            return;
        }

        var captureIndex = Array.FindIndex(args, argument =>
            argument.Equals("--capture-previews", StringComparison.OrdinalIgnoreCase));
        if (captureIndex >= 0)
        {
            var outputDirectory = captureIndex + 1 < args.Length
                ? Path.GetFullPath(args[captureIndex + 1])
                : Path.Combine(Environment.CurrentDirectory, "docs");
            Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            PreviewCapture.Capture(outputDirectory);
            return;
        }

        using var mutex = new Mutex(true, MutexName, out var createdNew);
        if (!createdNew)
        {
            return;
        }

        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        RuntimeLogger.Write(
            "app-start",
            $"version={Application.ProductVersion}; args={string.Join(' ', args)}");

        Application.ThreadException += (_, eventArgs) =>
        {
            RuntimeLogger.Write(eventArgs.Exception, "Application.ThreadException");
            CrashLogger.Write(eventArgs.Exception, "Application.ThreadException");
            var localizer = CreateLocalizer();
            MessageBox.Show(
                localizer.Get("ApplicationError", eventArgs.Exception.Message, AppPaths.CrashLogPath),
                localizer["AppName"],
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        };
        AppDomain.CurrentDomain.UnhandledException += (_, eventArgs) =>
        {
            if (eventArgs.ExceptionObject is Exception exception)
            {
                RuntimeLogger.Write(exception, "AppDomain.UnhandledException");
                CrashLogger.Write(exception, "AppDomain.UnhandledException");
            }
        };
        TaskScheduler.UnobservedTaskException += (_, eventArgs) =>
        {
            RuntimeLogger.Write(eventArgs.Exception, "TaskScheduler.UnobservedTaskException");
            CrashLogger.Write(eventArgs.Exception, "TaskScheduler.UnobservedTaskException");
            eventArgs.SetObserved();
        };

        try
        {
            Application.Run(new TrayApplicationContext(args.Any(argument =>
                argument.Equals("--settings", StringComparison.OrdinalIgnoreCase))));
            RuntimeLogger.Write("app-exit", "clean=true");
        }
        catch (Exception exception)
        {
            RuntimeLogger.Write(exception, "Program.Main");
            CrashLogger.Write(exception, "Program.Main");
            var localizer = CreateLocalizer();
            MessageBox.Show(
                localizer.Get("StartupError", exception.Message, AppPaths.CrashLogPath),
                localizer["AppName"],
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }

    private static Localizer CreateLocalizer()
    {
        var settingsStore = new SettingsStore();
        return new Localizer(() => settingsStore.Current);
    }
}
