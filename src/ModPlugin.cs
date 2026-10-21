using System;
using BepInEx;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using DiceModders.Shared;
using DiceModders.UiKit;
using UnityEngine;

namespace DiceModders.Tweaks;

// Inside namespace DiceModders.* the bare name "UiKit" would be the NAMESPACE DiceModders.UiKit; this alias (declared inside
// our namespace, so it wins) makes it mean the static class again.
using UiKit = DiceModders.UiKit.UiKit;

/// <summary>
/// DiceKingdoms Tweaks: interface and lobby quality of life.
///  * Anonymous attacks (host rule in the lobby settings),
///  * the host chooses the next host,
///  * a window with the whole chat and event history,
///  * auto-clear of the chat and the event log.
/// Performance options live in the separate DiceKingdoms Optimization mod.
/// </summary>
[BepInPlugin(Guid, Name, Version)]
[BepInDependency("DiceModders.UiKit")]
[BepInDependency("DiceModders.ModCore")]
[BepInProcess("Dice-Kingdoms.exe")]
public sealed class ModPlugin : BasePlugin
{
    public const string Guid = "org.dicemodders.dicekingdoms.tweaks";
    public const string Name = "DiceKingdoms Tweaks";
    public const string Version = "3.0.0";

    internal static ManualLogSource L;
    internal static Hotkeys Keys;

    public override void Load()
    {
        L = Log;
        Keys = new Hotkeys(Config, L, Name);

        var patcher = new Patcher(Guid, L);
        InterfaceTweaks.Load(Config, Keys, patcher);
        AnonymousAttacks.Load(patcher);
        History.Load(patcher);
        patcher.Summary();

        HistoryWindow.Init();
        HostSuccession.Init();
        LobbyLog.Init();

        Keys.Bind(Config, "History", HotkeyKey.H, "Open / close the chat and event history window", HistoryWindow.Toggle, "History window");
        Keys.Bind(Config, "NextHost", HotkeyKey.N, "Host: choose the next host", HostSuccession.Toggle, "Choose the next host (host only)");
        Keys.Bind(Config, "DumpLobby", HotkeyKey.L, "Write the Steam lobby data and member list to the log", LobbyLog.Dump, "Dump lobby data to the log");

        UiKit.Settings.Add(SettingsTab.Game, "Interface tweaks", s =>
        {
            s.Toggle("Auto-clear text chat", () => InterfaceTweaks.AutoClearChat.On, v => Set(InterfaceTweaks.AutoClearChat, v));
            s.Toggle("Auto-clear event log", () => InterfaceTweaks.AutoClearLog.On, v => Set(InterfaceTweaks.AutoClearLog, v));
            s.Button("History", "Open", HistoryWindow.Toggle);
            s.Info("The History window keeps every chat message and event of the match, also those that auto-clear removes.");
        });

        Keys.LogBindings();
        AddComponent<TweaksTicker>();
    }

    private static void Set(Toggle t, bool wanted)
    {
        if (t.On != wanted) t.Flip();
    }
}

public sealed class TweaksTicker : MonoBehaviour
{
    public TweaksTicker(IntPtr ptr) : base(ptr) { }

    private void Update()
    {
        ModPlugin.Keys?.Poll();
        HistoryWindow.Tick();
        HostSuccession.Tick();
        HistoryWindow.Upgrade?.Tick();
        HostSuccession.Upgrade?.Tick();
    }
}
