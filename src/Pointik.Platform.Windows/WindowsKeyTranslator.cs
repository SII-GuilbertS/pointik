using System.Text;
using Pointik.Core.Input;
using Pointik.Core.Platform;
using static Pointik.Platform.Windows.Interop.Native;

namespace Pointik.Platform.Windows;

/// <summary>
/// Traduit les frappes Windows en touches logiques en respectant la disposition active
/// de la fenêtre au premier plan (AZERTY compris) grâce à ToUnicodeEx.
/// ToUnicodeEx est appelé avec le drapeau « ne pas modifier l'état du clavier » afin de ne
/// jamais perturber les touches mortes (^, ¨) de l'application en cours de saisie.
/// </summary>
internal sealed class WindowsKeyTranslator : IKeyTranslator
{
    private const int VK_SHIFT = 0x10, VK_CONTROL = 0x11, VK_MENU = 0x12, VK_CAPITAL = 0x14;
    private const int VK_LSHIFT = 0xA0, VK_LCONTROL = 0xA2, VK_RMENU = 0xA5;

    private readonly byte[] _keyState = new byte[256];
    private readonly StringBuilder _buffer = new(8);

    // Touche morte en attente (« ^ » puis « e » donnent « ê »). Uniquement en mémoire.
    private char? _pendingDeadKey;

    public string MetaKeyName => "Win";
    public string AltKeyName => "Alt";

    public KeyboardInputEvent Translate(in NativeKeyEvent native)
    {
        var key = VirtualKeys.FromVirtualKey(native.KeyCode, native.IsExtended);
        string? label = null;
        string? text = null;

        if (native.IsDown && key != KeyId.None && !key.IsModifier())
        {
            if (key == KeyId.Character)
                label = GetLabel(native);
            var commandMods = native.Modifiers & (KeyModifiers.Ctrl | KeyModifiers.Alt | KeyModifiers.Meta);
            // Le texte n'a de sens que sans Ctrl/Alt/Win (sauf AltGr qui sert à taper « @ », « € »…).
            if (commandMods == KeyModifiers.None || native.IsAltGrDown)
                text = GetText(native);
        }

        return new KeyboardInputEvent(key, native.IsDown, native.Modifiers, native.IsAltGrDown, label, text);
    }

    /// <summary>Libellé gravé sur une touche de ponctuation (caractère sans modificateur).</summary>
    private static string? GetLabel(in NativeKeyEvent native)
    {
        var ch = MapVirtualKeyExW((uint)native.KeyCode, MAPVK_VK_TO_CHAR, native.LayoutHandle);
        // Bit de poids fort = touche morte ; le caractère est dans les 16 bits faibles.
        var c = (char)(ch & 0xFFFF);
        return c == 0 ? null : c.ToString();
    }

    /// <summary>Caractère réellement produit, en tenant compte de Maj, Verr. Maj et AltGr.</summary>
    private string? GetText(in NativeKeyEvent native)
    {
        Array.Clear(_keyState);
        if (native.Modifiers.HasFlag(KeyModifiers.Shift))
        {
            _keyState[VK_SHIFT] = 0x80;
            _keyState[VK_LSHIFT] = 0x80;
        }
        if (native.IsAltGrDown)
        {
            _keyState[VK_CONTROL] = 0x80;
            _keyState[VK_LCONTROL] = 0x80;
            _keyState[VK_MENU] = 0x80;
            _keyState[VK_RMENU] = 0x80;
        }
        if (native.IsCapsLockOn) _keyState[VK_CAPITAL] = 0x01;

        _buffer.Clear();
        var n = ToUnicodeEx((uint)native.KeyCode, (uint)native.ScanCode, _keyState, _buffer, 8,
            TOUNICODE_NO_STATE_CHANGE, native.LayoutHandle);

        // Touche morte : n < 0 en temps normal. Mais la traduction a lieu après que l'application
        // au premier plan a reçu la frappe : si elle a déjà mémorisé la touche morte, ToUnicodeEx
        // renvoie « ^^ ». On reconnaît alors la touche morte grâce à MapVirtualKeyEx.
        if (n < 0 || (n == 2 && _buffer[0] == _buffer[1] && IsDeadKey(native)))
        {
            _pendingDeadKey = _buffer.Length > 0 ? _buffer[0] : null;
            return null;
        }
        if (n == 0) return null;

        var produced = _buffer.ToString(0, Math.Min(n, _buffer.Length));
        if (_pendingDeadKey is { } dead)
        {
            _pendingDeadKey = null;
            return Compose(dead, produced);
        }
        return produced;
    }

    private static bool IsDeadKey(in NativeKeyEvent native) =>
        (MapVirtualKeyExW((uint)native.KeyCode, MAPVK_VK_TO_CHAR, native.LayoutHandle) & 0x80000000) != 0;

    /// <summary>Combine une touche morte et un caractère (« ^ » + « e » = « ê »).</summary>
    internal static string Compose(char dead, string next)
    {
        if (next == " ") return dead.ToString();
        // Caractère déjà composé par le système (« ê ») : on le garde tel quel.
        if (next.Length == 1 && next[0] > 0x7F && char.IsLetter(next[0])) return next;
        var combining = dead switch
        {
            '^' => '̂',
            '¨' => '̈',
            '`' => '̀',
            '´' => '́',
            '~' => '̃',
            '¸' => '̧',
            _ => '\0',
        };
        if (combining != '\0' && next.Length == 1)
        {
            var composed = (next + combining).Normalize(NormalizationForm.FormC);
            if (composed.Length == 1) return composed;
        }
        return dead + next;
    }
}
