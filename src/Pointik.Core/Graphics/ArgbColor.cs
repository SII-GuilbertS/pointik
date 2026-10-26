using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Pointik.Core.Graphics;

/// <summary>
/// Couleur ARGB indépendante de toute bibliothèque graphique.
/// Sérialisée en JSON sous la forme « #AARRGGBB ».
/// </summary>
[JsonConverter(typeof(ArgbColorJsonConverter))]
public readonly record struct ArgbColor(byte A, byte R, byte G, byte B)
{
    public static ArgbColor FromRgb(byte r, byte g, byte b) => new(255, r, g, b);

    /// <summary>Retourne la même couleur avec une opacité (0 à 1) donnée.</summary>
    public ArgbColor WithOpacity(double opacity) =>
        this with { A = (byte)Math.Round(Math.Clamp(opacity, 0, 1) * 255) };

    /// <summary>Opacité de la couleur entre 0 et 1.</summary>
    public double Opacity => A / 255.0;

    /// <summary>Format « #AARRGGBB ».</summary>
    public override string ToString() => $"#{A:X2}{R:X2}{G:X2}{B:X2}";

    /// <summary>Format « #RRGGBB » (sans alpha).</summary>
    public string ToRgbHex() => $"#{R:X2}{G:X2}{B:X2}";

    /// <summary>Analyse « #RGB », « #RRGGBB » ou « #AARRGGBB ».</summary>
    public static bool TryParse(string? text, out ArgbColor color)
    {
        color = default;
        if (string.IsNullOrWhiteSpace(text)) return false;
        var s = text.Trim().TrimStart('#');
        if (s.Length == 3)
            s = string.Concat(s[0], s[0], s[1], s[1], s[2], s[2]);
        if (s.Length == 6) s = "FF" + s;
        if (s.Length != 8) return false;
        if (!uint.TryParse(s, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var v)) return false;
        color = new ArgbColor((byte)(v >> 24), (byte)(v >> 16), (byte)(v >> 8), (byte)v);
        return true;
    }

    public static ArgbColor Parse(string text) =>
        TryParse(text, out var c) ? c : throw new FormatException($"Couleur invalide : « {text} »");
}

/// <summary>Convertisseur JSON pour <see cref="ArgbColor"/>.</summary>
public sealed class ArgbColorJsonConverter : JsonConverter<ArgbColor>
{
    public override ArgbColor Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var s = reader.GetString();
        if (ArgbColor.TryParse(s, out var c)) return c;
        throw new JsonException($"Couleur invalide : « {s} »");
    }

    public override void Write(Utf8JsonWriter writer, ArgbColor value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value.ToString());
}
