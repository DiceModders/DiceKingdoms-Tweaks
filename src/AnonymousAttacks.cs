using System;
using DiceModders.ModCore;
using DiceModders.Shared;
using DiceModders.UiKit;
using Il2CppInterop.Runtime;
using UnityEngine;

namespace DiceModders.Tweaks;

// The class ModCore and its namespace share a name; inside DiceModders.* this alias makes "ModCore" the static class.
using ModCore = DiceModders.ModCore.ModCore;

// Inside namespace DiceModders.* the bare name "UiKit" would be the NAMESPACE DiceModders.UiKit; this alias makes it the static class again.
using UiKit = DiceModders.UiKit.UiKit;

/// <summary>
/// "Anonymous attacks": a rule the HOST sets in the lobby settings. While it is on, nobody sees who attacks whom:
///  * the attacker flags next to the player names (PlayerSummary.attackerPrefab, the holder of the pooled flag images) are made invisible,
///  * the attack summaries at the top right of the scoreboard (Scoreboard.OnCombatStart creates one ScoreboardCombatSummary per attacker) are not created.
///
/// The rule travels through UiKit's lobby rules (Steam lobby data), so every player who has this mod follows the host; players without the mod
/// simply keep seeing the flags (the rule only changes what is DRAWN, never the simulation, so it cannot desync anybody).
///
/// The holder is hidden with a CanvasGroup (alpha 0) instead of SetActive(false): PlayerSummary.Update compares the holder's activeSelf with the state it
/// wants every frame and would re-activate it and re-create all flag images 60 times a second.
/// </summary>
internal static class AnonymousAttacks
{
    private static LobbyRule<bool> _rule;
    private static Type _summaryType;

    internal static long SummariesSkipped;
    internal static long FlagHoldersHidden;

    internal static bool Active => _rule != null && _rule.Value;

    internal static void Load(Patcher patcher)
    {
        _rule = ModCore.Lobby.Rules.Toggle(
            "tweaks.anonymousAttacks", "Anonymous attacks", false,
            "Hides who is attacking: no attacker flags next to the player names and no attack summaries at the top right of the scoreboard.",
            affectsSimulation: false);
        _rule.Changed += _ => RefreshAll();

        patcher.Patch("PlayerSummary", "OnEnable", typeof(AnonymousAttacks), postfix: nameof(SummaryPostfix));
        patcher.Patch("PlayerSummary", "OnSyncPhase", typeof(AnonymousAttacks), postfix: nameof(SummaryPostfix));
        patcher.Patch("Scoreboard", "OnCombatStart", typeof(AnonymousAttacks), prefix: nameof(CombatStartPrefix));
    }

    // Scoreboard.OnCombatStart(CombatGrid): skipped while the rule is on, so no summary is created.
    internal static bool CombatStartPrefix()
    {
        if (!Active) return true;
        SummariesSkipped++;
        return false;
    }

    internal static void SummaryPostfix(object __instance)
    {
        try { Apply(__instance); }
        catch (Exception e) { ModPlugin.L.LogWarning("[anonymous] " + e.GetType().Name + ": " + e.Message); }
    }

    private static void Apply(object summary)
    {
        if (!Reflect.TryGetMember(summary, "attackerPrefab", out object holder) || holder is not Component component) return;
        var go = component.gameObject;
        if (go == null) return;

        var group = go.GetComponent<CanvasGroup>();
        if (group == null)
        {
            if (!Active) return;              // nothing to undo
            group = go.AddComponent<CanvasGroup>();
        }
        float alpha = Active ? 0f : 1f;
        if (Mathf.Approximately(group.alpha, alpha)) return;
        group.alpha = alpha;
        group.blocksRaycasts = !Active;
        if (Active) FlagHoldersHidden++;
    }

    /// <summary>The rule changed (host toggled it, or a lobby was entered): re-apply to every scoreboard entry that already exists.</summary>
    private static void RefreshAll()
    {
        try
        {
            _summaryType ??= GameTypes.Find("PlayerSummary");
            if (_summaryType == null) return;
            var all = Resources.FindObjectsOfTypeAll(Il2CppType.From(_summaryType));
            if (all == null) return;
            for (int i = 0; i < all.Length; i++)
                Apply(Reflect.Wrap(_summaryType, Mem.PtrOf(all[i])));
        }
        catch (Exception e) { ModPlugin.L.LogWarning("[anonymous] refresh failed: " + e.GetType().Name + ": " + e.Message); }
    }
}
