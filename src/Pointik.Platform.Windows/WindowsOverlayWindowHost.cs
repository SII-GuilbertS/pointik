using Pointik.Core.Geometry;
using Pointik.Core.Platform;
using static Pointik.Platform.Windows.Interop.Native;

namespace Pointik.Platform.Windows;

/// <summary>Applique les styles étendus Win32 aux fenêtres créées par Avalonia.</summary>
internal sealed class WindowsOverlayWindowHost : IOverlayWindowHost
{
    public void ConfigureOverlay(nint windowHandle)
    {
        if (windowHandle == 0) return;
        var ex = (uint)GetWindowLongPtr(windowHandle, GWL_EXSTYLE);
        // Traversable aux clics (TRANSPARENT + LAYERED), hors Alt+Tab (TOOLWINDOW),
        // jamais activée (NOACTIVATE), toujours au premier plan (TOPMOST).
        ex |= WS_EX_LAYERED | WS_EX_TRANSPARENT | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE | WS_EX_TOPMOST;
        ex &= ~WS_EX_APPWINDOW;
        SetWindowLongPtr(windowHandle, GWL_EXSTYLE, (nint)ex);
        // Une fenêtre LAYERED sans attributs n'est pas affichée : opacité globale à 100 %
        // (la transparence par pixel de la composition reste active).
        SetLayeredWindowAttributes(windowHandle, 0, 255, LWA_ALPHA);
        SetWindowPos(windowHandle, HWND_TOPMOST, 0, 0, 0, 0,
            SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE | SWP_FRAMECHANGED | SWP_NOOWNERZORDER);
    }

    public void ConfigureFloatingTool(nint windowHandle)
    {
        if (windowHandle == 0) return;
        var ex = (uint)GetWindowLongPtr(windowHandle, GWL_EXSTYLE);
        ex |= WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE | WS_EX_TOPMOST;
        ex &= ~(WS_EX_APPWINDOW | WS_EX_TRANSPARENT);
        SetWindowLongPtr(windowHandle, GWL_EXSTYLE, (nint)ex);
        SetWindowPos(windowHandle, HWND_TOPMOST, 0, 0, 0, 0,
            SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE | SWP_FRAMECHANGED | SWP_NOOWNERZORDER);
    }

    public void SetBounds(nint windowHandle, ScreenRect bounds)
    {
        if (windowHandle == 0) return;
        SetWindowPos(windowHandle, HWND_TOPMOST,
            (int)Math.Round(bounds.X), (int)Math.Round(bounds.Y),
            (int)Math.Round(bounds.Width), (int)Math.Round(bounds.Height),
            SWP_NOACTIVATE | SWP_NOOWNERZORDER);
    }

    public void BringToFront(nint windowHandle)
    {
        if (windowHandle == 0) return;
        SetWindowPos(windowHandle, HWND_TOPMOST, 0, 0, 0, 0,
            SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE | SWP_NOOWNERZORDER);
    }
}
