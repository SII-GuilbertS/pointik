using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using Pointik.Core.Input;
using Pointik.Core.Keys;
using Pointik.Core.Platform;
using Pointik.Core.Settings;
using Pointik.Core.Zoom;
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
    private readonly KeyDisplayEngine _keyboard;
    private readonly ZoomController _zoom;
    private readonly DispatcherTimer _saveTimer;
    private OverlayManager? _overlays;
    private bool _holdZoom;
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
        _keyboard = new KeyDisplayEngine(() => Settings.Keyboard,
            new KeyNames(platform.KeyTranslator.MetaKeyName, platform.KeyTranslator.AltKeyName));
        _zoom = new ZoomController(platform.Zoom, () => Settings.Zoom);
        _zoom.Failed += message => Notify("Zoom indisponible", message);
        // Enregistrement différé : plusieurs changements rapprochés = une seule écriture.
        _saveTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(600) };
        _saveTimer.Tick += (_, _) =>
        {
            _saveTimer.Stop();
            _store.Save(Settings);
        };
    }

    public AppSettings Settings { get; private set; }

    /// <summary>Noms des touches propres à la plateforme (« Win », « ⌘ »).</summary>
    public KeyNames KeyNames => _keyboard.Names;

    /// <summary>Déclenché quand l'état d'une fonction change (menu de l'icône, fenêtre de réglages).</summary>
    public event Action? StateChanged;

    public void Start()
    {
        _overlays = new OverlayManager(_platform.Screens, _platform.OverlayHost, Settings, _keyboard, _zoom,
            _platform.Zoom.OverlaysAreMagnified);
        _overlays.Scene.Cursor = _platform.Screens.GetCursorPosition();
        _overlays.Scene.KeyboardScreenId = ResolveKeyboardScreen().Id;
        _overlays.Rebuild();

        _platform.InputHook.MouseEvent += OnMouse;
        _platform.InputHook.KeyEvent += OnKey;
        _platform.InputHook.CtrlAltWheel += OnCtrlAltWheel;
        _platform.Hotkeys.HotkeyPressed += OnHotkey;
        RegisterHotkeys();
        UpdateHooks();
    }

    // ------------------------------------------------------------------ État des fonctions

    private OverlayScene Scene => _overlays!.Scene;

    public bool RingEnabled
    {
        get => Settings.Ring.Enabled;
        set => Change(() => Settings.Ring.Enabled = value, highlightOn: value);
    }

    public bool SpotEnabled
    {
        get => Settings.Spot.Enabled;
        set => Change(() => Settings.Spot.Enabled = value, highlightOn: value);
    }

    public bool ClicksEnabled
    {
        get => Settings.Clicks.Enabled;
        set => Change(() => Settings.Clicks.Enabled = value);
    }

    public bool KeyboardEnabled
    {
        get => Settings.Keyboard.Enabled;
        set => Change(() =>
        {
            Settings.Keyboard.Enabled = value;
            if (!value) _keyboard.Clear();
        });
    }

    public bool ZoomEnabled
    {
        get => Settings.Zoom.Enabled;
        set => Change(() =>
        {
            Settings.Zoom.Enabled = value;
            if (!value) _zoom.ResetNow();
        });
    }

    /// <summary>Interrupteur général de l'anneau et du spot (Ctrl+Alt+M), propre à la session.</summary>
    public bool CursorHighlightOn
    {
        get => _overlays?.Scene.CursorHighlightOn ?? true;
        set => Change(() => Scene.CursorHighlightOn = value, save: false);
    }

    /// <summary>Pause totale de l'affichage du clavier (Ctrl+Alt+P).</summary>
    public bool KeyboardPaused
    {
        get => _overlays?.Scene.KeyboardPaused ?? false;
        set => Change(() =>
        {
            Scene.KeyboardPaused = value;
            // Effacement immédiat : plus aucune frappe à l'écran.
            _keyboard.Clear();
        }, save: false);
    }

    public bool IsZoomActive => _zoom.IsOnOrEntering;

    /// <summary>Applique un changement, met à jour hooks et affichage, puis enregistre.</summary>
    private void Change(Action apply, bool save = true, bool? highlightOn = null)
    {
        if (_overlays is null) return;
        apply();
        // Activer l'anneau ou le spot rallume l'interrupteur général.
        if (highlightOn == true) Scene.CursorHighlightOn = true;
        Scene.InvalidateStyle();
        UpdateHooks();
        _overlays.InvalidateAll();
        if (save) SaveSettings();
        StateChanged?.Invoke();
    }

    /// <summary>Enregistre les réglages (différé).</summary>
    public void SaveSettings()
    {
        _saveTimer.Stop();
        _saveTimer.Start();
    }

    /// <summary>
    /// N'installe que les hooks nécessaires aux fonctions actives :
    /// aucun hook souris si rien ne suit la souris (aucune latence ajoutée).
    /// </summary>
    private void UpdateHooks()
    {
        if (_overlays is null) return;
        var scene = Scene;
        _platform.InputHook.MouseEnabled = scene.RingVisible || scene.SpotVisible || Settings.Clicks.Enabled || _zoom.IsEngaged;
        // En pause totale, le hook clavier est retiré : aucune frappe n'est même lue
        // (sauf pendant le zoom, pour Échap et le mode « maintenir »).
        _platform.InputHook.KeyboardEnabled = (Settings.Keyboard.Enabled && !scene.KeyboardPaused) || _zoom.IsEngaged;
        _platform.InputHook.InterceptCtrlAltWheel = _zoom.IsOnOrEntering;
    }

    // ------------------------------------------------------------------ Raccourcis globaux

    /// <summary>Enregistre tous les raccourcis. Retourne les erreurs par action (raccourci refusé par l'OS).</summary>
    public IReadOnlyDictionary<HotkeyAction, string> RegisterHotkeys()
    {
        var errors = new Dictionary<HotkeyAction, string>();
        _platform.Hotkeys.UnregisterAll();
        foreach (var action in Enum.GetValues<HotkeyAction>())
        {
            var hotkey = Settings.Hotkeys.Get(action);
            if (!_platform.Hotkeys.TryRegister(action, hotkey, out var error))
                errors[action] = error ?? "Raccourci refusé.";
        }
        if (errors.Count > 0)
        {
            var list = string.Join(", ", errors.Keys.Select(a => _keyboard.Names.Format(Settings.Hotkeys.Get(a))));
            _env.Log.Warning($"Raccourcis non enregistrés : {list}");
            Notify("Raccourci indisponible", $"{list} : déjà utilisé par une autre application. Modifiez-le dans les réglages.");
        }
        return errors;
    }

    private void OnHotkey(HotkeyAction action)
    {
        switch (action)
        {
            case HotkeyAction.ToggleRingAndSpot:
                CursorHighlightOn = !CursorHighlightOn;
                break;
            case HotkeyAction.ToggleSpot:
                SpotEnabled = !SpotEnabled;
                break;
            case HotkeyAction.ToggleKeyboard:
                KeyboardEnabled = !KeyboardEnabled;
                break;
            case HotkeyAction.TogglePause:
                KeyboardPaused = !KeyboardPaused;
                break;
            case HotkeyAction.ToggleZoom:
                OnZoomHotkey();
                break;
        }
    }

    /// <summary>Le raccourci est l'un des nôtres : on ne l'affiche pas dans les bulles.</summary>
    private bool IsOwnHotkey(in KeyboardInputEvent e)
    {
        foreach (var action in Enum.GetValues<HotkeyAction>())
        {
            var h = Settings.Hotkeys.Get(action);
            if (!h.IsEmpty && h.Key == e.Key && h.Modifiers == e.Modifiers) return true;
        }
        return false;
    }

    // ------------------------------------------------------------------ Zoom

    private void OnZoomHotkey()
    {
        if (!Settings.Zoom.Enabled || _overlays is null) return;
        var cursor = _platform.Screens.GetCursorPosition();
        var screen = _platform.Screens.GetScreenFromPoint(cursor);
        var now = AppClock.Now;
        if (Settings.Zoom.Activation == ZoomActivation.Hold)
        {
            // Mode « maintenir » : le zoom dure tant que le raccourci reste enfoncé.
            if (!_zoom.IsOnOrEntering && _zoom.Start(cursor, screen, now)) _holdZoom = true;
        }
        else
        {
            _zoom.Toggle(cursor, screen, now);
        }
        Scene.Cursor = cursor;
        AfterZoomChange();
    }

    /// <summary>Quitte le zoom (Échap, menu).</summary>
    public void StopZoom()
    {
        _holdZoom = false;
        _zoom.Stop(AppClock.Now);
        AfterZoomChange();
    }

    private void AfterZoomChange()
    {
        UpdateHooks();
        _overlays?.InvalidateAll();
        _ticker.Ensure();
        StateChanged?.Invoke();
    }

    private void OnCtrlAltWheel(int delta)
    {
        if (!_zoom.IsOnOrEntering) return;
        _zoom.AdjustLevel(delta, AppClock.Now);
        _ticker.Ensure();
        SaveSettings();
    }

    // ------------------------------------------------------------------ Événements d'entrée

    private void OnMouse(MouseInputEvent e)
    {
        if (_overlays is null) return;
        var scene = Scene;
        var now = AppClock.Now;
        switch (e.Kind)
        {
            case MouseEventKind.Move:
                var previous = scene.Cursor;
                scene.Cursor = e.Position;
                if (_zoom.IsEngaged)
                {
                    if (_zoom.Update(e.Position, now)) _ticker.Ensure();
                    // La vue a bougé : tout l'écran zoomé est redessiné (bulles, pastille).
                    _overlays.InvalidateScreen(_zoom.Screen?.Id);
                }
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

    private void OnKey(NativeKeyEvent native)
    {
        if (_overlays is null) return;
        var e = _platform.KeyTranslator.Translate(native);
        var now = AppClock.Now;

        if (_zoom.IsOnOrEntering)
        {
            // Échap quitte le zoom (la touche n'est pas consommée).
            if (e.IsDown && e.Key == KeyId.Escape)
            {
                StopZoom();
            }
            else if (_holdZoom && !e.IsDown && IsPartOfZoomHotkey(e.Key))
            {
                // Mode « maintenir » : relâcher la touche ou un modificateur quitte le zoom.
                StopZoom();
            }
        }

        if (Scene.KeyboardPaused || !Settings.Keyboard.Enabled) return;
        if (e.IsDown && IsOwnHotkey(e)) return;
        if (!_keyboard.Process(e, now)) return;

        var screenId = ResolveKeyboardScreen().Id;
        if (screenId != Scene.KeyboardScreenId)
        {
            // Les bulles suivent l'écran du curseur : l'ancien écran est effacé.
            _overlays.InvalidateScreen(Scene.KeyboardScreenId);
            Scene.KeyboardScreenId = screenId;
        }
        _overlays.InvalidateScreen(screenId);
        _ticker.Ensure();
    }

    private bool IsPartOfZoomHotkey(KeyId key)
    {
        var h = Settings.Hotkeys.Zoom;
        return key == h.Key || (key.IsModifier() && h.Modifiers.HasFlag(key.ToModifier()));
    }

    /// <summary>Écran qui doit afficher les bulles du clavier.</summary>
    private ScreenInfo ResolveKeyboardScreen()
    {
        var screens = _platform.Screens.GetScreens();
        var primary = screens.FirstOrDefault(s => s.IsPrimary) ?? screens[0];
        return Settings.Keyboard.Position.Target switch
        {
            TargetScreenMode.CursorScreen => _platform.Screens.GetScreenFromPoint(_platform.Screens.GetCursorPosition()),
            TargetScreenMode.SpecificScreen => screens.FirstOrDefault(s => s.Id == Settings.Keyboard.Position.ScreenId) ?? primary,
            _ => primary,
        };
    }

    // ------------------------------------------------------------------ Animation

    /// <summary>Image d'animation : redessine les écrans concernés tant qu'un effet est en cours.</summary>
    private bool OnAnimationFrame(TimeSpan now)
    {
        if (_overlays is null) return false;
        var scene = Scene;
        var clicks = scene.Clicks;
        var animating = clicks.IsAnimating(now, scene.RippleDuration);
        if (_keyboard.Bubbles.Count > 0)
        {
            _overlays.InvalidateScreen(scene.KeyboardScreenId);
            animating |= _keyboard.Update(now);
        }
        if (_zoom.IsEngaged)
        {
            var zoomScreen = _zoom.Screen?.Id;
            animating |= _zoom.Update(scene.Cursor, now);
            _overlays.InvalidateScreen(zoomScreen);
            if (!_zoom.IsEngaged)
            {
                // Fin de la sortie du zoom : hooks remis à leur état normal.
                _holdZoom = false;
                UpdateHooks();
                StateChanged?.Invoke();
            }
        }
        // Une dernière image est dessinée après la fin pour effacer proprement la dernière onde.
        _overlays.InvalidateNear(scene.Cursor, scene.CursorEffectsRadius);
        foreach (var origin in clicks.ActiveOrigins)
            _overlays.InvalidateNear(origin, Settings.Clicks.MaxRadius + 8);
        return animating;
    }

    // ------------------------------------------------------------------ Divers

    /// <summary>Affiche une notification non intrusive.</summary>
    public void Notify(string title, string message) =>
        ToastWindow.Show(title, message, _platform.Screens, _platform.OverlayHost);

    /// <summary>Quitte proprement : hooks, raccourcis et zoom sont libérés.</summary>
    public void RequestExit() => _lifetime.Shutdown();

    public void Shutdown()
    {
        if (_shutDown) return;
        _shutDown = true;
        // Sécurité critique en premier : l'écran ne doit jamais rester zoomé.
        _zoom.ResetNow();
        _ticker.Stop();
        if (_saveTimer.IsEnabled)
        {
            _saveTimer.Stop();
            _store.Save(Settings);
        }
        _platform.InputHook.MouseEvent -= OnMouse;
        _platform.InputHook.KeyEvent -= OnKey;
        _platform.InputHook.CtrlAltWheel -= OnCtrlAltWheel;
        _platform.Hotkeys.HotkeyPressed -= OnHotkey;
        _keyboard.Clear();
        _overlays?.Dispose();
        _platform.Dispose();
    }
}
