using BepInEx.Configuration;
using DiceModders.UiKit;
using DiceModders.UiKit.Native;

namespace DiceModders.Tweaks;

/// <summary>The "Interface tweaks" block in Settings > Game. Rows change the config entries and follow them when they change elsewhere.</summary>
internal static class GameSettings
{
    internal static void Install(UiScope ui)
    {
        Tx.Block(ui, () => new SettingsBlockSpec
        {
            Id = Plugin.Guid + ".settings",
            Page = SettingsPage.Game,
            Title = Tx.T("settings.title"),
            Order = 10,
            Build = block =>
            {
                Slider(block, Tx.T("settings.chatLines"), Options.ChatLines, 20);
                Slider(block, Tx.T("settings.chatSeconds"), Options.ChatSeconds, 120);
                Slider(block, Tx.T("settings.eventLines"), Options.EventLines, 20);
                Slider(block, Tx.T("settings.eventSeconds"), Options.EventSeconds, 120);
                block.AddInfo(Tx.T("settings.zeroKeeps"));
                block.AddButton(Tx.T("history.title"), Tx.T("common.open"), HistoryWindow.Toggle);
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
