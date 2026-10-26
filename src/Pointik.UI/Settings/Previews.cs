using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Pointik.Core.Keys;
using Pointik.Core.Settings;
using Pointik.UI.Keyboard;

namespace Pointik.UI.Settings;

/// <summary>Aperçu en direct des bulles du clavier (style et position) dans une « mini-écran ».</summary>
public sealed class KeyboardPreview : Control
{
    private static readonly IBrush ScreenBrush = new LinearGradientBrush
    {
        StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
        EndPoint = new RelativePoint(1, 1, RelativeUnit.Relative),
        GradientStops = { new GradientStop(Color.FromRgb(0x5B, 0x6B, 0x84), 0), new GradientStop(Color.FromRgb(0xB9, 0xC3, 0xD0), 1) },
    };

    private readonly KeyDisplayEngine _samples;
    private KeyboardSettings _settings = new();

    public KeyboardPreview()
    {
        _samples = new KeyDisplayEngine(() => _settings, new KeyNames());
        ClipToBounds = true;
    }

    /// <summary>Réglages affichés ; <see cref="Refresh"/> redessine après une modification.</summary>
    public KeyboardSettings Settings
    {
        get => _settings;
        set
        {
            _settings = value;
            Refresh();
        }
    }

    /// <summary>Reconstruit les bulles d'exemple et redessine.</summary>
    public void Refresh()
    {
        _samples.Clear();
        var now = TimeSpan.FromHours(1);
        var names = new KeyNames();
        _samples.AddShortcut(names.GetParts(Core.Input.KeyModifiers.Ctrl, Core.Input.KeyId.C), now);
        _samples.AddShortcut(names.GetParts(Core.Input.KeyModifiers.Ctrl | Core.Input.KeyModifiers.Shift, Core.Input.KeyId.S), now);
        _samples.AddShortcut(names.GetParts(Core.Input.KeyModifiers.Ctrl, Core.Input.KeyId.Z), now);
        _samples.AddShortcut(names.GetParts(Core.Input.KeyModifiers.Ctrl, Core.Input.KeyId.Z), now);
        _samples.AddShortcut(names.GetParts(Core.Input.KeyModifiers.Ctrl, Core.Input.KeyId.Z), now);
        InvalidateVisual();
    }

    public override void Render(DrawingContext context)
    {
        var bounds = new Rect(Bounds.Size);
        context.DrawRectangle(ScreenBrush, null, new RoundedRect(bounds, 8));
        // Le mini-écran est une version réduite (60 %) de l'écran : décalages et position restent cohérents.
        const double factor = 0.6;
        using (context.PushTransform(Matrix.CreateScale(factor, factor)))
        {
            var area = new Rect(0, 0, bounds.Width / factor, bounds.Height / factor);
            var renderer = new BubbleRenderer(_settings.Style);
            KeyboardLayer.Draw(context, _samples.Bubbles, _ => new BubbleVisualState(1, 0), renderer,
                _settings.Style, _settings.Position, area);
        }
    }
}

/// <summary>Aperçu en direct de l'anneau, du spot et des ondes de clic.</summary>
public sealed class CursorPreview : Control
{
    private AppSettings _settings = new();

    public AppSettings Settings
    {
        get => _settings;
        set
        {
            _settings = value;
            InvalidateVisual();
        }
    }

    public void Refresh() => InvalidateVisual();

    public override void Render(DrawingContext context)
    {
        var b = new Rect(Bounds.Size);
        context.DrawRectangle(new SolidColorBrush(Color.FromRgb(0xEC, 0xEE, 0xF2)), null, new RoundedRect(b, 8));
        // Faux contenu d'écran pour juger du contraste.
        var line = new SolidColorBrush(Color.FromRgb(0xC5, 0xCB, 0xD3));
        for (var y = 18.0; y < b.Height - 10; y += 22)
            context.DrawRectangle(line, null, new Rect(16, y, b.Width * (0.45 + 0.4 * ((y / 22) % 2)), 8), 3, 3);

        var c = new Point(b.Width * 0.38, b.Height / 2);
        var s = _settings;
        if (s.Spot.Enabled)
        {
            var dim = Color.FromArgb((byte)(s.Spot.Dimming * 255), 0, 0, 0);
            var r = Math.Min(s.Spot.Radius, b.Height * 0.45);
            context.DrawRectangle(new RadialGradientBrush
            {
                Center = new RelativePoint(c, RelativeUnit.Absolute),
                GradientOrigin = new RelativePoint(c, RelativeUnit.Absolute),
                RadiusX = new RelativeScalar(r, RelativeUnit.Absolute),
                RadiusY = new RelativeScalar(r, RelativeUnit.Absolute),
                GradientStops = { new GradientStop(Color.FromArgb(0, 0, 0, 0), 0.88), new GradientStop(dim, 1) },
            }, null, new RoundedRect(b, 8));
        }
        if (s.Clicks.Enabled)
        {
            // Trois ondes figées à mi-parcours : gauche, droit, milieu.
            var colors = new[] { s.Clicks.LeftColor, s.Clicks.RightColor, s.Clicks.MiddleColor };
            for (var i = 0; i < 3; i++)
            {
                var p = new Point(b.Width * (0.66 + i * 0.12), b.Height / 2);
                var col = colors[i];
                var radius = s.Clicks.MaxRadius * 0.6;
                context.DrawEllipse(new SolidColorBrush(Color.FromRgb(col.R, col.G, col.B), 0.15), null, p, radius, radius);
                context.DrawEllipse(null, new Pen(new SolidColorBrush(Color.FromRgb(col.R, col.G, col.B), 0.7), 3), p, radius, radius);
            }
        }
        if (s.Ring.Enabled)
        {
            var ring = s.Ring;
            var rr = ring.Size / 2;
            context.DrawEllipse(null, new Pen(new SolidColorBrush(Colors.Black, 0.22 * ring.Opacity), ring.Thickness + 2), c, rr, rr);
            context.DrawEllipse(null, new Pen(new SolidColorBrush(Color.FromRgb(ring.Color.R, ring.Color.G, ring.Color.B), ring.Opacity), ring.Thickness), c, rr, rr);
        }
        // Pointeur stylisé au centre de l'anneau.
        var arrow = new StreamGeometry();
        using (var g = arrow.Open())
        {
            g.BeginFigure(c, true);
            g.LineTo(new Point(c.X, c.Y + 17));
            g.LineTo(new Point(c.X + 4.5, c.Y + 13));
            g.LineTo(new Point(c.X + 8, c.Y + 19));
            g.LineTo(new Point(c.X + 10.5, c.Y + 18));
            g.LineTo(new Point(c.X + 7, c.Y + 12));
            g.LineTo(new Point(c.X + 12, c.Y + 12));
            g.EndFigure(true);
        }
        context.DrawGeometry(Brushes.White, new Pen(Brushes.Black, 1.2), arrow);
    }
}
