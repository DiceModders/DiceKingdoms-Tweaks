# DiceKingdoms Tweaks (`DiceModders.Tweaks.dll`)

Quality-of-life tweaks for **Dice Kingdoms** (BepInEx 6 IL2CPP). It is one self-contained project: it shares no code with other mods. (Version 3.0.0 moved the performance options to the separate *Optimization* mod and the performance probe to the *Extractor*.)

## What it does

* **Anonymous attacks** - a **lobby rule** the host sets in Settings > Lobby > Rules. While it is on, nobody sees who attacks whom: the attacker flags next to the player names are hidden and the attack summaries at the top right of the scoreboard are not created. It only changes what is drawn, never the simulation; players without this mod simply keep seeing the flags.
* **History window** (top bar button, or `H`) - every chat message and every event of the match in one scrollable window with *All / Chat / Events* tabs, kept up to 5000 lines, **Save** writes them to `BepInEx\history\`. The game's own chat and event log only show the latest lines.
* **Choose the next host** (`N`, host only) - pick who becomes host when you leave; the mod keeps that player first in the lobby's host candidate list. Experimental: the real key names of the lobby are discovered at run time.
* **Lobby dump** (`L`) - writes the Steam lobby data and members to the log. Use it when a kicked player cannot rejoin and send the log.
* **Settings > Game > Interface tweaks**: *Auto-clear text chat*, *Auto-clear event log*, History button.

## Hotkeys (hold Right Ctrl; configurable in the .cfg)

| Key | Action |
|---|---|
| H | history window |
| N | choose the next host (host only) |
| L | dump lobby data to the log |
| F5 / F6 | auto-clear chat / auto-clear event log |
| Home | print the hotkeys to the log |

## Code map

`ModPlugin.cs`, `AnonymousAttacks.cs`, `History.cs` + `HistoryWindow.cs`, `HostSuccession.cs`, `LobbyLog.cs`, `InterfaceTweaks.cs` + `InterfacePatches.cs`, `Icons.cs`, `Common/`.

## Requirements

* Dice Kingdoms with **BepInEx 6 IL2CPP (x64)** - see the wiki's [Mods Installation](https://dicemodders.github.io/DiceKingdoms-Modding-Wiki/guides/mod-installation/) guide. Start the game once so BepInEx creates its folders.
* **[UiKit](https://github.com/DiceModders/UIKit_Another_Version)** (`DiceModders.UiKit.dll`) and **[ModCore](https://github.com/DiceModders/DiceKingdoms-ModCore)** (`DiceModders.ModCore.dll`) in `BepInEx\plugins` - separate plugins (interface; lobby and host rules). This mod does **not** contain a copy of them.
* **[ModBridge](https://github.com/DiceModders/DiceKingdoms-ModBridge)** (`DiceModders.ModBridge.dll`) to get the rows that edit the lobby rules in Settings > Lobby > Rules. Without it the rules still work, but the host has nothing to edit them with.

## Install (players)

Copy `DiceModders.Tweaks.dll` into `BepInEx\plugins` (next to `DiceModders.UiKit.dll`). Settings are created in `BepInEx\config` (`org.dicemodders.dicekingdoms.tweaks.cfg`).

## Build (developers)

Needs the .NET SDK (6 or newer) and PowerShell. Game files are never committed: the scripts link them from your game folder.

```powershell
.\scripts\link-interop.ps1 "D:\SteamLibrary\steamapps\common\Dice-Kingdoms"   # once: BepInEx\interop -> .\interop
.\scripts\link-uikit.ps1                                                  # once: copies DiceModders.UiKit.dll to .\libs
.\scripts\build.ps1 -GameDir "D:\SteamLibrary\steamapps\common\Dice-Kingdoms"   # Release, installed (path is remembered)
.\scripts\build-dev.ps1                                                  # Debug (defines DEVLOG), console and log ON
```

## License

MIT - see [LICENSE](LICENSE).
