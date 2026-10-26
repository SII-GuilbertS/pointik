using Pointik.Core.Effects;
using Pointik.Core.Geometry;
using Pointik.Core.Settings;

namespace Pointik.UI.Overlay;

/// <summary>
/// État partagé par toutes les fenêtres overlay (une par écran) : position du curseur,
/// réglages et effets en cours. Lu et modifié uniquement sur le thread UI.
/// </summary>
public sealed class OverlayScene
{
    public OverlayScene(AppSettings settings)
    {
        Settings = settings;
    }

    public AppSettings Settings { get; set; }

    /// <summary>Position du curseur en pixels physiques du bureau virtuel.</summary>
    public ScreenPoint Cursor { get; set; }

    /// <summary>Interrupteur général « anneau et spot » (raccourci Ctrl+Alt+M), propre à la session.</summary>
    public bool CursorHighlightOn { get; set; } = true;

    public ClickEffectsEngine Clicks { get; } = new();

    public bool RingVisible => CursorHighlightOn && Settings.Ring.Enabled;

    public bool SpotVisible => CursorHighlightOn && Settings.Spot.Enabled;

    /// <summary>Durée des ondes de clic.</summary>
    public TimeSpan RippleDuration => TimeSpan.FromMilliseconds(Settings.Clicks.DurationMs);

    /// <summary>
    /// Rayon (pixels logiques) au-delà duquel les effets autour du curseur ne débordent plus :
    /// sert à savoir quels écrans redessiner près d'une bordure.
    /// </summary>
    public double CursorEffectsRadius =>
        Math.Max(Math.Max(Settings.Ring.Size / 2 + Settings.Ring.Thickness, Settings.Clicks.MaxRadius + 4),
            SpotVisible ? Settings.Spot.Radius : 0);
}
