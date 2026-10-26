using System.Diagnostics;
using Pointik.Core.Geometry;
using Pointik.Core.Platform;
using Pointik.Platform.Windows.Interop;
using static Pointik.Platform.Windows.Interop.Native;

namespace Pointik.Platform.Windows;

/// <summary>
/// Zoom plein écran par l'API Magnification de Windows (MagSetFullscreenTransform).
/// Fonctionne sans droits administrateur. Les clics de souris tombent sur ce que l'on voit :
/// le curseur est lui-même dessiné dans la vue agrandie et Windows positionne le pointeur réel
/// en conséquence. (MagSetInputTransform ne concerne que le stylet et le tactile et exige le
/// privilège UIAccess, donc une signature et une installation dans Program Files : non utilisé.)
/// Tous les appels passent par le thread Win32 dédié, celui qui a appelé MagInitialize.
/// </summary>
internal sealed class MagnificationZoomService : IZoomService
{
    private readonly Win32MessageThread _thread;
    private volatile bool _initialized;

    public MagnificationZoomService(Win32MessageThread thread)
    {
        _thread = thread;
        // Fin de session : l'écran est remis à 1× avant que Windows ne ferme la session.
        _thread.SessionEnding += ResetOnCurrentThread;
    }

    /// <summary>Les fenêtres overlay sont agrandies comme le reste du bureau : l'UI compense par 1/zoom.</summary>
    public bool OverlaysAreMagnified => true;

    public bool TryInitialize(out string? error)
    {
        error = null;
        if (_initialized) return true;

        if (IsWindowsMagnifierRunning())
        {
            error = "La Loupe de Windows est ouverte. Fermez-la (Win + Échap) pour utiliser le zoom de Pointik.";
            return false;
        }

        string? failure = null;
        _thread.Invoke(() =>
        {
            if (!MagInitialize())
            {
                failure = "L'API de zoom de Windows (Magnification) n'a pas pu être initialisée.";
                return;
            }
            // Un autre logiciel applique déjà un zoom plein écran : on ne le perturbe pas.
            if (MagGetFullscreenTransform(out var level, out _, out _) && Math.Abs(level - 1f) > 0.01f)
            {
                MagUninitialize();
                failure = "Un autre logiciel de loupe est déjà actif. Fermez-le pour utiliser le zoom de Pointik.";
                return;
            }
            _initialized = true;
        });
        error = failure;
        return _initialized;
    }

    public bool TrySetTransform(double scale, ScreenPoint viewOrigin, out string? error)
    {
        error = null;
        if (!_initialized)
        {
            error = "Le zoom n'est pas initialisé.";
            return false;
        }
        var ok = false;
        _thread.Invoke(() =>
        {
            ok = MagSetFullscreenTransform((float)scale, (int)Math.Round(viewOrigin.X), (int)Math.Round(viewOrigin.Y));
        });
        if (!ok) error = "Windows a refusé d'appliquer le zoom (une autre loupe est peut-être active).";
        return ok;
    }

    /// <summary>Remet le zoom à 1×, depuis n'importe quel thread, sans jamais lever d'exception.</summary>
    public void Reset()
    {
        if (!_initialized) return;
        try
        {
            if (_thread.IsCurrentThread) ResetOnCurrentThread();
            else _thread.Invoke(ResetOnCurrentThread);
        }
        catch
        {
            // Thread Win32 indisponible : dernier recours, appel direct.
            try { MagSetFullscreenTransform(1f, 0, 0); }
            catch { /* rien de plus à faire */ }
        }
    }

    private void ResetOnCurrentThread()
    {
        if (!_initialized) return;
        try
        {
            MagSetFullscreenTransform(1f, 0, 0);
        }
        catch
        {
            // Ne jamais lever d'exception ici (appelé depuis la fin de session ou un plantage).
        }
    }

    private static bool IsWindowsMagnifierRunning()
    {
        try
        {
            var procs = Process.GetProcessesByName("Magnify");
            var running = procs.Length > 0;
            foreach (var p in procs) p.Dispose();
            return running;
        }
        catch
        {
            return false;
        }
    }

    public void Dispose()
    {
        Reset();
        if (!_initialized) return;
        try { _thread.Invoke(() => MagUninitialize()); }
        catch { /* au mieux */ }
        _initialized = false;
    }
}
