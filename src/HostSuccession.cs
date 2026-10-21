using System;
using DiceModders.ModCore;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using DiceModders.UiKit;

namespace DiceModders.Tweaks;

// The class ModCore and its namespace share a name; inside DiceModders.* this alias makes "ModCore" the static class.
using ModCore = DiceModders.ModCore.ModCore;

using UiKit = DiceModders.UiKit.UiKit;

/// <summary>
/// Lets the host choose who becomes the next host.
///
/// How the game picks a new host (MainMenu.UpdateJoiningLobby, local function HostConsensus): the host writes an ordered candidate list into a Steam lobby data
/// key (MainMenu...RefreshPlayers: String.Join("\n", gameState.internalPlayerIds...)). When the host disappears every client reads that list, counts how many
/// members "vote" for each candidate (member data = the host they are connected to) and keeps only candidates whose keep-alive is still fresh. The
/// candidate with the most votes wins; a later candidate replaces an earlier one only if it has MORE votes (strictly), so ties go to the earliest
/// candidate in the list. After the host left every vote still points at the old host, so all remaining candidates tie and the FIRST alive entry of the
/// list wins. Moving the chosen player to the front of that list is therefore all that is needed - no networking code of the game is touched.
///
/// The game rewrites the list when players join or leave, so the order is re-asserted every couple of seconds while a choice is set.
/// The lobby key is found by shape (every line is a Steam id, the host's own id is among them) instead of by name, so a renamed key does not break this.
/// </summary>
internal static class HostSuccession
{
    /// <summary>Steam id of the chosen next host; 0 = automatic (the game decides).</summary>
    internal static ulong Chosen { get; private set; }

    private static readonly Regex IdLine = new(@"^\D*(\d{15,20})\s*$", RegexOptions.Compiled);

    private static string _listKey;
    private static long _nextCheck;
    private static bool _warnedNotOwner;

    private static WindowHandle _window;
    private static DropdownHandle _dropdown;
    private static LabelHandle _status;
    private static readonly List<LobbyMember> Options = new();
    private static int _optionsSignature;

    internal static TopBarButton Button;
    internal static IconUpgrade Upgrade;

    internal static void Init()
    {
        Button = UiKit.TopBar.Add("tweaks.nexthost", "host", Toggle, "Choose the next host", order: 21);   // sprite 'host' exists in the game (uikit-sprites.txt)
        Upgrade = new IconUpgrade(Button, "host");
        ModCore.Lobby.Changed += OnLobbyChanged;
    }

    internal static bool IsOpen => _window != null && _window.IsOpen;

    internal static void Toggle()
    {
        if (!ModCore.Lobby.IsHost) { ModPlugin.L.LogMessage("[host] Only the host can choose the next host."); return; }
        if (IsOpen) { _window.Close(); _window = null; return; }
        Open();
    }

    private static void OnLobbyChanged()
    {
        _listKey = null;                       // a different lobby may use a different key
        _warnedNotOwner = false;
        if (!ModCore.Lobby.InLobby) Chosen = 0;
        RefreshOptions();
    }

    internal static void Tick()
    {
        Button?.SetVisible(ModCore.Lobby.InLobby && ModCore.Lobby.IsHost);
        if (_window != null && !_window.IsOpen) _window = null;

        long now = Environment.TickCount64;
        if (now < _nextCheck) return;
        _nextCheck = now + 2000;
        if (Chosen != 0) Enforce();
        if (IsOpen) RefreshOptions();
    }

    // ---- the actual work -----------------------------------------------------------------------------------------------------

    private static void Choose(ulong steamId)
    {
        Chosen = steamId;
        _nextCheck = 0;
        if (steamId == 0) { SetStatus("Automatic: the game decides."); return; }
        bool ok = Enforce();
        SetStatus(ok ? "Done. If you leave now, this player becomes the host." : "Saved, will be applied as soon as the lobby allows it.");
    }

    /// <summary>Moves the chosen player to the front of the candidate list. Returns true when the list is (now) in the wanted order.</summary>
    private static bool Enforce()
    {
        if (Chosen == 0 || !ModCore.Lobby.IsHost) return false;
        var current = ModCore.Lobby.Current;
        if (current == null) return false;
        var lobby = current.Value;

        try
        {
            _listKey ??= FindListKey(lobby);
            if (_listKey == null) return false;

            string value = lobby.GetData(_listKey);
            if (string.IsNullOrEmpty(value)) return false;

            var lines = new List<string>(value.Split('\n'));
            int at = lines.FindIndex(l => LineId(l) == Chosen);
            if (at < 0)
            {
                ModPlugin.L.LogMessage("[host] The chosen player is no longer in the lobby - back to automatic.");
                Chosen = 0;
                SetStatus("That player left - back to automatic.");
                return false;
            }
            if (at == 0) return true;

            string line = lines[at];
            lines.RemoveAt(at);
            lines.Insert(0, line);
            if (!lobby.SetData(_listKey, string.Join("\n", lines)))
            {
                if (!_warnedNotOwner)
                {
                    _warnedNotOwner = true;
                    ModPlugin.L.LogWarning("[host] Steam refused to change the lobby data: this machine does not own the Steam lobby (only its owner may).");
                }
                return false;
            }
            ModPlugin.L.LogMessage($"[host] Next host set to {Chosen} (candidate list '{_listKey}' reordered).");
            return true;
        }
        catch (Exception e)
        {
            ModPlugin.L.LogWarning("[host] " + e.GetType().Name + ": " + e.Message);
            return false;
        }
    }

    private static ulong LineId(string line)
    {
        var m = IdLine.Match(line ?? "");
        return m.Success && ulong.TryParse(m.Groups[1].Value, out ulong id) ? id : 0;
    }

    /// <summary>The lobby data key whose value is a list of Steam ids (one per line) that contains the local player.</summary>
    private static string FindListKey(Steamworks.Data.Lobby lobby)
    {
        ulong me = ModCore.Lobby.LocalSteamId;
        foreach (var pair in LobbyLog.DataOf(lobby))
        {
            string key = pair.Key, value = pair.Value;
            if (string.IsNullOrEmpty(value)) continue;
            var lines = value.Split('\n');
            bool all = true, hasMe = false;
            foreach (var l in lines)
            {
                ulong id = LineId(l);
                if (id == 0) { all = false; break; }
                if (id == me) hasMe = true;
            }
            if (all && hasMe)
            {
                ModPlugin.L.LogInfo($"[host] candidate list found in lobby data key '{key}' ({lines.Length} entries)");
                return key;
            }
        }
        return null;
    }

    // ---- window ----------------------------------------------------------------------------------------------------------------------

    private static void Open()
    {
        try
        {
            _window = UiKit.Window(w =>
            {
                w.Size(400, 250).Draggable().Closable().Title("Next host");
                w.Body.Paragraph("Choose who becomes the host when you leave the game. The game picks the first connected player of its host list; " +
                                 "this puts your choice at the top of that list.");
                RebuildOptions();
                _dropdown = w.Body.Dropdown(OptionNames(), SelectedIndex(), OnPicked).FixedH(36);
                _status = w.Body.Paragraph(Chosen == 0 ? "Automatic: the game decides." : "A next host is set.");
            });
        }
        catch (Exception e)
        {
            ModPlugin.L.LogWarning("[host] could not open the window: " + e.GetType().Name + ": " + e.Message);
            _window = null;
        }
    }

    private static void OnPicked(int index)
    {
        if (index <= 0 || index > Options.Count) Choose(0);
        else Choose(Options[index - 1].SteamId);
    }

    private static void RebuildOptions()
    {
        Options.Clear();
        foreach (var m in ModCore.Lobby.Members)
            if (!m.IsLocal) Options.Add(m);
        int sig = 17;
        foreach (var m in Options) sig = sig * 31 + m.SteamId.GetHashCode();
        _optionsSignature = sig;
    }

    private static IEnumerable<string> OptionNames()
    {
        yield return "Automatic (the game decides)";
        foreach (var m in Options) yield return m.Name;
    }

    private static int SelectedIndex()
    {
        if (Chosen == 0) return 0;
        int i = Options.FindIndex(m => m.SteamId == Chosen);
        return i < 0 ? 0 : i + 1;
    }

    private static void RefreshOptions()
    {
        if (!IsOpen || _dropdown == null) return;
        int before = _optionsSignature;
        RebuildOptions();
        if (before != _optionsSignature) _dropdown.SetOptions(OptionNames(), SelectedIndex());
    }

    private static void SetStatus(string text) => _status?.SetText(text);
}
