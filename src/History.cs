using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using BepInEx;
using DiceModders.ModCore.Tools;

namespace DiceModders.Tweaks;

internal enum HistoryKind { Chat, Event }

/// <summary>
/// Every chat message and event-log entry of the current match (the game shows only the last few). Filled by <see cref="ChatLog"/>; a local copy, so
/// it survives a host change, a reload and a rejoin. <see cref="Changed"/> tells the window when to redraw.
/// </summary>
internal static class History
{
    private sealed class Item { public HistoryKind Kind; public DateTime Time; public float GameTime; public string Text; }

    private const int MaxItems = 5000;
    private static readonly List<Item> Items = new();
    private static readonly HashSet<long> Seen = new();

    internal static event Action Changed;
    internal static int Count => Items.Count;

    /// <summary>A new match: the previous match's lines are dropped (the same match rebuilt after a host change keeps them).</summary>
    internal static void StartSession()
    {
        if (Items.Count > 0 && (DateTime.Now - Items[Items.Count - 1].Time).TotalMinutes < 1) return;
        Items.Clear();
        Seen.Clear();
        Changed?.Invoke();
    }

    internal static void Record(HistoryKind kind, object[] args)
    {
        try
        {
            if (args == null || args.Length == 0 || args[0] == null) return;
            object entry = args[0];
            float time = Reflect.TryGetMember(entry, "creationTime", out object t) && t is float f ? f : 0f;
            string text = "";
            if (Reflect.TryGetMember(entry, "Message", out object message) && message != null)
            {
                try { text = Reflect.Call(message, "Render") as string ?? ""; }
                catch (Exception) { text = "(message could not be rendered)"; }
            }
            if (text.Length == 0) return;
            if (!Seen.Add(KeyOf(kind, time, text))) return;   // the interfaces rebuild their lists from the same entries now and then

            Items.Add(new Item { Kind = kind, Time = DateTime.Now, GameTime = time, Text = text });
            if (Items.Count > MaxItems)
            {
                Items.RemoveRange(0, Items.Count - MaxItems);
                Seen.Clear();
                foreach (var it in Items) Seen.Add(KeyOf(it.Kind, it.GameTime, it.Text));
            }
            Changed?.Invoke();
        }
        catch (Exception e) { Plugin.L.LogWarning("history: " + e.Message); }
    }

    private static long KeyOf(HistoryKind kind, float time, string text) => ((long)(time * 1000f) * 397L) ^ text.GetHashCode() ^ ((long)kind << 40);

    /// <summary>The newest <paramref name="max"/> lines of the wanted kinds, oldest first, as one rich text block.</summary>
    internal static string Render(bool chat, bool events, int max)
    {
        var picked = new List<Item>();
        for (int i = Items.Count - 1; i >= 0 && picked.Count < max; i--)
        {
            var it = Items[i];
            if ((it.Kind == HistoryKind.Chat && chat) || (it.Kind == HistoryKind.Event && events)) picked.Add(it);
        }
        picked.Reverse();
        var sb = new StringBuilder();
        foreach (var it in picked)
        {
            sb.Append("<color=#8a8f98>").Append(it.Time.ToString("HH:mm:ss")).Append("</color>  ");
            if (it.Kind == HistoryKind.Event && chat) sb.Append("<color=#c9a66b>*</color> ");
            sb.Append(it.Text).Append('\n');
        }
        return sb.Length == 0 ? "(nothing yet)" : sb.ToString();
    }

    private static readonly Regex Tags = new("<[^>]+>", RegexOptions.Compiled);

    /// <summary>Writes the whole history to BepInEx\history\history-yyyyMMdd-HHmmss.txt (rich text removed) and returns the path.</summary>
    internal static string Save()
    {
        string dir = Path.Combine(Paths.BepInExRootPath, "history");
        Directory.CreateDirectory(dir);
        string path = Path.Combine(dir, "history-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".txt");
        var lines = new List<string>(Items.Count);
        foreach (var it in Items) lines.Add($"{it.Time:HH:mm:ss} [{(it.Kind == HistoryKind.Chat ? "chat " : "event")}] {Tags.Replace(it.Text, "")}");
        File.WriteAllLines(path, lines, Encoding.UTF8);
        return path;
    }
}
