using Pointik.Core.Animation;
using Pointik.Core.Geometry;
using Pointik.Core.Input;

namespace Pointik.Core.Effects;

/// <summary>Image d'une onde de clic à un instant donné.</summary>
/// <param name="Origin">Point du clic (pixels physiques).</param>
/// <param name="Button">Bouton à l'origine de l'onde.</param>
/// <param name="Radius">Rayon courant (pixels logiques).</param>
/// <param name="Opacity">Opacité courante (0 à 1).</param>
/// <param name="StrokeThickness">Épaisseur courante du trait (pixels logiques).</param>
/// <param name="FillOpacity">Opacité du disque intérieur, forte au tout début de l'onde.</param>
public readonly record struct RippleFrame(
    ScreenPoint Origin, MouseButton Button, double Radius, double Opacity, double StrokeThickness, double FillOpacity);

/// <summary>
/// Logique des effets de clic, indépendante de l'affichage : ondes qui s'estompent
/// et effet de pression de l'anneau tant qu'un bouton est maintenu.
/// </summary>
public sealed class ClickEffectsEngine
{
    /// <summary>Durée de l'effet de pression (enfoncement ou relâchement).</summary>
    public static readonly TimeSpan PressDuration = TimeSpan.FromMilliseconds(90);

    /// <summary>Réduction maximale de l'anneau quand un bouton est maintenu.</summary>
    public const double PressShrink = 0.2;

    private const int MaxRipples = 32;

    private readonly List<(ScreenPoint Origin, MouseButton Button, TimeSpan Start)> _ripples = new();
    private int _pressedCount;
    private double _pressFrom;
    private double _pressTarget;
    private TimeSpan _pressChanged;

    /// <summary>Ajoute une onde au point du clic.</summary>
    public void AddRipple(ScreenPoint origin, MouseButton button, TimeSpan now)
    {
        if (_ripples.Count >= MaxRipples) _ripples.RemoveAt(0);
        _ripples.Add((origin, button, now));
    }

    /// <summary>Signale l'appui ou le relâchement d'un bouton (effet de pression).</summary>
    public void SetButtonState(bool isDown, TimeSpan now)
    {
        var current = GetPressAmount(now);
        _pressedCount = isDown ? _pressedCount + 1 : Math.Max(0, _pressedCount - 1);
        var target = _pressedCount > 0 ? 1.0 : 0.0;
        if (target == _pressTarget) return;
        _pressFrom = current;
        _pressTarget = target;
        _pressChanged = now;
    }

    /// <summary>Oublie les boutons maintenus (ex. après une mise en pause ou un relâchement manqué).</summary>
    public void ResetPress()
    {
        _pressedCount = 0;
        _pressFrom = _pressTarget = 0;
    }

    /// <summary>Intensité de la pression (0 = relâché, 1 = enfoncé), animée.</summary>
    public double GetPressAmount(TimeSpan now)
    {
        var t = Easing.Progress(now, _pressChanged, PressDuration);
        return Easing.Lerp(_pressFrom, _pressTarget, Easing.QuadOut(t));
    }

    /// <summary>Facteur d'échelle de l'anneau (1 au repos, 0,8 bouton maintenu).</summary>
    public double GetRingScale(TimeSpan now) => 1 - PressShrink * GetPressAmount(now);

    /// <summary>Indique si une animation est en cours (le rendu doit alors continuer).</summary>
    public bool IsAnimating(TimeSpan now, TimeSpan rippleDuration) =>
        _ripples.Any(r => now - r.Start < rippleDuration) || now - _pressChanged < PressDuration;

    /// <summary>
    /// Calcule les ondes visibles et supprime celles qui sont terminées.
    /// </summary>
    /// <param name="startRadius">Rayon de départ (pixels logiques).</param>
    /// <param name="maxRadius">Rayon final (pixels logiques).</param>
    public IReadOnlyList<RippleFrame> GetRipples(TimeSpan now, TimeSpan duration, double startRadius, double maxRadius)
    {
        _ripples.RemoveAll(r => now - r.Start >= duration);
        if (_ripples.Count == 0) return Array.Empty<RippleFrame>();
        var frames = new RippleFrame[_ripples.Count];
        for (var i = 0; i < _ripples.Count; i++)
        {
            var (origin, button, start) = _ripples[i];
            var t = Easing.Progress(now, start, duration);
            var grow = Easing.CubicOut(t);
            var radius = Easing.Lerp(startRadius, maxRadius, grow);
            var opacity = 1 - Easing.QuadOut(t);
            var thickness = Easing.Lerp(5, 1.5, grow);
            var fill = 0.35 * (1 - Easing.Clamp01(t * 3));
            frames[i] = new RippleFrame(origin, button, radius, opacity, thickness, fill);
        }
        return frames;
    }

    /// <summary>Origines des ondes en cours (pour savoir quels écrans redessiner).</summary>
    public IEnumerable<ScreenPoint> ActiveOrigins => _ripples.Select(r => r.Origin);
}
