using Microsoft.Win32;
using Pointik.Core.Platform;

namespace Pointik.Platform.Windows;

/// <summary>
/// « Lancer avec Windows » : valeur dans HKCU\Software\Microsoft\Windows\CurrentVersion\Run.
/// Aucune écriture tant que l'option n'est pas cochée ; la valeur est retirée quand on la décoche.
/// Aucun droit administrateur requis (ruche de l'utilisateur).
/// </summary>
internal sealed class RegistryAutostartService : IAutostartService
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "Pointik";

    public bool IsEnabled
    {
        get
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: false);
                return key?.GetValue(ValueName) is string;
            }
            catch
            {
                return false;
            }
        }
    }

    public bool TryEnable(string executablePath, out string? error)
    {
        error = null;
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(RunKey, writable: true);
            // Guillemets : le chemin peut contenir des espaces (clé USB, dossier utilisateur).
            key.SetValue(ValueName, $"\"{executablePath}\"", RegistryValueKind.String);
            return true;
        }
        catch (Exception ex)
        {
            error = "Impossible d'ajouter Pointik au démarrage de Windows : " + ex.Message;
            return false;
        }
    }

    public bool TryDisable(out string? error)
    {
        error = null;
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: true);
            key?.DeleteValue(ValueName, throwOnMissingValue: false);
            return true;
        }
        catch (Exception ex)
        {
            error = "Impossible de retirer Pointik du démarrage de Windows : " + ex.Message;
            return false;
        }
    }
}
