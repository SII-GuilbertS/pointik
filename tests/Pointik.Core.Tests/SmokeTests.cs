using Pointik.Core.Input;
using Pointik.Core.Settings;

namespace Pointik.Core.Tests;

public class SmokeTests
{
    [Fact]
    public void Default_settings_round_trip_through_json()
    {
        var json = SettingsStore.Serialize(new AppSettings());
        var back = SettingsStore.Deserialize(json);
        Assert.NotNull(back);
        Assert.Equal(new Hotkey(KeyModifiers.Ctrl | KeyModifiers.Alt, KeyId.Z), back!.Hotkeys.Zoom);
    }
}
