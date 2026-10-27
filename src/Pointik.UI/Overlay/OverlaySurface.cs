using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Pointik.Core.Geometry;
using Pointik.Core.Graphics;
using Pointik.Core.Input;
using Pointik.Core.Platform;
using Pointik.UI.Keyboard;
using Pointik.UI.Services;

namespace Pointik.UI.Overlay;

/// <summary>
/// Surface de dessin d'un écran. Tout est dessiné dans <see cref="Render"/> à partir de la scène :
/// aucun contrôle enfant, aucune animation permanente (CPU nul au repos).
/// </summary>
public sealed class OverlaySurface : Control
{
    private static readonly Color ShadowColor = Color.FromArgb(255, 0, 0, 0);
    private readonly OverlayScene _scene;

    public OverlaySurface(OverlayScene scene, ScreenInfo screen)
    {
        _scene = scene;
        Screen = screen;
        IsHitTestVisible = false;
        ClipToBounds = true;
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

    /// <summary>Indique si un effet de rayon donné (pixels logiques) autour d'un point touche cet écran.</summary>
    public bool IsNear(ScreenPoint p, double logicalRadius)
    {
        var r = logicalRadius * Screen.Scale;
        return Screen.Bounds.DistanceSquaredTo(p) <= r * r;
    }

    public override void Render(DrawingContext context)
    {
        var now = AppClock.Now;
        var settings = _scene.Settings;
        // Le zoom plein écran agrandit aussi cette fenêtre : les effets autour du curseur sont
        // dessinés à l'échelle 1/zoom pour garder une taille apparente constante.
        var zoomed = IsZoomedScreen;
        var compensation = zoomed ? 1 / _scene.Zoom.Scale : 1.0;

        if (_scene.SpotVisible)
            DrawSpot(context, settings.Spot.Radius * compensation, settings.Spot.Dimming);

        if (settings.Clicks.Enabled)
            DrawRipples(context, now, compensation);

        if (settings.Clicks.Enabled && settings.Clicks.ShowScroll)
            DrawScroll(context, now, compensation);

        if (_scene.RingVisible && IsNear(_scene.Cursor, _scene.CursorEffectsRadius))
            DrawRing(context, now, compensation);

        var showKeyboard = Screen.Id == _scene.KeyboardScreenId;
        var notification = Screen.Id == _scene.NotificationScreenId ? _scene.Notification : null;
        if (showKeyboard || notification is not null)
        {
            // Bulles, pastille et notification : dessinées dans la zone visible du zoom, à taille normale.
            using var _ = zoomed ? context.PushTransform(HudTransform()) : default(DrawingContext.PushedState?);
            if (showKeyboard) DrawKeyboard(context, now);
            notification?.Draw(context, LocalWorkArea(), now, belowPauseBadge: showKeyboard && (_scene.KeyboardPaused || _scene.KeyboardSecureHidden));
        }
    }

    /// <summary>Cet écran est actuellement agrandi par le zoom (et la fenêtre l'est aussi).</summary>
    private bool IsZoomedScreen =>
        _scene.OverlaysMagnified && _scene.Zoom.IsEngaged && _scene.Zoom.Screen?.Id == Screen.Id && _scene.Zoom.Scale > 1.001;

    /// <summary>
    /// Transformation qui place un élément « fixe à l'écran » dans la vue agrandie :
    /// point local p (vu sans zoom) → origine de la vue + p / zoom.
    /// </summary>
    private Matrix HudTransform()
    {
        var zoom = _scene.Zoom;
        var s = Scaling;
        var tx = (zoom.ViewOrigin.X - Screen.Bounds.X) / s;
        var ty = (zoom.ViewOrigin.Y - Screen.Bounds.Y) / s;
        return Matrix.CreateScale(1 / zoom.Scale, 1 / zoom.Scale) * Matrix.CreateTranslation(tx, ty);
    }

    /// <summary>Zone de travail de l'écran (sans barre des tâches) en coordonnées locales.</summary>
    public Rect LocalWorkArea()
    {
        var s = Scaling;
        var w = Screen.WorkArea;
        return new Rect((w.X - Screen.Bounds.X) / s, (w.Y - Screen.Bounds.Y) / s, w.Width / s, w.Height / s);
    }

    private void DrawKeyboard(DrawingContext context, TimeSpan now)
    {
        var area = LocalWorkArea();
        var engine = _scene.Keyboard;
        if (engine.Bubbles.Count > 0)
        {
            var kb = _scene.Settings.Keyboard;
            KeyboardLayer.Draw(context, engine.Bubbles, b => engine.GetVisualState(b, now),
                _scene.BubbleRenderer, kb.Style, kb.Position, area);
        }
        if (_scene.KeyboardPaused)
            KeyboardLayer.DrawPauseBadge(context, area);
        else if (_scene.KeyboardSecureHidden)
            KeyboardLayer.DrawPauseBadge(context, area, passwordField: true);
    }

    private void DrawSpot(DrawingContext context, double radius, double dimming)
    {
        var dim = Color.FromArgb((byte)Math.Round(dimming * 255), 0, 0, 0);
        var rect = new Rect(Bounds.Size);
        if (!IsNear(_scene.Cursor, radius))
        {
            // Écran sans le curseur : entièrement assombri.
            context.FillRectangle(new SolidColorBrush(dim), rect);
            return;
        }
        var c = ToLocal(_scene.Cursor);
        // Dégradé radial : disque clair au centre, bord adouci sur les 12 derniers pour cent.
        var brush = new RadialGradientBrush
        {
            Center = new RelativePoint(c, RelativeUnit.Absolute),
            GradientOrigin = new RelativePoint(c, RelativeUnit.Absolute),
            RadiusX = new RelativeScalar(radius, RelativeUnit.Absolute),
            RadiusY = new RelativeScalar(radius, RelativeUnit.Absolute),
            GradientStops =
            {
                new GradientStop(Color.FromArgb(0, 0, 0, 0), 0),
                new GradientStop(Color.FromArgb(0, 0, 0, 0), 0.88),
                new GradientStop(dim, 1),
            },
        };
        context.FillRectangle(brush, rect);
    }

    private void DrawRipples(DrawingContext context, TimeSpan now, double compensation)
    {
        var clicks = _scene.Settings.Clicks;
        var startRadius = Math.Max(4, _scene.Settings.Ring.Size * 0.15);
        var ripples = _scene.Clicks.GetRipples(now, _scene.RippleDuration, startRadius, clicks.MaxRadius);
        foreach (var r in ripples)
        {
            if (!IsNear(r.Origin, r.Radius + r.StrokeThickness)) continue;
            var color = clicks.ColorFor(r.Button);
            var c = ToLocal(r.Origin);
            var radius = r.Radius * compensation;
            if (r.FillOpacity > 0.01)
                context.DrawEllipse(Brush(color, r.FillOpacity), null, c, radius, radius);
            var pen = new Pen(Brush(color, r.Opacity), r.StrokeThickness * compensation);
            context.DrawEllipse(null, pen, c, radius, radius);
        }
    }

    /// <summary>Chevrons de molette au-dessus (vers le haut) ou en dessous (vers le bas) de l'anneau.</summary>
    private void DrawScroll(DrawingContext context, TimeSpan now, double compensation)
    {
        if (_scene.Scroll.GetFrame(now) is not { } f || !IsNear(f.Origin, _scene.ScrollReach)) return;
        var color = _scene.Settings.Clicks.ScrollColor;
        var c = ToLocal(f.Origin);
        double k = compensation;
        double halfWidth = 9 * k, halfHeight = 4.5 * k, gap = 9 * k;
        // Les chevrons partent juste au-delà de l'anneau et glissent dans le sens du défilement.
        var start = (_scene.Settings.Ring.Size / 2 + 12) * k + f.Shift * 8 * k;
        var dir = -f.Direction; // axe Y vers le bas : défiler vers le haut = chevrons au-dessus
        for (var i = 0; i < f.Chevrons; i++)
        {
            var y = c.Y + dir * (start + i * gap);
            var tip = new Point(c.X, y + dir * halfHeight);
            var left = new Point(c.X - halfWidth, y - dir * halfHeight);
            var right = new Point(c.X + halfWidth, y - dir * halfHeight);
            var geometry = new StreamGeometry();
            using (var g = geometry.Open())
            {
                g.BeginFigure(left, false);
                g.LineTo(tip);
                g.LineTo(right);
                g.EndFigure(false);
            }
            // Les chevrons les plus éloignés sont un peu plus discrets.
            var opacity = f.Opacity * (1 - i * 0.25);
            context.DrawGeometry(null, new Pen(new SolidColorBrush(ShadowColor, 0.25 * opacity), 5 * k,
                lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round), geometry);
            context.DrawGeometry(null, new Pen(Brush(color, opacity), 3 * k,
                lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round), geometry);
        }
    }

    private void DrawRing(DrawingContext context, TimeSpan now, double compensation)
    {
        var ring = _scene.Settings.Ring;
        var visibility = _scene.RingIdleVisibility(now);
        if (visibility <= 0.003) return;
        var opacity = ring.Opacity * visibility;
        var scale = ring.PressEffect ? _scene.Clicks.GetRingScale(now) : 1;
        var radius = ring.Size / 2 * scale * compensation;
        var thickness = ring.Thickness * compensation;
        var c = ToLocal(_scene.Cursor);
        // Liseré sombre discret sous l'anneau : reste visible sur fond clair comme sur fond foncé.
        var shadow = new Pen(new SolidColorBrush(ShadowColor, 0.22 * opacity), thickness + 2 * compensation);
        context.DrawEllipse(null, shadow, c, radius, radius);
        context.DrawEllipse(null, new Pen(Brush(ring.Color, opacity), thickness), c, radius, radius);
    }

    private static IBrush Brush(ArgbColor color, double opacity) =>
        new SolidColorBrush(Color.FromArgb(255, color.R, color.G, color.B), Math.Clamp(opacity, 0, 1) * color.Opacity);
}
