using Pointik.Core.Settings;

namespace Pointik.Core.Input;

/// <summary>
/// Combinaisons de repli quand un raccourci par défaut est déjà pris par une autre application :
/// d'abord la même touche avec Maj, puis quelques lettres mnémotechniques.
/// Les raccourcis avec Win sont évités (souvent réservés par Windows et la Game Bar).
/// </summary>
public static class HotkeyAlternatives
{
    private const KeyModifiers CtrlAlt = KeyModifiers.Ctrl | KeyModifiers.Alt;
    private const KeyModifiers CtrlAltShift = CtrlAlt | KeyModifiers.Shift;

    /// <summary>Raccourcis de repli, dans l'ordre de préférence.</summary>
    public static IEnumerable<Hotkey> Candidates(HotkeyAction action)
    {
        var standard = new HotkeySettings().Get(action);
        yield return new Hotkey(CtrlAltShift, standard.Key);
        KeyId[] letters = action switch
        {
            // H comme « highlight », J à côté.
            HotkeyAction.ToggleRingAndSpot => [KeyId.H, KeyId.J],
            // L comme « lumière », O à côté.
            HotkeyAction.ToggleSpot => [KeyId.L, KeyId.O],
            // B comme « bulles », T comme « touches ».
            HotkeyAction.ToggleKeyboard => [KeyId.B, KeyId.T],
            // Q comme « quiet », Pause.
            HotkeyAction.TogglePause => [KeyId.Q, KeyId.Pause],
            // G comme « grossir », W à côté de Z en AZERTY.
            HotkeyAction.ToggleZoom => [KeyId.G, KeyId.W],
            _ => [],
        };
        foreach (var key in letters)
        {
            yield return new Hotkey(CtrlAlt, key);
            yield return new Hotkey(CtrlAltShift, key);
        }
    }

    /// <summary>Le raccourci est celui livré par défaut pour cette action.</summary>
    public static bool IsDefault(HotkeyAction action, Hotkey hotkey) => new HotkeySettings().Get(action) == hotkey;

    /// <summary>
    /// Candidats utilisables : valides, distincts des raccourcis des autres actions.
    /// </summary>
    public static IEnumerable<Hotkey> UsableCandidates(HotkeyAction action, HotkeySettings current) =>
        Candidates(action).Where(h =>
            HotkeyConflicts.Validate(h) is null
            && Enum.GetValues<HotkeyAction>().All(other => other == action || current.Get(other) != h));
}
