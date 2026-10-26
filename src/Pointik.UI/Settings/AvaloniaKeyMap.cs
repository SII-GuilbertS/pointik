using Avalonia.Input;
using Pointik.Core.Input;
using AKey = Avalonia.Input.Key;
using AModifiers = Avalonia.Input.KeyModifiers;
using CoreModifiers = Pointik.Core.Input.KeyModifiers;

namespace Pointik.UI.Settings;

/// <summary>Conversion des touches Avalonia (capture d'un raccourci) en touches logiques du Core.</summary>
public static class AvaloniaKeyMap
{
    public static KeyId ToKeyId(AKey key)
    {
        if (key >= AKey.A && key <= AKey.Z) return KeyId.A + (key - AKey.A);
        if (key >= AKey.D0 && key <= AKey.D9) return KeyId.D0 + (key - AKey.D0);
        if (key >= AKey.NumPad0 && key <= AKey.NumPad9) return KeyId.NumPad0 + (key - AKey.NumPad0);
        if (key >= AKey.F1 && key <= AKey.F24) return KeyId.F1 + (key - AKey.F1);
        return key switch
        {
            AKey.Enter => KeyId.Enter,
            AKey.Escape => KeyId.Escape,
            AKey.Back => KeyId.Backspace,
            AKey.Tab => KeyId.Tab,
            AKey.Space => KeyId.Space,
            AKey.Delete => KeyId.Delete,
            AKey.Insert => KeyId.Insert,
            AKey.Home => KeyId.Home,
            AKey.End => KeyId.End,
            AKey.PageUp => KeyId.PageUp,
            AKey.PageDown => KeyId.PageDown,
            AKey.Up => KeyId.Up,
            AKey.Down => KeyId.Down,
            AKey.Left => KeyId.Left,
            AKey.Right => KeyId.Right,
            AKey.Multiply => KeyId.NumPadMultiply,
            AKey.Add => KeyId.NumPadAdd,
            AKey.Subtract => KeyId.NumPadSubtract,
            AKey.Decimal => KeyId.NumPadDecimal,
            AKey.Divide => KeyId.NumPadDivide,
            AKey.Pause => KeyId.Pause,
            AKey.PrintScreen => KeyId.PrintScreen,
            AKey.Apps => KeyId.ContextMenu,
            AKey.LeftCtrl => KeyId.LeftCtrl,
            AKey.RightCtrl => KeyId.RightCtrl,
            AKey.LeftShift => KeyId.LeftShift,
            AKey.RightShift => KeyId.RightShift,
            AKey.LeftAlt => KeyId.LeftAlt,
            AKey.RightAlt => KeyId.RightAlt,
            AKey.LWin => KeyId.LeftMeta,
            AKey.RWin => KeyId.RightMeta,
            _ => KeyId.None,
        };
    }

    public static CoreModifiers ToModifiers(AModifiers m)
    {
        var r = CoreModifiers.None;
        if (m.HasFlag(AModifiers.Control)) r |= CoreModifiers.Ctrl;
        if (m.HasFlag(AModifiers.Shift)) r |= CoreModifiers.Shift;
        if (m.HasFlag(AModifiers.Alt)) r |= CoreModifiers.Alt;
        if (m.HasFlag(AModifiers.Meta)) r |= CoreModifiers.Meta;
        return r;
    }
}
