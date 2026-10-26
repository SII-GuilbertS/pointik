using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Layout;
using Avalonia.Media;
using Pointik.Core.Graphics;

namespace Pointik.UI.Settings;

/// <summary>
/// Choix d'une couleur sans dépendance supplémentaire : pastille (palette au clic) + saisie « #RRGGBB ».
/// </summary>
public sealed class ColorField : UserControl
{
    public static readonly StyledProperty<string> HexProperty =
        AvaloniaProperty.Register<ColorField, string>(nameof(Hex), "#FFFFFF", defaultBindingMode: BindingMode.TwoWay);

    private static readonly string[] Palette =
    [
        "#FFFFFF", "#D9DDE3", "#8A919C", "#1E222A", "#000000", "#FFC41F", "#FF8A1E", "#E5484D",
        "#E84393", "#9B5DE5", "#2D7FF9", "#16B1D9", "#2EC27E", "#8BC34A", "#FFEB3B", "#795548",
    ];

    private readonly Border _swatch;
    private readonly TextBox _text;
    private bool _updating;

    public ColorField()
    {
        _swatch = new Border
        {
            Width = 26, Height = 26, CornerRadius = new CornerRadius(5),
            BorderBrush = new SolidColorBrush(Color.FromArgb(120, 128, 128, 128)), BorderThickness = new Thickness(1),
        };
        var paletteGrid = new UniformGrid { Columns = 8, Width = 8 * 30 };
        var flyout = new Flyout { Content = paletteGrid, Placement = PlacementMode.Bottom };
        foreach (var hex in Palette)
        {
            var b = new Button
            {
                Width = 26, Height = 26, Margin = new Thickness(2), Padding = new Thickness(0),
                Background = new SolidColorBrush(Color.Parse(hex)), CornerRadius = new CornerRadius(4),
            };
            ToolTip.SetTip(b, hex);
            b.Click += (_, _) =>
            {
                Hex = hex;
                flyout.Hide();
            };
            paletteGrid.Children.Add(b);
        }
        var swatchButton = new Button { Content = _swatch, Padding = new Thickness(3), Flyout = flyout };
        ToolTip.SetTip(swatchButton, "Choisir dans la palette");

        _text = new TextBox { Width = 96, VerticalContentAlignment = VerticalAlignment.Center };
        _text.LostFocus += (_, _) => CommitText();
        _text.KeyDown += (_, e) =>
        {
            if (e.Key == Avalonia.Input.Key.Enter) CommitText();
        };

        Content = new StackPanel
        {
            Orientation = Orientation.Horizontal, Spacing = 8,
            Children = { swatchButton, _text },
        };
        Refresh();
    }

    /// <summary>Couleur au format « #RRGGBB ».</summary>
    public string Hex
    {
        get => GetValue(HexProperty);
        set => SetValue(HexProperty, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == HexProperty) Refresh();
    }

    private void CommitText()
    {
        if (_updating) return;
        if (ArgbColor.TryParse(_text.Text, out var c)) Hex = c.ToRgbHex();
        else Refresh(); // saisie invalide : on revient à la valeur courante
    }

    private void Refresh()
    {
        _updating = true;
        _text.Text = Hex;
        if (ArgbColor.TryParse(Hex, out var c))
            _swatch.Background = new SolidColorBrush(Color.FromRgb(c.R, c.G, c.B));
        _updating = false;
    }
}
