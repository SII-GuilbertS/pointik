using Pointik.Core.Geometry;

namespace Pointik.Core.Input;

/// <summary>Boutons de souris pris en charge.</summary>
public enum MouseButton
{
    Left,
    Right,
    Middle,
    X1,
    X2,
}

/// <summary>Nature d'un événement souris.</summary>
public enum MouseEventKind
{
    Move,
    ButtonDown,
    ButtonUp,
    Wheel,
}

/// <summary>Événement souris global, en pixels physiques du bureau virtuel.</summary>
public readonly record struct MouseInputEvent(
    MouseEventKind Kind,
    ScreenPoint Position,
    MouseButton Button = MouseButton.Left,
    int WheelDelta = 0);

/// <summary>
/// Frappe brute fournie par le hook de la plateforme, avant traduction.
/// Les champs restent génériques : code natif de la touche, code de balayage,
/// état des modificateurs et identifiant opaque de la disposition clavier.
/// Elle n'est jamais stockée : elle vit le temps d'être traduite et affichée.
/// </summary>
public readonly record struct NativeKeyEvent(
    int KeyCode,
    int ScanCode,
    bool IsExtended,
    bool IsDown,
    bool IsInjected,
    KeyModifiers Modifiers,
    bool IsAltGrDown,
    bool IsCapsLockOn,
    nint LayoutHandle);

/// <summary>
/// Frappe traduite en concepts indépendants de l'OS.
/// </summary>
/// <param name="Key">Touche logique.</param>
/// <param name="IsDown">Appui (true) ou relâchement (false).</param>
/// <param name="Modifiers">Modificateurs actifs (Ctrl, Maj, Alt, Méta).</param>
/// <param name="IsAltGr">AltGr est enfoncée (Ctrl+Alt simulés par Windows).</param>
/// <param name="Label">Libellé gravé sur la touche dans la disposition active (« A », « ù », « $ »).</param>
/// <param name="Text">Caractère réellement produit (tient compte de Maj, AltGr, Verr. Maj), ou null.</param>
/// <param name="IsRepeat">Répétition automatique d'une touche maintenue.</param>
public readonly record struct KeyboardInputEvent(
    KeyId Key,
    bool IsDown,
    KeyModifiers Modifiers,
    bool IsAltGr = false,
    string? Label = null,
    string? Text = null,
    bool IsRepeat = false);
