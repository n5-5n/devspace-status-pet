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
            SelfInstaller.Install(silent, launchAfterInstall);
            return;
        }
        if (args.Any(argument => argument.Equals("--uninstall", StringComparison.OrdinalIgnoreCase)))
        {
            SelfInstaller.Uninstall(
                args.Any(argument => argument.Equals("--remove-settings", StringComparison.OrdinalIgnoreCase)),
                silent);
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

        Application.ThreadException += (_, eventArgs) =>
        {
            CrashLogger.Write(eventArgs.Exception, "Application.ThreadException");
            MessageBox.Show(
                $"DevSpace Status Pet encountered an error.\n\n{eventArgs.Exception.Message}\n\n{AppPaths.CrashLogPath}",
                "DevSpace Status Pet",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        };
        AppDomain.CurrentDomain.UnhandledException += (_, eventArgs) =>
        {
            if (eventArgs.ExceptionObject is Exception exception)
            {
                CrashLogger.Write(exception, "AppDomain.UnhandledException");
            }
        };
        TaskScheduler.UnobservedTaskException += (_, eventArgs) =>
        {
            CrashLogger.Write(eventArgs.Exception, "TaskScheduler.UnobservedTaskException");
            eventArgs.SetObserved();
        };

        try
        {
            Application.Run(new TrayApplicationContext(args.Any(argument =>
                argument.Equals("--settings", StringComparison.OrdinalIgnoreCase))));
        }
        catch (Exception exception)
        {
            CrashLogger.Write(exception, "Program.Main");
            MessageBox.Show(
                $"DevSpace Status Pet could not start.\n\n{exception.Message}\n\n{AppPaths.CrashLogPath}",
                "DevSpace Status Pet",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }
}
