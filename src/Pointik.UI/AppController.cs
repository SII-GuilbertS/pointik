using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using Pointik.Core.Input;
using Pointik.Core.Keys;
using Pointik.Core.Platform;
using Pointik.Core.Settings;
using Pointik.Core.Zoom;
using Pointik.UI.Drawing;
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
    private readonly DispatcherTimer _idleTimer;
    private OverlayManager? _overlays;
    private DrawingWindow? _drawing;
    private bool _holdZoom;
    private bool _shutDown;
    private bool _tracesDeleted;

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
        // Souris immobile : au terme du délai, le fondu de l'anneau est lancé (aucune minuterie sinon).
        _idleTimer = new DispatcherTimer();
        _idleTimer.Tick += (_, _) =>
        {
            _idleTimer.Stop();
            _ticker.Ensure();
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
        _overlays.Scene.LastMouseActivity = AppClock.Now;
        _overlays.Scene.KeyboardScreenId = ResolveKeyboardScreen().Id;
        _overlays.Rebuild();

        _platform.InputHook.MouseEvent += OnMouse;
        _platform.InputHook.KeyEvent += OnKey;
        _platform.InputHook.CtrlAltWheel += OnCtrlAltWheel;
        _platform.Hotkeys.HotkeyPressed += OnHotkey;
        _platform.SecureInput.SecureInputChanged += OnSecureInputChanged;
        RegisterHotkeys();
        UpdateHooks();
        NoteMouseActivity(AppClock.Now);
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

    /// <summary>Le mode dessin est ouvert.</summary>
    public bool IsDrawing => _drawing is not null;

    /// <summary>Ouvre le mode dessin sur l'écran du curseur, ou le ferme (les dessins sont effacés).</summary>
    public void ToggleDrawing()
    {
        if (_drawing is { } open)
        {
            open.Close();
            return;
        }
        var screen = _platform.Screens.GetScreenFromPoint(_platform.Screens.GetCursorPosition());
        var window = new DrawingWindow(screen, _platform.OverlayHost, Settings.Drawing, SaveSettings);
        window.Closed += (_, _) =>
        {
            _drawing = null;
            StateChanged?.Invoke();
        };
        _drawing = window;
        window.Show();
        StateChanged?.Invoke();
    }

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

    /// <summary>Applique une modification faite dans la fenêtre de réglages (aperçu en direct).</summary>
    public void ApplySettingsChange() => Change(() => { });

    /// <summary>Écrans disponibles.</summary>
    public IReadOnlyList<ScreenInfo> Screens => _platform.Screens.GetScreens();

    /// <summary>Service d'écrans (mode placement).</summary>
    public IScreenService ScreenService => _platform.Screens;

    // ------------------------------------------------------------------ Mode portable

    /// <summary>Réglages enregistrés à côté de l'exécutable (sinon dans le dossier de repli).</summary>
    public bool IsPortable => _store.IsPortable;

    /// <summary>Fichier de réglages utilisé.</summary>
    public string SettingsFilePath => _store.ActiveFilePath;

    /// <summary>État réel du lancement avec Windows (relu depuis l'OS).</summary>
    public bool LaunchAtStartup => _platform.Autostart.IsEnabled;

    /// <summary>Active ou retire le lancement avec Windows. Retourne un message d'erreur éventuel.</summary>
    public string? SetLaunchAtStartup(bool enabled)
    {
        string? error;
        var ok = enabled
            ? _platform.Autostart.TryEnable(_env.ExecutablePath, out error)
            : _platform.Autostart.TryDisable(out error);
        Settings.General.LaunchAtStartup = _platform.Autostart.IsEnabled;
        SaveSettings();
        return ok ? null : error;
    }

    /// <summary>
    /// Supprime toutes les traces (réglages, journal, entrée de démarrage) puis quitte
    /// sans rien réécrire. Retourne la liste des éléments qui n'ont pas pu être supprimés.
    /// </summary>
    public IReadOnlyList<string> DeleteAllTracesAndExit()
    {
        var failures = new List<string>();
        if (!_platform.Autostart.TryDisable(out var error)) failures.Add(error ?? "Entrée de démarrage");
        _saveTimer.Stop();
        _tracesDeleted = true;
        _env.Log.Disable();
        failures.AddRange(_store.DeleteAllTraces());
        if (failures.Count == 0) RequestExit();
        return failures;
    }

    /// <summary>Affiche des bulles d'exemple sur l'écran cible (bouton « Tester »).</summary>
    public void TestKeyboard()
    {
        if (_overlays is null) return;
        var now = AppClock.Now;
        var n = _keyboard.Names;
        _keyboard.Clear();
        _keyboard.AddShortcut(n.GetParts(KeyModifiers.Ctrl, KeyId.C), now);
        _keyboard.AddShortcut(n.GetParts(KeyModifiers.Ctrl | KeyModifiers.Shift, KeyId.S), now);
        _keyboard.AddShortcut(n.GetParts(KeyModifiers.Alt, KeyId.Tab), now);
        Scene.KeyboardScreenId = ResolveKeyboardScreen().Id;
        _overlays.InvalidateAll();
        _ticker.Ensure();
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
        _platform.InputHook.MouseEnabled = scene.RingVisible || scene.SpotVisible || Settings.Clicks.Enabled
            || _zoom.IsEngaged || ShowsMouseInBubbles;
        // En pause totale, le hook clavier est retiré : aucune frappe n'est même lue
        // (sauf pendant le zoom, pour Échap et le mode « maintenir »).
        _platform.InputHook.KeyboardEnabled = (Settings.Keyboard.Enabled && !scene.KeyboardPaused) || _zoom.IsEngaged;
        _platform.InputHook.InterceptCtrlAltWheel = _zoom.IsOnOrEntering;
        var watchPasswords = Settings.Keyboard.Enabled && Settings.Keyboard.HideOnPasswordFields && !scene.KeyboardPaused;
        _platform.SecureInput.Enabled = watchPasswords;
        if (!watchPasswords) scene.KeyboardSecureHidden = false;
    }

    /// <summary>Un champ de mot de passe prend ou perd le focus.</summary>
    private void OnSecureInputChanged(bool secure)
    {
        if (_overlays is null || Scene.KeyboardSecureHidden == secure) return;
        Scene.KeyboardSecureHidden = secure && Settings.Keyboard.HideOnPasswordFields;
        // Effacement immédiat : une frappe éventuellement affichée disparaît.
        if (secure) _keyboard.Clear();
        _overlays.InvalidateScreen(Scene.KeyboardScreenId);
    }

    /// <summary>
    /// L'affichage des frappes est suspendu : champ de mot de passe actif, ou vérification
    /// en cours juste après un changement de focus (prudence).
    /// </summary>
    private bool IsSecureInputLikely =>
        Settings.Keyboard.HideOnPasswordFields && (Scene.KeyboardSecureHidden || _platform.SecureInput.IsChecking);

    /// <summary>Les clics et la molette sont affichés dans les bulles du clavier.</summary>
    private bool ShowsMouseInBubbles =>
        Settings.Keyboard.Enabled && Settings.Keyboard.ShowMouseActions && !(_overlays?.Scene.KeyboardPaused ?? false);

    // ------------------------------------------------------------------ Raccourcis globaux

    /// <summary>Raccourcis refusés par l'OS lors du dernier enregistrement.</summary>
    public IReadOnlyDictionary<HotkeyAction, string> HotkeyErrors { get; private set; } = new Dictionary<HotkeyAction, string>();

    /// <summary>
    /// Enregistre tous les raccourcis. Retourne les erreurs par action (raccourci refusé par l'OS).
    /// Les raccourcis en conflit interne (doublon, combinaison interdite) ne sont pas enregistrés.
    /// </summary>
    public IReadOnlyDictionary<HotkeyAction, string> RegisterHotkeys(bool notify = true)
    {
        var errors = new Dictionary<HotkeyAction, string>();
        var conflicts = HotkeyConflicts.Find(Settings.Hotkeys);
        _platform.Hotkeys.UnregisterAll();
        var replaced = new List<string>();
        foreach (var action in Enum.GetValues<HotkeyAction>())
        {
            if (conflicts.ContainsKey(action)) continue;
            var hotkey = Settings.Hotkeys.Get(action);
            if (_platform.Hotkeys.TryRegister(action, hotkey, out var error)) continue;

            // Raccourci par défaut déjà pris (au démarrage) : on essaie une combinaison de repli libre.
            if (notify && HotkeyAlternatives.IsDefault(action, hotkey) && TryRegisterAlternative(action) is { } alternative)
            {
                replaced.Add($"{_keyboard.Names.Format(hotkey)} est déjà utilisé par une autre application : "
                    + $"« {HotkeyConflicts.DisplayName(action)} » utilise désormais {_keyboard.Names.Format(alternative)}.");
                continue;
            }
            errors[action] = error ?? "Raccourci refusé.";
        }
        HotkeyErrors = errors;
        if (replaced.Count > 0)
        {
            _env.Log.Warning("Raccourcis remplacés : " + string.Join(" ", replaced));
            SaveSettings();
        }
        if (notify && (errors.Count > 0 || replaced.Count > 0))
        {
            var lines = new List<string>(replaced);
            if (errors.Count > 0)
            {
                var list = string.Join(", ", errors.Keys.Select(a => _keyboard.Names.Format(Settings.Hotkeys.Get(a))));
                _env.Log.Warning($"Raccourcis non enregistrés : {list}");
                lines.Add($"{list} : déjà utilisé par une autre application. Modifiez-le dans les réglages.");
            }
            Notify(errors.Count > 0 ? "Raccourci indisponible" : "Raccourci remplacé", string.Join("\n", lines));
        }
        return errors;
    }

    /// <summary>Enregistre la première combinaison de repli libre et la mémorise. Retourne null si aucune.</summary>
    private Hotkey? TryRegisterAlternative(HotkeyAction action)
    {
        foreach (var candidate in HotkeyAlternatives.UsableCandidates(action, Settings.Hotkeys))
        {
            if (!_platform.Hotkeys.TryRegister(action, candidate, out _)) continue;
            Settings.Hotkeys.Set(action, candidate);
            return candidate;
        }
        return null;
    }

    /// <summary>Suspend les raccourcis globaux (capture d'un nouveau raccourci dans les réglages).</summary>
    public void SuspendHotkeys() => _platform.Hotkeys.UnregisterAll();

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
            case HotkeyAction.ToggleDrawing:
                ToggleDrawing();
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
        if (e.Kind is MouseEventKind.Move or MouseEventKind.ButtonDown)
            NoteMouseActivity(now);
        switch (e.Kind)
        {
            case MouseEventKind.Move:
                var previous = scene.Cursor;
                scene.Cursor = e.Position;
                // Les chevrons de molette suivent le curseur pendant leur courte vie.
                if (scene.Scroll.IsActive(now)) scene.Scroll.MoveTo(e.Position);
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
                if (down && ShowsMouseInBubbles && _keyboard.ProcessClick(e.Button, e.Modifiers, now))
                    ShowBubbles();
                _ticker.Ensure();
                break;

            case MouseEventKind.Wheel:
                if (Settings.Clicks.Enabled && Settings.Clicks.ShowScroll)
                {
                    scene.Scroll.AddNotch(e.Position, e.WheelDelta, now);
                    _overlays.InvalidateNear(e.Position, scene.ScrollReach);
                    _ticker.Ensure();
                }
                if (ShowsMouseInBubbles && _keyboard.ProcessWheel(e.WheelDelta, e.Modifiers, now))
                    ShowBubbles();
                break;
        }
    }

    /// <summary>Mouvement ou clic : l'anneau réapparaît et le délai d'inactivité repart.</summary>
    private void NoteMouseActivity(TimeSpan now)
    {
        var scene = Scene;
        var wasHidden = scene.RingIdleVisibility(now) < 1;
        scene.LastMouseActivity = now;
        if (wasHidden) _overlays!.InvalidateNear(scene.Cursor, scene.CursorEffectsRadius);
        if (scene.RingVisible && Settings.Ring.HideWhenIdle)
        {
            _idleTimer.Stop();
            _idleTimer.Interval = TimeSpan.FromSeconds(Settings.Ring.IdleSeconds);
            _idleTimer.Start();
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
        if (IsSecureInputLikely) return;
        if (e.IsDown && IsOwnHotkey(e)) return;
        if (_keyboard.Process(e, now)) ShowBubbles();
    }

    /// <summary>Les bulles ont changé : redessine l'écran cible (qui suit éventuellement le curseur).</summary>
    private void ShowBubbles()
    {
        if (_overlays is null) return;
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
        var animating = clicks.IsAnimating(now, scene.RippleDuration) || scene.IsRingFading(now);
        if (scene.Scroll.IsActive(now) || scene.Scroll.GetFrame(now - TimeSpan.FromMilliseconds(40)) is not null)
        {
            // Une image de plus après la fin pour effacer proprement les chevrons.
            _overlays.InvalidateNear(scene.Scroll.Origin, scene.ScrollReach);
            animating |= scene.Scroll.IsActive(now);
        }
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
        if (scene.Notification is { } note)
        {
            _overlays.InvalidateScreen(scene.NotificationScreenId);
            if (note.IsExpired(now)) scene.Notification = null;
            else animating = true;
        }
        // Une dernière image est dessinée après la fin pour effacer proprement la dernière onde.
        _overlays.InvalidateNear(scene.Cursor, scene.CursorEffectsRadius);
        foreach (var origin in clicks.ActiveOrigins)
            _overlays.InvalidateNear(origin, Settings.Clicks.MaxRadius + 8);
        return animating;
    }

    // ------------------------------------------------------------------ Divers

    /// <summary>
    /// Affiche une notification non intrusive dans l'overlay : sur l'écran zoomé pendant le zoom,
    /// sinon sur l'écran principal. Elle remplace la précédente.
    /// </summary>
    public void Notify(string title, string message)
    {
        if (_overlays is null) return;
        var scene = Scene;
        _overlays.InvalidateScreen(scene.NotificationScreenId);
        var screens = _platform.Screens.GetScreens();
        scene.NotificationScreenId = _zoom.IsEngaged && _zoom.Screen is { } zs
            ? zs.Id
            : (screens.FirstOrDefault(s => s.IsPrimary) ?? screens[0]).Id;
        scene.Notification = new OverlayNotification(title, message, AppClock.Now, TimeSpan.FromSeconds(6));
        _overlays.InvalidateScreen(scene.NotificationScreenId);
        _ticker.Ensure();
    }

    /// <summary>Quitte proprement : hooks, raccourcis et zoom sont libérés.</summary>
    public void RequestExit() => _lifetime.Shutdown();

    public void Shutdown()
    {
        if (_shutDown) return;
        _shutDown = true;
        // Sécurité critique en premier : l'écran ne doit jamais rester zoomé.
        _zoom.ResetNow();
        _drawing?.Close();
        _ticker.Stop();
        _idleTimer.Stop();
        if (_saveTimer.IsEnabled && !_tracesDeleted)
        {
            _saveTimer.Stop();
            _store.Save(Settings);
        }
        _platform.InputHook.MouseEvent -= OnMouse;
        _platform.InputHook.KeyEvent -= OnKey;
        _platform.InputHook.CtrlAltWheel -= OnCtrlAltWheel;
        _platform.Hotkeys.HotkeyPressed -= OnHotkey;
        _platform.SecureInput.SecureInputChanged -= OnSecureInputChanged;
        _keyboard.Clear();
        _overlays?.Dispose();
        _platform.Dispose();
    }
}
