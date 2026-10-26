using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Pointik.Core.Drawing;
using Pointik.Core.Graphics;
using Pointik.Core.Platform;
using Pointik.Core.Settings;

namespace Pointik.UI.Drawing;

/// <summary>
/// Mode dessin : une fenêtre transparente couvre l'écran du curseur et capte la souris.
/// Barre d'outils en haut ; raccourcis : C cadre, O ovale, L ligne libre, Maj = carré/cercle,
/// Ctrl+Z annuler, Suppr tout effacer, Échap quitter (les dessins sont effacés).
/// </summary>
public sealed class DrawingWindow : Window
{
    private static readonly (string Name, string Hex)[] Palette =
    [
        ("Rouge", "#E52B2B"), ("Jaune", "#FFC41F"), ("Vert", "#2EC27E"),
        ("Bleu", "#2D7FF9"), ("Blanc", "#FFFFFF"), ("Noir", "#111111"),
    ];

    private static readonly (string Name, double Value)[] Thicknesses = [("Fin", 3), ("Moyen", 6), ("Épais", 12)];

    private static readonly IBrush BarBackground = new SolidColorBrush(Color.FromArgb(235, 28, 31, 38));
    private static readonly IBrush SelectedBackground = new SolidColorBrush(Color.FromArgb(255, 70, 76, 90));
    private static readonly IBrush NormalBackground = Brushes.Transparent;

    private readonly ScreenInfo _screen;
    private readonly IOverlayWindowHost _host;
    private readonly DrawingSettings _settings;
    private readonly Action _settingsChanged;
    private readonly DrawingCanvas _canvas;
    private readonly Dictionary<DrawingTool, Button> _toolButtons = new();
    private readonly List<(Button Button, ArgbColor Color)> _colorButtons = new();
    private readonly List<(Button Button, double Value)> _thicknessButtons = new();

    /// <param name="settingsChanged">Appelé quand l'outil, la couleur ou l'épaisseur change (enregistrement).</param>
    public DrawingWindow(ScreenInfo screen, IOverlayWindowHost host, DrawingSettings settings, Action settingsChanged)
    {
        _screen = screen;
        _host = host;
        _settings = settings;
        _settingsChanged = settingsChanged;

        Title = "Pointik — Dessin";
        SystemDecorations = SystemDecorations.None;
        Background = Brushes.Transparent;
        TransparencyLevelHint = [WindowTransparencyLevel.Transparent];
        Topmost = true;
        ShowInTaskbar = false;
        CanResize = false;
        WindowStartupLocation = WindowStartupLocation.Manual;
        Position = new PixelPoint((int)screen.Bounds.X, (int)screen.Bounds.Y);
        Width = screen.Bounds.Width / screen.Scale;
        Height = screen.Bounds.Height / screen.Scale;

        _canvas = new DrawingCanvas(new DrawingBoard())
        {
            Tool = settings.Tool,
            Color = settings.Color,
            Thickness = settings.Thickness,
        };

        var root = new Panel();
        root.Children.Add(_canvas);
        root.Children.Add(BuildToolbar());
        Content = root;
        RefreshSelection();
    }

    public DrawingBoard Board => _canvas.Board;

    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);
        var handle = TryGetPlatformHandle()?.Handle ?? 0;
        _host.ConfigureDrawingSurface(handle);
        _host.SetBounds(handle, _screen.Bounds);
        Activate();
        _canvas.Focus();
    }

    // ------------------------------------------------------------------ Barre d'outils

    private Control BuildToolbar()
    {
        var bar = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4 };

        AddTool(bar, DrawingTool.Rectangle, "▭  Cadre", "Cadre (C) — Maj : carré");
        AddTool(bar, DrawingTool.Ellipse, "◯  Ovale", "Ovale (O) — Maj : cercle");
        AddTool(bar, DrawingTool.Freehand, "✎  Main levée", "Ligne libre (L)");
        bar.Children.Add(Separator());

        foreach (var (name, hex) in Palette)
        {
            var color = ArgbColor.Parse(hex);
            var b = BarButton(new Border
            {
                Width = 18, Height = 18, CornerRadius = new CornerRadius(9),
                Background = new SolidColorBrush(Color.Parse(hex)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(140, 255, 255, 255)), BorderThickness = new Thickness(1),
            }, name);
            b.Click += (_, _) => SetColor(color);
            _colorButtons.Add((b, color));
            bar.Children.Add(b);
        }
        bar.Children.Add(Separator());

        foreach (var (name, value) in Thicknesses)
        {
            var dot = Math.Min(16, value + 2);
            var b = BarButton(new Border
            {
                Width = 18, Height = 18,
                Child = new Ellipse { Width = dot, Height = dot, Fill = Brushes.White, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center },
            }, $"Trait {name.ToLowerInvariant()}");
            b.Click += (_, _) => SetThickness(value);
            _thicknessButtons.Add((b, value));
            bar.Children.Add(b);
        }
        bar.Children.Add(Separator());

        var undo = BarButton(Label("↶  Annuler"), "Annuler la dernière forme (Ctrl + Z)");
        undo.Click += (_, _) => Undo();
        var clear = BarButton(Label("Tout effacer"), "Tout effacer (Suppr)");
        clear.Click += (_, _) => ClearAll();
        var quit = BarButton(Label("✕  Quitter"), "Quitter le dessin (Échap)");
        quit.Click += (_, _) => Close();
        bar.Children.Add(undo);
        bar.Children.Add(clear);
        bar.Children.Add(quit);

        return new Border
        {
            Background = BarBackground,
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(8, 6),
            Margin = new Thickness(0, 12, 0, 0),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Top,
            BoxShadow = new BoxShadows(new BoxShadow { OffsetY = 3, Blur = 14, Color = Color.FromArgb(110, 0, 0, 0) }),
            Child = bar,
            Cursor = new Cursor(StandardCursorType.Arrow),
        };
    }

    private void AddTool(Panel bar, DrawingTool tool, string text, string tip)
    {
        var b = BarButton(Label(text), tip);
        b.Click += (_, _) => SetTool(tool);
        _toolButtons[tool] = b;
        bar.Children.Add(b);
    }

    private static Button BarButton(Control content, string tip)
    {
        var b = new Button
        {
            Content = content,
            Background = NormalBackground,
            Padding = new Thickness(8, 5),
            CornerRadius = new CornerRadius(6),
            VerticalAlignment = VerticalAlignment.Center,
            Focusable = false,
        };
        ToolTip.SetTip(b, tip);
        return b;
    }

    private static TextBlock Label(string text) =>
        new() { Text = text, Foreground = Brushes.White, FontSize = 13, VerticalAlignment = VerticalAlignment.Center };

    private static Control Separator() =>
        new Border { Width = 1, Margin = new Thickness(4, 4), Background = new SolidColorBrush(Color.FromArgb(70, 255, 255, 255)) };

    private void RefreshSelection()
    {
        foreach (var (tool, b) in _toolButtons)
            b.Background = tool == _canvas.Tool ? SelectedBackground : NormalBackground;
        foreach (var (b, c) in _colorButtons)
            b.Background = c == _canvas.Color ? SelectedBackground : NormalBackground;
        foreach (var (b, v) in _thicknessButtons)
            b.Background = Math.Abs(v - _canvas.Thickness) < 0.01 ? SelectedBackground : NormalBackground;
    }

    // ------------------------------------------------------------------ Actions

    private void SetTool(DrawingTool tool)
    {
        _canvas.Tool = _settings.Tool = tool;
        RefreshSelection();
        _settingsChanged();
    }

    private void SetColor(ArgbColor color)
    {
        _canvas.Color = _settings.Color = color;
        RefreshSelection();
        _settingsChanged();
    }

    private void SetThickness(double value)
    {
        _canvas.Thickness = _settings.Thickness = value;
        RefreshSelection();
        _settingsChanged();
    }

    private void Undo()
    {
        if (Board.Undo()) _canvas.InvalidateVisual();
    }

    private void ClearAll()
    {
        Board.Clear();
        _canvas.InvalidateVisual();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        var ctrl = e.KeyModifiers.HasFlag(KeyModifiers.Control);
        switch (e.Key)
        {
            case Key.Escape:
                if (Board.Current is not null)
                {
                    // Échap pendant un tracé : on abandonne seulement la forme en cours.
                    Board.Cancel();
                    _canvas.InvalidateVisual();
                }
                else
                {
                    Close();
                }
                break;
            case Key.Z when ctrl:
                Undo();
                break;
            case Key.Delete or Key.Back:
                ClearAll();
                break;
            case Key.C when !ctrl:
                SetTool(DrawingTool.Rectangle);
                break;
            case Key.O when !ctrl:
                SetTool(DrawingTool.Ellipse);
                break;
            case Key.L when !ctrl:
                SetTool(DrawingTool.Freehand);
                break;
            default:
                return;
        }
        e.Handled = true;
    }
}
