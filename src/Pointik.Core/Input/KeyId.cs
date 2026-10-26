namespace Pointik.Core.Input;

/// <summary>
/// Identifiant logique d'une touche, indépendant de l'OS (aucun code VK ici).
/// Chaque plateforme fournit la correspondance entre ses codes natifs et ces valeurs.
/// </summary>
public enum KeyId
{
    None = 0,

    // Lettres (identifiées par la lettre gravée sur la touche dans la disposition active)
    A, B, C, D, E, F, G, H, I, J, K, L, M, N, O, P, Q, R, S, T, U, V, W, X, Y, Z,

    // Rangée des chiffres
    D0, D1, D2, D3, D4, D5, D6, D7, D8, D9,

    // Pavé numérique
    NumPad0, NumPad1, NumPad2, NumPad3, NumPad4, NumPad5, NumPad6, NumPad7, NumPad8, NumPad9,
    NumPadMultiply, NumPadAdd, NumPadSubtract, NumPadDecimal, NumPadDivide, NumPadEnter,

    // Édition et navigation
    Enter, Escape, Backspace, Tab, Space, Delete, Insert, Home, End, PageUp, PageDown,
    Up, Down, Left, Right,

    // Touches de fonction
    F1, F2, F3, F4, F5, F6, F7, F8, F9, F10, F11, F12,
    F13, F14, F15, F16, F17, F18, F19, F20, F21, F22, F23, F24,

    // Divers
    PrintScreen, Pause, CapsLock, NumLock, ScrollLock, ContextMenu,

    // Modificateurs
    LeftCtrl, RightCtrl, LeftShift, RightShift, LeftAlt, RightAlt, LeftMeta, RightMeta,

    // Multimédia
    VolumeUp, VolumeDown, VolumeMute, MediaPlayPause, MediaNext, MediaPrevious, MediaStop,

    /// <summary>
    /// Touche de caractère dont le libellé dépend de la disposition (ponctuation, touches OEM).
    /// Le libellé réel est transmis à part, par exemple « ù » ou « $ » en AZERTY.
    /// </summary>
    Character,
}

/// <summary>Modificateurs logiques actifs au moment d'une frappe.</summary>
[Flags]
public enum KeyModifiers
{
    None = 0,
    Ctrl = 1,
    Shift = 2,
    Alt = 4,
    /// <summary>Touche « système » : Win sous Windows, ⌘ sous macOS.</summary>
    Meta = 8,
}

/// <summary>Méthodes utilitaires sur <see cref="KeyId"/>.</summary>
public static class KeyIdExtensions
{
    public static bool IsLetter(this KeyId k) => k >= KeyId.A && k <= KeyId.Z;
    public static bool IsDigit(this KeyId k) => k >= KeyId.D0 && k <= KeyId.D9;
    public static bool IsNumPadDigit(this KeyId k) => k >= KeyId.NumPad0 && k <= KeyId.NumPad9;
    public static bool IsFunctionKey(this KeyId k) => k >= KeyId.F1 && k <= KeyId.F24;
    public static bool IsArrow(this KeyId k) => k is KeyId.Up or KeyId.Down or KeyId.Left or KeyId.Right;

    public static bool IsModifier(this KeyId k) => k is KeyId.LeftCtrl or KeyId.RightCtrl
        or KeyId.LeftShift or KeyId.RightShift or KeyId.LeftAlt or KeyId.RightAlt
        or KeyId.LeftMeta or KeyId.RightMeta;

    /// <summary>
    /// Touche qui produit normalement un caractère imprimable
    /// (lettres, chiffres, ponctuation, pavé numérique hors Entrée).
    /// </summary>
    public static bool IsPrintable(this KeyId k) =>
        k.IsLetter() || k.IsDigit() || k.IsNumPadDigit() || k == KeyId.Character
        || k is KeyId.NumPadMultiply or KeyId.NumPadAdd or KeyId.NumPadSubtract
            or KeyId.NumPadDecimal or KeyId.NumPadDivide;

    /// <summary>Modificateur logique correspondant à une touche de modification.</summary>
    public static KeyModifiers ToModifier(this KeyId k) => k switch
    {
        KeyId.LeftCtrl or KeyId.RightCtrl => KeyModifiers.Ctrl,
        KeyId.LeftShift or KeyId.RightShift => KeyModifiers.Shift,
        KeyId.LeftAlt or KeyId.RightAlt => KeyModifiers.Alt,
        KeyId.LeftMeta or KeyId.RightMeta => KeyModifiers.Meta,
        _ => KeyModifiers.None,
    };
}
