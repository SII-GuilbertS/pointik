using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using static Pointik.Platform.Windows.Interop.Native;

namespace Pointik.Platform.Windows.Interop;

/// <summary>
/// Thread dédié avec sa propre boucle de messages Win32 et une fenêtre cachée.
/// Il héberge les hooks bas niveau (qui exigent une boucle de messages), les raccourcis globaux,
/// les appels à l'API Magnification, et reçoit les notifications système (écrans, fin de session).
/// Ainsi, un thread UI occupé à dessiner ne ralentit jamais la souris ni le clavier.
/// </summary>
internal sealed class Win32MessageThread : IDisposable
{
    private const uint WM_APP_INVOKE = WM_APP + 1;
    private const string ClassName = "Pointik.MessageWindow";

    private readonly Thread _thread;
    private readonly ConcurrentQueue<Action> _queue = new();
    private readonly ManualResetEventSlim _ready = new();
    // Référence statique au delegate : il ne doit jamais être collecté tant que la fenêtre existe.
    private static WndProc? s_wndProc;
    private nint _hwnd;
    private int _threadId;
    private bool _disposed;

    /// <summary>Message reçu par la fenêtre cachée. Retourne true si le message est traité.</summary>
    public event Func<uint, nint, nint, bool>? MessageReceived;

    /// <summary>Fin de session Windows (déconnexion, arrêt) : déclenché sur ce thread, à traiter immédiatement.</summary>
    public event Action? SessionEnding;

    public Win32MessageThread()
    {
        _thread = new Thread(Run) { IsBackground = true, Name = "Pointik.Win32" };
        _thread.Start();
        _ready.Wait();
    }

    public nint WindowHandle => _hwnd;

    public bool IsCurrentThread => (int)GetCurrentThreadId() == _threadId;

    /// <summary>Exécute l'action sur le thread de messages et attend sa fin.</summary>
    public void Invoke(Action action)
    {
        if (IsCurrentThread || _disposed)
        {
            if (IsCurrentThread) action();
            return;
        }
        using var done = new ManualResetEventSlim();
        Exception? error = null;
        _queue.Enqueue(() =>
        {
            try { action(); }
            catch (Exception ex) { error = ex; }
            finally { done.Set(); }
        });
        if (!PostMessageW(_hwnd, WM_APP_INVOKE, 0, 0))
            return;
        // Garde-fou : ne jamais bloquer indéfiniment le thread appelant.
        done.Wait(TimeSpan.FromSeconds(5));
        if (error is not null) throw new InvalidOperationException("Erreur sur le thread Win32.", error);
    }

    /// <summary>Exécute l'action sur le thread de messages sans attendre.</summary>
    public void Post(Action action)
    {
        if (_disposed) return;
        _queue.Enqueue(action);
        PostMessageW(_hwnd, WM_APP_INVOKE, 0, 0);
    }

    private void Run()
    {
        _threadId = (int)GetCurrentThreadId();
        s_wndProc ??= WindowProc;
        var hInstance = GetModuleHandleW(null);
        var wc = new WNDCLASSEXW
        {
            cbSize = (uint)Marshal.SizeOf<WNDCLASSEXW>(),
            lpfnWndProc = Marshal.GetFunctionPointerForDelegate(s_wndProc),
            hInstance = hInstance,
            lpszClassName = ClassName,
        };
        RegisterClassExW(ref wc);
        // Fenêtre de premier niveau jamais affichée (et non « message-only ») : elle reçoit
        // ainsi les messages diffusés WM_DISPLAYCHANGE, WM_SETTINGCHANGE et WM_ENDSESSION.
        _hwnd = CreateWindowExW(WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE, ClassName, "Pointik", WS_POPUP,
            0, 0, 0, 0, 0, 0, hInstance, 0);
        _ready.Set();

        while (GetMessageW(out var msg, 0, 0, 0) > 0)
        {
            TranslateMessage(ref msg);
            DispatchMessageW(ref msg);
        }
        UnregisterClassW(ClassName, hInstance);
    }

    private nint WindowProc(nint hWnd, uint msg, nint wParam, nint lParam)
    {
        try
        {
            switch (msg)
            {
                case WM_APP_INVOKE:
                    while (_queue.TryDequeue(out var action))
                    {
                        try { action(); }
                        catch { /* une action défaillante ne doit pas arrêter la boucle */ }
                    }
                    return 0;
                case WM_QUERYENDSESSION:
                    // On autorise toujours la fin de session, mais on remet déjà l'écran en état.
                    SessionEnding?.Invoke();
                    return 1;
                case WM_ENDSESSION:
                    if (wParam != 0) SessionEnding?.Invoke();
                    return 0;
                case WM_DESTROY:
                    PostQuitMessage(0);
                    return 0;
            }
            if (MessageReceived?.Invoke(msg, wParam, lParam) == true)
                return 0;
        }
        catch
        {
            // Aucune exception ne doit traverser la frontière native.
        }
        return DefWindowProcW(hWnd, msg, wParam, lParam);
    }

    public void Dispose()
    {
        if (_disposed) return;
        Invoke(() => DestroyWindow(_hwnd));
        _disposed = true;
        _thread.Join(TimeSpan.FromSeconds(2));
        _ready.Dispose();
    }
}
