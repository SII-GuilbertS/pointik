using System.Runtime.InteropServices;
using Pointik.Core.Platform;
using Pointik.Platform.Windows.Interop;
using static Pointik.Platform.Windows.Interop.Native;

namespace Pointik.Platform.Windows;

/// <summary>
/// Détecte un champ de mot de passe au premier plan grâce à UI Automation (propriété IsPassword),
/// ce qui couvre les champs Win32, WinForms, WPF, UWP et les navigateurs exposant leur accessibilité.
/// Un hook WinEvent (hors contexte) signale chaque changement de focus ; la requête UI Automation,
/// potentiellement lente, est faite sur un thread MTA dédié : la souris et le clavier ne sont jamais ralentis.
/// L'interface COM est appelée directement par sa table de méthodes (aucune dépendance ni
/// infrastructure COM intégrée) ; emplacements vérifiés : GetFocusedElement = 8, GetCurrentPropertyValue = 10.
/// </summary>
internal sealed unsafe class UiaSecureInputDetector : ISecureInputDetector
{
    private static readonly Guid CLSID_CUIAutomation = new("ff48dba4-60ef-4201-aa87-54103eef594e");
    private static readonly Guid IID_IUIAutomation = new("30cbe57d-d9d0-452a-ab13-7ac5ac4825ee");
    private const int SlotRelease = 2;
    private const int SlotGetFocusedElement = 8;
    private const int SlotGetCurrentPropertyValue = 10;
    private const int UIA_IsPasswordPropertyId = 30019;
    private const ushort VT_BOOL = 11;

    // Références statiques : le delegate du hook WinEvent ne doit pas être collecté.
    private static WinEventProc? s_focusProc;

    private readonly Win32MessageThread _thread;
    private readonly IUiDispatcher _ui;
    private readonly AutoResetEvent _signal = new(false);
    private Thread? _worker;
    private nint _focusHook, _foregroundHook;
    private volatile bool _enabled, _disposed, _lastSecure;
    private int _pending, _dirty;

    public UiaSecureInputDetector(Win32MessageThread thread, IUiDispatcher ui)
    {
        _thread = thread;
        _ui = ui;
        s_focusProc ??= OnFocusChanged;
    }

    public event Action<bool>? SecureInputChanged;

    public bool IsChecking => Volatile.Read(ref _pending) != 0;

    public bool Enabled
    {
        get => _enabled;
        set
        {
            if (value == _enabled || _disposed) return;
            _enabled = value;
            _thread.Invoke(() =>
            {
                if (value)
                {
                    _focusHook = SetWinEventHook(EVENT_OBJECT_FOCUS, EVENT_OBJECT_FOCUS, 0, s_focusProc!, 0, 0,
                        WINEVENT_OUTOFCONTEXT | WINEVENT_SKIPOWNPROCESS);
                    // Fenêtres de Pointik incluses : revenir aux réglages lève le masquage.
                    _foregroundHook = SetWinEventHook(EVENT_SYSTEM_FOREGROUND, EVENT_SYSTEM_FOREGROUND, 0, s_focusProc!, 0, 0,
                        WINEVENT_OUTOFCONTEXT);
                }
                else
                {
                    if (_focusHook != 0) UnhookWinEvent(_focusHook);
                    if (_foregroundHook != 0) UnhookWinEvent(_foregroundHook);
                    _focusHook = _foregroundHook = 0;
                }
            });
            if (value)
            {
                _worker ??= StartWorker();
                // État initial : le champ déjà actif est peut-être un mot de passe.
                RequestCheck();
            }
            else
            {
                Volatile.Write(ref _pending, 0);
                Publish(false);
            }
        }
    }

    private void OnFocusChanged(nint hook, uint type, nint hwnd, int obj, int child, uint thread, uint time)
    {
        // Le callback ne fait que signaler : la requête UI Automation est faite ailleurs.
        if (_enabled) RequestCheck();
    }

    private void RequestCheck()
    {
        Volatile.Write(ref _dirty, 1);
        Volatile.Write(ref _pending, 1);
        _signal.Set();
    }

    private Thread StartWorker()
    {
        var t = new Thread(WorkerLoop) { IsBackground = true, Name = "Pointik.SecureInput" };
        t.SetApartmentState(ApartmentState.MTA);
        t.Start();
        return t;
    }

    private void WorkerLoop()
    {
        CoInitializeEx(0, COINIT_MULTITHREADED);
        nint automation = 0;
        try
        {
            if (CoCreateInstance(CLSID_CUIAutomation, 0, CLSCTX_INPROC_SERVER, IID_IUIAutomation, out automation) != 0)
                automation = 0;
            while (!_disposed)
            {
                _signal.WaitOne();
                if (_disposed) break;
                bool secure;
                do
                {
                    // Courte attente : le focus se stabilise souvent en plusieurs étapes.
                    Thread.Sleep(15);
                    Volatile.Write(ref _dirty, 0);
                    secure = automation != 0 && IsFocusedElementPassword(automation);
                }
                while (Volatile.Read(ref _dirty) != 0 && !_disposed);
                if (_enabled) Publish(secure);
                Volatile.Write(ref _pending, 0);
            }
        }
        finally
        {
            if (automation != 0) Release(automation);
            CoUninitialize();
        }
    }

    private void Publish(bool secure)
    {
        if (secure == _lastSecure) return;
        _lastSecure = secure;
        _ui.Post(() => SecureInputChanged?.Invoke(secure));
    }

    /// <summary>Interroge UI Automation : l'élément qui a le focus est-il un champ de mot de passe ?</summary>
    private static bool IsFocusedElementPassword(nint automation)
    {
        nint element = 0;
        var variant = stackalloc byte[24]; // VARIANT : 24 octets en 64 bits, 16 en 32 bits
        try
        {
            var getFocused = (delegate* unmanaged[Stdcall]<nint, nint*, int>)Slot(automation, SlotGetFocusedElement);
            if (getFocused(automation, &element) != 0 || element == 0) return false;
            new Span<byte>(variant, 24).Clear();
            var getProperty = (delegate* unmanaged[Stdcall]<nint, int, byte*, int>)Slot(element, SlotGetCurrentPropertyValue);
            if (getProperty(element, UIA_IsPasswordPropertyId, variant) != 0) return false;
            var vt = *(ushort*)variant;
            var value = *(short*)(variant + 8);
            VariantClear((nint)variant);
            return vt == VT_BOOL && value != 0;
        }
        catch
        {
            return false;
        }
        finally
        {
            if (element != 0) Release(element);
        }
    }

    private static nint Slot(nint com, int index) => (*(nint**)com)[index];

    private static void Release(nint com) =>
        ((delegate* unmanaged[Stdcall]<nint, uint>)Slot(com, SlotRelease))(com);

    public void Dispose()
    {
        if (_disposed) return;
        Enabled = false;
        _disposed = true;
        _signal.Set();
        _worker?.Join(TimeSpan.FromSeconds(1));
        _signal.Dispose();
    }
}
