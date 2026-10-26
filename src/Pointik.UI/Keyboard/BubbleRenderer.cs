using System.Globalization;
using Avalonia;
using Avalonia.Media;
using Pointik.Core.Graphics;
using Pointik.Core.Keys;
using Pointik.Core.Themes;

namespace Pointik.UI.Keyboard;

/// <summary>
/// Mesure et dessine les bulles du clavier selon un style. Utilisé par l'overlay
/// et par l'aperçu en direct de la fenêtre de réglages (rendu identique).
/// </summary>
public sealed class BubbleRenderer
{
    private const double KeycapDepth = 3;
    private const double KeycapGap = 6;

    private readonly KeyboardStyle _style;
    private readonly Typeface _typeface;
    private readonly IBrush _text;
    private readonly IBrush _background;
    private readonly IBrush _keycapEdge;
    private readonly IPen? _border;
    private readonly BoxShadows _shadow;

    public BubbleRenderer(KeyboardStyle style)
    {
        _style = style;
        Scale = style.ScalePercent / 100.0;
        _typeface = new Typeface(ResolveFont(style.FontFamily), FontStyle.Normal,
            style.Bold ? FontWeight.SemiBold : FontWeight.Normal);
        _text = new SolidColorBrush(ToColor(style.TextColor));
        // L'alpha de la couleur de fond est ignoré : seule l'opacité du fond compte.
        var bg = Color.FromRgb(style.BackgroundColor.R, style.BackgroundColor.G, style.BackgroundColor.B);
        _background = new SolidColorBrush(bg, style.BackgroundOpacity);
        _keycapEdge = new SolidColorBrush(Darken(bg, 0.28), Math.Max(style.BackgroundOpacity, 0.6));
        _border = style.BorderThickness > 0
            ? new Pen(new SolidColorBrush(ToColor(style.BorderColor)), style.BorderThickness * Scale)
            : null;
        _shadow = style.Shadow
            ? new BoxShadows(new BoxShadow { OffsetY = 3 * Scale, Blur = 14 * Scale, Color = Color.FromArgb(110, 0, 0, 0) })
            : default;
    }

    /// <summary>Échelle globale (1 = 100 %).</summary>
    public double Scale { get; }

    /// <summary>Espacement entre bulles, mis à l'échelle.</summary>
    public double Spacing => _style.Spacing * Scale;

    private double FontSize => _style.FontSize * Scale;
    private double PadH => _style.PaddingHorizontal * Scale;
    private double PadV => _style.PaddingVertical * Scale;
    private double Radius => _style.CornerRadius * Scale;
    private bool UseKeycaps(KeyBubble b) => _style.Presentation == KeyPresentation.Keycaps && !b.IsTextLine;

    /// <summary>Taille d'une bulle (unités logiques).</summary>
    public Size Measure(KeyBubble b)
    {
        if (!UseKeycaps(b))
        {
            var ft = Text(b.DisplayText);
            return new Size(ft.WidthIncludingTrailingWhitespace + 2 * PadH, ft.Height + 2 * PadV);
        }
        double width = 0, height = 0;
        foreach (var cap in Caps(b))
        {
            var size = MeasureCap(cap);
            width += size.Width + KeycapGap * Scale;
            height = Math.Max(height, size.Height);
        }
        return new Size(Math.Max(0, width - KeycapGap * Scale), height);
    }

    /// <summary>Dessine une bulle dans le rectangle donné.</summary>
    public void Draw(DrawingContext ctx, KeyBubble b, Rect rect, double opacity)
    {
        if (opacity <= 0.003) return;
        using var _ = ctx.PushOpacity(Math.Clamp(opacity, 0, 1));
        if (!UseKeycaps(b))
        {
            var rr = new RoundedRect(rect, Math.Min(Radius, rect.Height / 2));
            ctx.DrawRectangle(_background, _border, rr, _shadow);
            var ft = Text(b.DisplayText);
            ctx.DrawText(ft, new Point(rect.X + (rect.Width - ft.WidthIncludingTrailingWhitespace) / 2,
                rect.Y + (rect.Height - ft.Height) / 2));
            return;
        }
        var x = rect.X;
        foreach (var cap in Caps(b))
        {
            var size = MeasureCap(cap);
            DrawCap(ctx, cap, new Rect(x, rect.Y + (rect.Height - size.Height), size.Width, size.Height));
            x += size.Width + KeycapGap * Scale;
        }
    }

    /// <summary>Touches à dessiner : les éléments du raccourci, puis « ×3 » le cas échéant.</summary>
    private static IEnumerable<string> Caps(KeyBubble b)
    {
        foreach (var p in b.Parts) yield return p;
        if (b.Count > 1) yield return b.CountSuffix;
    }

    private Size MeasureCap(string label)
    {
        var ft = Text(label);
        var h = ft.Height + 2 * PadV + KeycapDepth * Scale;
        // Une touche n'est jamais plus étroite que haute (lettres seules : touche carrée).
        var w = Math.Max(ft.WidthIncludingTrailingWhitespace + 2 * PadH, h - KeycapDepth * Scale);
        return new Size(w, h);
    }

    private void DrawCap(DrawingContext ctx, string label, Rect rect)
    {
        var depth = KeycapDepth * Scale;
        var radius = Math.Min(Radius, rect.Height / 3);
        // Socle plus sombre (relief), puis face de la touche légèrement surélevée.
        ctx.DrawRectangle(_keycapEdge, null, new RoundedRect(rect, radius), _shadow);
        var face = new Rect(rect.X, rect.Y, rect.Width, rect.Height - depth);
        ctx.DrawRectangle(_background, _border, new RoundedRect(face, radius));
        var ft = Text(label);
        ctx.DrawText(ft, new Point(face.X + (face.Width - ft.WidthIncludingTrailingWhitespace) / 2,
            face.Y + (face.Height - ft.Height) / 2));
    }

    private FormattedText Text(string s) =>
        new(s, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, _typeface, FontSize, _text);

    /// <summary>Police demandée, avec repli sur la police par défaut si elle est absente.</summary>
    private static FontFamily ResolveFont(string name)
    {
        try
        {
            return string.IsNullOrWhiteSpace(name) ? FontFamily.Default : new FontFamily(name + ", Segoe UI, " + FontFamily.Default.Name);
        }
        catch
        {
            return FontFamily.Default;
        }
    }

    public static Color ToColor(ArgbColor c) => Color.FromArgb(c.A, c.R, c.G, c.B);

    private static Color Darken(Color c, double amount) => Color.FromArgb(c.A,
        (byte)(c.R * (1 - amount)), (byte)(c.G * (1 - amount)), (byte)(c.B * (1 - amount)));
}
