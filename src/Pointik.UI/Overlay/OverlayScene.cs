using Pointik.Core.Effects;
using Pointik.Core.Geometry;
using Pointik.Core.Keys;
using Pointik.Core.Settings;
using Pointik.Core.Zoom;
using Pointik.UI.Keyboard;

namespace Pointik.UI.Overlay;

/// <summary>
/// État partagé par toutes les fenêtres overlay (une par écran) : position du curseur,
/// réglages et effets en cours. Lu et modifié uniquement sur le thread UI.
/// </summary>
public sealed class OverlayScene
{
    private BubbleRenderer? _bubbleRenderer;

    public OverlayScene(AppSettings settings, KeyDisplayEngine keyboard, ZoomController zoom, bool overlaysMagnified)
    {
        Settings = settings;
        Keyboard = keyboard;
        Zoom = zoom;
        OverlaysMagnified = overlaysMagnified;
    }

    /// <summary>État du zoom (pour compenser la taille des effets).</summary>
    public ZoomController Zoom { get; }

    /// <summary>Le zoom de la plateforme agrandit aussi les fenêtres overlay.</summary>
    public bool OverlaysMagnified { get; }

    /// <summary>Bulles du clavier en cours d'affichage.</summary>
    public KeyDisplayEngine Keyboard { get; }

    /// <summary>Identifiant de l'écran qui affiche les bulles du clavier.</summary>
    public string? KeyboardScreenId { get; set; }

    /// <summary>Pause totale : aucune frappe affichée, pastille rouge visible.</summary>
    public bool KeyboardPaused { get; set; }

    /// <summary>Moteur de rendu des bulles pour le style courant (reconstruit après un changement de style).</summary>
    public BubbleRenderer BubbleRenderer => _bubbleRenderer ??= new BubbleRenderer(Settings.Keyboard.Style);

    /// <summary>À appeler quand le style du clavier change.</summary>
    public void InvalidateStyle() => _bubbleRenderer = null;

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
