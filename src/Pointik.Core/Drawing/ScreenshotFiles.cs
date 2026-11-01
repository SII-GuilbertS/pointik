using System.Globalization;

namespace Pointik.Core.Drawing;

/// <summary>Nommage des captures d'écran : « Pointik 2026-11-01 21-03-05.png », sans jamais écraser un fichier.</summary>
public static class ScreenshotFiles
{
    /// <summary>Sous-dossier des captures dans le dossier Images de l'utilisateur.</summary>
    public const string FolderName = "Pointik";

    /// <summary>Dossier des captures : Images\Pointik (ou Documents\Pointik si Images est introuvable).</summary>
    public static string DefaultFolder()
    {
        var pictures = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
        if (string.IsNullOrEmpty(pictures)) pictures = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        return Path.Combine(pictures, FolderName);
    }

    /// <summary>
    /// Chemin libre pour une capture prise à <paramref name="now"/> : un suffixe « (2) », « (3) »…
    /// est ajouté si plusieurs captures tombent dans la même seconde.
    /// </summary>
    public static string NextPath(string folder, DateTime now, Func<string, bool> exists)
    {
        var stem = "Pointik " + now.ToString("yyyy-MM-dd HH-mm-ss", CultureInfo.InvariantCulture);
        var path = Path.Combine(folder, stem + ".png");
        for (var n = 2; exists(path); n++) path = Path.Combine(folder, $"{stem} ({n}).png");
        return path;
    }
}
