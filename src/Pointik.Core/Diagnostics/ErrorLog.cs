using System.Text;

namespace Pointik.Core.Diagnostics;

/// <summary>
/// Journal d'erreurs local. Il ne contient QUE des erreurs techniques (type, message, pile d'appels) :
/// aucune frappe, aucun texte saisi n'y est jamais écrit. Taille plafonnée.
/// </summary>
public sealed class ErrorLog
{
    public const string FileName = "Pointik.errors.log";
    private const long MaxBytes = 256 * 1024;

    private readonly object _gate = new();
    private string? _directory;

    /// <summary>Chemin complet du journal (null tant qu'aucun dossier n'est défini).</summary>
    public string? FilePath => _directory is null ? null : Path.Combine(_directory, FileName);

    /// <summary>Définit le dossier du journal (celui des réglages).</summary>
    public void SetDirectory(string directory)
    {
        lock (_gate) _directory = directory;
    }

    /// <summary>N'écrit plus rien (après « Supprimer toutes les traces »).</summary>
    public void Disable()
    {
        lock (_gate) _directory = null;
    }

    public void Error(string context, Exception ex) => Write("ERREUR", $"{context} : {ex}");

    public void Warning(string message) => Write("AVERT.", message);

    private void Write(string level, string message)
    {
        try
        {
            lock (_gate)
            {
                var path = FilePath;
                if (path is null) return;
                Directory.CreateDirectory(_directory!);
                var info = new FileInfo(path);
                // Rotation simple : au-delà de la taille maximale, on repart d'un fichier vide.
                if (info.Exists && info.Length > MaxBytes) info.Delete();
                var line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} [{level}] {message}{Environment.NewLine}";
                File.AppendAllText(path, line, Encoding.UTF8);
            }
        }
        catch
        {
            // Le journal ne doit jamais faire planter l'application.
        }
    }
}
