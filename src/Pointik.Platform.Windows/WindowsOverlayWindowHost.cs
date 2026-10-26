using Pointik.Core.Geometry;
using Pointik.Core.Platform;
using Pointik.Platform.Windows.Interop;
using static Pointik.Platform.Windows.Interop.Native;

namespace Pointik.Platform.Windows;

/// <summary>Applique les styles étendus Win32 aux fenêtres créées par Avalonia.</summary>
internal sealed class WindowsOverlayWindowHost : IOverlayWindowHost
{
    // Référence statique : le delegate du hook WinEvent ne doit pas être collecté.
    private static WinEventProc? s_foregroundProc;
    private readonly IUiDispatcher _ui;
    private int _pending;

    public WindowsOverlayWindowHost(Win32MessageThread thread, IUiDispatcher ui)
    {
        _ui = ui;
        s_foregroundProc ??= OnForegroundChanged;
        // Hook hors contexte sur le thread Win32 : aucune DLL injectée dans les autres processus.
        thread.Invoke(() => SetWinEventHook(EVENT_SYSTEM_FOREGROUND, EVENT_SYSTEM_FOREGROUND, 0,
            s_foregroundProc, 0, 0, WINEVENT_OUTOFCONTEXT | WINEVENT_SKIPOWNPROCESS));
    }

    public event Action? StackingMayHaveChanged;

    private void OnForegroundChanged(nint hook, uint type, nint hwnd, int obj, int child, uint thread, uint time)
    {
        if (Interlocked.Exchange(ref _pending, 1) == 0)
        {
            _ui.Post(() =>
            {
                Interlocked.Exchange(ref _pending, 0);
                StackingMayHaveChanged?.Invoke();
            });
        }
    }

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
