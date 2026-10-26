using System.Runtime.InteropServices;
using Pointik.Core.Geometry;
using Pointik.Core.Input;
using Pointik.Core.Platform;
using Pointik.Platform.Windows.Interop;
using static Pointik.Platform.Windows.Interop.Native;

namespace Pointik.Platform.Windows;

/// <summary>
/// Hooks globaux WH_MOUSE_LL et WH_KEYBOARD_LL installés sur le thread Win32 dédié.
/// Les callbacks ne font que copier l'événement et le poster vers le thread UI ; ils appellent
/// toujours CallNextHookEx (sauf molette + Ctrl+Alt pendant le zoom) et n'enregistrent rien.
/// </summary>
internal sealed class WindowsInputHook : IInputHook
{
    private const int VK_SHIFT = 0x10, VK_CONTROL = 0x11, VK_MENU = 0x12, VK_CAPITAL = 0x14;
    private const int VK_LWIN = 0x5B, VK_RWIN = 0x5C, VK_LCONTROL = 0xA2, VK_RMENU = 0xA5;

    private readonly Win32MessageThread _thread;
    private readonly IUiDispatcher _ui;

    // Références statiques aux delegates : le ramasse-miettes ne doit jamais les collecter
    // tant que les hooks sont installés (sinon plantage natif).
    private static LowLevelHookProc? s_mouseProc;
    private static LowLevelHookProc? s_keyboardProc;

    private nint _mouseHook;
    private nint _keyboardHook;
    private volatile bool _interceptWheel;

    // Fusion des déplacements : seule la dernière position est transmise au thread UI.
    private long _pendingMove; // 0 = rien en attente, 1 = position en attente
    private int _lastX, _lastY;

    public WindowsInputHook(Win32MessageThread thread, IUiDispatcher ui)
    {
        _thread = thread;
        _ui = ui;
        s_mouseProc ??= MouseProc;
        s_keyboardProc ??= KeyboardProc;
    }

    public event Action<MouseInputEvent>? MouseEvent;
    public event Action<NativeKeyEvent>? KeyEvent;
    public event Action<int>? CtrlAltWheel;

    public bool MouseEnabled
    {
        get => _mouseHook != 0;
        set => _thread.Invoke(() =>
        {
            if (value && _mouseHook == 0)
                _mouseHook = SetWindowsHookExW(WH_MOUSE_LL, s_mouseProc!, GetModuleHandleW(null), 0);
            else if (!value && _mouseHook != 0)
            {
                UnhookWindowsHookEx(_mouseHook);
                _mouseHook = 0;
            }
        });
    }

    public bool KeyboardEnabled
    {
        get => _keyboardHook != 0;
        set => _thread.Invoke(() =>
        {
            if (value && _keyboardHook == 0)
                _keyboardHook = SetWindowsHookExW(WH_KEYBOARD_LL, s_keyboardProc!, GetModuleHandleW(null), 0);
            else if (!value && _keyboardHook != 0)
            {
                UnhookWindowsHookEx(_keyboardHook);
                _keyboardHook = 0;
            }
        });
    }

    public bool InterceptCtrlAltWheel
    {
        get => _interceptWheel;
        set => _interceptWheel = value;
    }

    private nint MouseProc(int nCode, nint wParam, nint lParam)
    {
        if (nCode >= 0)
        {
            try
            {
                var data = Marshal.PtrToStructure<MSLLHOOKSTRUCT>(lParam);
                var msg = (int)wParam;
                if (msg == WM_MOUSEMOVE)
                {
                    Volatile.Write(ref _lastX, data.pt.X);
                    Volatile.Write(ref _lastY, data.pt.Y);
                    if (Interlocked.Exchange(ref _pendingMove, 1) == 0)
                        _ui.Post(FlushMove);
                }
                else if (msg == WM_MOUSEWHEEL)
                {
                    if (_interceptWheel && IsDown(VK_CONTROL) && IsDown(VK_MENU))
                    {
                        var delta = (short)(data.mouseData >> 16);
                        _ui.Post(() => CtrlAltWheel?.Invoke(delta));
                        // Seul cas où un événement est consommé : Ctrl+Alt+molette pendant le zoom.
                        return 1;
                    }
                }
                else if (TryGetButton(msg, data.mouseData, out var button, out var isDown))
                {
                    var e = new MouseInputEvent(isDown ? MouseEventKind.ButtonDown : MouseEventKind.ButtonUp,
                        new ScreenPoint(data.pt.X, data.pt.Y), button);
                    _ui.Post(() => MouseEvent?.Invoke(e));
                }
            }
            catch
            {
                // Ne jamais laisser une exception remonter dans le hook natif.
            }
        }
        return CallNextHookEx(_mouseHook, nCode, wParam, lParam);
    }

    private void FlushMove()
    {
        Interlocked.Exchange(ref _pendingMove, 0);
        var p = new ScreenPoint(Volatile.Read(ref _lastX), Volatile.Read(ref _lastY));
        MouseEvent?.Invoke(new MouseInputEvent(MouseEventKind.Move, p));
    }

    private static bool TryGetButton(int msg, uint mouseData, out MouseButton button, out bool isDown)
    {
        (button, isDown) = msg switch
        {
            WM_LBUTTONDOWN => (MouseButton.Left, true),
            WM_LBUTTONUP => (MouseButton.Left, false),
            WM_RBUTTONDOWN => (MouseButton.Right, true),
            WM_RBUTTONUP => (MouseButton.Right, false),
            WM_MBUTTONDOWN => (MouseButton.Middle, true),
            WM_MBUTTONUP => (MouseButton.Middle, false),
            WM_XBUTTONDOWN => ((mouseData >> 16) == 1 ? MouseButton.X1 : MouseButton.X2, true),
            WM_XBUTTONUP => ((mouseData >> 16) == 1 ? MouseButton.X1 : MouseButton.X2, false),
            _ => (MouseButton.Left, false),
        };
        return msg is WM_LBUTTONDOWN or WM_LBUTTONUP or WM_RBUTTONDOWN or WM_RBUTTONUP
            or WM_MBUTTONDOWN or WM_MBUTTONUP or WM_XBUTTONDOWN or WM_XBUTTONUP;
    }

    private nint KeyboardProc(int nCode, nint wParam, nint lParam)
    {
        if (nCode >= 0)
        {
            try
            {
                var data = Marshal.PtrToStructure<KBDLLHOOKSTRUCT>(lParam);
                var msg = (int)wParam;
                var isDown = msg is WM_KEYDOWN or WM_SYSKEYDOWN;

                // Instantané minimal de l'état des modificateurs, pris au moment exact de la frappe.
                var mods = KeyModifiers.None;
                if (IsDown(VK_CONTROL)) mods |= KeyModifiers.Ctrl;
                if (IsDown(VK_SHIFT)) mods |= KeyModifiers.Shift;
                if (IsDown(VK_MENU)) mods |= KeyModifiers.Alt;
                if (IsDown(VK_LWIN) || IsDown(VK_RWIN)) mods |= KeyModifiers.Meta;
                // AltGr = Alt droite + Ctrl gauche simulé par Windows (dispositions européennes).
                var altGr = IsDown(VK_RMENU) && IsDown(VK_LCONTROL);
                var caps = (GetKeyState(VK_CAPITAL) & 1) != 0;
                var layout = GetKeyboardLayout(GetWindowThreadProcessId(GetForegroundWindow(), out _));

                var e = new NativeKeyEvent((int)data.vkCode, (int)data.scanCode,
                    (data.flags & LLKHF_EXTENDED) != 0, isDown, (data.flags & LLKHF_INJECTED) != 0,
                    mods, altGr, caps, layout);
                _ui.Post(() => KeyEvent?.Invoke(e));
            }
            catch
            {
                // Ne jamais laisser une exception remonter dans le hook natif.
            }
        }
        return CallNextHookEx(_keyboardHook, nCode, wParam, lParam);
    }

    private static bool IsDown(int vk) => (GetAsyncKeyState(vk) & 0x8000) != 0;

    public void Dispose()
    {
        _interceptWheel = false;
        MouseEnabled = false;
        KeyboardEnabled = false;
    }
}
