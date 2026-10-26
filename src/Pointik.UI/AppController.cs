using Avalonia.Controls.ApplicationLifetimes;
using Pointik.Core.Input;
using Pointik.Core.Platform;
using Pointik.Core.Settings;
using Pointik.UI.Overlay;
using Pointik.UI.Services;

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
    private readonly AnimationTicker _ticker;
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
        _ticker = new AnimationTicker(OnAnimationFrame);
    }

    public AppSettings Settings { get; private set; }

    public void Start()
    {
        _overlays = new OverlayManager(_platform.Screens, _platform.OverlayHost, Settings);
        _overlays.Scene.Cursor = _platform.Screens.GetCursorPosition();
        _overlays.Rebuild();

        _platform.InputHook.MouseEvent += OnMouse;
        UpdateHooks();
    }

    /// <summary>
    /// N'installe que les hooks nécessaires aux fonctions actives :
    /// aucun hook souris si rien ne suit la souris (aucune latence ajoutée).
    /// </summary>
    private void UpdateHooks()
    {
        var scene = _overlays?.Scene;
        if (scene is null) return;
        _platform.InputHook.MouseEnabled = scene.RingVisible || scene.SpotVisible || Settings.Clicks.Enabled;
    }

    private void OnMouse(MouseInputEvent e)
    {
        if (_overlays is null) return;
        var scene = _overlays.Scene;
        var now = AppClock.Now;
        switch (e.Kind)
        {
            case MouseEventKind.Move:
                var previous = scene.Cursor;
                scene.Cursor = e.Position;
                if (scene.RingVisible || scene.SpotVisible)
                {
                    var radius = scene.CursorEffectsRadius;
                    _overlays.InvalidateNear(previous, radius);
                    _overlays.InvalidateNear(e.Position, radius);
                }
                break;

            case MouseEventKind.ButtonDown or MouseEventKind.ButtonUp:
                if (e.Button is MouseButton.X1 or MouseButton.X2) break;
                scene.Cursor = e.Position;
                var down = e.Kind == MouseEventKind.ButtonDown;
                scene.Clicks.SetButtonState(down, now);
                if (down && Settings.Clicks.Enabled)
                {
                    scene.Clicks.AddRipple(e.Position, e.Button, now);
                    if (Settings.Clicks.Sound) _platform.Sound.PlayClick(e.Button);
                }
                _ticker.Ensure();
                break;
        }
    }

    /// <summary>Image d'animation : redessine les écrans concernés tant qu'un effet est en cours.</summary>
    private bool OnAnimationFrame(TimeSpan now)
    {
        if (_overlays is null) return false;
        var scene = _overlays.Scene;
        var clicks = scene.Clicks;
        var animating = clicks.IsAnimating(now, scene.RippleDuration);
        // Une dernière image est dessinée après la fin pour effacer proprement la dernière onde.
        _overlays.InvalidateNear(scene.Cursor, scene.CursorEffectsRadius);
        foreach (var origin in clicks.ActiveOrigins)
            _overlays.InvalidateNear(origin, Settings.Clicks.MaxRadius + 8);
        return animating;
    }

    /// <summary>Quitte proprement : hooks, raccourcis et zoom sont libérés.</summary>
    public void RequestExit() => _lifetime.Shutdown();

    public void Shutdown()
    {
        if (_shutDown) return;
        _shutDown = true;
        _ticker.Stop();
        _platform.InputHook.MouseEvent -= OnMouse;
        _overlays?.Dispose();
        _platform.Dispose();
    }
}
