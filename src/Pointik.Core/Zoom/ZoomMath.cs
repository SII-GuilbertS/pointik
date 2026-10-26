using Pointik.Core.Geometry;

namespace Pointik.Core.Zoom;

/// <summary>
/// Calculs purs du zoom : la « vue » est la zone de l'écran (pixels physiques, non agrandis)
/// affichée en plein écran. Son coin supérieur gauche est l'origine transmise à l'API de zoom.
/// </summary>
public static class ZoomMath
{
    /// <summary>Taille de la zone visible pour un facteur donné.</summary>
    public static ScreenSize ViewSize(ScreenRect screen, double scale) =>
        new(screen.Width / scale, screen.Height / scale);

    /// <summary>Garde la vue entièrement à l'intérieur de l'écran.</summary>
    public static ScreenPoint Clamp(ScreenPoint origin, ScreenRect screen, double scale)
    {
        var v = ViewSize(screen, scale);
        return new ScreenPoint(
            Math.Clamp(origin.X, screen.X, screen.Right - v.Width),
            Math.Clamp(origin.Y, screen.Y, screen.Bottom - v.Height));
    }

    /// <summary>Mode « centré » : le curseur reste au centre de la vue (sauf contre les bords de l'écran).</summary>
    public static ScreenPoint Centered(ScreenPoint cursor, ScreenRect screen, double scale)
    {
        var v = ViewSize(screen, scale);
        return Clamp(new ScreenPoint(cursor.X - v.Width / 2, cursor.Y - v.Height / 2), screen, scale);
    }

    /// <summary>
    /// Vue qui garde le curseur au même endroit de l'écran pendant un changement de facteur
    /// (effet « on plonge vers le curseur »).
    /// </summary>
    public static ScreenPoint AnchoredAtCursor(ScreenPoint cursor, ScreenRect screen, double scale) =>
        Clamp(new ScreenPoint(
            cursor.X - (cursor.X - screen.X) / scale,
            cursor.Y - (cursor.Y - screen.Y) / scale), screen, scale);

    /// <summary>
    /// Mode « bord » : la vue ne bouge que si le curseur entre dans la marge (fraction de la vue).
    /// </summary>
    public static ScreenPoint EdgePush(ScreenPoint origin, ScreenPoint cursor, ScreenRect screen, double scale, double marginFraction)
    {
        var v = ViewSize(screen, scale);
        var mx = v.Width * marginFraction;
        var my = v.Height * marginFraction;
        var x = origin.X;
        var y = origin.Y;
        if (cursor.X < x + mx) x = cursor.X - mx;
        else if (cursor.X > x + v.Width - mx) x = cursor.X - v.Width + mx;
        if (cursor.Y < y + my) y = cursor.Y - my;
        else if (cursor.Y > y + v.Height - my) y = cursor.Y - v.Height + my;
        return Clamp(new ScreenPoint(x, y), screen, scale);
    }

    /// <summary>Position à l'écran (après agrandissement) d'un point non agrandi.</summary>
    public static ScreenPoint ToMagnified(ScreenPoint p, ScreenRect screen, ScreenPoint origin, double scale) =>
        new(screen.X + (p.X - origin.X) * scale, screen.Y + (p.Y - origin.Y) * scale);
}
