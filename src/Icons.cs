using System;
using System.Collections.Generic;
using DiceModders.UiKit;

namespace DiceModders.Tweaks;

/// <summary>
/// Chooses a top-bar icon among the game's sprites. UiKit.Assets.SpriteNames lists them, but only after the game's UI has loaded, so a button is
/// created with a safe default and upgraded once, a few seconds later, to the first sprite whose name contains one of the wanted words.
/// </summary>
internal sealed class IconUpgrade
{
    private readonly TopBarButton _button;
    private readonly string[] _wanted;
    private bool _done;
    private long _nextTry = Environment.TickCount64 + 4000;

    public IconUpgrade(TopBarButton button, params string[] wanted) { _button = button; _wanted = wanted; }

    /// <summary>Call from the ticker; does nothing after the first success.</summary>
    public void Tick()
    {
        if (_done || Environment.TickCount64 < _nextTry) return;
        _nextTry = Environment.TickCount64 + 5000;
        try
        {
            var names = new List<string>(Assets.SpriteNames());
            if (names.Count == 0) return;                 // sprites not loaded yet - try again
            foreach (var w in _wanted)
            {
                // exact names only: a substring match once picked 'Scroll-Handle' (a dark bar) for the history button
                string hit = names.Find(n => string.Equals(n, w, StringComparison.OrdinalIgnoreCase));
                if (hit == null) continue;
                _button.SetIcon(hit);
                ModPlugin.L.LogInfo($"[icons] '{_button.Id}' uses sprite '{hit}'");
                break;
            }
            _done = true;
        }
        catch (Exception) { /* keep the default icon */ }
    }
}
