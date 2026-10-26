using Pointik.Core.Geometry;
using Pointik.Core.Settings;

namespace Pointik.Core.Keys;

/// <summary>
/// Calcule l'emplacement des bulles dans une zone (unités logiques de l'écran cible).
/// Les bulles forment une colonne : son bord est ancré au coin choisi (ou au point libre
/// du mode placement), et chaque bulle est alignée dans la colonne (gauche, centre, droite).
/// </summary>
public static class BubbleStackLayout
{
    /// <summary>Résultat : rectangle de chaque bulle (même ordre que l'entrée) et cadre de la colonne.</summary>
    public readonly record struct Result(IReadOnlyList<ScreenRect> Bubbles, ScreenRect Column);

    /// <param name="area">Zone de travail de l'écran (unités logiques).</param>
    /// <param name="position">Réglages de position.</param>
    /// <param name="sizes">Tailles des bulles, de la plus ancienne à la plus récente.</param>
    /// <param name="spacing">Espacement vertical entre bulles.</param>
    public static Result Compute(ScreenRect area, KeyboardPosition position, IReadOnlyList<ScreenSize> sizes, double spacing)
    {
        if (sizes.Count == 0) return new Result(Array.Empty<ScreenRect>(), default);

        var columnWidth = sizes.Max(s => s.Width);
        var columnHeight = sizes.Sum(s => s.Height) + spacing * (sizes.Count - 1);
        var (anchorX, anchorY, anchoredBottom) = GetAnchor(area, position);

        // Position horizontale de la colonne par rapport au point d'ancrage.
        double left;
        if (position.UseCustomPosition)
        {
            left = position.Alignment switch
            {
                BubbleAlignment.Left => anchorX,
                BubbleAlignment.Center => anchorX - columnWidth / 2,
                _ => anchorX - columnWidth,
            };
        }
        else
        {
            var rightCorner = position.Corner is ScreenCorner.TopRight or ScreenCorner.BottomRight;
            left = rightCorner ? anchorX - columnWidth : anchorX;
        }
        var top = anchoredBottom ? anchorY - columnHeight : anchorY;

        // La colonne reste entièrement visible dans la zone.
        left = Math.Clamp(left, area.X, Math.Max(area.X, area.Right - columnWidth));
        top = Math.Clamp(top, area.Y, Math.Max(area.Y, area.Bottom - columnHeight));

        // Ordre vertical : « vers le haut » = la plus récente en bas.
        var order = position.Stack == StackDirection.Up
            ? Enumerable.Range(0, sizes.Count)
            : Enumerable.Range(0, sizes.Count).Reverse();

        var rects = new ScreenRect[sizes.Count];
        var y = top;
        foreach (var i in order)
        {
            var s = sizes[i];
            var x = position.Alignment switch
            {
                BubbleAlignment.Left => left,
                BubbleAlignment.Center => left + (columnWidth - s.Width) / 2,
                _ => left + columnWidth - s.Width,
            };
            rects[i] = new ScreenRect(x, y, s.Width, s.Height);
            y += s.Height + spacing;
        }
        return new Result(rects, new ScreenRect(left, top, columnWidth, columnHeight));
    }

    /// <summary>
    /// Point d'ancrage de la colonne. En mode coin : décalage depuis le coin de la zone ;
    /// en position libre : fraction de la zone. Indique si l'ancrage est le bas de la colonne.
    /// </summary>
    public static (double X, double Y, bool AnchoredBottom) GetAnchor(ScreenRect area, KeyboardPosition position)
    {
        if (position.UseCustomPosition)
        {
            return (area.X + position.CustomX * area.Width,
                area.Y + position.CustomY * area.Height,
                position.Stack == StackDirection.Up);
        }
        var right = position.Corner is ScreenCorner.TopRight or ScreenCorner.BottomRight;
        var bottom = position.Corner is ScreenCorner.BottomLeft or ScreenCorner.BottomRight;
        var x = right ? area.Right - position.OffsetX : area.X + position.OffsetX;
        var y = bottom ? area.Bottom - position.OffsetY : area.Y + position.OffsetY;
        return (x, y, bottom);
    }

    /// <summary>
    /// Convertit l'emplacement d'un aperçu déplacé à la souris (mode placement) en position libre,
    /// exprimée en fraction de la zone (résiste aux changements de résolution et de DPI).
    /// </summary>
    public static (double X, double Y) ToCustomFraction(ScreenRect area, ScreenRect previewRect, BubbleAlignment alignment, StackDirection stack)
    {
        var x = alignment switch
        {
            BubbleAlignment.Left => previewRect.Left,
            BubbleAlignment.Center => previewRect.Center.X,
            _ => previewRect.Right,
        };
        var y = stack == StackDirection.Up ? previewRect.Bottom : previewRect.Top;
        return (Math.Clamp((x - area.X) / area.Width, 0, 1), Math.Clamp((y - area.Y) / area.Height, 0, 1));
    }
}
