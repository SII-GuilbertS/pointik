using Pointik.Core.Input;
using Pointik.Core.Keys;

namespace Pointik.Core.Tests;

public class KeyNamesTests
{
    private readonly KeyNames _names = new("Win", "Alt");

    [Theory]
    [InlineData(KeyId.Enter, "Entrée")]
    [InlineData(KeyId.NumPadEnter, "Entrée")]
    [InlineData(KeyId.Escape, "Échap")]
    [InlineData(KeyId.Delete, "Suppr")]
    [InlineData(KeyId.Backspace, "Retour")]
    [InlineData(KeyId.Tab, "Tab")]
    [InlineData(KeyId.Space, "Espace")]
    [InlineData(KeyId.Up, "↑")]
    [InlineData(KeyId.Down, "↓")]
    [InlineData(KeyId.Left, "←")]
    [InlineData(KeyId.Right, "→")]
    [InlineData(KeyId.F1, "F1")]
    [InlineData(KeyId.F12, "F12")]
    [InlineData(KeyId.A, "A")]
    [InlineData(KeyId.D7, "7")]
    public void Special_keys_have_french_names(KeyId key, string expected) =>
        Assert.Equal(expected, _names.GetName(key));

    [Fact]
    public void Punctuation_uses_layout_label_in_uppercase() =>
        Assert.Equal("Ù", _names.GetName(KeyId.Character, "ù"));

    [Theory]
    [InlineData(KeyModifiers.Ctrl | KeyModifiers.Shift, KeyId.S, "Ctrl + Maj + S")]
    [InlineData(KeyModifiers.Alt, KeyId.Tab, "Alt + Tab")]
    [InlineData(KeyModifiers.Meta, KeyId.D, "Win + D")]
    [InlineData(KeyModifiers.Ctrl | KeyModifiers.Alt | KeyModifiers.Shift | KeyModifiers.Meta, KeyId.F5, "Win + Ctrl + Alt + Maj + F5")]
    public void Shortcuts_are_formatted_in_windows_order(KeyModifiers mods, KeyId key, string expected) =>
        Assert.Equal(expected, _names.Format(mods, key));

    [Fact]
    public void Meta_key_name_comes_from_the_platform()
    {
        var mac = new KeyNames("⌘", "⌥");
        Assert.Equal("⌘ + ⌥ + Z", mac.Format(KeyModifiers.Meta | KeyModifiers.Alt, KeyId.Z));
    }

    [Theory]
    [InlineData("Ctrl+Alt+Z", KeyModifiers.Ctrl | KeyModifiers.Alt, KeyId.Z)]
    [InlineData("ctrl + shift + F5", KeyModifiers.Ctrl | KeyModifiers.Shift, KeyId.F5)]
    [InlineData("Meta+D", KeyModifiers.Meta, KeyId.D)]
    public void Hotkeys_round_trip_through_text(string text, KeyModifiers mods, KeyId key)
    {
        Assert.True(Hotkey.TryParse(text, out var h));
        Assert.Equal(new Hotkey(mods, key), h);
        Assert.Equal(h, Hotkey.Parse(h.ToString()));
    }

    [Theory]
    [InlineData("Ctrl+Alt")]
    [InlineData("Ctrl+A+B")]
    [InlineData("Ctrl+Inconnue")]
    public void Invalid_hotkeys_are_rejected(string text) => Assert.False(Hotkey.TryParse(text, out _));
}
