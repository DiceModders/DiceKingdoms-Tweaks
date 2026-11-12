using System;
using DiceModders.ModCore;
using DiceModders.ModCore.Tools;
using Il2CppInterop.Runtime;
using UnityEngine;

namespace DiceModders.Tweaks;

using Core = DiceModders.ModCore.ModCore;

/// <summary>
/// "Anonymous attacks": a lobby rule the HOST sets. While it is on, nobody sees who attacks whom:
///  * the attacker flags next to the player names (<c>PlayerSummary.attackerPrefab</c>, the holder of the pooled flag images) are made invisible,
///  * the attack summaries at the top right of the scoreboard (<c>Scoreboard.OnCombatStart</c> makes one per attacker) are not created.
/// It only changes what is drawn, never the simulation, so players without the mod simply keep seeing the flags.
/// The holder is hidden with a CanvasGroup (alpha 0) instead of SetActive(false): <c>PlayerSummary.Update</c> compares the holder's activeSelf with
/// the state it wants every frame and would re-activate it.
/// </summary>
internal static class AnonymousAttacks
{
    private static LobbyRule<bool> _rule;
    private static Type _summaryType;

    internal static bool Active => Plugin.On && _rule != null && _rule.Value;

    internal static void Install(Patcher patcher)
    {
        _rule = Core.Lobby.Rules.Toggle("tweaks.anonymousAttacks", "Anonymous attacks", false,
            "Hides who is attacking: no attacker flags next to the player names, no attack summaries at the top right of the scoreboard, and every other player's models in one grey colour.",
            affectsSimulation: false);
        _rule.Changed += _ => RefreshAll();

        patcher.Patch("PlayerSummary", "OnEnable", typeof(AnonymousAttacks), postfix: nameof(SummaryPostfix));
        patcher.Patch("PlayerSummary", "OnSyncPhase", typeof(AnonymousAttacks), postfix: nameof(SummaryPostfix));
        patcher.Patch("Scoreboard", "OnCombatStart", typeof(AnonymousAttacks), prefix: nameof(CombatStartPrefix));
    }

    // Scoreboard.OnCombatStart(CombatGrid): skipped while the rule is on, so no summary is created.
    internal static bool CombatStartPrefix() => !Active;

    internal static void SummaryPostfix(object __instance)
    {
        try { Apply(__instance); }
        catch (Exception e) { Plugin.L.LogWarning("anonymous attacks: " + e.Message); }
    }

    private static void Apply(object summary)
    {
        if (!Reflect.TryGetMember(summary, "attackerPrefab", out object holder) || holder is not Component component || component.gameObject == null) return;
        var go = component.gameObject;
        var group = go.GetComponent<CanvasGroup>();
        if (group == null)
        {
            if (!Active) return;   // nothing to undo
            group = go.AddComponent<CanvasGroup>();
        }
        group.alpha = Active ? 0f : 1f;
        group.blocksRaycasts = !Active;
    }

    /// <summary>The rule or the mod switch changed: re-apply to every scoreboard entry that exists.</summary>
    internal static void RefreshAll()
    {
        try
        {
            _summaryType ??= GameTypes.Find("PlayerSummary");
            if (_summaryType == null) return;
            foreach (var o in Resources.FindObjectsOfTypeAll(Il2CppType.From(_summaryType)))
                Apply(Reflect.Wrap(_summaryType, Mem.PtrOf(o)));
        }
        catch (Exception e) { Plugin.L.LogWarning("anonymous attacks: refresh failed: " + e.Message); }
    }
}
