using Pointik.Core.Input;
using Pointik.Core.Settings;

namespace Pointik.Core.Keys;

/// <summary>Ce que l'affichage doit faire d'une frappe.</summary>
public enum KeyDisplayKind
{
    /// <summary>Rien à afficher (relâchement, modificateur seul, texte en mode « raccourcis »).</summary>
    Ignore,
    /// <summary>Raccourci ou touche spéciale, affiché dans sa propre bulle.</summary>
    Shortcut,
    /// <summary>Caractère tapé, ajouté à la ligne de texte (mode « Tout afficher »).</summary>
    Text,
}

/// <summary>Décision d'affichage pour une frappe.</summary>
public readonly record struct KeyDisplayAction(KeyDisplayKind Kind, IReadOnlyList<string> Parts, string? Text)
{
    public static readonly KeyDisplayAction Ignore = new(KeyDisplayKind.Ignore, Array.Empty<string>(), null);

    public static KeyDisplayAction Shortcut(IReadOnlyList<string> parts) => new(KeyDisplayKind.Shortcut, parts, null);

    public static KeyDisplayAction TypedText(string text) => new(KeyDisplayKind.Text, Array.Empty<string>(), text);
}

/// <summary>
/// Règles qui décident si une frappe est un raccourci, du texte, ou rien.
/// Logique pure : testable sans clavier ni OS.
/// </summary>
public static class KeyClassifier
{
    public static KeyDisplayAction Classify(in KeyboardInputEvent e, KeyboardDisplayMode mode, KeyNames names)
    {
        if (!e.IsDown || e.Key == KeyId.None || e.Key.IsModifier())
            return KeyDisplayAction.Ignore;

        var mods = e.Modifiers;
        // AltGr (Ctrl+Alt simulés) sert à taper « @ », « € », « # »… : ce n'est pas un raccourci.
        if (e.IsAltGr) mods &= ~(KeyModifiers.Ctrl | KeyModifiers.Alt);

        var isCommand = (mods & (KeyModifiers.Ctrl | KeyModifiers.Alt | KeyModifiers.Meta)) != 0;
        if (isCommand)
            return KeyDisplayAction.Shortcut(names.GetParts(mods, e.Key, e.Label));

        if (e.Key == KeyId.Space)
        {
            return mode == KeyboardDisplayMode.Everything
                ? KeyDisplayAction.TypedText(" ")
                : KeyDisplayAction.Shortcut(names.GetParts(mods, e.Key));
        }

        if (e.Key.IsPrintable())
        {
            // Lettres, chiffres, ponctuation sans Ctrl/Alt/Win : c'est de la saisie.
            if (mode != KeyboardDisplayMode.Everything) return KeyDisplayAction.Ignore;
            return IsDisplayableText(e.Text) ? KeyDisplayAction.TypedText(e.Text!) : KeyDisplayAction.Ignore;
        }

        // Touches spéciales (Entrée, Échap, flèches, F1…), éventuellement avec Maj.
        return KeyDisplayAction.Shortcut(names.GetParts(mods, e.Key, e.Label));
    }

    private static bool IsDisplayableText(string? text) =>
        !string.IsNullOrEmpty(text) && !text.Any(char.IsControl);
}
