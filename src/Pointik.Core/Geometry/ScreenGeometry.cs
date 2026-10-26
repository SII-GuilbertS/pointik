namespace Pointik.Core.Geometry;

/// <summary>
/// Point en pixels physiques dans le repère du bureau virtuel (peut être négatif
/// pour les écrans placés à gauche ou au-dessus de l'écran principal).
/// </summary>
public readonly record struct ScreenPoint(double X, double Y)
{
    public static ScreenPoint operator +(ScreenPoint a, ScreenPoint b) => new(a.X + b.X, a.Y + b.Y);
    public static ScreenPoint operator -(ScreenPoint a, ScreenPoint b) => new(a.X - b.X, a.Y - b.Y);

    /// <summary>Distance euclidienne entre deux points.</summary>
    public double DistanceTo(ScreenPoint other)
    {
        var dx = X - other.X;
        var dy = Y - other.Y;
        return Math.Sqrt(dx * dx + dy * dy);
    }
}

/// <summary>Taille en pixels physiques.</summary>
public readonly record struct ScreenSize(double Width, double Height);

/// <summary>Rectangle en pixels physiques dans le repère du bureau virtuel.</summary>
public readonly record struct ScreenRect(double X, double Y, double Width, double Height)
{
    public double Left => X;
    public double Top => Y;
    public double Right => X + Width;
    public double Bottom => Y + Height;
    public ScreenPoint TopLeft => new(X, Y);
    public ScreenPoint Center => new(X + Width / 2, Y + Height / 2);
    public ScreenSize Size => new(Width, Height);

    /// <summary>Indique si le point est dans le rectangle (bord droit et bas exclus).</summary>
    public bool Contains(ScreenPoint p) => p.X >= X && p.X < Right && p.Y >= Y && p.Y < Bottom;

    /// <summary>Distance au carré entre le point et le rectangle (0 si le point est dedans).</summary>
    public double DistanceSquaredTo(ScreenPoint p)
    {
        var dx = Math.Max(Math.Max(X - p.X, 0), p.X - Right);
        var dy = Math.Max(Math.Max(Y - p.Y, 0), p.Y - Bottom);
        return dx * dx + dy * dy;
    }
}
