using Pointik.Core.Geometry;

namespace Pointik.UI.Overlay;

/// <summary>
/// État partagé par toutes les fenêtres overlay (une par écran) : position du curseur
/// et éléments à dessiner. Mis à jour uniquement sur le thread UI.
/// </summary>
public sealed class OverlayScene
{
    /// <summary>Position du curseur en pixels physiques du bureau virtuel.</summary>
    public ScreenPoint Cursor { get; set; }

    /// <summary>Affiche un repère de test autour du curseur.</summary>
    public bool ShowCursorMarker { get; set; } = true;
}
