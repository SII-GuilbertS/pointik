using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Pointik.Core.Platform;

namespace Pointik.UI.Overlay;

/// <summary>
/// Fenêtre overlay couvrant un écran : transparente, sans bordure, hors barre des tâches.
/// Les styles natifs (traversable aux clics, non activable, hors Alt+Tab) sont appliqués
/// par <see cref="IOverlayWindowHost"/> une fois le handle natif créé.
/// </summary>
public sealed class OverlayWindow : Window
{
    private readonly IOverlayWindowHost _host;
    private ScreenInfo _screen;

    public OverlayWindow(ScreenInfo screen, OverlayScene scene, IOverlayWindowHost host)
    {
        _screen = screen;
        _host = host;
        Surface = new OverlaySurface(scene, screen);

        Title = "Pointik Overlay";
        SystemDecorations = SystemDecorations.None;
        Background = Brushes.Transparent;
        TransparencyLevelHint = [WindowTransparencyLevel.Transparent];
        Topmost = true;
        ShowInTaskbar = false;
        ShowActivated = false;
        CanResize = false;
        Focusable = false;
        IsHitTestVisible = false;
        WindowStartupLocation = WindowStartupLocation.Manual;
        // Position initiale sur le bon écran : Avalonia y adopte le DPI de cet écran.
        Position = new PixelPoint((int)screen.Bounds.X, (int)screen.Bounds.Y);
        Width = screen.Bounds.Width / screen.Scale;
        Height = screen.Bounds.Height / screen.Scale;
        Content = Surface;
    }

    public OverlaySurface Surface { get; }

    public nint NativeHandle => TryGetPlatformHandle()?.Handle ?? 0;

    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);
        _host.ConfigureOverlay(NativeHandle);
        _host.SetBounds(NativeHandle, _screen.Bounds);
    }

    /// <summary>Met à jour l'écran couvert (changement de résolution, de DPI ou de disposition).</summary>
    public void UpdateScreen(ScreenInfo screen)
    {
        _screen = screen;
        Surface.Screen = screen;
        _host.SetBounds(NativeHandle, screen.Bounds);
        Surface.InvalidateVisual();
    }

    /// <summary>Remet la fenêtre au sommet (une autre fenêtre « toujours au premier plan » a pu passer devant).</summary>
    public void BringToFront() => _host.BringToFront(NativeHandle);
}
