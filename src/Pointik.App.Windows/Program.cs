using Avalonia;
using Avalonia.Threading;
using Pointik.Core.Diagnostics;
using Pointik.Platform.Windows;
using Pointik.UI;

namespace Pointik.App.Windows;

/// <summary>Point d'entrée de Pointik pour Windows.</summary>
internal static class Program
{
    /// <summary>Nom du mutex d'instance unique (portée : session utilisateur).</summary>
    private const string MutexName = @"Local\Pointik.Instance.7C2E41";

    private static readonly ErrorLog Log = new();

    [STAThread]
    private static int Main(string[] args)
    {
        using var mutex = new Mutex(initiallyOwned: true, MutexName, out var isFirstInstance);
        if (!isFirstInstance)
            return 0;

        InstallGlobalHandlers();
        try
        {
            BuildAvaloniaApp(args).StartWithClassicDesktopLifetime(args);
            return 0;
        }
        catch (Exception ex)
        {
            Log.Error("Arrêt sur exception non gérée", ex);
            return 1;
        }
        finally
        {
            // Sécurité critique : l'écran ne doit jamais rester zoomé.
            WindowsPlatform.EmergencyResetZoom();
        }
    }

    public static AppBuilder BuildAvaloniaApp(string[] args)
    {
        var exePath = Environment.ProcessPath ?? Path.Combine(AppContext.BaseDirectory, "Pointik.exe");
        var env = new AppEnvironment
        {
            // En publication « fichier unique », AppContext.BaseDirectory est le dossier de l'exe.
            ExecutableDirectory = AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar),
            ExecutablePath = exePath,
            FallbackDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Pointik"),
            Log = Log,
            Arguments = args,
        };
        return AppBuilder.Configure(() => new PointikApp(WindowsPlatform.Create, env))
            .UsePlatformDetect()
            .With(new Win32PlatformOptions())
            .AfterSetup(_ => Dispatcher.UIThread.UnhandledException += (_, e) =>
            {
                Log.Error("Exception sur le thread UI", e.Exception);
                // L'application continue : une erreur d'affichage ne doit pas la faire planter.
                e.Handled = true;
            })
            .LogToTrace();
    }

    /// <summary>Gestionnaires globaux : journalisent l'erreur et remettent le zoom à 1×.</summary>
    private static void InstallGlobalHandlers()
    {
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            WindowsPlatform.EmergencyResetZoom();
            if (e.ExceptionObject is Exception ex) Log.Error("Exception non gérée", ex);
        };
        AppDomain.CurrentDomain.ProcessExit += (_, _) => WindowsPlatform.EmergencyResetZoom();
        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            Log.Error("Tâche en erreur non observée", e.Exception);
            e.SetObserved();
        };
    }
}
