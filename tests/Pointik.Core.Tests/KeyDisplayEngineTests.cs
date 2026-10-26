using Pointik.Core.Input;
using Pointik.Core.Keys;
using Pointik.Core.Settings;

namespace Pointik.Core.Tests;

public class KeyDisplayEngineTests
{
    private readonly KeyboardSettings _settings = new();
    private readonly KeyDisplayEngine _engine;

    public KeyDisplayEngineTests() => _engine = new KeyDisplayEngine(() => _settings, new KeyNames());

    private static TimeSpan Ms(double ms) => TimeSpan.FromMilliseconds(ms);

    private static KeyboardInputEvent Down(KeyId key, KeyModifiers mods = KeyModifiers.None, string? text = null, bool altGr = false) =>
        new(key, true, mods, altGr, null, text);

    [Fact]
    public void Repeated_shortcut_is_merged_with_a_counter()
    {
        for (var i = 0; i < 3; i++)
            _engine.Process(Down(KeyId.Z, KeyModifiers.Ctrl), Ms(100 * i));

        var bubble = Assert.Single(_engine.Bubbles);
        Assert.Equal(3, bubble.Count);
        Assert.Equal("Ctrl + Z ×3", bubble.DisplayText);
    }

    [Fact]
    public void Different_shortcut_breaks_the_merge()
    {
        _engine.Process(Down(KeyId.Z, KeyModifiers.Ctrl), Ms(0));
        _engine.Process(Down(KeyId.Y, KeyModifiers.Ctrl), Ms(50));
        _engine.Process(Down(KeyId.Z, KeyModifiers.Ctrl), Ms(100));

        Assert.Equal(["Ctrl + Z", "Ctrl + Y", "Ctrl + Z"], _engine.Bubbles.Select(b => b.DisplayText));
    }

    [Fact]
    public void Repeat_after_the_bubble_has_vanished_starts_a_new_bubble()
    {
        _engine.Process(Down(KeyId.Z, KeyModifiers.Ctrl), Ms(0));
        var later = TimeSpan.FromSeconds(_settings.DisplaySeconds) + KeyDisplayEngine.ExitDuration + Ms(10);
        _engine.Update(later);
        _engine.Process(Down(KeyId.Z, KeyModifiers.Ctrl), later);

        Assert.Equal(1, Assert.Single(_engine.Bubbles).Count);
    }

    [Fact]
    public void Shortcuts_mode_ignores_plain_typing_but_shows_special_keys()
    {
        _settings.Mode = KeyboardDisplayMode.ShortcutsAndSpecialKeys;
        Assert.False(_engine.Process(Down(KeyId.A, text: "a"), Ms(0)));
        Assert.False(_engine.Process(Down(KeyId.A, KeyModifiers.Shift, "A"), Ms(10)));
        Assert.True(_engine.Process(Down(KeyId.Enter), Ms(20)));
        Assert.True(_engine.Process(Down(KeyId.Tab, KeyModifiers.Shift), Ms(30)));

        Assert.Equal(["Entrée", "Maj + Tab"], _engine.Bubbles.Select(b => b.Text));
    }

    [Fact]
    public void Everything_mode_groups_typed_characters_on_one_line()
    {
        _settings.Mode = KeyboardDisplayMode.Everything;
        _engine.Process(Down(KeyId.B, KeyModifiers.Shift, "B"), Ms(0));
        _engine.Process(Down(KeyId.O, text: "o"), Ms(50));
        _engine.Process(Down(KeyId.Space, text: " "), Ms(100));
        _engine.Process(Down(KeyId.D2, text: "é"), Ms(150));

        var line = Assert.Single(_engine.Bubbles);
        Assert.True(line.IsTextLine);
        Assert.Equal("Bo é", line.Text);
    }

    [Fact]
    public void Text_line_is_cleared_after_the_pause()
    {
        _settings.Mode = KeyboardDisplayMode.Everything;
        _settings.TextPauseSeconds = 2;
        _engine.Process(Down(KeyId.A, text: "a"), Ms(0));
        _engine.Process(Down(KeyId.B, text: "b"), Ms(2100));

        // La première ligne a expiré : « b » démarre une nouvelle ligne.
        Assert.Equal(["a", "b"], _engine.Bubbles.Select(b => b.Text));
        _engine.Update(Ms(2100) + TimeSpan.FromSeconds(2) + KeyDisplayEngine.ExitDuration);
        Assert.Empty(_engine.Bubbles);
    }

    [Fact]
    public void Shortcut_inside_typing_gets_its_own_bubble()
    {
        _settings.Mode = KeyboardDisplayMode.Everything;
        _engine.Process(Down(KeyId.A, text: "a"), Ms(0));
        _engine.Process(Down(KeyId.C, KeyModifiers.Ctrl), Ms(50));
        _engine.Process(Down(KeyId.B, text: "b"), Ms(100));

        Assert.Equal(["a", "Ctrl + C", "b"], _engine.Bubbles.Select(b => b.Text));
    }

    [Fact]
    public void AltGr_characters_are_text_not_shortcuts()
    {
        _settings.Mode = KeyboardDisplayMode.ShortcutsAndSpecialKeys;
        var at = Down(KeyId.D0, KeyModifiers.Ctrl | KeyModifiers.Alt, "@", altGr: true);
        Assert.False(_engine.Process(at, Ms(0)));

        _settings.Mode = KeyboardDisplayMode.Everything;
        Assert.True(_engine.Process(at, Ms(10)));
        Assert.Equal("@", Assert.Single(_engine.Bubbles).Text);
    }

    [Fact]
    public void Modifiers_alone_and_key_releases_are_ignored()
    {
        Assert.False(_engine.Process(Down(KeyId.LeftCtrl, KeyModifiers.Ctrl), Ms(0)));
        Assert.False(_engine.Process(new KeyboardInputEvent(KeyId.Enter, false, KeyModifiers.None), Ms(10)));
        Assert.Empty(_engine.Bubbles);
    }

    [Fact]
    public void Number_of_visible_bubbles_is_limited()
    {
        _settings.MaxVisible = 3;
        var keys = new[] { KeyId.F1, KeyId.F2, KeyId.F3, KeyId.F4, KeyId.F5 };
        for (var i = 0; i < keys.Length; i++) _engine.Process(Down(keys[i]), Ms(i * 10));

        Assert.Equal(["F3", "F4", "F5"], _engine.Bubbles.Select(b => b.Text));
    }

    [Fact]
    public void Clicks_and_wheel_are_shown_with_modifiers_and_merged()
    {
        _settings.MaxVisible = 4;
        _engine.ProcessClick(MouseButton.Left, KeyModifiers.Ctrl, Ms(0));
        _engine.ProcessClick(MouseButton.Left, KeyModifiers.None, Ms(100));
        _engine.ProcessClick(MouseButton.Left, KeyModifiers.None, Ms(250)); // double-clic
        for (var i = 0; i < 4; i++) _engine.ProcessWheel(-120, KeyModifiers.None, Ms(300 + i * 30));
        _engine.ProcessClick(MouseButton.Right, KeyModifiers.Shift, Ms(500));

        Assert.Equal(["Ctrl + Clic gauche", "Clic gauche ×2", "Molette ↓ ×4", "Maj + Clic droit"],
            _engine.Bubbles.Select(b => b.DisplayText));
        Assert.Equal(["Ctrl", "Clic gauche"], _engine.Bubbles[0].Parts);
    }

    [Fact]
    public void Clear_removes_everything_immediately()
    {
        _engine.Process(Down(KeyId.C, KeyModifiers.Ctrl), Ms(0));
        _engine.Clear();
        Assert.Empty(_engine.Bubbles);
    }
}
