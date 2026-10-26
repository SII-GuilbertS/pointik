using Pointik.Core.Animation;
using Pointik.Core.Geometry;
using Pointik.Core.Platform;
using Pointik.Core.Settings;

namespace Pointik.Core.Zoom;

/// <summary>Phase du zoom.</summary>
public enum ZoomPhase
{
    Off,
    Entering,
    Active,
    Leaving,
}

/// <summary>
/// Pilote le zoom : transitions animées, suivi du curseur (centré ou au bord), réglage à la molette.
/// Ne connaît que <see cref="IZoomService"/> : la logique est identique sur toutes les plateformes.
/// Toute erreur de l'API remet immédiatement le zoom à 1× et désactive la fonction.
/// </summary>
public sealed class ZoomController
{
    /// <summary>Durée de l'animation d'un changement de niveau à la molette.</summary>
    public static readonly TimeSpan LevelChangeDuration = TimeSpan.FromMilliseconds(120);

    private readonly IZoomService _service;
    private readonly Func<ZoomSettings> _settings;

    private double _fromScale = 1, _toScale = 1;
    private TimeSpan _animStart, _animDuration;
    private ScreenPoint _leaveFromOrigin;
    private bool _initialized;

    public ZoomController(IZoomService service, Func<ZoomSettings> settings)
    {
        _service = service;
        _settings = settings;
    }

    public ZoomPhase Phase { get; private set; } = ZoomPhase.Off;

    /// <summary>Le zoom est visible (en cours d'entrée, actif ou en cours de sortie).</summary>
    public bool IsEngaged => Phase != ZoomPhase.Off;

    /// <summary>Le zoom est demandé (entrée ou actif) : Échap et la molette le pilotent.</summary>
    public bool IsOnOrEntering => Phase is ZoomPhase.Entering or ZoomPhase.Active;

    /// <summary>Facteur courant (animé).</summary>
    public double Scale { get; private set; } = 1;

    /// <summary>Coin supérieur gauche de la zone visible (pixels physiques non agrandis).</summary>
    public ScreenPoint ViewOrigin { get; private set; }

    /// <summary>Écran zoomé.</summary>
    public ScreenInfo? Screen { get; private set; }

    /// <summary>Dernière erreur rencontrée (message en français), pour notification.</summary>
    public string? LastError { get; private set; }

    /// <summary>Le zoom a été désactivé après une erreur de l'API.</summary>
    public bool Disabled { get; private set; }

    /// <summary>Déclenché quand le zoom échoue et se coupe (message à afficher).</summary>
    public event Action<string>? Failed;

    /// <summary>Démarre le zoom sur l'écran du curseur, avec transition animée.</summary>
    public bool Start(ScreenPoint cursor, ScreenInfo screen, TimeSpan now)
    {
        if (Disabled) return Fail(LastError ?? "Le zoom est indisponible.");
        if (IsOnOrEntering) return true;
        if (!_initialized)
        {
            if (!_service.TryInitialize(out var error))
                return Fail(error ?? "Le zoom ne peut pas être initialisé.");
            _initialized = true;
        }
        Screen = screen;
        if (Phase == ZoomPhase.Off)
        {
            Scale = 1;
            ViewOrigin = screen.Bounds.TopLeft;
        }
        Animate(Scale, _settings().Level, TransitionDuration, now);
        Phase = ZoomPhase.Entering;
        return Apply(cursor);
    }

    /// <summary>Quitte le zoom avec transition animée.</summary>
    public void Stop(TimeSpan now)
    {
        if (!IsOnOrEntering) return;
        _leaveFromOrigin = ViewOrigin;
        Animate(Scale, 1, TransitionDuration, now);
        Phase = ZoomPhase.Leaving;
    }

    /// <summary>Bascule entre zoom et normal.</summary>
    public bool Toggle(ScreenPoint cursor, ScreenInfo screen, TimeSpan now)
    {
        if (IsOnOrEntering)
        {
            Stop(now);
            return true;
        }
        return Start(cursor, screen, now);
    }

    /// <summary>Retour immédiat à 1× (fermeture, erreur). Ne lève jamais d'exception.</summary>
    public void ResetNow()
    {
        Phase = ZoomPhase.Off;
        Scale = 1;
        _fromScale = _toScale = 1;
        try { _service.Reset(); }
        catch { /* au mieux */ }
    }

    /// <summary>
    /// Ajuste le niveau (Ctrl+Alt+molette). <paramref name="wheelDelta"/> en unités de molette (120 = un cran).
    /// Le nouveau niveau est enregistré dans les réglages.
    /// </summary>
    public void AdjustLevel(int wheelDelta, TimeSpan now)
    {
        var s = _settings();
        var target = Math.Clamp(Math.Round((s.Level + s.WheelStep * wheelDelta / 120.0) * 100) / 100,
            ZoomSettings.MinLevel, ZoomSettings.MaxLevel);
        s.Level = target;
        if (IsOnOrEntering)
        {
            Animate(Scale, target, LevelChangeDuration, now);
            if (Phase == ZoomPhase.Active) Phase = ZoomPhase.Entering;
        }
    }

    /// <summary>
    /// Met à jour le zoom (déplacement du curseur ou image d'animation).
    /// Retourne true tant qu'une animation est en cours.
    /// </summary>
    public bool Update(ScreenPoint cursor, TimeSpan now)
    {
        if (Phase == ZoomPhase.Off || Screen is null) return false;

        var t = Easing.Progress(now, _animStart, _animDuration);
        Scale = Easing.Lerp(_fromScale, _toScale, Easing.CubicInOut(t));
        var finished = t >= 1;

        if (finished && Phase == ZoomPhase.Leaving)
        {
            ResetNow();
            return false;
        }
        if (finished && Phase == ZoomPhase.Entering) Phase = ZoomPhase.Active;

        if (Phase == ZoomPhase.Leaving && _settings().Follow == ZoomFollowMode.EdgePush)
        {
            // Sortie en mode « bord » : la vue revient en douceur vers le coin de l'écran.
            var p = Easing.CubicInOut(t);
            ViewOrigin = new ScreenPoint(
                Easing.Lerp(_leaveFromOrigin.X, Screen.Bounds.X, p),
                Easing.Lerp(_leaveFromOrigin.Y, Screen.Bounds.Y, p));
            ViewOrigin = ZoomMath.Clamp(ViewOrigin, Screen.Bounds, Scale);
            ApplyTransform();
            return true;
        }

        Apply(cursor);
        return !finished;
    }

    private TimeSpan TransitionDuration => TimeSpan.FromMilliseconds(_settings().TransitionMs);

    private void Animate(double from, double to, TimeSpan duration, TimeSpan now)
    {
        _fromScale = from;
        _toScale = to;
        _animStart = now;
        _animDuration = duration;
    }

    /// <summary>Calcule la vue selon le mode de suivi et l'applique.</summary>
    private bool Apply(ScreenPoint cursor)
    {
        if (Screen is null) return false;
        var bounds = Screen.Bounds;
        var s = _settings();
        if (s.Follow == ZoomFollowMode.CenterOnCursor || Phase == ZoomPhase.Leaving)
        {
            ViewOrigin = ZoomMath.Centered(cursor, bounds, Scale);
        }
        else if (Phase == ZoomPhase.Entering)
        {
            // Entrée en mode « bord » : le curseur reste fixe à l'écran pendant l'agrandissement.
            ViewOrigin = ZoomMath.AnchoredAtCursor(cursor, bounds, Scale);
        }
        else
        {
            ViewOrigin = ZoomMath.EdgePush(ViewOrigin, cursor, bounds, Scale, s.EdgeMargin);
        }
        return ApplyTransform();
    }

    private bool ApplyTransform()
    {
        if (_service.TrySetTransform(Scale, ViewOrigin, out var error)) return true;
        ResetNow();
        Disabled = true;
        return Fail(error ?? "Le zoom a été refusé par le système.");
    }

    private bool Fail(string message)
    {
        LastError = message;
        Failed?.Invoke(message);
        return false;
    }
}
