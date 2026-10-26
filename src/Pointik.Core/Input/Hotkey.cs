using System.Text.Json;
using System.Text.Json.Serialization;

namespace Pointik.Core.Input;

/// <summary>Actions déclenchables par un raccourci global.</summary>
public enum HotkeyAction
{
    ToggleRingAndSpot,
    ToggleSpot,
    ToggleKeyboard,
    TogglePause,
    ToggleZoom,
}

/// <summary>
/// Raccourci global : modificateurs + touche logique.
/// Forme texte (fichier de réglages) : « Ctrl+Alt+Z ».
/// </summary>
[JsonConverter(typeof(HotkeyJsonConverter))]
public readonly record struct Hotkey(KeyModifiers Modifiers, KeyId Key)
{
    public static readonly Hotkey None = new(KeyModifiers.None, KeyId.None);

    public bool IsEmpty => Key == KeyId.None;

    /// <summary>Forme invariante utilisée dans le fichier de réglages.</summary>
    public override string ToString()
    {
        if (IsEmpty) return "";
        var parts = new List<string>(5);
        if (Modifiers.HasFlag(KeyModifiers.Ctrl)) parts.Add("Ctrl");
        if (Modifiers.HasFlag(KeyModifiers.Alt)) parts.Add("Alt");
        if (Modifiers.HasFlag(KeyModifiers.Shift)) parts.Add("Shift");
        if (Modifiers.HasFlag(KeyModifiers.Meta)) parts.Add("Meta");
        parts.Add(Key.ToString());
        return string.Join("+", parts);
    }

    /// <summary>Analyse la forme invariante (« Ctrl+Alt+Z »). Insensible à la casse.</summary>
    public static bool TryParse(string? text, out Hotkey hotkey)
    {
        hotkey = None;
        if (string.IsNullOrWhiteSpace(text)) return true;
        var mods = KeyModifiers.None;
        KeyId key = KeyId.None;
        foreach (var raw in text.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            switch (raw.ToLowerInvariant())
            {
                case "ctrl" or "control": mods |= KeyModifiers.Ctrl; break;
                case "alt": mods |= KeyModifiers.Alt; break;
                case "shift" or "maj": mods |= KeyModifiers.Shift; break;
                case "meta" or "win" or "cmd": mods |= KeyModifiers.Meta; break;
                default:
                    if (key != KeyId.None) return false;
                    if (!Enum.TryParse(raw, ignoreCase: true, out key) || key.IsModifier() || key == KeyId.Character)
                        return false;
                    break;
            }
        }
        if (key == KeyId.None) return false;
        hotkey = new Hotkey(mods, key);
        return true;
    }

    public static Hotkey Parse(string text) =>
        TryParse(text, out var h) ? h : throw new FormatException($"Raccourci invalide : « {text} »");
}

/// <summary>Convertisseur JSON pour <see cref="Hotkey"/>.</summary>
public sealed class HotkeyJsonConverter : JsonConverter<Hotkey>
{
    public override Hotkey Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var s = reader.GetString();
        if (Hotkey.TryParse(s, out var h)) return h;
        throw new JsonException($"Raccourci invalide : « {s} »");
    }

    public override void Write(Utf8JsonWriter writer, Hotkey value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value.ToString());
}
