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
    /// <summary>Surligneur : trait large et semi-transparent (ligne droite avec Maj).</summary>
    Highlighter,
    /// <summary>Texte saisi au clavier à l'endroit du clic.</summary>
    Text,
    /// <summary>Gomme : efface les formes touchées.</summary>
    Eraser,
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
    public ArgbColor Color { get; internal set; }

    /// <summary>Épaisseur du trait (pour le texte : épaisseur choisie, qui donne la taille de police).</summary>
    public double Thickness { get; internal set; }

    /// <summary>Point de départ du tracé (coin haut gauche pour un texte).</summary>
    public ScreenPoint Start { get; }

    /// <summary>Point d'arrivée (coin opposé du cadre ou de l'ovale, déjà contraint si Maj).</summary>
    public ScreenPoint End { get; internal set; }

    /// <summary>Points de la ligne libre ou du surligneur (vide de sens pour les autres formes).</summary>
    public IReadOnlyList<ScreenPoint> Points => _points;

    /// <summary>Texte saisi (outil Texte ; lignes séparées par « \n »).</summary>
    public string Text { get; internal set; } = "";

    /// <summary>Taille de police du texte.</summary>
    public double FontSize => DrawingBoard.FontSizeFor(Thickness);

    /// <summary>Dimensions mesurées du texte terminé.</summary>
    public double TextWidth { get; internal set; }
    public double TextHeight { get; internal set; }

    /// <summary>Rectangle englobant du cadre, de l'ovale ou du texte, toujours de largeur et hauteur positives.</summary>
    public ScreenRect Bounds => Tool == DrawingTool.Text
        ? new ScreenRect(Start.X, Start.Y, TextWidth, TextHeight)
        : DrawingBoard.NormalizedRect(Start, End);

    internal void AddPoint(ScreenPoint p) => _points.Add(p);

    /// <summary>Remplace le tracé par un segment droit (surligneur avec Maj).</summary>
    internal void MakeStraight(ScreenPoint end)
    {
        _points.Clear();
        _points.Add(Start);
        _points.Add(end);
    }
}

/// <summary>
/// Planche de dessin : forme en cours, formes terminées, gomme, texte, annuler et rétablir.
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

    /// <summary>Rayon de la gomme autour du pointeur.</summary>
    public const double EraserRadius = 10;

    /// <summary>Opacité du surligneur.</summary>
    public const double HighlighterOpacity = 0.4;

    private readonly List<Annotation> _shapes = new();
    private readonly Stack<Edit> _undo = new();
    private readonly Stack<Edit> _redo = new();
    private List<(int Index, Annotation Shape)>? _erased;
    private ScreenPoint _eraserLast;

    /// <summary>Formes terminées, de la plus ancienne à la plus récente.</summary>
    public IReadOnlyList<Annotation> Shapes => _shapes;

    /// <summary>Forme en cours de tracé (bouton enfoncé) ou texte en cours de saisie, ou null.</summary>
    public Annotation? Current { get; private set; }

    public bool IsEmpty => _shapes.Count == 0 && Current is null;

    /// <summary>Un texte est en cours de saisie.</summary>
    public bool IsEditingText => Current?.Tool == DrawingTool.Text;

    /// <summary>La gomme est en cours d'utilisation (bouton enfoncé).</summary>
    public bool IsErasing => _erased is not null;

    public bool CanUndo => _undo.Count > 0;
    public bool CanRedo => _redo.Count > 0;

    /// <summary>Épaisseur réelle du surligneur pour une épaisseur choisie : bien plus large qu'un trait.</summary>
    public static double HighlighterWidth(double thickness) => 10 + 2.5 * Math.Clamp(thickness, 1, 40);

    /// <summary>Taille de police du texte pour une épaisseur choisie.</summary>
    public static double FontSizeFor(double thickness) => 14 + 2 * Math.Clamp(thickness, 1, 40);

    // ------------------------------------------------------------------ Formes tracées

    /// <summary>Commence une forme au point donné (cadre, ovale, flèche, ligne libre, surligneur).</summary>
    public void Begin(DrawingTool tool, ArgbColor color, double thickness, ScreenPoint p)
    {
        thickness = Math.Clamp(thickness, 1, 40);
        if (tool == DrawingTool.Highlighter)
        {
            color = color.WithOpacity(HighlighterOpacity);
            thickness = HighlighterWidth(thickness);
        }
        Current = new Annotation(tool, color, thickness, p);
    }

    /// <summary>Prolonge la forme en cours. <paramref name="constrain"/> : carré, cercle, 45° ou ligne droite (Maj).</summary>
    public void Extend(ScreenPoint p, bool constrain)
    {
        var c = Current;
        if (c is null || c.Tool == DrawingTool.Text) return;
        switch (c.Tool)
        {
            case DrawingTool.Highlighter when constrain:
                c.End = SnapAngle(c.Start, p);
                c.MakeStraight(c.End);
                return;
            case DrawingTool.Freehand or DrawingTool.Highlighter:
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
        if (c is null || c.Tool == DrawingTool.Text) return false;
        Extend(p, constrain);
        Current = null;
        var keep = c.Tool switch
        {
            DrawingTool.Freehand or DrawingTool.Highlighter => c.Points.Count >= 2 || c.Start.DistanceTo(c.End) > 0,
            DrawingTool.Arrow => c.Start.DistanceTo(c.End) >= MinArrowLength,
            _ => c.Bounds.Width >= MinShapeSize || c.Bounds.Height >= MinShapeSize,
        };
        if (keep) Commit(c);
        return keep;
    }

    /// <summary>Abandonne la forme ou le texte en cours (Échap pendant un tracé).</summary>
    public void Cancel() => Current = null;

    // ------------------------------------------------------------------ Texte

    /// <summary>Commence la saisie d'un texte dont le coin haut gauche est <paramref name="topLeft"/>.</summary>
    public void BeginText(ArgbColor color, double thickness, ScreenPoint topLeft) =>
        Current = new Annotation(DrawingTool.Text, color, Math.Clamp(thickness, 1, 40), topLeft);

    /// <summary>Ajoute du texte saisi ou collé (retours à la ligne normalisés, caractères de contrôle ignorés).</summary>
    public void TypeText(string text)
    {
        if (!IsEditingText || string.IsNullOrEmpty(text)) return;
        // Un « \r » isolé (touche Entrée relayée comme caractère) est ignoré : la nouvelle ligne passe par « \n ».
        var normalized = text.Replace("\r\n", "\n").Replace("\r", "").Replace('\t', ' ');
        var kept = new string(normalized.Where(ch => ch == '\n' || !char.IsControl(ch)).ToArray());
        Current!.Text += kept;
    }

    /// <summary>Change la couleur et la taille du texte en cours de saisie.</summary>
    public void RestyleText(ArgbColor color, double thickness)
    {
        if (!IsEditingText) return;
        Current!.Color = color;
        Current.Thickness = Math.Clamp(thickness, 1, 40);
    }

    /// <summary>Efface le dernier caractère saisi (Retour arrière).</summary>
    public void DeleteLastChar()
    {
        if (!IsEditingText) return;
        var t = Current!.Text;
        if (t.Length == 0) return;
        var cut = t.Length >= 2 && char.IsLowSurrogate(t[^1]) && char.IsHighSurrogate(t[^2]) ? 2 : 1;
        Current.Text = t[..^cut];
    }

    /// <summary>
    /// Termine la saisie. Le texte est débarrassé des espaces et lignes vides finales puis mesuré par
    /// <paramref name="measure"/> (rendu propre à la plateforme). Retourne false si le texte était vide (ignoré).
    /// </summary>
    public bool EndText(Func<Annotation, (double Width, double Height)> measure)
    {
        var c = Current;
        if (c is null || c.Tool != DrawingTool.Text) return false;
        Current = null;
        c.Text = c.Text.TrimEnd();
        if (c.Text.Length == 0) return false;
        (c.TextWidth, c.TextHeight) = measure(c);
        Commit(c);
        return true;
    }

    // ------------------------------------------------------------------ Gomme

    /// <summary>Commence à gommer : efface les formes touchées au point donné.</summary>
    public void BeginErase(ScreenPoint p)
    {
        _erased = new List<(int, Annotation)>();
        _eraserLast = p;
        EraseAt(p);
    }

    /// <summary>Prolonge le geste de gomme jusqu'à <paramref name="p"/>, sans sauter de forme si la souris va vite.</summary>
    public void EraseTo(ScreenPoint p)
    {
        if (_erased is null) return;
        var distance = _eraserLast.DistanceTo(p);
        var steps = Math.Max(1, (int)Math.Ceiling(distance / (EraserRadius / 2)));
        for (var i = 1; i <= steps; i++)
        {
            var t = (double)i / steps;
            EraseAt(new ScreenPoint(_eraserLast.X + (p.X - _eraserLast.X) * t, _eraserLast.Y + (p.Y - _eraserLast.Y) * t));
        }
        _eraserLast = p;
    }

    /// <summary>Termine le geste de gomme (une seule étape d'annulation). Retourne le nombre de formes effacées.</summary>
    public int EndErase()
    {
        var erased = _erased;
        _erased = null;
        if (erased is null || erased.Count == 0) return 0;
        Push(new RemoveEdit(erased));
        return erased.Count;
    }

    private void EraseAt(ScreenPoint p)
    {
        for (var i = _shapes.Count - 1; i >= 0; i--)
        {
            if (!Hits(_shapes[i], p, EraserRadius)) continue;
            _erased!.Add((i, _shapes[i]));
            _shapes.RemoveAt(i);
        }
    }

    /// <summary>La forme passe-t-elle à moins de <paramref name="tolerance"/> pixels du point ?</summary>
    public static bool Hits(Annotation a, ScreenPoint p, double tolerance)
    {
        var reach = a.Thickness / 2 + tolerance;
        switch (a.Tool)
        {
            case DrawingTool.Text:
            {
                var r = a.Bounds;
                return p.X >= r.X - tolerance && p.X <= r.Right + tolerance
                    && p.Y >= r.Y - tolerance && p.Y <= r.Bottom + tolerance;
            }
            case DrawingTool.Rectangle:
                return DistanceToRectOutline(a.Bounds, p) <= reach;
            case DrawingTool.Ellipse:
            {
                var r = a.Bounds;
                var rx = r.Width / 2;
                var ry = r.Height / 2;
                if (Math.Min(rx, ry) < 1) return DistanceToRectOutline(r, p) <= reach;
                var cx = r.X + rx;
                var cy = r.Y + ry;
                // Point de l'ellipse dans la direction du point (approximation suffisante pour la gomme).
                var t = Math.Atan2((p.Y - cy) * rx, (p.X - cx) * ry);
                return p.DistanceTo(new ScreenPoint(cx + rx * Math.Cos(t), cy + ry * Math.Sin(t))) <= reach;
            }
            case DrawingTool.Arrow:
                return DistanceToSegment(p, a.Start, a.End) <= reach;
            default:
            {
                var pts = a.Points;
                if (pts.Count == 1) return p.DistanceTo(pts[0]) <= reach;
                for (var i = 1; i < pts.Count; i++)
                    if (DistanceToSegment(p, pts[i - 1], pts[i]) <= reach) return true;
                return false;
            }
        }
    }

    /// <summary>Distance d'un point au segment [a, b].</summary>
    public static double DistanceToSegment(ScreenPoint p, ScreenPoint a, ScreenPoint b)
    {
        var dx = b.X - a.X;
        var dy = b.Y - a.Y;
        var lengthSquared = dx * dx + dy * dy;
        if (lengthSquared < 1e-12) return p.DistanceTo(a);
        var t = Math.Clamp(((p.X - a.X) * dx + (p.Y - a.Y) * dy) / lengthSquared, 0, 1);
        return p.DistanceTo(new ScreenPoint(a.X + dx * t, a.Y + dy * t));
    }

    private static double DistanceToRectOutline(ScreenRect r, ScreenPoint p)
    {
        var inside = p.X >= r.X && p.X <= r.Right && p.Y >= r.Y && p.Y <= r.Bottom;
        if (inside) return Math.Min(Math.Min(p.X - r.X, r.Right - p.X), Math.Min(p.Y - r.Y, r.Bottom - p.Y));
        var dx = Math.Max(Math.Max(r.X - p.X, 0), p.X - r.Right);
        var dy = Math.Max(Math.Max(r.Y - p.Y, 0), p.Y - r.Bottom);
        return Math.Sqrt(dx * dx + dy * dy);
    }

    // ------------------------------------------------------------------ Historique

    /// <summary>Annule la dernière action (forme ajoutée, gommée ou tout effacé). Retourne false s'il n'y avait rien à annuler.</summary>
    public bool Undo()
    {
        if (_undo.Count == 0) return false;
        var edit = _undo.Pop();
        Revert(edit);
        _redo.Push(edit);
        return true;
    }

    /// <summary>Rétablit la dernière action annulée. Retourne false s'il n'y avait rien à rétablir.</summary>
    public bool Redo()
    {
        if (_redo.Count == 0) return false;
        var edit = _redo.Pop();
        Apply(edit);
        _undo.Push(edit);
        return true;
    }

    /// <summary>Efface tout (annulable). Retourne false s'il n'y avait rien à effacer.</summary>
    public bool Clear()
    {
        Current = null;
        if (_shapes.Count == 0) return false;
        var removed = new List<(int, Annotation)>();
        for (var i = _shapes.Count - 1; i >= 0; i--) removed.Add((i, _shapes[i]));
        var edit = new RemoveEdit(removed);
        Apply(edit);
        Push(edit);
        return true;
    }

    private void Commit(Annotation shape)
    {
        _shapes.Add(shape);
        Push(new AddEdit(shape));
    }

    private void Push(Edit edit)
    {
        _undo.Push(edit);
        _redo.Clear();
    }

    private void Apply(Edit edit)
    {
        switch (edit)
        {
            case AddEdit add:
                _shapes.Add(add.Shape);
                break;
            case RemoveEdit remove:
                // Les index ont été relevés au moment de chaque suppression, dans l'ordre.
                foreach (var (index, _) in remove.Removed) _shapes.RemoveAt(index);
                break;
        }
    }

    private void Revert(Edit edit)
    {
        switch (edit)
        {
            case AddEdit add:
                _shapes.RemoveAt(_shapes.LastIndexOf(add.Shape));
                break;
            case RemoveEdit remove:
                // Réinsertion dans l'ordre inverse des suppressions : chaque forme retrouve sa place exacte.
                for (var i = remove.Removed.Count - 1; i >= 0; i--)
                    _shapes.Insert(remove.Removed[i].Index, remove.Removed[i].Shape);
                break;
        }
    }

    private abstract record Edit;
    private sealed record AddEdit(Annotation Shape) : Edit;
    private sealed record RemoveEdit(List<(int Index, Annotation Shape)> Removed) : Edit;

    // ------------------------------------------------------------------ Géométrie

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
