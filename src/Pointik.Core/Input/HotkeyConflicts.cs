using Pointik.Core.Settings;

namespace Pointik.Core.Input;

/// <summary>
/// Détection des conflits entre raccourcis, avant même de les soumettre à l'OS :
/// doublons, absence de modificateur, et combinaisons à éviter.
/// </summary>
public static class HotkeyConflicts
{
    /// <summary>Retourne, pour chaque action en défaut, un message en français.</summary>
    public static IReadOnlyDictionary<HotkeyAction, string> Find(HotkeySettings hotkeys)
    {
        var result = new Dictionary<HotkeyAction, string>();
        var actions = Enum.GetValues<HotkeyAction>();
        foreach (var action in actions)
        {
            var h = hotkeys.Get(action);
            if (h.IsEmpty) continue;
            var problem = Validate(h);
            if (problem is not null)
            {
                result[action] = problem;
                continue;
            }
            var duplicate = actions.FirstOrDefault(other => other != action && hotkeys.Get(other) == h, (HotkeyAction)(-1));
            if ((int)duplicate >= 0)
                result[action] = $"Même raccourci que « {DisplayName(duplicate)} ».";
        }
        return result;
    }

    /// <summary>Vérifie un raccourci isolé. Retourne null s'il est acceptable.</summary>
    public static string? Validate(Hotkey h)
    {
        if (h.IsEmpty) return null;
        if ((h.Modifiers & (KeyModifiers.Ctrl | KeyModifiers.Alt | KeyModifiers.Meta)) == 0)
            return "Un raccourci global doit contenir Ctrl, Alt ou Win.";
        // Ctrl+Alt+flèches fait pivoter l'écran avec certains pilotes graphiques.
        if (h.Modifiers.HasFlag(KeyModifiers.Ctrl) && h.Modifiers.HasFlag(KeyModifiers.Alt) && h.Key.IsArrow())
            return "Ctrl + Alt + flèche est réservé à la rotation de l'écran par certains pilotes.";
        // Ctrl+Alt+Suppr est réservé au système.
        if (h.Modifiers.HasFlag(KeyModifiers.Ctrl) && h.Modifiers.HasFlag(KeyModifiers.Alt) && h.Key == KeyId.Delete)
            return "Ctrl + Alt + Suppr est réservé à Windows.";
        return null;
    }

    /// <summary>Nom lisible d'une action.</summary>
    public static string DisplayName(HotkeyAction action) => action switch
    {
        HotkeyAction.ToggleRingAndSpot => "Anneau et spot",
        HotkeyAction.ToggleSpot => "Spot seul",
        HotkeyAction.ToggleKeyboard => "Affichage du clavier",
        HotkeyAction.TogglePause => "Pause totale",
        HotkeyAction.ToggleZoom => "Zoom",
        _ => action.ToString(),
    };
}
