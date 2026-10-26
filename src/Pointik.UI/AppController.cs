using Avalonia.Controls.ApplicationLifetimes;
using Pointik.Core.Input;
using Pointik.Core.Platform;
using Pointik.Core.Settings;
using Pointik.UI.Overlay;

namespace Pointik.UI;

/// <summary>
/// Chef d'orchestre de l'application : relie hooks, réglages, overlays et services de plateforme.
/// Tout s'exécute sur le thread UI.
/// </summary>
public sealed class AppController
{
    private readonly PlatformServices _platform;
    private readonly AppEnvironment _env;
    private readonly IClassicDesktopStyleApplicationLifetime _lifetime;
    private readonly SettingsStore _store;
    private OverlayManager? _overlays;
    private bool _shutDown;

    public AppController(PlatformServices platform, AppEnvironment env, IClassicDesktopStyleApplicationLifetime lifetime)
    {
        _platform = platform;
        _env = env;
        _lifetime = lifetime;
        _store = new SettingsStore(env.ExecutableDirectory, env.FallbackDirectory, env.Log);
        env.Log.SetDirectory(_store.ActiveDirectory);
        Settings = _store.Load();
    }

    public AppSettings Settings { get; private set; }

    public void Start()
    {
        _overlays = new OverlayManager(_platform.Screens, _platform.OverlayHost);
        _overlays.Scene.Cursor = _platform.Screens.GetCursorPosition();
        _overlays.Rebuild();

        _platform.InputHook.MouseEvent += OnMouse;
        _platform.InputHook.MouseEnabled = true;
    }

    private void OnMouse(MouseInputEvent e)
    {
        if (_overlays is null) return;
        var previous = _overlays.Scene.Cursor;
        _overlays.Scene.Cursor = e.Position;
        _overlays.InvalidateAt(previous);
        _overlays.InvalidateAt(e.Position);
    }

    /// <summary>Quitte proprement : hooks, raccourcis et zoom sont libérés.</summary>
    public void RequestExit() => _lifetime.Shutdown();

    public void Shutdown()
    {
        if (_shutDown) return;
        _shutDown = true;
        _platform.InputHook.MouseEvent -= OnMouse;
        _overlays?.Dispose();
        _platform.Dispose();
    }
}
