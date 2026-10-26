using Pointik.Core.Animation;

namespace Pointik.Core.Effects;

/// <summary>
/// Visibilité d'un élément qui s'efface après une période d'inactivité
/// (anneau autour d'un curseur immobile) et réapparaît dès la reprise.
/// </summary>
public static class IdleFade
{
    /// <summary>Durée du fondu de disparition.</summary>
    public static readonly TimeSpan FadeDuration = TimeSpan.FromMilliseconds(400);

    /// <summary>Visibilité entre 0 (effacé) et 1 (visible).</summary>
    /// <param name="now">Instant courant.</param>
    /// <param name="lastActivity">Dernier mouvement ou clic.</param>
    /// <param name="delay">Inactivité tolérée avant le fondu.</param>
    public static double Visibility(TimeSpan now, TimeSpan lastActivity, TimeSpan delay)
    {
        var idle = now - lastActivity;
        if (idle <= delay) return 1;
        return 1 - Easing.CubicOut(Easing.Progress(now, lastActivity + delay, FadeDuration));
    }

    /// <summary>Le fondu est en cours (le rendu doit continuer).</summary>
    public static bool IsFading(TimeSpan now, TimeSpan lastActivity, TimeSpan delay)
    {
        var idle = now - lastActivity;
        return idle > delay && idle < delay + FadeDuration;
    }
}
