using System.Runtime.InteropServices;
using Pointik.Core.Effects;
using Pointik.Core.Input;
using Pointik.Core.Platform;
using static Pointik.Platform.Windows.Interop.Native;

namespace Pointik.Platform.Windows;

/// <summary>Joue le son de clic généré en mémoire (aucun fichier, aucune dépendance).</summary>
internal sealed class WindowsSoundPlayer : ISoundPlayer
{
    // En lecture asynchrone, Windows lit le tampon après le retour de PlaySound :
    // il est donc copié une fois pour toutes en mémoire non managée (adresse fixe).
    private readonly Lazy<nint> _left = new(() => Allocate(MouseButton.Left));
    private readonly Lazy<nint> _right = new(() => Allocate(MouseButton.Right));
    private readonly Lazy<nint> _middle = new(() => Allocate(MouseButton.Middle));

    public void PlayClick(MouseButton button)
    {
        try
        {
            var data = button switch
            {
                MouseButton.Left => _left.Value,
                MouseButton.Right => _right.Value,
                _ => _middle.Value,
            };
            PlaySoundW(data, 0, SND_MEMORY | SND_ASYNC | SND_NODEFAULT);
        }
        catch
        {
            // Pas de son disponible : sans conséquence.
        }
    }

    /// <summary>Tampon conservé jusqu'à la fin du processus (3 sons de quelques Ko).</summary>
    private static nint Allocate(MouseButton button)
    {
        var wav = ClickSoundGenerator.CreateWav(button);
        var ptr = Marshal.AllocHGlobal(wav.Length);
        Marshal.Copy(wav, 0, ptr, wav.Length);
        return ptr;
    }
}
