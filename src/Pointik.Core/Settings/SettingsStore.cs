using System.Text.Json;
using Pointik.Core.Diagnostics;

namespace Pointik.Core.Settings;

/// <summary>
/// Lecture et écriture de « Pointik.settings.json ».
/// Mode portable : le fichier est à côté de l'exécutable. Si ce dossier est en lecture seule,
/// repli sur un dossier utilisateur (%AppData%\Pointik sous Windows).
/// </summary>
public sealed class SettingsStore
{
    public const string FileName = "Pointik.settings.json";

    private readonly string _portableDirectory;
    private readonly string _fallbackDirectory;
    private readonly ErrorLog _log;

    public SettingsStore(string portableDirectory, string fallbackDirectory, ErrorLog log)
    {
        _portableDirectory = portableDirectory;
        _fallbackDirectory = fallbackDirectory;
        _log = log;
        IsPortable = IsDirectoryWritable(portableDirectory);
        ActiveDirectory = IsPortable ? portableDirectory : fallbackDirectory;
    }

    /// <summary>Vrai si les réglages sont enregistrés à côté de l'exécutable.</summary>
    public bool IsPortable { get; }

    /// <summary>Dossier où les réglages (et le journal d'erreurs) sont écrits.</summary>
    public string ActiveDirectory { get; }

    public string ActiveFilePath => Path.Combine(ActiveDirectory, FileName);

    /// <summary>
    /// Charge les réglages. Retourne des valeurs par défaut sûres si le fichier est absent ou corrompu
    /// (le fichier corrompu est alors renommé en « .corrompu » pour ne pas être perdu).
    /// </summary>
    public AppSettings Load()
    {
        var path = ActiveFilePath;
        // Dossier portable en lecture seule sans réglages en repli : on lit ceux de la clé USB.
        if (!File.Exists(path) && !IsPortable)
        {
            var portable = Path.Combine(_portableDirectory, FileName);
            if (File.Exists(portable)) path = portable;
        }
        if (!File.Exists(path)) return new AppSettings().Normalize();

        try
        {
            var json = File.ReadAllText(path);
            var settings = Deserialize(json);
            if (settings is not null) return settings;
            throw new JsonException("Fichier de réglages vide.");
        }
        catch (Exception ex)
        {
            _log.Error($"Réglages illisibles ({path}), valeurs par défaut utilisées", ex);
            TryQuarantine(path);
            return new AppSettings().Normalize();
        }
    }

    /// <summary>Enregistre les réglages (écriture atomique via un fichier temporaire).</summary>
    public bool Save(AppSettings settings)
    {
        try
        {
            Directory.CreateDirectory(ActiveDirectory);
            var tmp = ActiveFilePath + ".tmp";
            File.WriteAllText(tmp, Serialize(settings));
            File.Move(tmp, ActiveFilePath, overwrite: true);
            return true;
        }
        catch (Exception ex)
        {
            _log.Error("Échec de l'enregistrement des réglages", ex);
            return false;
        }
    }

    /// <summary>
    /// Supprime toutes les traces de réglages : fichier portable, fichier de repli, journal d'erreurs,
    /// et le dossier de repli s'il est vide. Retourne la liste des éléments non supprimés.
    /// </summary>
    public IReadOnlyList<string> DeleteAllTraces()
    {
        var failures = new List<string>();
        foreach (var dir in new[] { _portableDirectory, _fallbackDirectory })
        {
            foreach (var name in new[] { FileName, FileName + ".tmp", FileName + ".corrompu", ErrorLog.FileName })
            {
                var p = Path.Combine(dir, name);
                try
                {
                    if (File.Exists(p)) File.Delete(p);
                }
                catch
                {
                    failures.Add(p);
                }
            }
        }
        try
        {
            if (Directory.Exists(_fallbackDirectory) && !Directory.EnumerateFileSystemEntries(_fallbackDirectory).Any())
                Directory.Delete(_fallbackDirectory);
        }
        catch
        {
            failures.Add(_fallbackDirectory);
        }
        return failures;
    }

    public static string Serialize(AppSettings settings) =>
        JsonSerializer.Serialize(settings, PointikJson.Options);

    public static AppSettings? Deserialize(string json) =>
        JsonSerializer.Deserialize<AppSettings>(json, PointikJson.Options)?.Normalize();

    /// <summary>Teste réellement l'écriture (les attributs « lecture seule » ne suffisent pas, ex. clé USB verrouillée).</summary>
    public static bool IsDirectoryWritable(string directory)
    {
        try
        {
            if (!Directory.Exists(directory)) return false;
            var probe = Path.Combine(directory, $".pointik-test-{Guid.NewGuid():N}.tmp");
            using (File.Create(probe, 1, FileOptions.DeleteOnClose)) { }
            return true;
        }
        catch
        {
            return false;
        }
    }

    private void TryQuarantine(string path)
    {
        try
        {
            if (Path.GetDirectoryName(path) == ActiveDirectory)
                File.Move(path, path + ".corrompu", overwrite: true);
        }
        catch (Exception ex)
        {
            _log.Error("Impossible de mettre de côté le fichier de réglages corrompu", ex);
        }
    }
}
