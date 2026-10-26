using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Pointik.Core.Drawing;
using Pointik.Core.Geometry;
using Pointik.Core.Graphics;

namespace Pointik.UI.Drawing;

/// <summary>Surface où l'on dessine à la souris : cadres, ovales et lignes libres.</summary>
public sealed class DrawingCanvas : Control
{
    // Fond presque invisible (alpha 1/255) : la fenêtre reçoit les clics partout sans masquer l'écran.
    private static readonly IBrush CatchAllBackground = new SolidColorBrush(Avalonia.Media.Color.FromArgb(1, 0, 0, 0));

    public DrawingCanvas(DrawingBoard board)
    {
        Board = board;
        Cursor = new Cursor(StandardCursorType.Cross);
        Focusable = true;
    }

    public DrawingBoard Board { get; }

    /// <summary>Outil, couleur et épaisseur des prochaines formes.</summary>
    public DrawingTool Tool { get; set; }
    public ArgbColor Color { get; set; }
    public double Thickness { get; set; }

    /// <summary>Une forme a été ajoutée, annulée ou effacée.</summary>
    public event Action? Changed;

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;
        Board.Begin(Tool, Color, Thickness, ToPoint(e.GetPosition(this)));
        e.Pointer.Capture(this);
        e.Handled = true;
        InvalidateVisual();
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (Board.Current is null) return;
        Board.Extend(ToPoint(e.GetPosition(this)), e.KeyModifiers.HasFlag(KeyModifiers.Shift));
        InvalidateVisual();
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (Board.Current is null) return;
        Board.End(ToPoint(e.GetPosition(this)), e.KeyModifiers.HasFlag(KeyModifiers.Shift));
        e.Pointer.Capture(null);
        InvalidateVisual();
        Changed?.Invoke();
    }

    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        if (Board.Current is null) return;
        Board.Cancel();
        InvalidateVisual();
    }

    public override void Render(DrawingContext context)
    {
        context.FillRectangle(CatchAllBackground, new Rect(Bounds.Size));
        foreach (var shape in Board.Shapes) DrawShape(context, shape);
        if (Board.Current is { } current) DrawShape(context, current);
    }

    private static void DrawShape(DrawingContext ctx, Annotation a)
    {
        var brush = new SolidColorBrush(Avalonia.Media.Color.FromArgb(a.Color.A, a.Color.R, a.Color.G, a.Color.B));
        var pen = new Pen(brush, a.Thickness, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round);
        switch (a.Tool)
        {
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
            case DrawingTool.Freehand:
            {
                if (a.Points.Count == 1)
                {
                    // Simple clic : un point.
                    ctx.DrawEllipse(brush, null, new Point(a.Start.X, a.Start.Y), a.Thickness / 2, a.Thickness / 2);
                    break;
                }
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

    private static ScreenPoint ToPoint(Point p) => new(p.X, p.Y);
}
