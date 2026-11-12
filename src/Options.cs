using BepInEx.Configuration;

namespace DiceModders.Tweaks;

/// <summary>The mod's local options, in its config file. The Settings rows and in-game config editors change them; the features listen.</summary>
internal static class Options
{
    /// <summary>0 = the game's own value.</summary>
    internal static ConfigEntry<int> ChatLines, ChatSeconds, EventLines, EventSeconds;

    internal static void Bind(ConfigFile config)
    {
        ChatLines = config.Bind("Chat", "Lines", 0, new ConfigDescription(
            "How many chat messages stay on screen (0 = the game's number). The History window keeps all of them.", new AcceptableValueRange<int>(0, 20)));
        ChatSeconds = config.Bind("Chat", "Seconds", 0, new ConfigDescription(
            "How many seconds a chat message stays on screen (0 = the game's time).", new AcceptableValueRange<int>(0, 120)));
        EventLines = config.Bind("EventLog", "Lines", 0, new ConfigDescription(
            "How many events stay on screen (0 = the game's number).", new AcceptableValueRange<int>(0, 20)));
        EventSeconds = config.Bind("EventLog", "Seconds", 0, new ConfigDescription(
            "How many seconds an event stays on screen (0 = the game's time).", new AcceptableValueRange<int>(0, 120)));
    }
}
