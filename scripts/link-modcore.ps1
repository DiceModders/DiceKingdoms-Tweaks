# Copies DiceModders.ModCore.dll into <repo>\libs so the mod can be compiled against it.
# ModCore is a separate BepInEx plugin (lobby, host rules) (https://github.com/DiceModders/DiceKingdoms-ModCore): build it once, put it into <game>\BepInEx\plugins,
# then run this script. Players also need that DLL in BepInEx\plugins - the mod does NOT contain a copy of it.
# Usage:  .\scripts\link-uikit.ps1                         (takes <game>\BepInEx\plugins\DiceModders.ModCore.dll)
#         .\scripts\link-uikit.ps1 -Dll "D:\...\DiceModders.ModCore.dll"
param([string]$GameDir, [string]$Dll)
$ErrorActionPreference = "Stop"
$root = Split-Path $PSScriptRoot -Parent
if (-not $Dll) {
    . (Join-Path $PSScriptRoot "common.ps1")
    $GameDir = Get-GameDir $GameDir $root
    $Dll = Join-Path $GameDir "BepInEx\plugins\DiceModders.ModCore.dll"
}
if (-not (Test-Path $Dll)) { throw "ModCore not found at '$Dll'. Build ModCore and copy DiceModders.ModCore.dll into BepInEx\plugins, or pass -Dll." }
$libs = Join-Path $root "libs"
New-Item -ItemType Directory -Force -Path $libs | Out-Null
Copy-Item $Dll (Join-Path $libs "DiceModders.ModCore.dll") -Force
Write-Host "libs\DiceModders.ModCore.dll <- $Dll"
