using System;
using System.Collections.Generic;
using DiceModders.ModCore.Tools;
using UnityEngine;

namespace DiceModders.Tweaks;

using GamePalette = DiceKingdoms.Interface.ColoredPaperOverrides;
using GamePlayer = DiceKingdoms.Game.Player;
using GamePlayerColors = DiceKingdoms.Game.PlayerColors;

/// <summary>
/// Part of "Anonymous attacks": while the rule is on, every OTHER player's buildings, units, ships and flags are drawn in one grey colour, so
/// attackers cannot be told apart by colour; your own keep your colour, so you still see your army.
/// The game colours a model by giving its <c>ColoredPaperOverrides</c> the player's palette texture (<c>PlayerColor.CustomColorPalette</c>) through
/// <c>SetColorPalette</c> / <c>SlowSetColorPalette</c>. A prefix swaps any player palette that is not yours for a grey copy of yours (same
/// shading, no colour). Only drawing changes, on this computer.
/// </summary>
internal static class AnonymousColours
{
    private static readonly HashSet<IntPtr> PlayerPalettes = new();
    private static Texture2D _grey;
    private static IntPtr _greyOf;

    internal static void Install(Patcher patcher)
    {
        patcher.Patch(typeof(GamePalette), nameof(GamePalette.SetColorPalette), typeof(AnonymousColours), prefix: nameof(PalettePrefix));
        patcher.Patch(typeof(GamePalette), nameof(GamePalette.SlowSetColorPalette), typeof(AnonymousColours), prefix: nameof(PalettePrefix));
    }

    internal static void PalettePrefix(ref Texture __0)
    {
        if (!AnonymousAttacks.Active || __0 == null) return;
        try
        {
            var mine = GamePlayer.LocalPlayer?.Color?.CustomColorPalette;
            if (mine == null || __0.Pointer == mine.Pointer || !IsPlayerPalette(__0)) return;
            __0 = Grey(mine);
        }
        catch (Exception e) { Plugin.L.LogWarning("anonymous colours: " + e.Message); }
    }

    /// <summary>True for the palette texture of one of the player colours (other palettes, e.g. of nature, are left alone).</summary>
    private static bool IsPlayerPalette(Texture t)
    {
        if (PlayerPalettes.Count == 0)
            foreach (var colors in Resources.FindObjectsOfTypeAll<GamePlayerColors>())
                foreach (var c in colors.Colors)
                    if (c?.CustomColorPalette != null) PlayerPalettes.Add(c.CustomColorPalette.Pointer);
        return PlayerPalettes.Contains(t.Pointer);
    }

    /// <summary>A grey copy of a palette (luminance of every pixel), made once through the GPU since palette textures are not readable.</summary>
    private static Texture2D Grey(Texture2D source)
    {
        if (_grey != null && _greyOf == source.Pointer) return _grey;
        var rt = RenderTexture.GetTemporary(source.width, source.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
        var previous = RenderTexture.active;
        Graphics.Blit(source, rt);
        RenderTexture.active = rt;
        var copy = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false) { filterMode = source.filterMode, wrapMode = source.wrapMode };
        copy.ReadPixels(new Rect(0, 0, source.width, source.height), 0, 0);
        RenderTexture.active = previous;
        RenderTexture.ReleaseTemporary(rt);
        var px = copy.GetPixels32();
        for (int i = 0; i < px.Length; i++)
        {
            byte l = (byte)Math.Clamp((int)(0.3f * px[i].r + 0.59f * px[i].g + 0.11f * px[i].b), 0, 255);
            px[i] = new Color32(l, l, l, px[i].a);
        }
        copy.SetPixels32(px);
        copy.Apply(false, true);
        if (_grey != null) UnityEngine.Object.Destroy(_grey);
        _grey = copy;
        _greyOf = source.Pointer;
        return _grey;
    }
}
