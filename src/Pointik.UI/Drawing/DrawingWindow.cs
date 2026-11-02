using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Pointik.Core.Drawing;
using Pointik.Core.Graphics;
using Pointik.Core.Platform;
using Pointik.Core.Settings;

namespace Pointik.UI.Drawing;

/// <summary>
/// Mode dessin : une fenêtre transparente couvre l'écran du curseur et capte la souris.
/// Barre d'outils en haut ; raccourcis : C cadre, O ovale, R zone, D ligne droite, F flèche, L ligne libre,
/// S surligneur, N numéro, T texte,
/// G gomme, Maj = carré/cercle/45°/ligne droite, Ctrl+Z annuler, Ctrl+Y rétablir, Suppr tout effacer,
/// Ctrl+S capture d'écran avec les annotations, Échap quitter (les dessins sont masqués, pas effacés :
/// la planche appartient à l'appelant et se retrouve à la réouverture ; avec « Laisser affiché »,
/// elle reste visible dans l'overlay traversable aux clics).
/// Pendant la saisie d'un texte, le clavier sert à écrire.
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
    private readonly IScreenCapture _capture;
    private readonly Action<string, string> _notify;
    private readonly Action<string, Exception> _logError;
    private readonly Action _toggleKeepVisible;
    private Button? _keepVisibleButton;

    /// <param name="settingsChanged">Appelé quand l'outil, la couleur ou l'épaisseur change (enregistrement).</param>
    /// <param name="notify">Affiche une notification (titre, message).</param>
    /// <param name="logError">Consigne une erreur dans le journal.</param>
    /// <param name="toggleKeepVisible">Bascule « laisser les dessins affichés » hors du mode dessin.</param>
    public DrawingWindow(ScreenInfo screen, DrawingBoard board, IOverlayWindowHost host, IScreenCapture capture, DrawingSettings settings,
        Action settingsChanged, Action<string, string> notify, Action<string, Exception> logError,
        Action toggleKeepVisible)
    {
        _screen = screen;
        _host = host;
        _capture = capture;
        _settings = settings;
        _settingsChanged = settingsChanged;
        _notify = notify;
        _logError = logError;
        _toggleKeepVisible = toggleKeepVisible;

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

        _canvas = new DrawingCanvas(board)
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

    /// <summary>Redessine la planche après une modification extérieure (tout effacer depuis le menu).</summary>
    public void RefreshCanvas() => _canvas.InvalidateVisual();

    /// <summary>Met à jour les boutons après un changement extérieur (menu de l'icône).</summary>
    public void RefreshToolbar() => RefreshSelection();

    protected override void OnClosing(WindowClosingEventArgs e)
    {
        base.OnClosing(e);
        if (e.Cancel) return;
        // La planche est conservée : on valide le texte en cours et on termine proprement un geste inachevé.
        _canvas.CommitText();
        if (Board.IsErasing) Board.EndErase();
        else if (Board.Current is not null) Board.Cancel();
    }

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
        // WrapPanel : sur un petit écran, la barre passe sur deux lignes au lieu de déborder.
        var bar = new WrapPanel { Orientation = Orientation.Horizontal };

        AddTool(bar, DrawingTool.Rectangle, "▭  Cadre", "Cadre (C) — Maj : carré");
        AddTool(bar, DrawingTool.Ellipse, "◯  Ovale", "Ovale (O) — Maj : cercle");
        AddTool(bar, DrawingTool.FilledRectangle, "▆  Zone", "Zone : rectangle plein semi-transparent (R) — Maj : carré");
        AddTool(bar, DrawingTool.Line, "╱  Ligne", "Ligne droite (D) — Maj : angle de 45°");
        AddTool(bar, DrawingTool.Arrow, "➜  Flèche", "Flèche (F) — Maj : angle de 45°");
        AddTool(bar, DrawingTool.Freehand, "✎  Main levée", "Ligne libre (L)");
        AddTool(bar, DrawingTool.Highlighter, "▬  Surligneur", "Surligneur (S) — Maj : ligne droite");
        AddTool(bar, DrawingTool.Number, "①  Numéro", "Numéro d'étape (N) — chaque clic pose le numéro suivant : 1, 2, 3…");
        AddTool(bar, DrawingTool.Text, "T  Texte", "Texte (T) — cliquer puis taper ; Entrée : nouvelle ligne ; Échap ou clic ailleurs : valider");
        AddTool(bar, DrawingTool.Eraser, "⌫  Gomme", "Gomme (G) — cliquer ou glisser sur les formes à effacer");
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
            }, $"Trait {name.ToLowerInvariant()} (et taille du texte)");
            b.Click += (_, _) => SetThickness(value);
            _thicknessButtons.Add((b, value));
            bar.Children.Add(b);
        }
        bar.Children.Add(Separator());

        var undo = BarButton(Label("↶  Annuler"), "Annuler (Ctrl + Z)");
        undo.Click += (_, _) => Undo();
        var redo = BarButton(Label("↷  Rétablir"), "Rétablir (Ctrl + Y)");
        redo.Click += (_, _) => Redo();
        var clear = BarButton(Label("🗑  Tout effacer"), "Tout effacer (Suppr) — annulable avec Ctrl + Z");
        clear.Click += (_, _) => ClearAll();
        var capture = BarButton(Label("📷  Capture"),
            "Capture de l'écran avec les annotations (Ctrl + S) — enregistrée dans Images\\Pointik et copiée dans le presse-papiers");
        capture.Click += (_, _) => CaptureScreen();
        _keepVisibleButton = BarButton(Label("📌  Laisser affiché"),
            "Laisser les dessins affichés après avoir quitté le mode dessin — les clics atteignent alors les applications");
        _keepVisibleButton.Click += (_, _) => _toggleKeepVisible();
        var quit = BarButton(Label("✕  Quitter"), "Quitter le dessin (Échap) — les dessins sont masqués et réapparaissent à la prochaine ouverture");
        quit.Click += (_, _) => Close();
        bar.Children.Add(undo);
        bar.Children.Add(redo);
        bar.Children.Add(clear);
        bar.Children.Add(Separator());
        bar.Children.Add(capture);
        bar.Children.Add(_keepVisibleButton);
        bar.Children.Add(quit);

        return new Border
        {
            Background = BarBackground,
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(8, 6),
            Margin = new Thickness(16, 12, 16, 0),
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
            Margin = new Thickness(2, 1),
            VerticalAlignment = VerticalAlignment.Center,
            Focusable = false,
        };
        ToolTip.SetTip(b, tip);
        return b;
    }

    private static TextBlock Label(string text) =>
        new() { Text = text, Foreground = Brushes.White, FontSize = 13, VerticalAlignment = VerticalAlignment.Center };

    private static Control Separator() =>
        new Border { Width = 1, Height = 22, Margin = new Thickness(5, 0), VerticalAlignment = VerticalAlignment.Center, Background = new SolidColorBrush(Color.FromArgb(70, 255, 255, 255)) };

    private void RefreshSelection()
    {
        foreach (var (tool, b) in _toolButtons)
            b.Background = tool == _canvas.Tool ? SelectedBackground : NormalBackground;
        foreach (var (b, c) in _colorButtons)
            b.Background = c == _canvas.Color ? SelectedBackground : NormalBackground;
        foreach (var (b, v) in _thicknessButtons)
            b.Background = Math.Abs(v - _canvas.Thickness) < 0.01 ? SelectedBackground : NormalBackground;
        if (_keepVisibleButton is not null)
            _keepVisibleButton.Background = _settings.KeepVisible ? SelectedBackground : NormalBackground;
    }

    // ------------------------------------------------------------------ Actions

    private void SetTool(DrawingTool tool)
    {
        // Changer d'outil valide le texte en cours.
        _canvas.CommitText();
        _canvas.Tool = _settings.Tool = tool;
        RefreshSelection();
        _settingsChanged();
    }

    private void SetColor(ArgbColor color)
    {
        _canvas.Color = _settings.Color = color;
        _canvas.RestyleText();
        RefreshSelection();
        _settingsChanged();
    }

    private void SetThickness(double value)
    {
        _canvas.Thickness = _settings.Thickness = value;
        _canvas.RestyleText();
        RefreshSelection();
        _settingsChanged();
    }

    private void Undo()
    {
        // Pendant la saisie d'un texte, annuler abandonne ce texte.
        if (Board.IsEditingText) _canvas.CancelText();
        else if (Board.Undo()) _canvas.InvalidateVisual();
    }

    private void Redo()
    {
        _canvas.CommitText();
        if (Board.Redo()) _canvas.InvalidateVisual();
    }

    private void ClearAll()
    {
        Board.Clear();
        _canvas.InvalidateVisual();
    }

    /// <summary>
    /// Capture l'écran sans les fenêtres de Pointik, y dessine les annotations à la résolution réelle,
    /// enregistre un PNG dans Images\Pointik et le copie dans le presse-papiers.
    /// </summary>
    private void CaptureScreen()
    {
        _canvas.CommitText();
        if (Board.Current is not null) return; // pendant un tracé
        try
        {
            var shot = _capture.Capture(_screen.Bounds);
            if (shot is null)
            {
                _notify("Capture impossible", "Windows n'a pas fourni l'image de l'écran.");
                return;
            }

            var size = new PixelSize(shot.Width, shot.Height);
            var dpi = new Vector(96, 96);
            using var background = new WriteableBitmap(size, dpi, PixelFormat.Bgra8888, AlphaFormat.Opaque);
            using (var buffer = background.Lock())
            {
                for (var row = 0; row < shot.Height; row++)
                    Marshal.Copy(shot.Bgra, row * shot.Stride, buffer.Address + row * buffer.RowBytes, shot.Stride);
            }

            using var image = new RenderTargetBitmap(size, dpi);
            using (var ctx = image.CreateDrawingContext())
            {
                ctx.DrawImage(background, new Rect(0, 0, shot.Width, shot.Height));
                // Les annotations sont en pixels logiques : mise à l'échelle de l'écran (125 %, 150 %…).
                using (ctx.PushTransform(Matrix.CreateScale(_screen.Scale, _screen.Scale)))
                    DrawingCanvas.RenderAnnotations(ctx, Board);
            }

            var folder = ScreenshotFiles.DefaultFolder();
            Directory.CreateDirectory(folder);
            var path = ScreenshotFiles.NextPath(folder, DateTime.Now, File.Exists);
            image.Save(path);

            var copied = _capture.CopyToClipboard(new CapturedImage(shot.Width, shot.Height, ReadBgra(image, size)));
            _canvas.Flash();
            var name = System.IO.Path.GetFileName(path);
            _notify("Capture enregistrée", copied
                ? $"Copiée dans le presse-papiers et enregistrée dans Images\\Pointik :\n{name}"
                : $"Enregistrée dans Images\\Pointik :\n{name}\n(presse-papiers indisponible)");
        }
        catch (Exception ex)
        {
            _logError("Capture d'écran", ex);
            _notify("Capture impossible", ex.Message);
        }
    }

    /// <summary>Pixels du rendu final en BGRA (l'image est opaque : prémultiplication sans effet).</summary>
    private static byte[] ReadBgra(Bitmap image, PixelSize size)
    {
        var stride = size.Width * 4;
        var pixels = new byte[stride * size.Height];
        var handle = GCHandle.Alloc(pixels, GCHandleType.Pinned);
        try
        {
            image.CopyPixels(new PixelRect(size), handle.AddrOfPinnedObject(), pixels.Length, stride);
        }
        finally
        {
            handle.Free();
        }
        if (image.Format == PixelFormat.Rgba8888)
        {
            for (var i = 0; i < pixels.Length; i += 4) (pixels[i], pixels[i + 2]) = (pixels[i + 2], pixels[i]);
        }
        return pixels;
    }

    private async void PasteText()
    {
        try
        {
            if (Clipboard is { } clipboard && await clipboard.TryGetTextAsync() is { Length: > 0 } text)
                _canvas.TypeText(text);
        }
        catch
        {
            // Presse-papiers indisponible (occupé par une autre application) : on ignore.
        }
    }

    protected override void OnTextInput(TextInputEventArgs e)
    {
        base.OnTextInput(e);
        if (!Board.IsEditingText || string.IsNullOrEmpty(e.Text)) return;
        _canvas.TypeText(e.Text);
        e.Handled = true;
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (Board.IsEditingText ? HandleTextKey(e) : HandleKey(e)) e.Handled = true;
    }

    /// <summary>Touches pendant la saisie d'un texte ; les caractères arrivent par <see cref="OnTextInput"/>.</summary>
    private bool HandleTextKey(KeyEventArgs e)
    {
        var ctrl = e.KeyModifiers.HasFlag(KeyModifiers.Control);
        switch (e.Key)
        {
            case Key.Escape:
            case Key.Enter when ctrl:
                _canvas.CommitText();
                return true;
            case Key.Enter:
                _canvas.TypeText("\n");
                return true;
            case Key.Back:
                _canvas.DeleteLastChar();
                return true;
            case Key.V when ctrl:
                PasteText();
                return true;
            case Key.S when ctrl:
                CaptureScreen();
                return true;
            case Key.Z when ctrl:
                Undo();
                return true;
            case Key.Y when ctrl:
                Redo();
                return true;
            default:
                return false;
        }
    }

    private bool HandleKey(KeyEventArgs e)
    {
        var ctrl = e.KeyModifiers.HasFlag(KeyModifiers.Control);
        var shift = e.KeyModifiers.HasFlag(KeyModifiers.Shift);
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
            case Key.Z when ctrl && shift:
            case Key.Y when ctrl:
                Redo();
                break;
            case Key.Z when ctrl:
                Undo();
                break;
            case Key.Delete or Key.Back:
                ClearAll();
                break;
            case Key.S when ctrl:
            case Key.PrintScreen:
                CaptureScreen();
                break;
            case Key.S when !ctrl:
                SetTool(DrawingTool.Highlighter);
                break;
            case Key.T when !ctrl:
                SetTool(DrawingTool.Text);
                break;
            case Key.G when !ctrl:
                SetTool(DrawingTool.Eraser);
                break;
            case Key.C when !ctrl:
                SetTool(DrawingTool.Rectangle);
                break;
            case Key.O when !ctrl:
                SetTool(DrawingTool.Ellipse);
                break;
            case Key.F when !ctrl:
                SetTool(DrawingTool.Arrow);
                break;
            case Key.L when !ctrl:
                SetTool(DrawingTool.Freehand);
                break;
            case Key.D when !ctrl:
                SetTool(DrawingTool.Line);
                break;
            case Key.R when !ctrl:
                SetTool(DrawingTool.FilledRectangle);
                break;
            case Key.N when !ctrl:
                SetTool(DrawingTool.Number);
                break;
            default:
                return false;
        }
        return true;
    }
}
