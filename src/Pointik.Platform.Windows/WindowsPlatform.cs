using Pointik.Core.Platform;
using Pointik.Platform.Windows.Interop;

namespace Pointik.Platform.Windows;

/// <summary>Assemble les implémentations Windows des services de plateforme.</summary>
public static class WindowsPlatform
{
    private static Win32MessageThread? s_thread;
    private static IZoomService? s_zoom;

    /// <summary>Crée tous les services. Un seul jeu de services par processus.</summary>
    public static PlatformServices Create(IUiDispatcher ui)
    {
        var thread = s_thread = new Win32MessageThread();
        var zoom = s_zoom = new MagnificationZoomService(thread);
        return new PlatformServices
        {
            InputHook = new WindowsInputHook(thread, ui),
            KeyTranslator = new WindowsKeyTranslator(),
            Screens = new WindowsScreenService(thread, ui),
            OverlayHost = new WindowsOverlayWindowHost(thread, ui),
            Zoom = zoom,
            Hotkeys = new WindowsHotkeyService(thread, ui),
            Autostart = new RegistryAutostartService(),
            Sound = new WindowsSoundPlayer(),
            DisposeExtra = () =>
            {
                s_thread = null;
                thread.Dispose();
            },
        };
    }

    /// <summary>
    /// Remise à 1× d'urgence (exception non gérée, arrêt du processus). Sans exception, appelable de n'importe quel thread.
    /// </summary>
    public static void EmergencyResetZoom()
    {
        try { s_zoom?.Reset(); }
        catch { /* au mieux */ }
    }
}
