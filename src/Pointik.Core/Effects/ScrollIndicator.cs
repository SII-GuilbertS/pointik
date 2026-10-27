using Pointik.Core.Animation;
using Pointik.Core.Geometry;

namespace Pointik.Core.Effects;

/// <summary>Image de l'indicateur de défilement à un instant donné.</summary>
/// <param name="Origin">Position du curseur au dernier cran.</param>
/// <param name="Direction">+1 vers le haut, -1 vers le bas.</param>
/// <param name="Chevrons">Nombre de chevrons (1 à 3 selon la vitesse de défilement).</param>
/// <param name="Shift">Glissement dans le sens du défilement (0 à 1).</param>
/// <param name="Opacity">Opacité (0 à 1).</param>
public readonly record struct ScrollFrame(ScreenPoint Origin, int Direction, int Chevrons, double Shift, double Opacity);

/// <summary>
/// Petite animation de molette : des chevrons apparaissent au-dessus ou en dessous du curseur,
/// glissent dans le sens du défilement puis s'estompent. Des crans rapprochés dans le même
/// sens prolongent l'animation et ajoutent des chevrons (3 au maximum).
/// </summary>
public sealed class ScrollIndicator
{
    /// <summary>Durée de vie après le dernier cran.</summary>
    public static readonly TimeSpan Duration = TimeSpan.FromMilliseconds(550);

    /// <summary>Durée du glissement suivant chaque cran.</summary>
    public static readonly TimeSpan SlideDuration = TimeSpan.FromMilliseconds(220);

    public const int MaxChevrons = 3;

    private ScreenPoint _origin;
    private int _direction;
    private int _notches;
    private TimeSpan _lastNotch = TimeSpan.MinValue;

    /// <summary>Signale un cran de molette (<paramref name="delta"/> &gt; 0 : vers le haut).</summary>
    public void AddNotch(ScreenPoint origin, int delta, TimeSpan now)
    {
        if (delta == 0) return;
        var direction = Math.Sign(delta);
        var continuing = IsActive(now) && direction == _direction;
        _notches = continuing ? Math.Min(MaxChevrons, _notches + 1) : 1;
        _direction = direction;
        _origin = origin;
        _lastNotch = now;
    }

    /// <summary>Le curseur a bougé : l'indicateur le suit.</summary>
    public void MoveTo(ScreenPoint origin) => _origin = origin;

    public bool IsActive(TimeSpan now) => _lastNotch != TimeSpan.MinValue && now - _lastNotch < Duration;

    public ScreenPoint Origin => _origin;

    /// <summary>État courant, ou null si rien n'est affiché.</summary>
    public ScrollFrame? GetFrame(TimeSpan now)
    {
        if (!IsActive(now)) return null;
        var slide = Easing.CubicOut(Easing.Progress(now, _lastNotch, SlideDuration));
        // Pleinement visible au début, puis fondu sur la seconde moitié de la durée de vie.
        var fadeStart = _lastNotch + Duration / 2;
        var opacity = 1 - Easing.QuadOut(Easing.Progress(now, fadeStart, Duration / 2));
        return new ScrollFrame(_origin, _direction, _notches, slide, opacity);
    }
}
