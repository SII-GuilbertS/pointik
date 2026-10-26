using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Pointik.Core.Geometry;
using Pointik.Core.Platform;

namespace Pointik.UI.Overlay;

/// <summary>
/// Surface de dessin d'un écran. Tout est dessiné dans <see cref="Render"/> à partir de la scène :
/// aucun contrôle enfant, aucune animation permanente (CPU nul au repos).
/// </summary>
public sealed class OverlaySurface : Control
{
    private readonly OverlayScene _scene;

    public OverlaySurface(OverlayScene scene, ScreenInfo screen)
    {
        _scene = scene;
        Screen = screen;
        IsHitTestVisible = false;
        ClipToBounds = false;
    }

    public ScreenInfo Screen { get; set; }

    /// <summary>Facteur entre pixels physiques et unités Avalonia pour cette fenêtre.</summary>
    private double Scaling => TopLevel.GetTopLevel(this)?.RenderScaling ?? Screen.Scale;

    /// <summary>Convertit un point du bureau virtuel en coordonnées locales de la surface.</summary>
    public Point ToLocal(ScreenPoint p)
    {
        var s = Scaling;
        return new Point((p.X - Screen.Bounds.X) / s, (p.Y - Screen.Bounds.Y) / s);
    }

    public override void Render(DrawingContext context)
    {
        if (_scene.ShowCursorMarker && Screen.Bounds.Contains(_scene.Cursor))
        {
            var c = ToLocal(_scene.Cursor);
            context.DrawEllipse(null, new Pen(new SolidColorBrush(Color.FromArgb(220, 255, 196, 31)), 4), c, 27, 27);
        }
    }
}
