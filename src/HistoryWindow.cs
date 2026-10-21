using System;
using DiceModders.UiKit;
using UnityEngine;

namespace DiceModders.Tweaks;

using UiKit = DiceModders.UiKit.UiKit;

/// <summary>The chat + event history window (button in the top bar, hotkey, or the Settings row).</summary>
internal static class HistoryWindow
{
    private const int MaxLines = 400;     // one TextMeshPro mesh holds roughly 16k characters; 400 short lines stay well below that

    private static WindowHandle _window;
    private static LabelHandle _text;
    private static ScrollHandle _scroll;
    private static ButtonHandle _tabAll, _tabChat, _tabEvents;
    private static LabelHandle _status;
    private static bool _chat = true, _events = true;
    private static int _shownVersion = -1;
    private static long _nextRefresh;
    private static bool _stickToBottom = true;

    internal static TopBarButton Button;
    internal static IconUpgrade Upgrade;

    internal static bool IsOpen => _window != null && _window.IsOpen;

    internal static void Init()
    {
        Button = UiKit.TopBar.Add("tweaks.history", "research", Toggle, "Chat and event history", order: 20);   // sprite 'research' exists in the game (uikit-sprites.txt)
        Upgrade = new IconUpgrade(Button, "research");
    }

    internal static void Toggle()
    {
        if (IsOpen) { _window.Close(); _window = null; return; }
        Open();
    }

    private static void Open()
    {
        try
        {
            _window = UiKit.Window(w =>
            {
                w.Size(560, 480).Position(40, 40).Draggable().Fullscreenable().Closable().Title("History");
                w.TitleBar.Button("Save", SaveToFile);
                w.TitleBar.Button("Latest", () => { _stickToBottom = true; _scroll?.ScrollToBottom(); });

                w.Body.Horizontal(row =>
                {
                    _tabAll = row.Button("All", () => SetFilter(true, true));
                    _tabChat = row.Button("Chat", () => SetFilter(true, false));
                    _tabEvents = row.Button("Events", () => SetFilter(false, true));
                }).FixedH(32);

                _scroll = w.Body.ScrollView(sv => { _text = sv.Paragraph("", 15).Left(); }).WeightH(1);
                _status = w.Body.Label("").Left();
            });
            _shownVersion = -1;
            _stickToBottom = true;
            SetFilter(_chat, _events);
        }
        catch (Exception e)
        {
            ModPlugin.L.LogWarning("[history] could not open the window: " + e.GetType().Name + ": " + e.Message);
            _window = null;
        }
    }

    private static void SetFilter(bool chat, bool events)
    {
        _chat = chat; _events = events;
        _tabAll?.SetSelected(chat && events);
        _tabChat?.SetSelected(chat && !events);
        _tabEvents?.SetSelected(!chat && events);
        _shownVersion = -1;
        Refresh();
    }

    /// <summary>Called by the mod's ticker every frame; updates the text at most twice a second and only when something changed.</summary>
    internal static void Tick()
    {
        if (!IsOpen) { _window = null; return; }
        long now = Environment.TickCount64;
        if (now < _nextRefresh) return;
        _nextRefresh = now + 500;
        Refresh();
    }

    private static void Refresh()
    {
        if (!IsOpen || _text == null) return;
        if (_shownVersion == History.Version) return;
        _shownVersion = History.Version;
        _text.SetText(History.Render(_chat, _events, MaxLines));
        _status?.SetText($"{History.Count} lines kept this match (showing the newest {MaxLines}).");
        if (_stickToBottom) _scroll?.ScrollToBottom();
    }

    private static void SaveToFile()
    {
        try
        {
            string path = History.Save();
            _status?.SetText("Saved: " + path);
            ModPlugin.L.LogMessage("[history] saved to " + path);
        }
        catch (Exception e) { _status?.SetText("Could not save: " + e.Message); }
    }
}
