using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Pointik.Core.Geometry;
using Pointik.Core.Keys;
using Pointik.Core.Settings;
using Pointik.UI.Keyboard;

namespace Pointik.UI.Settings;

/// <summary>
/// Mode placement : un aperçu des bulles que l'on déplace à la souris. À la validation,
/// la position est mémorisée en fraction de la zone de travail de l'écran (résiste aux
/// changements de résolution et de DPI).
/// </summary>
public sealed class PlacementWindow : Window
{
    private readonly SettingsViewModel _vm;
    private readonly AppController _app;
    private readonly SampleBubbles _sample;

    public PlacementWindow(SettingsViewModel vm, AppController app)
    {
        _vm = vm;
        _app = app;
        Title = "Pointik — Placement";
        SystemDecorations = SystemDecorations.None;
        Background = Brushes.Transparent;
        TransparencyLevelHint = [WindowTransparencyLevel.Transparent];
        Topmost = true;
        ShowInTaskbar = false;
        CanResize = false;
        SizeToContent = SizeToContent.WidthAndHeight;
        WindowStartupLocation = WindowStartupLocation.Manual;
        Cursor = new Cursor(StandardCursorType.SizeAll);

        _sample = new SampleBubbles(app.Settings.Keyboard);
        var hint = new TextBlock
        {
            Text = "Glissez l'aperçu à l'endroit voulu — Entrée : valider, Échap : annuler",
            Foreground = Brushes.White, FontSize = 12, Margin = new Thickness(0, 0, 0, 6),
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        var ok = new Button { Content = "Valider", Padding = new Thickness(14, 4) };
        var cancel = new Button { Content = "Annuler", Padding = new Thickness(14, 4) };
        ok.Click += (_, _) => Validate();
        cancel.Click += (_, _) => Close();

        Content = new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(150, 20, 22, 28)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(0xFF, 0xC4, 0x1F)),
            BorderThickness = new Thickness(2),
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(12),
            Child = new StackPanel
            {
                Children =
                {
                    hint,
                    _sample,
                    new StackPanel
                    {
                        Orientation = Orientation.Horizontal, Spacing = 8, Margin = new Thickness(0, 10, 0, 0),
                        HorizontalAlignment = HorizontalAlignment.Center, Children = { ok, cancel },
                    },
                },
            },
        };
        PointerPressed += (_, e) =>
        {
            if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed && e.Source is not Button)
                BeginMoveDrag(e);
        };
        KeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter) Validate();
            else if (e.Key == Key.Escape) Close();
        };
    }

    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);
        // Départ : centre de l'écran principal.
        var screens = _app.Screens;
        var primary = screens.FirstOrDefault(s => s.IsPrimary) ?? screens[0];
        var scale = RenderScaling;
        Position = new PixelPoint(
            (int)(primary.WorkArea.Center.X - Bounds.Width * scale / 2),
            (int)(primary.WorkArea.Center.Y - Bounds.Height * scale / 2));
        Activate();
    }

    /// <summary>Calcule la position des bulles d'aperçu (pixels physiques) et l'enregistre en fraction.</summary>
    private void Validate()
    {
        var scale = RenderScaling;
        var origin = _sample.TranslatePoint(new Point(0, 0), this) ?? default;
        var rect = new ScreenRect(
            Position.X + origin.X * scale, Position.Y + origin.Y * scale,
            _sample.Bounds.Width * scale, _sample.Bounds.Height * scale);
        var screen = _app.ScreenService.GetScreenFromPoint(rect.Center);
        var pos = _app.Settings.Keyboard.Position;
        var (fx, fy) = BubbleStackLayout.ToCustomFraction(screen.WorkArea, rect, pos.Alignment, pos.Stack);
        _vm.SetCustomPosition(fx, fy, screen);
        Close();
    }

    /// <summary>Trois bulles d'exemple, dessinées avec le style courant et à leur taille réelle.</summary>
    private sealed class SampleBubbles : Control
    {
        private readonly KeyboardSettings _settings;
        private readonly KeyDisplayEngine _engine;
        private readonly BubbleRenderer _renderer;
        private readonly KeyboardPosition _layout;

        public SampleBubbles(KeyboardSettings settings)
        {
            _settings = settings;
            _renderer = new BubbleRenderer(settings.Style);
            _engine = new KeyDisplayEngine(() => settings, new KeyNames());
            var n = new KeyNames();
            var t = TimeSpan.FromHours(1);
            _engine.AddShortcut(n.GetParts(Core.Input.KeyModifiers.Ctrl, Core.Input.KeyId.C), t);
            _engine.AddShortcut(n.GetParts(Core.Input.KeyModifiers.Ctrl | Core.Input.KeyModifiers.Shift, Core.Input.KeyId.S), t);
            _engine.AddShortcut(n.GetParts(Core.Input.KeyModifiers.Alt, Core.Input.KeyId.Tab), t);
            // Colonne dessinée depuis le coin supérieur gauche du contrôle, avec l'alignement choisi.
            _layout = new KeyboardPosition
            {
                Corner = ScreenCorner.TopLeft, OffsetX = 0, OffsetY = 0,
                Alignment = settings.Position.Alignment, Stack = settings.Position.Stack,
            };
        }

        protected override Size MeasureOverride(Size availableSize)
        {
            var sizes = _engine.Bubbles.Select(_renderer.Measure).ToList();
            return new Size(sizes.Max(s => s.Width), sizes.Sum(s => s.Height) + _renderer.Spacing * (sizes.Count - 1));
        }

        public override void Render(DrawingContext context) =>
            KeyboardLayer.Draw(context, _engine.Bubbles, _ => new BubbleVisualState(1, 0), _renderer,
                _settings.Style, _layout, new Rect(Bounds.Size));
    }
}
