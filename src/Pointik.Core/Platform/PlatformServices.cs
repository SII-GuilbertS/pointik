namespace Pointik.Core.Platform;

/// <summary>
/// Ensemble des services fournis par une plateforme (Windows en V1, macOS plus tard).
/// Construit par le point d'entrée propre à l'OS et transmis à l'interface.
/// </summary>
public sealed class PlatformServices : IDisposable
{
    public required IInputHook InputHook { get; init; }
    public required IKeyTranslator KeyTranslator { get; init; }
    public required IScreenService Screens { get; init; }
    public required IOverlayWindowHost OverlayHost { get; init; }
    public required IZoomService Zoom { get; init; }
    public required IHotkeyService Hotkeys { get; init; }
    public required IAutostartService Autostart { get; init; }
    public required ISoundPlayer Sound { get; init; }
    public required ISecureInputDetector SecureInput { get; init; }

    /// <summary>Libération supplémentaire propre à la plateforme (thread de messages…).</summary>
    public Action? DisposeExtra { get; init; }

    /// <summary>Désinstalle hooks et raccourcis, remet le zoom à 1×. Ne lève jamais d'exception.</summary>
    public void Dispose()
    {
        Try(() => Zoom.Reset());
        Try(Zoom.Dispose);
        Try(Hotkeys.Dispose);
        Try(InputHook.Dispose);
        Try(SecureInput.Dispose);
        Try(() => DisposeExtra?.Invoke());
    }

    private static void Try(Action a)
    {
        try { a(); }
        catch { /* arrêt au mieux */ }
    }
}
