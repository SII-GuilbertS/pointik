using System.Diagnostics;
using Avalonia.Threading;

namespace Pointik.UI.Services;

/// <summary>Horloge monotone de l'application (indépendante de l'heure système).</summary>
public static class AppClock
{
    private static readonly Stopwatch Watch = Stopwatch.StartNew();

    public static TimeSpan Now => Watch.Elapsed;
}

/// <summary>
/// Minuterie d'animation (~60 images/s) qui ne tourne QUE pendant une animation :
/// dès que le rappel retourne false, elle s'arrête. CPU nul au repos.
/// </summary>
public sealed class AnimationTicker
{
    private readonly DispatcherTimer _timer;
    private readonly Func<TimeSpan, bool> _onFrame;

    /// <param name="onFrame">Appelé à chaque image ; retourne true pour continuer.</param>
    public AnimationTicker(Func<TimeSpan, bool> onFrame)
    {
        _onFrame = onFrame;
        _timer = new DispatcherTimer(DispatcherPriority.Render) { Interval = TimeSpan.FromMilliseconds(15) };
        _timer.Tick += OnTick;
    }

    public bool IsRunning => _timer.IsEnabled;

    /// <summary>Démarre la minuterie si elle ne tourne pas déjà.</summary>
    public void Ensure()
    {
        if (!_timer.IsEnabled) _timer.Start();
    }

    public void Stop() => _timer.Stop();

    private void OnTick(object? sender, EventArgs e)
    {
        var keepGoing = false;
        try
        {
            keepGoing = _onFrame(AppClock.Now);
        }
        finally
        {
            // Arrêt systématique si l'animation est terminée ou en erreur.
            if (!keepGoing) _timer.Stop();
        }
    }
}
