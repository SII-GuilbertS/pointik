using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Pointik.Core.Diagnostics;
using Pointik.Core.Platform;
using Pointik.UI.Services;

namespace Pointik.UI;

/// <summary>Application Avalonia : crée le contrôleur principal une fois le framework prêt.</summary>
public partial class PointikApp : Application
{
    private readonly Func<IUiDispatcher, PlatformServices>? _platformFactory;
    private readonly AppEnvironment? _environment;

    /// <summary>Constructeur requis par le concepteur XAML.</summary>
    public PointikApp()
    {
    }

    public PointikApp(Func<IUiDispatcher, PlatformServices> platformFactory, AppEnvironment environment)
    {
        _platformFactory = platformFactory;
        _environment = environment;
    }

    /// <summary>Contrôleur actif (null avant le démarrage).</summary>
    public AppController? Controller { get; private set; }

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop
            && _platformFactory is not null && _environment is not null)
        {
            desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;
            var platform = _platformFactory(new AvaloniaUiDispatcher());
            Controller = new AppController(platform, _environment, desktop);
            desktop.Exit += (_, _) => Controller.Shutdown();
            Controller.Start();
        }
        base.OnFrameworkInitializationCompleted();
    }
}

/// <summary>Informations fournies par le point d'entrée (chemins, journal).</summary>
public sealed class AppEnvironment
{
    /// <summary>Dossier de l'exécutable (mode portable).</summary>
    public required string ExecutableDirectory { get; init; }

    /// <summary>Chemin complet de l'exécutable (lancement avec l'OS).</summary>
    public required string ExecutablePath { get; init; }

    /// <summary>Dossier de repli si le dossier de l'exécutable est en lecture seule.</summary>
    public required string FallbackDirectory { get; init; }

    public required ErrorLog Log { get; init; }

    /// <summary>Options de diagnostic passées en ligne de commande.</summary>
    public IReadOnlyList<string> Arguments { get; init; } = [];
}
