using System;
using BepInEx.Configuration;
using DiceModders.ModCore.Tools;

namespace DiceModders.Tweaks;

using Core = DiceModders.ModCore.ModCore;
using BuildInterface = DiceKingdoms.Interface.BuildInterface;

/// <summary>
/// Two build-phase keys (ModCore hotkeys, changeable in the hotkey settings):
///  * Pickaxe (default Q): the game's pickaxe tool, exactly as its button (<c>BuildInterface.&lt;Awake&gt;b__33_0</c>: clears the building placers and turns the
///    CleanTileTool on). Leaving it works as in the game (right click / Esc).
///  * Burn (default X): burns your own building under the mouse with the game's own burn action (<c>BuildData.BurnBuilding</c>, as the burn button
///    <c>b__33_3</c> does), then refreshes the buttons. The game's rules stay: build phase only, one tool per burn, checked again when the turn is applied.
///    The Castle is skipped unless [BuildKeys] BurnCastle is on: burning a Castle burns every other building and, with no Castle left, loses the match.
/// Both are ordinary planned actions sent like a click, so other players need nothing. The tile under the mouse is the game's TilePointer selection; the
/// building on it is read from the island's buildings layer in memory (IslandGrid.BuildingAt returns a Nullable struct that does not survive the interop).
/// </summary>
internal static class BuildHotkeys
{
    private const int GridBuildings = 0x108;   // IslandGrid.buildings: Layer<Nullable<Building.Id>> (array +0x10, width +0x18, height +0x1C; 8-byte items)

    private static ConfigEntry<bool> _burnCastle;

    internal static void Install(ConfigFile config, Patcher patcher)
    {
        _burnCastle = config.Bind("BuildKeys", "BurnCastle", false,
            "The burn key may also burn your Castle (burning a Castle burns every other building; with no Castle left you lose).");
        // the game tells us when its build interface is shown and hidden (OnEnable / OnDisable: no arguments, safe to patch); no searching
        patcher.Patch(typeof(BuildInterface), "OnEnable", typeof(BuildHotkeys), postfix: nameof(Shown));
        patcher.Patch(typeof(BuildInterface), "OnDisable", typeof(BuildHotkeys), postfix: nameof(Hidden));
        Core.Hotkeys.Add(config, "pickaxe", "Pickaxe tool", "Q", Pickaxe, () => Plugin.On && _ui != null);
        Core.Hotkeys.Add(config, "burn", "Burn the building under the mouse", "X", Burn, () => Plugin.On && _ui != null);
    }

    // The build interface while the build phase is shown. Looking it up with FindObjectOfType<T> when none was active crashed the .NET runtime.
    private static BuildInterface _ui;
    internal static void Shown(BuildInterface __instance) => _ui = __instance;
    internal static void Hidden(BuildInterface __instance) { if (_ui != null && __instance != null && _ui.Pointer == __instance.Pointer) _ui = null; }

    private static BuildInterface Active() => _ui;

    private static void Pickaxe()
    {
        try { Active()?._Awake_b__33_0(); }
        catch (Exception e) { Plugin.L.LogWarning("[keys] pickaxe: " + e.Message); }
    }

    private static int _selection = -1;   // offset of TilePointer.selection inside the object
    private static IntPtr SelectionField() =>
        (IntPtr)typeof(DiceKingdoms.Interface.TilePointer).GetField("NativeFieldInfoPtr_selection",
            System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic).GetValue(null);

    private static unsafe void Burn()
    {
        try
        {
            var ui = Active();
            var data = ui?.Data;
            var pointer = ui?.buildingSelector?.tilePointer;
            if (data == null || pointer == null) return;
            // TilePointer.selection is a Nullable<Selection> struct: read it in place (hasValue +0, island +8, position int2 +0x10)
            if (_selection < 0) _selection = (int)Il2CppInterop.Runtime.IL2CPP.il2cpp_field_get_offset(SelectionField());
            byte* sel = (byte*)pointer.Pointer + _selection;
            if (sel[0] == 0) return;                                    // mouse over no tile
            IntPtr island = *(IntPtr*)(sel + 8);
            var own = data.Island;
            if (island == IntPtr.Zero || own == null || island != own.Pointer) return;   // only your own island, as the burn button
            var grid = own.grid;
            if (grid == null) return;
            var pos = (x: *(int*)(sel + 0x10), y: *(int*)(sel + 0x14));

            // building id on that tile, read from the buildings layer
            IntPtr layer = *(IntPtr*)((byte*)grid.Pointer + GridBuildings);
            if (layer == IntPtr.Zero) return;
            IntPtr array = *(IntPtr*)((byte*)layer + 0x10);
            int w = *(int*)((byte*)layer + 0x18), h = *(int*)((byte*)layer + 0x1C);
            if (array == IntPtr.Zero || pos.x < 0 || pos.y < 0 || pos.x >= w || pos.y >= h) return;
            byte* item = (byte*)array + 0x20 + (pos.y * w + pos.x) * 8;
            if (item[0] == 0) return;                                   // no building on that tile
            int id = *(int*)(item + 4);

            if (!_burnCastle.Value && IsCastle(grid, id)) { Plugin.L.LogInfo("[keys] burn: that is your Castle; skipped ([BuildKeys] BurnCastle is off)"); return; }

            var buildingId = System.Runtime.CompilerServices.Unsafe.As<int, DiceKingdoms.Game.Building.Id>(ref id);   // Building.Id is one int; its field is readonly
            data.BurnBuilding(buildingId);
            ui.RefreshButtons();
        }
        catch (Exception e) { Plugin.L.LogWarning("[keys] burn: " + e.Message); }
    }

    private static bool IsCastle(DiceKingdoms.Game.IslandGrid grid, int id)
    {
        var e = grid.Buildings.GetEnumerator();
        while (e.MoveNext())
        {
            var b = e.Current;
            if (b.id.value == id) return b.type != null && b.type.TryCast<DiceKingdoms.Game.BuildingTypeCastle>() != null;
        }
        return false;
    }
}
