using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using DiceModders.ModCore;
using DiceModders.ModCore.Tools;
using DiceModders.UiKit;
using DiceModders.UiKit.Custom;
using DiceModders.UiKit.Native;

namespace DiceModders.Tweaks;

using Core = DiceModders.ModCore.ModCore;

/// <summary>
/// The host chooses who becomes the next host.
/// How the game picks one (MainMenu.UpdateJoiningLobby, local functions RefreshPlayers / HostConsensus): the host writes an ordered candidate list into
/// a Steam lobby data key (one Steam id per line). When the host disappears, every client keeps the candidates that are still alive and the FIRST of them
/// wins (all votes still point at the old host, so they tie and the earliest entry wins). Moving the chosen player to the front of that list is all that
/// is needed. The game rewrites the list in <c>RefreshPlayers</c>, so a postfix on it puts the choice back right after every rewrite.
/// The key is found by shape (every line a Steam id, the host's own id among them), so a renamed key does not break this.
/// </summary>
internal static class NextHost
{
    private static readonly Regex IdLine = new(@"^\D*(\d{15,20})\s*$", RegexOptions.Compiled);

    internal static ulong Chosen { get; private set; }
    private static string _listKey;
    private static UiScope _ui;
    private static TopBarButtonHandle _button;
    private static UiWindow _window;
    private static UiDropdown _dropdown;
    private static UiText _status;
    private static readonly List<LobbyMember> Candidates = new();

    internal static void Install(Patcher patcher)
    {
        var refresh = typeof(DiceKingdoms.Meta.MainMenu).GetNestedTypes(BindingFlags.Public | BindingFlags.NonPublic)
            .SelectMany(t => t.GetMethods(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
            .FirstOrDefault(m => m.Name.Contains("g__RefreshPlayers"));
        if (refresh != null) patcher.Patch(refresh.DeclaringType, refresh.Name, typeof(NextHost), postfix: nameof(AfterRefresh));
        else Plugin.L.LogWarning("next host: the game's RefreshPlayers was not found; the choice is applied when the lobby changes only");
        Core.Lobby.Changed += OnLobbyChanged;
    }

    internal static void InstallButton(UiScope ui)
    {
        _ui = ui;
        _ui?.WhenReady(s => { _button = s.Native.TopBar.AddIconButton("host", "Choose the next host", Toggle, 21); RefreshButton(); });
    }

    internal static void RefreshButton() => _button?.SetVisible(Plugin.On && Core.Lobby.InLobby && Core.Lobby.IsHost);

    internal static void AfterRefresh() { if (Chosen != 0) Enforce(); }

    private static void OnLobbyChanged()
    {
        _listKey = null;   // another lobby may use another key
        if (!Core.Lobby.InLobby) Chosen = 0;
        RefreshButton();
        if (Chosen != 0) Enforce();
        if (_window != null) FillCandidates();
    }

    private static void Choose(ulong steamId)
    {
        Chosen = steamId;
        if (steamId == 0) { Status("Automatic: the game decides."); return; }
        Status(Enforce() ? "Done. If you leave now, this player becomes the host." : "Saved; applied as soon as the lobby allows it.");
    }

    /// <summary>Moves the chosen player to the front of the candidate list. True when the list is (now) in the wanted order.</summary>
    private static bool Enforce()
    {
        if (!Plugin.On || Chosen == 0 || !Core.Lobby.IsHost || Core.Lobby.Current is not Steamworks.Data.Lobby lobby) return false;
        try
        {
            _listKey ??= FindListKey(lobby);
            if (_listKey == null) return false;
            var lines = new List<string>((lobby.GetData(_listKey) ?? "").Split('\n'));
            int at = lines.FindIndex(l => IdOf(l) == Chosen);
            if (at < 0) { Chosen = 0; Status("That player left - back to automatic."); return false; }
            if (at == 0) return true;
            var line = lines[at];
            lines.RemoveAt(at);
            lines.Insert(0, line);
            return lobby.SetData(_listKey, string.Join("\n", lines));
        }
        catch (Exception e) { Plugin.L.LogWarning("next host: " + e.Message); return false; }
    }

    private static ulong IdOf(string line)
    {
        var m = IdLine.Match(line ?? "");
        return m.Success && ulong.TryParse(m.Groups[1].Value, out ulong id) ? id : 0;
    }

    private static string FindListKey(Steamworks.Data.Lobby lobby)
    {
        ulong me = Core.Lobby.LocalSteamId;
        var e = lobby.Data.GetEnumerator();
        while (((System.Collections.IEnumerator)e).MoveNext())
        {
            var kv = e.Current;
            if (string.IsNullOrEmpty(kv.Value)) continue;
            var ids = kv.Value.Split('\n').Select(IdOf).ToList();
            if (ids.All(id => id != 0) && ids.Contains(me)) return kv.Key;
        }
        return null;
    }

    // ---- window

    private static void Toggle()
    {
        if (_window != null) { _window.Close(); return; }
        _window = _ui?.Custom.Window(w =>
        {
            w.Title("Next host").Size(420f, 260f).Center().Draggable().Closable();
            w.OnClose(() => { _window = null; _dropdown = null; _status = null; });
            w.Body.Paragraph("Choose who becomes the host when you leave. The game picks the first connected player of its host list; this puts your choice at the top.");
            _dropdown = w.Body.Dropdown(new[] { "Automatic (the game decides)" }, 0, i => Choose(i <= 0 || i > Candidates.Count ? 0 : Candidates[i - 1].SteamId));
            _status = w.Body.Paragraph(Chosen == 0 ? "Automatic: the game decides." : "A next host is set.");
        });
        FillCandidates();
    }

    private static void FillCandidates()
    {
        Candidates.Clear();
        Candidates.AddRange(Core.Lobby.Members.Where(m => !m.IsLocal));
        _dropdown?.Options(new[] { "Automatic (the game decides)" }.Concat(Candidates.Select(m => m.Name)));
        int i = Candidates.FindIndex(m => m.SteamId == Chosen);
        _dropdown?.Set(i < 0 ? 0 : i + 1);
    }

    private static void Status(string text) { if (_status != null) _status.Value = text; }
}
