using System;
using System.Collections.Generic;
using DiceModders.ModCore.Tools;

namespace DiceModders.Tweaks;

/// <summary>
/// The in-match chat (<c>ChatInterface</c>) and event log (<c>LogInterface</c>). Both get every entry through <c>Add(Log.Entry)</c>, which makes a
/// message from <c>messagePrefab</c>, keeps it for <c>displayDuration</c> seconds and destroys the oldest above <c>maximumMessages</c>.
///  * The prefix on <c>Add</c> hands every entry to <see cref="History"/>, including the ones the game removes.
///  * The line count and display time are the game's own two fields, set when an interface appears and when an option changes (the game's values
///    are remembered, 0 gives them back).
/// </summary>
internal static class ChatLog
{
    private sealed class Original { public int Lines; public float Seconds; }
    private static readonly Dictionary<IntPtr, Original> Originals = new();
    private static readonly List<object> Live = new();

    internal static void Install(Patcher patcher)
    {
        patcher.Patch("ChatInterface", "Add", typeof(ChatLog), prefix: nameof(ChatAdd));
        patcher.Patch("LogInterface", "Add", typeof(ChatLog), prefix: nameof(EventAdd));
        patcher.Patch("ChatInterface", "Awake", typeof(ChatLog), prefix: nameof(MatchUiCreated));
        patcher.Patch("ChatInterface", "OnEnable", typeof(ChatLog), postfix: nameof(Enabled));
        patcher.Patch("LogInterface", "OnEnable", typeof(ChatLog), postfix: nameof(Enabled));
        foreach (var e in new[] { Options.ChatLines, Options.ChatSeconds, Options.EventLines, Options.EventSeconds }) e.SettingChanged += (_, _) => ApplyAll();
    }

    internal static void ChatAdd(object[] __args) => History.Record(HistoryKind.Chat, __args);
    internal static void EventAdd(object[] __args) => History.Record(HistoryKind.Event, __args);

    // ChatInterface.Awake runs once per match scene: the start of a new history (or the same one, rebuilt after a host change)
    internal static void MatchUiCreated() { Live.Clear(); History.StartSession(); }

    internal static void Enabled(object __instance)
    {
        if (!Live.Contains(__instance)) Live.Add(__instance);
        Apply(__instance);
    }

    internal static void ApplyAll()
    {
        Live.RemoveAll(o => o is UnityEngine.Object u && u == null);
        foreach (var o in Live) Apply(o);
    }

    private static void Apply(object ui)
    {
        try
        {
            bool chat = ui.GetType().Name.Contains("Chat");
            int lines = Plugin.On ? (chat ? Options.ChatLines : Options.EventLines).Value : 0;
            int seconds = Plugin.On ? (chat ? Options.ChatSeconds : Options.EventSeconds).Value : 0;
            if (!Reflect.TryGetMember(ui, "messagePrefab", out object prefab) || prefab == null) return;
            var key = Mem.PtrOf(ui);
            if (!Originals.TryGetValue(key, out var o))
            {
                Reflect.TryGetMember(ui, "maximumMessages", out object max);
                Reflect.TryGetMember(prefab, "displayDuration", out object dur);
                Originals[key] = o = new Original { Lines = max is int m ? m : 5, Seconds = dur is float d ? d : 10f };
            }
            Reflect.TrySetMember(ui, "maximumMessages", lines > 0 ? lines : o.Lines);
            Reflect.TrySetMember(prefab, "displayDuration", seconds > 0 ? (float)seconds : o.Seconds);   // the prefab: every new message
        }
        catch (Exception e) { Plugin.L.LogWarning("chat / event log options: " + e.Message); }
    }
}
