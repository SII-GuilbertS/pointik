using Pointik.Core.Geometry;
using Pointik.Core.Platform;
using Pointik.Core.Settings;

namespace Pointik.UI.Overlay;

/// <summary>Crée et maintient une fenêtre overlay par écran.</summary>
public sealed class OverlayManager : IDisposable
{
    private readonly IScreenService _screens;
    private readonly IOverlayWindowHost _host;
    private readonly List<OverlayWindow> _windows = new();

    public OverlayManager(IScreenService screens, IOverlayWindowHost host, AppSettings settings)
    {
        _screens = screens;
        _host = host;
        Scene = new OverlayScene(settings);
        _screens.ScreensChanged += Rebuild;
        _host.StackingMayHaveChanged += BringAllToFront;
    }

    public OverlayScene Scene { get; }

    public IReadOnlyList<OverlayWindow> Windows => _windows;

    /// <summary>Crée (ou recrée) les fenêtres pour la configuration d'écrans actuelle.</summary>
    public void Rebuild()
    {
        var screens = _screens.GetScreens();
        // Réutilise les fenêtres existantes quand c'est possible (évite un clignotement).
        while (_windows.Count > screens.Count)
        {
            var last = _windows[^1];
            _windows.RemoveAt(_windows.Count - 1);
            last.Close();
        }
        for (var i = 0; i < screens.Count; i++)
        {
            if (i < _windows.Count)
            {
                _windows[i].UpdateScreen(screens[i]);
            }
            else
            {
                var w = new OverlayWindow(screens[i], Scene, _host);
                _windows.Add(w);
                w.Show();
            }
        }
    }

    /// <summary>Redessine l'écran contenant ce point (s'il existe).</summary>
    public void InvalidateAt(ScreenPoint p)
    {
        foreach (var w in _windows)
            if (w.Surface.Screen.Bounds.Contains(p))
                w.Surface.InvalidateVisual();
    }

    /// <summary>Redessine les écrans touchés par un effet de rayon donné (pixels logiques) autour d'un point.</summary>
    public void InvalidateNear(ScreenPoint p, double logicalRadius)
    {
        foreach (var w in _windows)
            if (w.Surface.IsNear(p, logicalRadius))
                w.Surface.InvalidateVisual();
    }

    /// <summary>Redessine tous les écrans.</summary>
    public void InvalidateAll()
    {
        foreach (var w in _windows) w.Surface.InvalidateVisual();
    }

    /// <summary>Repasse toutes les fenêtres au premier plan.</summary>
    public void BringAllToFront()
    {
        foreach (var w in _windows) w.BringToFront();
    }

    public void Dispose()
    {
        _screens.ScreensChanged -= Rebuild;
        _host.StackingMayHaveChanged -= BringAllToFront;
        foreach (var w in _windows) w.Close();
        _windows.Clear();
    }
}
