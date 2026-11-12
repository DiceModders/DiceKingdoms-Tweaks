using BepInEx.Configuration;
using DiceModders.UiKit;
using DiceModders.UiKit.Native;

namespace DiceModders.Tweaks;

/// <summary>The "Interface tweaks" block in Settings > Game. Rows change the config entries and follow them when they change elsewhere.</summary>
internal static class GameSettings
{
    internal static void Install(UiScope ui)
    {
        ui?.Native.Settings.AddBlock(new SettingsBlockSpec
        {
            Id = Plugin.Guid + ".settings",
            Page = SettingsPage.Game,
            Title = "Interface tweaks",
            Order = 10,
            Build = block =>
            {
                Slider(block, "Chat lines on screen", Options.ChatLines, 20);
                Slider(block, "Chat message seconds", Options.ChatSeconds, 120);
                Slider(block, "Event lines on screen", Options.EventLines, 20);
                Slider(block, "Event seconds", Options.EventSeconds, 120);
                block.AddInfo("0 keeps the game's own value.");
                block.AddButton("History", "Open", HistoryWindow.Toggle);
            },
        });
    }

    /// <summary>The game's own slider, whole numbers from 0 (= the game's value) to <paramref name="max"/>.</summary>
    private static void Slider(SettingsBlockHandle block, string label, ConfigEntry<int> entry, int max)
    {
        var row = block.AddSlider(label, 0, max, entry.Value, v => entry.Value = (int)v, "0", true);
        if (row != null) entry.SettingChanged += (_, _) => row.SetValue(entry.Value);
    }
}
