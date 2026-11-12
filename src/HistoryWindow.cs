using System;
using DiceModders.UiKit;
using DiceModders.UiKit.Custom;
using DiceModders.UiKit.Native;

namespace DiceModders.Tweaks;

/// <summary>The chat + event history window, opened from the top bar (or the Settings row). It redraws when <see cref="History.Changed"/> fires.</summary>
internal static class HistoryWindow
{
    private const int MaxLines = 400;   // one TextMeshPro mesh holds about 16k characters; 400 short lines stay below that

    private static UiScope _ui;
    private static TopBarButtonHandle _button;
    private static UiWindow _window;
    private static UiText _text, _status;
    private static UiScroll _scroll;
    private static UiButton _all, _chat, _events;
    private static bool _showChat = true, _showEvents = true;

    internal static void Install(UiScope ui)
    {
        _ui = ui;
        _ui?.WhenReady(s => _button = s.Native.TopBar.AddIconButton("research", "Chat and event history", Toggle, 20));
        History.Changed += Redraw;
    }

    internal static void RefreshButton() => _button?.SetVisible(Plugin.On);

    internal static void Toggle()
    {
        if (_window != null) { _window.Close(); return; }
        _window = _ui?.Custom.Window(w =>
        {
            w.Title("History").Size(560f, 480f).Position(40f, 40f).Draggable().Closable().Fullscreenable().FullscreenButton();
            w.OnClose(() => { _window = null; _text = _status = null; _scroll = null; });
            w.Body.Row(row =>
            {
                _all = row.Button("All", () => Filter(true, true))?.FillWidth() as UiButton;
                _chat = row.Button("Chat", () => Filter(true, false))?.FillWidth() as UiButton;
                _events = row.Button("Events", () => Filter(false, true))?.FillWidth() as UiButton;
                row.Button("Save", Save)?.FillWidth();
            })?.Height(36f);
            _scroll = w.Body.Scroll(s => _text = s.Paragraph(""))?.FillWidth().FillHeight() as UiScroll;
            _status = w.Body.Text("");
        });
        Filter(_showChat, _showEvents);
    }

    private static void Filter(bool chat, bool events)
    {
        _showChat = chat; _showEvents = events;
        _all?.Selected(chat && events); _chat?.Selected(chat && !events); _events?.Selected(!chat && events);
        Redraw();
    }

    private static void Redraw()
    {
        if (_window == null || _text == null) return;
        _text.Value = History.Render(_showChat, _showEvents, MaxLines);
        if (_status != null) _status.Value = $"{History.Count} lines kept this match (showing the newest {MaxLines}).";
        _scroll?.ToBottom();
    }

    private static void Save()
    {
        try { var path = History.Save(); if (_status != null) _status.Value = "Saved: " + path; }
        catch (Exception e) { if (_status != null) _status.Value = "Could not save: " + e.Message; }
    }
}
