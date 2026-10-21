using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using BepInEx;
using DiceModders.Shared;

namespace DiceModders.Tweaks;

internal enum HistoryKind { Chat, Event }

internal sealed class HistoryItem
{
    public HistoryKind Kind;
    public DateTime Time;     // local clock when this machine received the entry
    public float GameTime;    // Log.Entry.creationTime (the host's unscaled time)
    public string Text;       // rendered, may contain rich-text tags
}

/// <summary>
/// Keeps every chat message and event-log entry of the current match. The game shows only the last few of each (ChatInterface/LogInterface
/// keep <c>maximumMessages</c> UI objects and destroy the oldest), but both interfaces receive each entry through <c>Add(Log.Entry)</c>, so
/// a prefix on those two methods sees all of them - including the ones auto-clear removes.
/// The game also has the full lists (GameState.chat / GameState.log -> Log.Entries), but those are networked lists whose content and order
/// belong to the host; a local copy survives a host change, a reload and a rejoin.
/// </summary>
internal static class History
{
    private const int MaxItems = 5000;
    private static readonly List<HistoryItem> Items = new();
    private static readonly HashSet<long> Seen = new();

    /// <summary>Raised on every added item - the window polls <see cref="Version"/> instead.</summary>
    internal static int Version { get; private set; }

    internal static void Load(Patcher patcher)
    {
        patcher.Patch("ChatInterface", "Add", typeof(History), prefix: nameof(ChatAddPrefix));
        patcher.Patch("LogInterface", "Add", typeof(History), prefix: nameof(LogAddPrefix));
    }

    /// <summary>A new match (scene) started - the previous match's lines are dropped.</summary>
    internal static void StartSession()
    {
        // Same match being rebuilt after a host change keeps its own history: only clear when the old one is stale (> 1 minute without news).
        if (Items.Count > 0 && (DateTime.Now - Items[Items.Count - 1].Time).TotalMinutes < 1) return;
        Items.Clear();
        Seen.Clear();
        Version++;
    }

    internal static void ChatAddPrefix(object[] __args) => Record(HistoryKind.Chat, __args);
    internal static void LogAddPrefix(object[] __args) => Record(HistoryKind.Event, __args);

    private static void Record(HistoryKind kind, object[] args)
    {
        try
        {
            if (args == null || args.Length == 0 || args[0] == null) return;
            object entry = args[0];

            float time = 0f;
            if (Reflect.TryGetMember(entry, "creationTime", out object t) && t is float f) time = f;

            string text = "";
            if (Reflect.TryGetMember(entry, "Message", out object message) && message != null)
            {
                try { text = Reflect.Call(message, "Render") as string ?? ""; }
                catch (Exception) { text = "(message could not be rendered)"; }
            }
            if (text.Length == 0) return;

            // The interfaces rebuild their lists from the same entries now and then; ignore what we have already stored.
            long key = ((long)(time * 1000f) * 397L) ^ text.GetHashCode() ^ ((long)kind << 40);
            if (!Seen.Add(key)) return;

            Items.Add(new HistoryItem { Kind = kind, Time = DateTime.Now, GameTime = time, Text = text });
            if (Items.Count > MaxItems)
            {
                Items.RemoveRange(0, Items.Count - MaxItems);
                Seen.Clear();                       // dedupe keys of dropped lines are no longer needed; recent ones are re-learned on the next rebuild
                foreach (var it in Items) Seen.Add(((long)(it.GameTime * 1000f) * 397L) ^ it.Text.GetHashCode() ^ ((long)it.Kind << 40));
            }
            Version++;
        }
        catch (Exception e) { ModPlugin.L.LogWarning("[history] " + e.GetType().Name + ": " + e.Message); }
    }

    /// <summary>The newest <paramref name="max"/> lines of the wanted kinds, oldest first, as one rich text block.</summary>
    internal static string Render(bool chat, bool events, int max)
    {
        var picked = new List<HistoryItem>();
        for (int i = Items.Count - 1; i >= 0 && picked.Count < max; i--)
        {
            var it = Items[i];
            if ((it.Kind == HistoryKind.Chat && chat) || (it.Kind == HistoryKind.Event && events)) picked.Add(it);
        }
        picked.Reverse();

        var sb = new StringBuilder();
        foreach (var it in picked)
        {
            string stamp = it.Time.ToString("HH:mm:ss");
            // dim clock; events are marked so a mixed list stays readable
            sb.Append("<color=#8a8f98>").Append(stamp).Append("</color>  ");
            if (it.Kind == HistoryKind.Event && chat) sb.Append("<color=#c9a66b>*</color> ");
            sb.Append(it.Text).Append('\n');
        }
        return sb.Length == 0 ? "(nothing yet)" : sb.ToString();
    }

    internal static int Count => Items.Count;

    private static readonly Regex Tags = new("<[^>]+>", RegexOptions.Compiled);

    /// <summary>Writes the whole history to BepInEx\history\history-yyyyMMdd-HHmmss.txt (rich text removed) and returns the path.</summary>
    internal static string Save()
    {
        string dir = Path.Combine(Paths.BepInExRootPath, "history");
        Directory.CreateDirectory(dir);
        string path = Path.Combine(dir, "history-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".txt");
        var lines = new List<string>(Items.Count);
        foreach (var it in Items)
            lines.Add($"{it.Time:HH:mm:ss} [{(it.Kind == HistoryKind.Chat ? "chat " : "event")}] {Tags.Replace(it.Text, "")}");
        File.WriteAllLines(path, lines, Encoding.UTF8);
        return path;
    }
}
