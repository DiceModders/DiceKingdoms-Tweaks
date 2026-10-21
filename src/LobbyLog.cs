using System;
using DiceModders.ModCore;
using System.Text;
using DiceModders.UiKit;

namespace DiceModders.Tweaks;

// The class ModCore and its namespace share a name; inside DiceModders.* this alias makes "ModCore" the static class.
using ModCore = DiceModders.ModCore.ModCore;

using UiKit = DiceModders.UiKit.UiKit;

/// <summary>
/// Writes what Steam knows about the current lobby to the log: every lobby data key and value and the member list. Meant for the "I was kicked and
/// cannot rejoin" problem - press the hotkey on the host and on the affected machine and send both logs.
/// A short summary is also logged whenever the lobby changes.
/// </summary>
internal static class LobbyLog
{
    /// <summary>The lobby's data as a list. The interop type implements the generic IEnumerable without a usable foreach, so it is walked through the plain IEnumerator interface.</summary>
    internal static System.Collections.Generic.List<(string Key, string Value)> DataOf(Steamworks.Data.Lobby lobby)
    {
        var list = new System.Collections.Generic.List<(string Key, string Value)>();
        var e = lobby.Data.GetEnumerator();
        while (((System.Collections.IEnumerator)e).MoveNext()) { var kv = e.Current; list.Add((kv.Key, kv.Value)); }
        return list;
    }

    internal static void Init() => ModCore.Lobby.Changed += OnChanged;

    private static void OnChanged()
    {
        try
        {
            var sb = new StringBuilder("[lobby] ");
            sb.Append(ModCore.Lobby.InLobby ? "in lobby" : "not in a lobby");
            sb.Append(ModCore.Lobby.IsHost ? ", HOST" : ", client");
            sb.Append(", members: ");
            foreach (var m in ModCore.Lobby.Members) sb.Append(m.Name).Append(m.IsHost ? "(host) " : " ");
            ModPlugin.L.LogInfo(sb.ToString());
        }
        catch (Exception) { }
    }

    internal static void Dump()
    {
        var current = ModCore.Lobby.Current;
        if (current == null) { ModPlugin.L.LogMessage("[lobby] not in a lobby."); return; }
        try
        {
            var lobby = current.Value;
            ModPlugin.L.LogMessage($"[lobby] id {lobby.Id}, members {lobby.MemberCount}/{lobby.MaxMembers}, owner {lobby.Owner.Id}, I am {ModCore.Lobby.LocalSteamId}");
            foreach (var pair in DataOf(lobby))
            {
                string v = pair.Value ?? "";
                ModPlugin.L.LogMessage($"[lobby]   {pair.Key} = {v.Replace("\n", " | ")}");
            }
            foreach (var m in ModCore.Lobby.Members)
                ModPlugin.L.LogMessage($"[lobby]   member {m.SteamId} {m.Name}{(m.IsHost ? " (host)" : "")}{(m.IsLocal ? " (me)" : "")}{(m.IsConnected ? "" : " DISCONNECTED")}");
        }
        catch (Exception e) { ModPlugin.L.LogWarning("[lobby] dump failed: " + e.GetType().Name + ": " + e.Message); }
    }
}
