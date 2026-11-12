# DiceKingdoms Tweaks (`DiceModders.Tweaks.dll`)

Quality-of-life tweaks for **Dice Kingdoms** (BepInEx 6 IL2CPP).

## What it does

* **Anonymous attacks**: a **lobby rule** the host sets in Settings > Lobby > Rules. While it is on, nobody sees who attacks whom: the attacker flags next to the player names and the attack summaries of the scoreboard are hidden, and every other player's buildings, units and ships are drawn in one grey colour (yours keep your colour). It only changes what is drawn, never the simulation; players without this mod simply keep seeing the flags.
* **History window** (top bar button; Escape closes it): every chat message and every event of the match in one scrollable window with *All / Chat / Events* filters. Up to 5000 lines are kept for the match; **Save** writes them to `BepInEx\history\`. The game's own chat and event log only show the latest lines.
* **Choose the next host** (top bar button, shown only to the host): pick who becomes host when you leave; the mod keeps that player first in the game's own list of host candidates.
* **Settings > Game > Interface tweaks**: sliders for how many chat lines and event lines stay on screen and for how many seconds (0 keeps the game's own value), and a button that opens the History window.

The whole mod can be switched off in the mod list; the game's own values come back.

## Code map

| File | Job |
|---|---|
| `Plugin.cs` | the plugin: binds the options, starts the features |
| `Options.cs` | every option (config entries) |
| `GameSettings.cs` | the block in Settings > Game |
| `AnonymousAttacks.cs` | the lobby rule and hiding the attack flags |
| `ChatLog.cs` | the chat / event log line counts and times; feeds the history |
| `History.cs` | the kept lines of the match, and saving them |
| `HistoryWindow.cs` | the History window and its top bar button |
| `NextHost.cs` | the next-host window and its top bar button |

## Requirements

* Dice Kingdoms with **BepInEx 6 IL2CPP (x64)**: see the wiki's [Mods Installation](https://dicemodders.github.io/DiceKingdoms-Modding-Wiki/guides/mod-installation/) guide. Start the game once so BepInEx creates its folders.
* The DiceModders libraries in `BepInEx\plugins`: [ModCore](https://github.com/DiceModders/DiceKingdoms-ModCore) (`DiceModders.ModCore.dll`), [UiKit](https://github.com/DiceModders/UiKit) (`DiceModders.UiKit.dll`) and [ModBridge](https://github.com/DiceModders/DiceKingdoms-ModBridge) (`DiceModders.ModBridge.dll`, shows ModCore's mod list and lobby rules on the game's screens; without it the host has nothing to edit the lobby rule with). This mod does not contain copies of them.

## Install (players)

Copy `DiceModders.Tweaks.dll` into `BepInEx\plugins`, next to the libraries. Settings are kept in `BepInEx\config\org.dicemodders.dicekingdoms.tweaks.cfg`.

## Build (developers)

Needs the .NET SDK (6 or newer) and PowerShell. Game files are never committed: the script links `BepInEx\interop` from your game folder and copies the libraries from `BepInEx\plugins` into `libs`.

```powershell
.\scripts\build.ps1 -GameDir "<your Dice-Kingdoms folder>"   # first time: remembers the folder
.\scripts\build.ps1                                           # build + install (Release, console and log OFF)
.\scripts\build.ps1 -Dev -Run                                 # Debug build, console and log ON, then start the game
```

Close the game before building: it locks the DLLs. If PowerShell refuses to run scripts: `powershell -ExecutionPolicy Bypass -File .\scripts\build.ps1`.

## License

MIT, see [LICENSE](LICENSE).
