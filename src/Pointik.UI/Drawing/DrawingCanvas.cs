using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using Pointik.Core.Drawing;
using Pointik.Core.Geometry;
using Pointik.Core.Graphics;

namespace Pointik.UI.Drawing;

/// <summary>Surface où l'on dessine à la souris : formes, surligneur, texte et gomme.</summary>
public sealed class DrawingCanvas : Control
{
    // Fond presque invisible (alpha 1/255) : la fenêtre reçoit les clics partout sans masquer l'écran.
    private static readonly IBrush CatchAllBackground = new SolidColorBrush(Avalonia.Media.Color.FromArgb(1, 0, 0, 0));
    private static readonly Typeface TextTypeface = new(FontFamily.Default, FontStyle.Normal, FontWeight.SemiBold);
    private static readonly IPen EditFramePen = new Pen(new SolidColorBrush(Avalonia.Media.Color.FromArgb(170, 255, 255, 255)), 1, new DashStyle([4, 3], 0));
    private static readonly IPen EraserPen = new Pen(new SolidColorBrush(Avalonia.Media.Color.FromArgb(220, 255, 255, 255)), 1.5);
    private static readonly IPen EraserShadowPen = new Pen(new SolidColorBrush(Avalonia.Media.Color.FromArgb(160, 0, 0, 0)), 3.5);

    private readonly DispatcherTimer _caretTimer;
    private bool _caretVisible = true;
    private DrawingTool _tool;
    private Point? _hover;

    public DrawingCanvas(DrawingBoard board)
    {
        Board = board;
        Cursor = new Cursor(StandardCursorType.Cross);
        Focusable = true;
        _caretTimer = new DispatcherTimer(TimeSpan.FromMilliseconds(530), DispatcherPriority.Background, (_, _) =>
        {
            if (!Board.IsEditingText) return;
            _caretVisible = !_caretVisible;
            InvalidateVisual();
        });
    }

    public DrawingBoard Board { get; }

    /// <summary>Outil des prochaines formes (le curseur s'adapte).</summary>
    public DrawingTool Tool
    {
        get => _tool;
        set
        {
            _tool = value;
            Cursor = new Cursor(value == DrawingTool.Text ? StandardCursorType.Ibeam : StandardCursorType.Cross);
            InvalidateVisual();
        }
    }

    /// <summary>Couleur et épaisseur des prochaines formes.</summary>
    public ArgbColor Color { get; set; }
    public double Thickness { get; set; }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _caretTimer.Start();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _caretTimer.Stop();
    }

    // ------------------------------------------------------------------ Souris

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;
        var p = ToPoint(e.GetPosition(this));
        // Un clic ailleurs valide le texte en cours.
        CommitText();
        e.Handled = true;
        switch (Tool)
        {
            case DrawingTool.Text:
                // Le clic désigne le coin haut gauche ; on remonte d'un demi-interligne pour que le texte
                // apparaisse centré sur le pointeur.
                Board.BeginText(Color, Thickness, new ScreenPoint(p.X, p.Y - LineHeight(DrawingBoard.FontSizeFor(Thickness)) / 2));
                RestartCaret();
                return;
            case DrawingTool.Eraser:
                Board.BeginErase(p);
                break;
            default:
                Board.Begin(Tool, Color, Thickness, p);
                break;
        }
        e.Pointer.Capture(this);
        InvalidateVisual();
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        var position = e.GetPosition(this);
        _hover = position;
        if (Board.IsErasing)
            Board.EraseTo(ToPoint(position));
        else if (Board.Current is not null && !Board.IsEditingText)
            Board.Extend(ToPoint(position), e.KeyModifiers.HasFlag(KeyModifiers.Shift));
        else if (Tool != DrawingTool.Eraser)
            return;
        InvalidateVisual();
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        _hover = null;
        if (Tool == DrawingTool.Eraser) InvalidateVisual();
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (Board.IsErasing)
        {
            Board.EraseTo(ToPoint(e.GetPosition(this)));
            Board.EndErase();
        }
        else if (Board.Current is not null && !Board.IsEditingText)
        {
            Board.End(ToPoint(e.GetPosition(this)), e.KeyModifiers.HasFlag(KeyModifiers.Shift));
        }
        else
        {
            return;
        }
        e.Pointer.Capture(null);
        InvalidateVisual();
    }

    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        if (Board.IsErasing)
            Board.EndErase();
        else if (Board.Current is not null && !Board.IsEditingText)
            Board.Cancel();
        else
            return;
        InvalidateVisual();
    }

    // ------------------------------------------------------------------ Texte

    /// <summary>Ajoute du texte saisi ou collé au texte en cours.</summary>
    public void TypeText(string text)
    {
        if (!Board.IsEditingText) return;
        Board.TypeText(text);
        RestartCaret();
    }

    /// <summary>Efface le dernier caractère du texte en cours.</summary>
    public void DeleteLastChar()
    {
        Board.DeleteLastChar();
        RestartCaret();
    }

    /// <summary>Valide le texte en cours (ignoré s'il est vide).</summary>
    public void CommitText()
    {
        if (!Board.IsEditingText) return;
        Board.EndText(a =>
        {
            var text = CreateText(a.Text, a);
            return (text.WidthIncludingTrailingWhitespace, text.Height);
        });
        InvalidateVisual();
    }

    /// <summary>Abandonne le texte en cours.</summary>
    public void CancelText()
    {
        if (!Board.IsEditingText) return;
        Board.Cancel();
        InvalidateVisual();
    }

    /// <summary>Applique la couleur et l'épaisseur actuelles au texte en cours de saisie.</summary>
    public void RestyleText()
    {
        Board.RestyleText(Color, Thickness);
        RestartCaret();
    }

    private void RestartCaret()
    {
        _caretVisible = true;
        InvalidateVisual();
    }

    private static FormattedText CreateText(string text, Annotation a) =>
        new(text, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, TextTypeface, a.FontSize, ToBrush(a.Color));

    private static double LineHeight(double fontSize) =>
        new FormattedText("Ag", CultureInfo.CurrentCulture, FlowDirection.LeftToRight, TextTypeface, fontSize, Brushes.White).Height;

    // ------------------------------------------------------------------ Rendu

    public override void Render(DrawingContext context)
    {
        context.FillRectangle(CatchAllBackground, new Rect(Bounds.Size));
        foreach (var shape in Board.Shapes) DrawShape(context, shape);
        if (Board.Current is { } current)
        {
            if (current.Tool == DrawingTool.Text) DrawTextEditor(context, current);
            else DrawShape(context, current);
        }
        if (Tool == DrawingTool.Eraser && _hover is { } h)
        {
            context.DrawEllipse(null, EraserShadowPen, h, DrawingBoard.EraserRadius, DrawingBoard.EraserRadius);
            context.DrawEllipse(null, EraserPen, h, DrawingBoard.EraserRadius, DrawingBoard.EraserRadius);
        }
    }

    private void DrawTextEditor(DrawingContext ctx, Annotation a)
    {
        var lineHeight = LineHeight(a.FontSize);
        var lines = a.Text.Split('\n');
        var lastLineWidth = lines[^1].Length == 0 ? 0 : CreateText(lines[^1], a).WidthIncludingTrailingWhitespace;
        var width = a.Text.Length == 0 ? 0 : CreateText(a.Text, a).WidthIncludingTrailingWhitespace;
        var height = Math.Max(lineHeight, lines.Length * lineHeight);
        if (a.Text.Length > 0) DrawText(ctx, a);

        // Cadre pointillé autour de la zone de saisie, et curseur clignotant en fin de texte.
        ctx.DrawRectangle(null, EditFramePen, new Rect(a.Start.X - 6, a.Start.Y - 4, width + 14, height + 8), 4, 4);
        if (_caretVisible)
        {
            var x = a.Start.X + lastLineWidth + 1;
            var y = a.Start.Y + (lines.Length - 1) * lineHeight;
            var caret = new Pen(ToBrush(a.Color), Math.Max(2, a.FontSize / 14));
            ctx.DrawLine(caret, new Point(x, y + lineHeight * 0.1), new Point(x, y + lineHeight * 0.9));
        }
    }

    private static void DrawText(DrawingContext ctx, Annotation a)
    {
        var text = CreateText(a.Text, a);
        var geometry = text.BuildGeometry(new Point(a.Start.X, a.Start.Y));
        if (geometry is null) return;
        // Halo contrasté autour des lettres : le texte reste lisible sur n'importe quel fond.
        var c = a.Color;
        var luminance = (0.2126 * c.R + 0.7152 * c.G + 0.0722 * c.B) / 255;
        var halo = luminance > 0.55 ? Avalonia.Media.Color.FromArgb(150, 0, 0, 0) : Avalonia.Media.Color.FromArgb(190, 255, 255, 255);
        ctx.DrawGeometry(null, new Pen(new SolidColorBrush(halo), Math.Max(3, a.FontSize / 7), lineJoin: PenLineJoin.Round), geometry);
        ctx.DrawGeometry(ToBrush(a.Color), null, geometry);
    }

    private static void DrawShape(DrawingContext ctx, Annotation a)
    {
        var brush = ToBrush(a.Color);
        var pen = new Pen(brush, a.Thickness, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round);
        switch (a.Tool)
        {
            case DrawingTool.Text:
                DrawText(ctx, a);
                break;
            case DrawingTool.Rectangle:
            {
                var r = a.Bounds;
                ctx.DrawRectangle(null, pen, new Rect(r.X, r.Y, r.Width, r.Height));
                break;
            }
            case DrawingTool.Ellipse:
            {
                var r = a.Bounds;
                ctx.DrawEllipse(null, pen, new Point(r.Center.X, r.Center.Y), r.Width / 2, r.Height / 2);
                break;
            }
            case DrawingTool.Arrow:
            {
                var arrow = ArrowShape.Compute(a.Start, a.End, a.Thickness);
                ctx.DrawLine(pen, ToAvalonia(arrow.Tail), ToAvalonia(arrow.ShaftEnd));
                var head = new StreamGeometry();
                using (var g = head.Open())
                {
                    g.BeginFigure(ToAvalonia(arrow.Tip), true);
                    g.LineTo(ToAvalonia(arrow.WingLeft));
                    g.LineTo(ToAvalonia(arrow.WingRight));
                    g.EndFigure(true);
                }
                // Pointe pleine, aux angles légèrement arrondis par le contour.
                ctx.DrawGeometry(brush, new Pen(brush, Math.Min(a.Thickness, 4), lineJoin: PenLineJoin.Round), head);
                break;
            }
            case DrawingTool.Freehand or DrawingTool.Highlighter:
            {
                if (a.Points.Count == 1)
                {
                    // Simple clic : un point.
                    ctx.DrawEllipse(brush, null, new Point(a.Start.X, a.Start.Y), a.Thickness / 2, a.Thickness / 2);
                    break;
                }
                // Un seul tracé : le surligneur semi-transparent ne fonce pas là où il repasse sur lui-même.
                var geometry = new StreamGeometry();
                using (var g = geometry.Open())
                {
                    g.BeginFigure(new Point(a.Points[0].X, a.Points[0].Y), false);
                    for (var i = 1; i < a.Points.Count; i++) g.LineTo(new Point(a.Points[i].X, a.Points[i].Y));
                    g.EndFigure(false);
                }
                ctx.DrawGeometry(null, pen, geometry);
                break;
            }
        }
    }

    private static IBrush ToBrush(ArgbColor c) => new SolidColorBrush(Avalonia.Media.Color.FromArgb(c.A, c.R, c.G, c.B));

    private static ScreenPoint ToPoint(Point p) => new(p.X, p.Y);

    private static Point ToAvalonia(ScreenPoint p) => new(p.X, p.Y);
}
