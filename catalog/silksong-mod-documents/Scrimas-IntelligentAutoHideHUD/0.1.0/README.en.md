## Description

Tired of a cluttered screen while you're just exploring Pharloom? This mod gives Silksong a dynamic, Elden Ring-style HUD: masks, silk spool, tool icons and the crest fade out of view while you're safely exploring, and snap back the instant it matters — an enemy gets close, you take a hit, or you spend a resource. No more permanently-on health/silk/tool clutter sitting over your beautiful screenshots and exploration, but never any risk of missing information in a fight.

## Main Features

- Combat-driven visibility — the HUD appears the moment a living enemy is near (configurable proximity radius) or aggroed onto you, and during scripted arena/battle-gate encounters. Background/foreground decoration enemies (different depth plane) are correctly ignored so they don't false-trigger the HUD.
- Per-resource smart reveal — masks, silk, and tool ammo each reveal independently the instant they change (take damage → health pops up; spend silk → spool pops up)
- Low-resource lock — each element has its own "always show below X%" threshold, so you're never caught unaware when you're low.

## Installation

1. Install [BepInEx 5 with Configuration Manager](https://thunderstore.io/c/hollow-knight-silksong/p/silksong_modding/BepInExPack_Silksong/)if you haven't already.
2. Download this mod and drop HideHudSilksong.dll into your Hollow Knight Silksong/BepInEx/plugins/ folder.
3. Launch the game

## Configuration

Configurable via ConfigurationManager:

- Proximity/aggro radius and arena-latch distance cap
- Combat-exit linger duration
- Per-element (health/silk/tools) manage toggle, always-show-below-% threshold, and change-linger duration
- Fade in/out duration and hidden-state opacity (fully invisible by default), or set a fain

## Links

NexusMods page: https://www.nexusmods.com/hollowknightsilksong/mods/1190
GitHub page: https://github.com/Scrimas/Silksong-Intelligent-AutoHide-HUD
