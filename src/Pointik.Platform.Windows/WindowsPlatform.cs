using Pointik.Core.Input;
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
        var zoom = s_zoom = new PendingZoomService();
        return new PlatformServices
        {
            InputHook = new WindowsInputHook(thread, ui),
            KeyTranslator = new PendingKeyTranslator(),
            Screens = new WindowsScreenService(thread, ui),
            OverlayHost = new WindowsOverlayWindowHost(thread, ui),
            Zoom = zoom,
            Hotkeys = new PendingHotkeyService(),
            Autostart = new PendingAutostart(),
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

    // Implémentations provisoires, remplacées aux étapes suivantes.
    private sealed class PendingZoomService : IZoomService
    {
        public bool OverlaysAreMagnified => true;
        public bool TryInitialize(out string? error) { error = "Zoom non disponible."; return false; }
        public bool TrySetTransform(double scale, Core.Geometry.ScreenPoint viewOrigin, out string? error) { error = null; return false; }
        public void Reset() { }
        public void Dispose() { }
    }

    private sealed class PendingKeyTranslator : IKeyTranslator
    {
        public string MetaKeyName => "Win";
        public string AltKeyName => "Alt";
        public KeyboardInputEvent Translate(in NativeKeyEvent native) => new(KeyId.None, native.IsDown, native.Modifiers);
    }

    private sealed class PendingHotkeyService : IHotkeyService
    {
        public event Action<HotkeyAction>? HotkeyPressed { add { } remove { } }
        public bool TryRegister(HotkeyAction action, Hotkey hotkey, out string? error) { error = null; return true; }
        public void Unregister(HotkeyAction action) { }
        public void UnregisterAll() { }
        public void Dispose() { }
    }

    private sealed class PendingAutostart : IAutostartService
    {
        public bool IsEnabled => false;
        public bool TryEnable(string executablePath, out string? error) { error = null; return false; }
        public bool TryDisable(out string? error) { error = null; return true; }
    }
}
