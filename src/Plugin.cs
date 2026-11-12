using BepInEx;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using DiceModders.ModCore.Tools;

namespace DiceModders.Tweaks;

using Core = DiceModders.ModCore.ModCore;   // inside DiceModders.* the bare name would be the namespace
using Ui = DiceModders.UiKit.UiKit;

/// <summary>
/// DiceKingdoms Tweaks: interface and lobby quality of life.
///  * Anonymous attacks: a lobby rule the host sets (hides who attacks whom).
///  * History: every chat message and event of the match in one window (top bar button).
///  * Next host: the host chooses who takes over when he leaves (top bar button, host only).
///  * Chat and event log: how many lines they show and for how long (Settings > Game > Interface tweaks).
/// Everything reacts to the game's own events; nothing runs every frame.
/// </summary>
[BepInPlugin(Guid, Name, Version)]
[BepInDependency("DiceModders.ModCore")]
[BepInDependency("DiceModders.UiKit")]
[BepInProcess("Dice-Kingdoms.exe")]
public sealed class Plugin : BasePlugin
{
    public const string Guid = "org.dicemodders.dicekingdoms.tweaks";
    public const string Name = "DiceKingdoms Tweaks";
    public const string Version = "4.0.0";

    internal static ManualLogSource L;
    /// <summary>The mod's own switch ([General] Enabled); while off the game behaves as without the mod.</summary>
    internal static bool On;

    public override void Load()
    {
        L = Log;
        Options.Bind(Config);

        var patcher = new Patcher(Guid, Log);
        AnonymousAttacks.Install(patcher);
        AnonymousColours.Install(patcher);
        ChatLog.Install(patcher);
        NextHost.Install(patcher);
        patcher.Summary();

        var ui = Ui.CreateScope(Guid);
        HistoryWindow.Install(ui);
        NextHost.InstallButton(ui);
        GameSettings.Install(ui);

        Core.Mods.Switch(Config, description: "Turns every tweak of this mod on or off.")
            .WhenOn(() => { On = true; Changed(); }, () => { On = false; Changed(); });
    }

    private static void Changed()
    {
        AnonymousAttacks.RefreshAll();
        ChatLog.ApplyAll();
        NextHost.RefreshButton();
        HistoryWindow.RefreshButton();
    }
}
