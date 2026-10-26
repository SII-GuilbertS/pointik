using Pointik.Core.Input;

namespace Pointik.Platform.Windows;

/// <summary>Correspondance entre codes de touches virtuelles Windows (VK) et touches logiques du Core.</summary>
internal static class VirtualKeys
{
    private static readonly Dictionary<int, KeyId> ToKey = Build();
    private static readonly Dictionary<KeyId, int> ToVk = ToKey
        .GroupBy(kv => kv.Value)
        .ToDictionary(g => g.Key, g => g.First().Key);

    /// <summary>Touche logique d'un code VK. <paramref name="extended"/> distingue Entrée du pavé numérique.</summary>
    public static KeyId FromVirtualKey(int vk, bool extended)
    {
        if (vk == 0x0D && extended) return KeyId.NumPadEnter;
        if (ToKey.TryGetValue(vk, out var key)) return key;
        // Touches OEM (ponctuation) : libellé dépendant de la disposition.
        if (vk is >= 0xBA and <= 0xC0 or >= 0xDB and <= 0xDF or 0xE2) return KeyId.Character;
        return KeyId.None;
    }

    /// <summary>Code VK d'une touche logique (raccourcis globaux), 0 si inconnu.</summary>
    public static int ToVirtualKey(KeyId key) => ToVk.TryGetValue(key, out var vk) ? vk : 0;

    private static Dictionary<int, KeyId> Build()
    {
        var d = new Dictionary<int, KeyId>
        {
            [0x08] = KeyId.Backspace,
            [0x09] = KeyId.Tab,
            [0x0D] = KeyId.Enter,
            [0x13] = KeyId.Pause,
            [0x14] = KeyId.CapsLock,
            [0x1B] = KeyId.Escape,
            [0x20] = KeyId.Space,
            [0x21] = KeyId.PageUp,
            [0x22] = KeyId.PageDown,
            [0x23] = KeyId.End,
            [0x24] = KeyId.Home,
            [0x25] = KeyId.Left,
            [0x26] = KeyId.Up,
            [0x27] = KeyId.Right,
            [0x28] = KeyId.Down,
            [0x2C] = KeyId.PrintScreen,
            [0x2D] = KeyId.Insert,
            [0x2E] = KeyId.Delete,
            [0x5B] = KeyId.LeftMeta,
            [0x5C] = KeyId.RightMeta,
            [0x5D] = KeyId.ContextMenu,
            [0x6A] = KeyId.NumPadMultiply,
            [0x6B] = KeyId.NumPadAdd,
            [0x6D] = KeyId.NumPadSubtract,
            [0x6E] = KeyId.NumPadDecimal,
            [0x6F] = KeyId.NumPadDivide,
            [0x90] = KeyId.NumLock,
            [0x91] = KeyId.ScrollLock,
            [0xA0] = KeyId.LeftShift,
            [0xA1] = KeyId.RightShift,
            [0xA2] = KeyId.LeftCtrl,
            [0xA3] = KeyId.RightCtrl,
            [0xA4] = KeyId.LeftAlt,
            [0xA5] = KeyId.RightAlt,
            // Codes génériques (rares dans les hooks bas niveau, mais possibles avec des frappes simulées)
            [0x10] = KeyId.LeftShift,
            [0x11] = KeyId.LeftCtrl,
            [0x12] = KeyId.LeftAlt,
            [0xAD] = KeyId.VolumeMute,
            [0xAE] = KeyId.VolumeDown,
            [0xAF] = KeyId.VolumeUp,
            [0xB0] = KeyId.MediaNext,
            [0xB1] = KeyId.MediaPrevious,
            [0xB2] = KeyId.MediaStop,
            [0xB3] = KeyId.MediaPlayPause,
        };
        for (var i = 0; i < 26; i++) d[0x41 + i] = KeyId.A + i;
        for (var i = 0; i < 10; i++) d[0x30 + i] = KeyId.D0 + i;
        for (var i = 0; i < 10; i++) d[0x60 + i] = KeyId.NumPad0 + i;
        for (var i = 0; i < 24; i++) d[0x70 + i] = KeyId.F1 + i;
        return d;
    }
}
