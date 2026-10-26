using Pointik.Core.Input;
using Pointik.Core.Platform;
using Pointik.Platform.Windows.Interop;
using static Pointik.Platform.Windows.Interop.Native;

namespace Pointik.Platform.Windows;

/// <summary>Raccourcis globaux par RegisterHotKey, sur la fenêtre cachée du thread Win32.</summary>
internal sealed class WindowsHotkeyService : IHotkeyService
{
    private const int ERROR_HOTKEY_ALREADY_REGISTERED = 1409;

    private readonly Win32MessageThread _thread;
    private readonly IUiDispatcher _ui;
    private readonly HashSet<HotkeyAction> _registered = new();

    public WindowsHotkeyService(Win32MessageThread thread, IUiDispatcher ui)
    {
        _thread = thread;
        _ui = ui;
        _thread.MessageReceived += OnMessage;
    }

    public event Action<HotkeyAction>? HotkeyPressed;

    public bool TryRegister(HotkeyAction action, Hotkey hotkey, out string? error)
    {
        error = null;
        Unregister(action);
        if (hotkey.IsEmpty) return true;

        var vk = VirtualKeys.ToVirtualKey(hotkey.Key);
        if (vk == 0)
        {
            error = "Touche non prise en charge pour un raccourci global.";
            return false;
        }
        var mods = MOD_NOREPEAT;
        if (hotkey.Modifiers.HasFlag(KeyModifiers.Ctrl)) mods |= MOD_CONTROL;
        if (hotkey.Modifiers.HasFlag(KeyModifiers.Alt)) mods |= MOD_ALT;
        if (hotkey.Modifiers.HasFlag(KeyModifiers.Shift)) mods |= MOD_SHIFT;
        if (hotkey.Modifiers.HasFlag(KeyModifiers.Meta)) mods |= MOD_WIN;

        var ok = false;
        var lastError = 0;
        _thread.Invoke(() =>
        {
            ok = RegisterHotKey(_thread.WindowHandle, IdOf(action), mods, (uint)vk);
            if (!ok) lastError = System.Runtime.InteropServices.Marshal.GetLastWin32Error();
        });
        if (ok)
        {
            _registered.Add(action);
            return true;
        }
        error = lastError == ERROR_HOTKEY_ALREADY_REGISTERED
            ? "Ce raccourci est déjà utilisé par une autre application."
            : $"Windows a refusé ce raccourci (code {lastError}).";
        return false;
    }

    public void Unregister(HotkeyAction action)
    {
        if (!_registered.Remove(action)) return;
        _thread.Invoke(() => UnregisterHotKey(_thread.WindowHandle, IdOf(action)));
    }

    public void UnregisterAll()
    {
        foreach (var action in _registered.ToList()) Unregister(action);
    }

    private bool OnMessage(uint msg, nint wParam, nint lParam)
    {
        if (msg != WM_HOTKEY) return false;
        var id = (int)wParam - IdBase;
        if (Enum.IsDefined(typeof(HotkeyAction), id))
        {
            var action = (HotkeyAction)id;
            _ui.Post(() => HotkeyPressed?.Invoke(action));
        }
        return true;
    }

    private const int IdBase = 0x5000;

    private static int IdOf(HotkeyAction action) => IdBase + (int)action;

    public void Dispose()
    {
        UnregisterAll();
        _thread.MessageReceived -= OnMessage;
    }
}
