using Pointik.Core.Geometry;
using Pointik.Core.Graphics;

namespace Pointik.Core.Drawing;

/// <summary>Outils de dessin à l'écran.</summary>
public enum DrawingTool
{
    /// <summary>Cadre rectangulaire (carré avec Maj).</summary>
    Rectangle,
    /// <summary>Ovale (cercle avec Maj).</summary>
    Ellipse,
    /// <summary>Ligne libre (main levée).</summary>
    Freehand,
    /// <summary>Flèche du point de départ vers la pointe (angle multiple de 45° avec Maj).</summary>
    Arrow,
}

/// <summary>Géométrie d'une flèche : pointe pleine proportionnelle à l'épaisseur du trait.</summary>
public readonly record struct ArrowShape(ScreenPoint Tail, ScreenPoint ShaftEnd, ScreenPoint Tip, ScreenPoint WingLeft, ScreenPoint WingRight)
{
    /// <summary>Demi-angle d'ouverture de la pointe.</summary>
    public const double HeadHalfAngleDegrees = 28;

    /// <summary>Calcule la flèche de <paramref name="tail"/> vers <paramref name="tip"/>.</summary>
    public static ArrowShape Compute(ScreenPoint tail, ScreenPoint tip, double thickness)
    {
        var dx = tip.X - tail.X;
        var dy = tip.Y - tail.Y;
        var length = Math.Sqrt(dx * dx + dy * dy);
        if (length < 1e-6) return new ArrowShape(tail, tip, tip, tip, tip);
        var ux = dx / length;
        var uy = dy / length;
        // Pointe : au moins 14 px, plus grande pour un trait épais, jamais plus de la moitié de la flèche.
        var headLength = Math.Min(Math.Max(14, thickness * 3.6), length * 0.5);
        var halfWidth = headLength * Math.Tan(HeadHalfAngleDegrees * Math.PI / 180);
        var baseX = tip.X - ux * headLength;
        var baseY = tip.Y - uy * headLength;
        // Le trait s'arrête un peu avant la base pour que son extrémité arrondie ne dépasse pas de la pointe.
        var shaftEnd = new ScreenPoint(tip.X - ux * headLength * 0.8, tip.Y - uy * headLength * 0.8);
        return new ArrowShape(tail, shaftEnd, tip,
            new ScreenPoint(baseX + uy * halfWidth, baseY - ux * halfWidth),
            new ScreenPoint(baseX - uy * halfWidth, baseY + ux * halfWidth));
    }
}

/// <summary>Une forme dessinée. Les coordonnées sont locales à la surface de dessin.</summary>
public sealed class Annotation
{
    internal Annotation(DrawingTool tool, ArgbColor color, double thickness, ScreenPoint start)
    {
        Tool = tool;
        Color = color;
        Thickness = thickness;
        Start = start;
        End = start;
        _points.Add(start);
    }

    private readonly List<ScreenPoint> _points = new();

    public DrawingTool Tool { get; }
    public ArgbColor Color { get; }
    public double Thickness { get; }

    /// <summary>Point de départ du tracé.</summary>
    public ScreenPoint Start { get; }

    /// <summary>Point d'arrivée (coin opposé du cadre ou de l'ovale, déjà contraint si Maj).</summary>
    public ScreenPoint End { get; internal set; }

    /// <summary>Points de la ligne libre (vide de sens pour les cadres et ovales).</summary>
    public IReadOnlyList<ScreenPoint> Points => _points;

    /// <summary>Rectangle englobant du cadre ou de l'ovale, toujours de largeur et hauteur positives.</summary>
    public ScreenRect Bounds => DrawingBoard.NormalizedRect(Start, End);

    internal void AddPoint(ScreenPoint p) => _points.Add(p);
}

/// <summary>
/// Planche de dessin : forme en cours, formes terminées, annulation.
/// Logique pure (aucun rendu) : la même sur toutes les plateformes.
/// </summary>
public sealed class DrawingBoard
{
    /// <summary>Distance minimale entre deux points d'une ligne libre (allège le tracé).</summary>
    public const double MinPointSpacing = 1.5;

    /// <summary>Taille en dessous de laquelle un cadre ou un ovale est considéré comme un clic accidentel.</summary>
    public const double MinShapeSize = 3;

    /// <summary>Longueur en dessous de laquelle une flèche est considérée comme un clic accidentel.</summary>
    public const double MinArrowLength = 8;

    private readonly List<Annotation> _shapes = new();

    /// <summary>Formes terminées, de la plus ancienne à la plus récente.</summary>
    public IReadOnlyList<Annotation> Shapes => _shapes;

    /// <summary>Forme en cours de tracé (bouton enfoncé), ou null.</summary>
    public Annotation? Current { get; private set; }

    public bool IsEmpty => _shapes.Count == 0 && Current is null;

    /// <summary>Commence une forme au point donné.</summary>
    public void Begin(DrawingTool tool, ArgbColor color, double thickness, ScreenPoint p) =>
        Current = new Annotation(tool, color, Math.Clamp(thickness, 1, 40), p);

    /// <summary>Prolonge la forme en cours. <paramref name="constrain"/> : carré ou cercle (Maj).</summary>
    public void Extend(ScreenPoint p, bool constrain)
    {
        var c = Current;
        if (c is null) return;
        if (c.Tool == DrawingTool.Freehand)
        {
            if (c.Points[^1].DistanceTo(p) >= MinPointSpacing) c.AddPoint(p);
            c.End = p;
            return;
        }
        c.End = !constrain ? p
            : c.Tool == DrawingTool.Arrow ? SnapAngle(c.Start, p)
            : Constrain(c.Start, p);
    }

    /// <summary>Termine la forme en cours. Retourne false si elle était trop petite (ignorée).</summary>
    public bool End(ScreenPoint p, bool constrain)
    {
        var c = Current;
        if (c is null) return false;
        Extend(p, constrain);
        Current = null;
        var keep = c.Tool switch
        {
            DrawingTool.Freehand => c.Points.Count >= 2 || c.Start.DistanceTo(c.End) > 0,
            DrawingTool.Arrow => c.Start.DistanceTo(c.End) >= MinArrowLength,
            _ => c.Bounds.Width >= MinShapeSize || c.Bounds.Height >= MinShapeSize,
        };
        if (keep) _shapes.Add(c);
        return keep;
    }

    /// <summary>Abandonne la forme en cours (Échap pendant un tracé).</summary>
    public void Cancel() => Current = null;

    /// <summary>Retire la dernière forme. Retourne false s'il n'y avait rien à annuler.</summary>
    public bool Undo()
    {
        if (_shapes.Count == 0) return false;
        _shapes.RemoveAt(_shapes.Count - 1);
        return true;
    }

    /// <summary>Efface tout.</summary>
    public void Clear()
    {
        _shapes.Clear();
        Current = null;
    }

    /// <summary>Rectangle aux dimensions positives à partir de deux coins opposés.</summary>
    public static ScreenRect NormalizedRect(ScreenPoint a, ScreenPoint b) =>
        new(Math.Min(a.X, b.X), Math.Min(a.Y, b.Y), Math.Abs(b.X - a.X), Math.Abs(b.Y - a.Y));

    /// <summary>Extrémité contrainte à un angle multiple de 45° (horizontal, vertical, diagonales), même longueur.</summary>
    public static ScreenPoint SnapAngle(ScreenPoint start, ScreenPoint p)
    {
        var dx = p.X - start.X;
        var dy = p.Y - start.Y;
        var length = Math.Sqrt(dx * dx + dy * dy);
        var step = Math.PI / 4;
        var angle = Math.Round(Math.Atan2(dy, dx) / step) * step;
        return new ScreenPoint(start.X + Math.Cos(angle) * length, start.Y + Math.Sin(angle) * length);
    }

    /// <summary>Coin opposé contraint pour obtenir un carré (ou un cercle), dans la direction du tracé.</summary>
    public static ScreenPoint Constrain(ScreenPoint start, ScreenPoint p)
    {
        var dx = p.X - start.X;
        var dy = p.Y - start.Y;
        var side = Math.Max(Math.Abs(dx), Math.Abs(dy));
        return new ScreenPoint(start.X + (dx < 0 ? -side : side), start.Y + (dy < 0 ? -side : side));
    }
}
