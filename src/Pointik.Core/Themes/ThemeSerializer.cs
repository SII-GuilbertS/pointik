using System.Text.Json;
using Pointik.Core.Settings;

namespace Pointik.Core.Themes;

/// <summary>Import et export d'un thème du clavier dans un fichier JSON autonome.</summary>
public static class ThemeSerializer
{
    /// <summary>Marqueur de format, pour refuser un fichier JSON qui ne serait pas un thème.</summary>
    public const string FormatId = "pointik-theme";
    public const int FormatVersion = 1;

    private sealed class ThemeFile
    {
        public string? Format { get; set; }
        public int Version { get; set; }
        public string? Name { get; set; }
        public KeyboardStyle? Style { get; set; }
    }

    public static string Export(KeyboardTheme theme) =>
        JsonSerializer.Serialize(new ThemeFile
        {
            Format = FormatId,
            Version = FormatVersion,
            Name = theme.Name,
            Style = theme.Style,
        }, PointikJson.Options);

    /// <summary>Lit un thème. Lève <see cref="FormatException"/> avec un message en français si le fichier n'est pas valide.</summary>
    public static KeyboardTheme Import(string json)
    {
        ThemeFile? file;
        try
        {
            file = JsonSerializer.Deserialize<ThemeFile>(json, PointikJson.Options);
        }
        catch (JsonException ex)
        {
            throw new FormatException("Ce fichier n'est pas un JSON valide.", ex);
        }
        if (file is null || !string.Equals(file.Format, FormatId, StringComparison.Ordinal))
            throw new FormatException("Ce fichier n'est pas un thème Pointik.");
        if (file.Version > FormatVersion)
            throw new FormatException("Ce thème a été créé par une version plus récente de Pointik.");
        if (string.IsNullOrWhiteSpace(file.Name) || file.Style is null)
            throw new FormatException("Le thème est incomplet (nom ou style manquant).");

        var style = file.Style;
        style.Normalize();
        return new KeyboardTheme { Name = file.Name.Trim(), Style = style };
    }
}
