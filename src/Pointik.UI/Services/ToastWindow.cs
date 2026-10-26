using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Pointik.Core.Platform;

namespace Pointik.UI.Services;

/// <summary>
/// Petite notification en bas à droite de l'écran principal : n'active jamais de fenêtre,
/// ne vole pas le focus et se ferme seule.
/// </summary>
public sealed class ToastWindow : Window
{
    private static ToastWindow? s_current;
    private readonly IOverlayWindowHost _host;
    private readonly ScreenInfo _screen;

    private ToastWindow(string title, string message, ScreenInfo screen, IOverlayWindowHost host)
    {
        _host = host;
        _screen = screen;
        Title = "Pointik";
        SystemDecorations = SystemDecorations.None;
        Background = Brushes.Transparent;
        TransparencyLevelHint = [WindowTransparencyLevel.Transparent];
        Topmost = true;
        ShowInTaskbar = false;
        ShowActivated = false;
        CanResize = false;
        SizeToContent = SizeToContent.WidthAndHeight;
        WindowStartupLocation = WindowStartupLocation.Manual;
        Position = new PixelPoint((int)screen.WorkArea.X, (int)screen.WorkArea.Y);

        Content = new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(240, 28, 31, 38)),
            BorderBrush = new SolidColorBrush(Color.FromArgb(80, 255, 255, 255)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(16, 12),
            MaxWidth = 380,
            Child = new StackPanel
            {
                Spacing = 4,
                Children =
                {
                    new TextBlock
                    {
                        Text = title, FontWeight = FontWeight.SemiBold, FontSize = 14,
                        Foreground = new SolidColorBrush(Color.FromRgb(0xFF, 0xC4, 0x1F)),
                    },
                    new TextBlock
                    {
                        Text = message, FontSize = 13, TextWrapping = TextWrapping.Wrap,
                        Foreground = Brushes.White, HorizontalAlignment = HorizontalAlignment.Left,
                    },
                },
            },
        };
        PointerPressed += (_, _) => Close();
    }

    /// <summary>Affiche une notification (remplace la précédente).</summary>
    public static void Show(string title, string message, IScreenService screens, IOverlayWindowHost host, double seconds = 5)
    {
        s_current?.Close();
        var all = screens.GetScreens();
        var primary = all.FirstOrDefault(s => s.IsPrimary) ?? all[0];
        var toast = new ToastWindow(title, message, primary, host);
        s_current = toast;
        toast.Closed += (_, _) =>
        {
            if (ReferenceEquals(s_current, toast)) s_current = null;
        };
        toast.Show();
        DispatcherTimer.RunOnce(() => toast.Close(), TimeSpan.FromSeconds(seconds));
    }

    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);
        var handle = TryGetPlatformHandle()?.Handle ?? 0;
        _host.ConfigureFloatingTool(handle);
        Reposition();
    }

    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        Reposition();
    }

    /// <summary>Placement en bas à droite de la zone de travail, dès que la taille est connue.</summary>
    private void Reposition()
    {
        if (Bounds.Width <= 0) return;
        var scale = RenderScaling;
        var w = Bounds.Width * scale;
        var h = Bounds.Height * scale;
        const double margin = 16;
        Position = new PixelPoint(
            (int)(_screen.WorkArea.Right - w - margin * scale),
            (int)(_screen.WorkArea.Bottom - h - margin * scale));
    }
}
