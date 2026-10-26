using Pointik.Core.Diagnostics;
using Pointik.Core.Graphics;
using Pointik.Core.Settings;
using Pointik.Core.Themes;

namespace Pointik.Core.Tests;

public class ThemeAndSettingsTests
{
    [Fact]
    public void Theme_round_trips_through_json()
    {
        var theme = new KeyboardTheme
        {
            Name = "Mon thème",
            Style = new KeyboardStyle
            {
                FontFamily = "Arial",
                FontSize = 30,
                TextColor = ArgbColor.Parse("#FF112233"),
                BackgroundOpacity = 0.5,
                Presentation = KeyPresentation.Keycaps,
                EnterAnimation = BubbleAnimation.None,
            },
        };

        var json = ThemeSerializer.Export(theme);
        var back = ThemeSerializer.Import(json);

        Assert.Contains("\"format\": \"pointik-theme\"", json);
        Assert.Contains("Mon thème", json); // accents lisibles dans le fichier
        Assert.Equal("Mon thème", back.Name);
        Assert.Equal("Arial", back.Style.FontFamily);
        Assert.Equal(30, back.Style.FontSize);
        Assert.Equal(ArgbColor.Parse("#FF112233"), back.Style.TextColor);
        Assert.Equal(KeyPresentation.Keycaps, back.Style.Presentation);
        Assert.Equal(BubbleAnimation.None, back.Style.EnterAnimation);
    }

    [Fact]
    public void All_builtin_themes_round_trip()
    {
        foreach (var t in BuiltInThemes.All)
        {
            var back = ThemeSerializer.Import(ThemeSerializer.Export(t));
            Assert.Equal(t.Name, back.Name);
            Assert.Equal(t.Style.Presentation, back.Style.Presentation);
            Assert.Equal(t.Style.BackgroundColor, back.Style.BackgroundColor);
        }
    }

    [Theory]
    [InlineData("pas du json")]
    [InlineData("{\"format\":\"autre\",\"version\":1,\"name\":\"x\",\"style\":{}}")]
    [InlineData("{\"format\":\"pointik-theme\",\"version\":1}")]
    [InlineData("{\"format\":\"pointik-theme\",\"version\":99,\"name\":\"x\",\"style\":{}}")]
    public void Invalid_theme_files_are_rejected_with_a_message(string json)
    {
        var ex = Assert.Throws<FormatException>(() => ThemeSerializer.Import(json));
        Assert.False(string.IsNullOrWhiteSpace(ex.Message));
    }

    [Fact]
    public void Imported_values_are_clamped()
    {
        var json = "{\"format\":\"pointik-theme\",\"version\":1,\"name\":\"x\",\"style\":{\"fontSize\":999,\"scalePercent\":-5}}";
        var t = ThemeSerializer.Import(json);
        Assert.Equal(96, t.Style.FontSize);
        Assert.Equal(50, t.Style.ScalePercent);
    }

    [Fact]
    public void Partial_settings_file_keeps_safe_defaults()
    {
        var s = SettingsStore.Deserialize("{ \"ring\": { \"size\": 500 }, \"spot\": { \"enabled\": true } }")!;
        Assert.Equal(RingSettings.MaxSize, s.Ring.Size);
        Assert.True(s.Spot.Enabled);
        Assert.Equal(2, s.Zoom.Level);
        Assert.Equal(3, s.Keyboard.MaxVisible);
        Assert.Equal(ScreenCorner.BottomRight, s.Keyboard.Position.Corner);
    }

    [Fact]
    public void Corrupted_settings_file_falls_back_to_defaults_and_is_kept_aside()
    {
        var dir = Directory.CreateTempSubdirectory("pointik-tests-").FullName;
        try
        {
            File.WriteAllText(Path.Combine(dir, SettingsStore.FileName), "{ ceci n'est pas du json");
            var store = new SettingsStore(dir, Path.Combine(dir, "repli"), new ErrorLog());

            var settings = store.Load();

            Assert.True(store.IsPortable);
            Assert.True(settings.Ring.Enabled);
            Assert.True(File.Exists(Path.Combine(dir, SettingsStore.FileName + ".corrompu")));
            Assert.True(store.Save(settings));
            Assert.NotNull(SettingsStore.Deserialize(File.ReadAllText(store.ActiveFilePath)));

            Assert.Empty(store.DeleteAllTraces());
            Assert.False(File.Exists(store.ActiveFilePath));
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Theory]
    [InlineData("#FFC41F", 255, 0xFF, 0xC4, 0x1F)]
    [InlineData("#80102030", 0x80, 0x10, 0x20, 0x30)]
    [InlineData("abc", 255, 0xAA, 0xBB, 0xCC)]
    public void Colors_parse_hex_forms(string text, int a, int r, int g, int b)
    {
        Assert.True(ArgbColor.TryParse(text, out var c));
        Assert.Equal(new ArgbColor((byte)a, (byte)r, (byte)g, (byte)b), c);
    }
}
