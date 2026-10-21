using BepInEx.Configuration;
using DiceModders.Shared;

namespace DiceModders.Tweaks;

/// <summary>Declutters the in-match UI: auto-clear of the text chat and the event log (the History window keeps everything).</summary>
internal static class InterfaceTweaks
{
    internal static Toggle AutoClearChat;
    internal static Toggle AutoClearLog;
    internal static ConfigEntry<int> ClearIntervalMs;

    internal static void Load(ConfigFile config, Hotkeys keys, Patcher patcher)
    {
        var L = ModPlugin.L;

        ClearIntervalMs = config.Bind("General", "ClearIntervalMs", 500,
            new ConfigDescription("How often auto-clear runs, in milliseconds.", new AcceptableValueRange<int>(100, 10000)));

        AutoClearChat = new Toggle("Auto-clear text chat",
            config.Bind("Startup", "AutoClearChat", false, "Start with auto-clear of the text chat enabled.").Value, L);
        AutoClearLog = new Toggle("Auto-clear event log",
            config.Bind("Startup", "AutoClearEventLog", false, "Start with auto-clear of the event log enabled.").Value, L);

        keys.Bind(config, "AutoClearChat", HotkeyKey.F5, "Toggle: auto-clear text chat", AutoClearChat.Flip, "Auto-clear text chat", () => AutoClearChat.On);
        keys.Bind(config, "AutoClearEventLog", HotkeyKey.F6, "Toggle: auto-clear event log", AutoClearLog.Flip, "Auto-clear event log", () => AutoClearLog.On);

        patcher.Patch("ChatInterface", "Awake", typeof(InterfacePatches), prefix: nameof(InterfacePatches.ChatAwakePrefix));
        patcher.Patch("ChatInterface", "Update", typeof(InterfacePatches), postfix: nameof(InterfacePatches.ChatUpdatePostfix));
        patcher.Patch("LogInterface", "OnEnable", typeof(InterfacePatches), postfix: nameof(InterfacePatches.LogEnablePostfix));
        patcher.Patch("LogInterface", "OnDisable", typeof(InterfacePatches), postfix: nameof(InterfacePatches.LogDisablePostfix));
    }
}
