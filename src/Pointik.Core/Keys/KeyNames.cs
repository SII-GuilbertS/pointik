using System.Globalization;
using Pointik.Core.Input;

namespace Pointik.Core.Keys;

/// <summary>
/// Noms français des touches et formatage des raccourcis (« Ctrl + Maj + S »).
/// Les noms propres à l'OS (Win/⌘, Alt/⌥) sont fournis par la plateforme.
/// </summary>
public sealed class KeyNames
{
    public const string Separator = " + ";
    private static readonly CultureInfo French = CultureInfo.GetCultureInfo("fr-FR");

    public KeyNames(string metaName = "Win", string altName = "Alt")
    {
        MetaName = metaName;
        AltName = altName;
    }

    /// <summary>Nom de la touche Méta (« Win » ou « ⌘ »).</summary>
    public string MetaName { get; }

    /// <summary>Nom de la touche Alt (« Alt » ou « ⌥ »).</summary>
    public string AltName { get; }

    public const string CtrlName = "Ctrl";
    public const string ShiftName = "Maj";

    /// <summary>Nom affiché d'une touche (sans modificateurs).</summary>
    /// <param name="key">Touche logique.</param>
    /// <param name="label">Libellé gravé (pour les touches de ponctuation dépendantes de la disposition).</param>
    public string GetName(KeyId key, string? label = null)
    {
        if (key.IsLetter()) return ((char)('A' + (key - KeyId.A))).ToString();
        if (key.IsDigit()) return ((char)('0' + (key - KeyId.D0))).ToString();
        if (key.IsNumPadDigit()) return "Num " + (char)('0' + (key - KeyId.NumPad0));
        if (key.IsFunctionKey()) return "F" + (key - KeyId.F1 + 1).ToString(CultureInfo.InvariantCulture);

        return key switch
        {
            KeyId.Enter or KeyId.NumPadEnter => "Entrée",
            KeyId.Escape => "Échap",
            KeyId.Backspace => "Retour",
            KeyId.Tab => "Tab",
            KeyId.Space => "Espace",
            KeyId.Delete => "Suppr",
            KeyId.Insert => "Inser",
            KeyId.Home => "Début",
            KeyId.End => "Fin",
            KeyId.PageUp => "Pg préc.",
            KeyId.PageDown => "Pg suiv.",
            KeyId.Up => "↑",
            KeyId.Down => "↓",
            KeyId.Left => "←",
            KeyId.Right => "→",
            KeyId.PrintScreen => "Impr. écran",
            KeyId.Pause => "Pause",
            KeyId.CapsLock => "Verr. Maj",
            KeyId.NumLock => "Verr. Num",
            KeyId.ScrollLock => "Arrêt défil.",
            KeyId.ContextMenu => "Menu",
            KeyId.NumPadMultiply => "Num *",
            KeyId.NumPadAdd => "Num +",
            KeyId.NumPadSubtract => "Num -",
            KeyId.NumPadDecimal => "Num .",
            KeyId.NumPadDivide => "Num /",
            KeyId.LeftCtrl or KeyId.RightCtrl => CtrlName,
            KeyId.LeftShift or KeyId.RightShift => ShiftName,
            KeyId.LeftAlt => AltName,
            KeyId.RightAlt => "AltGr",
            KeyId.LeftMeta or KeyId.RightMeta => MetaName,
            KeyId.VolumeUp => "Volume +",
            KeyId.VolumeDown => "Volume −",
            KeyId.VolumeMute => "Muet",
            KeyId.MediaPlayPause => "Lecture/Pause",
            KeyId.MediaNext => "Piste suivante",
            KeyId.MediaPrevious => "Piste précédente",
            KeyId.MediaStop => "Arrêt",
            KeyId.Character => string.IsNullOrEmpty(label) ? "?" : label.ToUpper(French),
            _ => "?",
        };
    }

    /// <summary>Nom d'un bouton de souris (« Clic gauche »).</summary>
    public static string GetMouseName(MouseButton button) => button switch
    {
        MouseButton.Left => "Clic gauche",
        MouseButton.Right => "Clic droit",
        MouseButton.Middle => "Clic milieu",
        MouseButton.X1 => "Bouton 4",
        _ => "Bouton 5",
    };

    /// <summary>Nom d'un mouvement de molette (« Molette ↑ »).</summary>
    public static string GetWheelName(int delta) => delta > 0 ? "Molette ↑" : "Molette ↓";

    /// <summary>Éléments affichables d'une action de souris avec ses modificateurs (« Ctrl », « Clic gauche »).</summary>
    public IReadOnlyList<string> GetMouseParts(KeyModifiers modifiers, string actionName)
    {
        var parts = new List<string>(GetParts(modifiers, KeyId.None)) { actionName };
        return parts;
    }

    /// <summary>
    /// Décompose un raccourci en éléments affichables, dans l'ordre usuel de Windows :
    /// Win, Ctrl, Alt, Maj, puis la touche.
    /// </summary>
    public IReadOnlyList<string> GetParts(KeyModifiers modifiers, KeyId key, string? label = null)
    {
        var parts = new List<string>(5);
        if (modifiers.HasFlag(KeyModifiers.Meta)) parts.Add(MetaName);
        if (modifiers.HasFlag(KeyModifiers.Ctrl)) parts.Add(CtrlName);
        if (modifiers.HasFlag(KeyModifiers.Alt)) parts.Add(AltName);
        if (modifiers.HasFlag(KeyModifiers.Shift)) parts.Add(ShiftName);
        if (key != KeyId.None && !key.IsModifier()) parts.Add(GetName(key, label));
        return parts;
    }

    /// <summary>Raccourci sous forme de texte : « Ctrl + Maj + S ».</summary>
    public string Format(KeyModifiers modifiers, KeyId key, string? label = null) =>
        string.Join(Separator, GetParts(modifiers, key, label));

    /// <summary>Raccourci global sous forme lisible (fenêtre de réglages).</summary>
    public string Format(Hotkey hotkey) => hotkey.IsEmpty ? "(aucun)" : Format(hotkey.Modifiers, hotkey.Key);
}
