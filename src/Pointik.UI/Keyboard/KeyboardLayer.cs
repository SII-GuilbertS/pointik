using Avalonia;
using Avalonia.Media;
using Pointik.Core.Geometry;
using Pointik.Core.Keys;
using Pointik.Core.Settings;
using Pointik.Core.Themes;

namespace Pointik.UI.Keyboard;

/// <summary>Dessine la pile de bulles (overlay et aperçu des réglages).</summary>
public static class KeyboardLayer
{
    /// <summary>Amplitude du glissement d'entrée et de sortie (unités logiques).</summary>
    private const double SlideDistance = 36;

    /// <summary>Dessine les bulles dans la zone donnée et retourne le cadre de la colonne.</summary>
    public static Rect Draw(DrawingContext ctx, IReadOnlyList<KeyBubble> bubbles, Func<KeyBubble, BubbleVisualState> stateOf,
        BubbleRenderer renderer, KeyboardStyle style, KeyboardPosition position, Rect area)
    {
        if (bubbles.Count == 0) return default;

        var sizes = new ScreenSize[bubbles.Count];
        for (var i = 0; i < bubbles.Count; i++)
        {
            var s = renderer.Measure(bubbles[i]);
            sizes[i] = new ScreenSize(s.Width, s.Height);
        }
        var layout = BubbleStackLayout.Compute(new ScreenRect(area.X, area.Y, area.Width, area.Height),
            position, sizes, renderer.Spacing);
        // Les bulles glissent depuis le bord le plus proche de l'écran.
        var fromRight = layout.Column.Center.X >= area.Center.X;
        var direction = fromRight ? 1 : -1;

        for (var i = 0; i < bubbles.Count; i++)
        {
            var b = bubbles[i];
            var st = stateOf(b);
            var r = layout.Bubbles[i];
            var (dx, opacity) = Animate(style, st, direction);
            renderer.Draw(ctx, b, new Rect(r.X + dx, r.Y, r.Width, r.Height), opacity);
        }
        var c = layout.Column;
        return new Rect(c.X, c.Y, c.Width, c.Height);
    }

    private static (double Dx, double Opacity) Animate(KeyboardStyle style, BubbleVisualState st, int direction)
    {
        double dx = 0, opacity = 1;
        switch (style.EnterAnimation)
        {
            case BubbleAnimation.Fade:
                opacity *= st.Enter;
                break;
            case BubbleAnimation.Slide:
                dx += direction * SlideDistance * (1 - st.Enter);
                opacity *= st.Enter;
                break;
        }
        switch (style.ExitAnimation)
        {
            case BubbleAnimation.Fade:
                opacity *= 1 - st.Exit;
                break;
            case BubbleAnimation.Slide:
                dx += direction * SlideDistance * st.Exit;
                opacity *= 1 - st.Exit;
                break;
            case BubbleAnimation.None:
                if (st.Exit > 0) opacity = 0;
                break;
        }
        return (dx, opacity);
    }

    /// <summary>Pastille rouge « clavier en pause », en haut à droite de la zone.</summary>
    public static void DrawPauseBadge(DrawingContext ctx, Rect area)
    {
        const double margin = 14, dot = 7;
        var label = new FormattedText("Clavier en pause", System.Globalization.CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight, new Typeface(FontFamily.Default, FontStyle.Normal, FontWeight.SemiBold), 12,
            Brushes.White);
        var w = label.Width + dot * 2 + 22;
        var h = Math.Max(label.Height + 8, 24);
        var rect = new Rect(area.Right - margin - w, area.Y + margin, w, h);
        ctx.DrawRectangle(new SolidColorBrush(Color.FromArgb(200, 20, 20, 24)), null, new RoundedRect(rect, h / 2));
        var center = new Point(rect.X + 10 + dot, rect.Center.Y);
        ctx.DrawEllipse(new SolidColorBrush(Color.FromRgb(0xE5, 0x2B, 0x2B)), null, center, dot, dot);
        ctx.DrawText(label, new Point(center.X + dot + 7, rect.Center.Y - label.Height / 2));
    }
}
