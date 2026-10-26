using Pointik.Core.Geometry;
using Pointik.Core.Input;

namespace Pointik.Core.Platform;

// Interfaces de plateforme : chaque OS (Windows en V1, macOS plus tard) en fournit une
// implémentation. Le Core et l'UI ne dépendent que de ces contrats.

/// <summary>
/// Hook global de la souris et du clavier.
/// Les implémentations doivent : ne rien enregistrer, ne faire que transmettre les
/// événements au thread UI, et toujours laisser passer les événements aux autres
/// applications, sauf le cas explicite de la molette avec Ctrl+Alt pendant le zoom.
/// </summary>
public interface IInputHook : IDisposable
{
    /// <summary>Événements souris, déclenchés sur le thread UI. Les déplacements sont fusionnés.</summary>
    event Action<MouseInputEvent>? MouseEvent;

    /// <summary>Frappes brutes, déclenchées sur le thread UI, à traduire par <see cref="IKeyTranslator"/>.</summary>
    event Action<NativeKeyEvent>? KeyEvent;

    /// <summary>Molette tournée avec Ctrl+Alt alors que <see cref="InterceptCtrlAltWheel"/> est actif (delta en crans de 120).</summary>
    event Action<int>? CtrlAltWheel;

    /// <summary>Active ou désactive le hook souris (désactivé = aucune latence ajoutée).</summary>
    bool MouseEnabled { get; set; }

    /// <summary>Active ou désactive le hook clavier.</summary>
    bool KeyboardEnabled { get; set; }

    /// <summary>
    /// Quand vrai, la molette tournée avec Ctrl+Alt enfoncés est consommée (non transmise)
    /// et signalée par <see cref="CtrlAltWheel"/>. Jamais consommée sinon.
    /// </summary>
    bool InterceptCtrlAltWheel { get; set; }
}

/// <summary>Traduit les frappes natives en touches logiques et fournit les noms propres à l'OS.</summary>
public interface IKeyTranslator
{
    /// <summary>Nom de la touche Méta : « Win » sous Windows, « ⌘ » sous macOS.</summary>
    string MetaKeyName { get; }

    /// <summary>Nom de la touche Alt : « Alt » sous Windows, « ⌥ » sous macOS.</summary>
    string AltKeyName { get; }

    /// <summary>Traduit une frappe native (respecte la disposition active, AZERTY compris).</summary>
    KeyboardInputEvent Translate(in NativeKeyEvent native);
}

/// <summary>Description d'un écran physique.</summary>
/// <param name="Id">Identifiant stable (nom de périphérique).</param>
/// <param name="Name">Nom lisible (« Écran 1 »).</param>
/// <param name="Bounds">Limites en pixels physiques dans le bureau virtuel.</param>
/// <param name="WorkArea">Zone de travail (sans la barre des tâches).</param>
/// <param name="Scale">Facteur d'échelle DPI (1,0 = 96 ppp ; 1,25 = 125 %).</param>
/// <param name="IsPrimary">Écran principal.</param>
public sealed record ScreenInfo(string Id, string Name, ScreenRect Bounds, ScreenRect WorkArea, double Scale, bool IsPrimary);

/// <summary>Énumération des écrans et position du curseur.</summary>
public interface IScreenService
{
    IReadOnlyList<ScreenInfo> GetScreens();

    /// <summary>Écran contenant le point, ou le plus proche.</summary>
    ScreenInfo GetScreenFromPoint(ScreenPoint point);

    ScreenPoint GetCursorPosition();

    /// <summary>Écrans ajoutés, retirés, déplacés ou changement de DPI (déclenché sur le thread UI).</summary>
    event Action? ScreensChanged;
}

/// <summary>
/// Configure les fenêtres natives créées par l'UI : overlay transparent traversable aux clics,
/// jamais activé, hors Alt+Tab, toujours au premier plan.
/// </summary>
public interface IOverlayWindowHost
{
    /// <summary>Rend la fenêtre traversable aux clics, non activable, hors Alt+Tab et au premier plan.</summary>
    void ConfigureOverlay(nint windowHandle);

    /// <summary>Place la fenêtre exactement sur des limites en pixels physiques.</summary>
    void SetBounds(nint windowHandle, ScreenRect bounds);

    /// <summary>Remet la fenêtre au sommet de la pile des fenêtres « toujours au premier plan ».</summary>
    void BringToFront(nint windowHandle);

    /// <summary>
    /// Une autre fenêtre a pu passer devant les overlays (changement de fenêtre active) :
    /// déclenché sur le thread UI pour que les overlays reprennent le dessus.
    /// </summary>
    event Action? StackingMayHaveChanged;
}

/// <summary>
/// Zoom plein écran. L'implémentation doit pouvoir revenir à 1× à tout moment,
/// y compris depuis un gestionnaire d'exception ou d'arrêt de session.
/// </summary>
public interface IZoomService : IDisposable
{
    /// <summary>Initialise l'API de zoom. Retourne false et un message clair si c'est impossible.</summary>
    bool TryInitialize(out string? error);

    /// <summary>
    /// Applique le facteur et le coin supérieur gauche (pixels physiques, non agrandis) de la zone visible.
    /// Retourne false si l'API refuse (autre loupe active…).
    /// </summary>
    bool TrySetTransform(double scale, ScreenPoint viewOrigin, out string? error);

    /// <summary>Remet le zoom à 1×. Ne lève jamais d'exception.</summary>
    void Reset();

    /// <summary>Indique si les fenêtres overlay sont agrandies par le zoom (et doivent donc être compensées).</summary>
    bool OverlaysAreMagnified { get; }
}

/// <summary>Raccourcis clavier globaux.</summary>
public interface IHotkeyService : IDisposable
{
    /// <summary>Déclenché sur le thread UI quand un raccourci enregistré est pressé.</summary>
    event Action<HotkeyAction>? HotkeyPressed;

    /// <summary>Enregistre (ou remplace) le raccourci d'une action. Retourne false si l'OS le refuse (déjà pris).</summary>
    bool TryRegister(HotkeyAction action, Hotkey hotkey, out string? error);

    void Unregister(HotkeyAction action);

    void UnregisterAll();
}

/// <summary>Lancement automatique à l'ouverture de session.</summary>
public interface IAutostartService
{
    bool IsEnabled { get; }

    /// <summary>Active le lancement automatique pour l'exécutable indiqué.</summary>
    bool TryEnable(string executablePath, out string? error);

    /// <summary>Retire proprement l'entrée de démarrage (sans erreur si absente).</summary>
    bool TryDisable(out string? error);
}

/// <summary>Lecture d'un court son de clic (optionnel).</summary>
public interface ISoundPlayer
{
    void PlayClick(MouseButton button);
}

/// <summary>Accès au thread UI, fourni par la couche d'interface.</summary>
public interface IUiDispatcher
{
    void Post(Action action);
    bool CheckAccess();
}
