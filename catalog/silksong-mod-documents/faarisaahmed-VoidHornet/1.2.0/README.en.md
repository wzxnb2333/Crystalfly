# Void Hornet

A pitch-black **Void Hornet** skin for *Hollow Knight: Silksong*, built on
[Stitchwork](https://thunderstore.io/c/hollow-knight-silksong/p/SilksongSkinCreators/Stitchwork/).

Hornet is rendered as a pure void silhouette -- every part of her goes black,
with only her eyes glowing white.

## What it changes

* **2326 sprites** across 37 collections: every Hornet animation, all crest
  weapons, her tools, and every cutscene or enemy-grab frame she appears in.
* The **loading-screen Hornet** is black with a glowing white rim.
* Her silhouette is **pixel-exact to vanilla** -- only the colours are replaced,
  so every animation, hitbox and pivot lines up perfectly.
* **VFX are left vanilla** (166 frames): silk, glows, sparkles and slash trails
  keep their original look so they still read against the black.

## Installation

1. Install with any Thunderstore mod manager, or drop this folder into
   `BepInEx/plugins/` (or `<GameRoot>/Packs/`).
2. Press `1` at the Main Menu, enable **VoidHornet**, press Apply.
   No pack ordering or conditions are needed.

## Performance

Individual sprite replacement is used rather than whole-atlas sheets on
purpose: the replaced frames total ~88 Mpx, where the atlases containing them
come to well over 150 Mpx even for a handful of collections. Swapping sheets
would mean decoding roughly twice the pixels for the same result.

| | files | pixels | on disk |
| --- | --- | --- | --- |
| Void Hornet | 2334 | 88 Mpx | 16 MB |
| Nightmare Hornet (for scale) | 3055 | 196 Mpx | 74 MB |

## Coverage

| Collection | Voidified | Left vanilla (VFX) |
| --- | --- | --- |
| Belltown House Cln | 21 | 0 |\n| Bone Beast Child Call Cln | 41 | 0 |\n| Coral Tendril Cln | 8 | 0 |\n| Crest Get Cln | 24 | 5 |\n| Dice Game Cln | 38 | 0 |\n| Dustroach Cln | 9 | 0 |\n| Dustroach Pollywog Cln | 6 | 0 |\n| Farmer Scissors Cln | 8 | 0 |\n| Gloom Beast Cln | 9 | 0 |\n| Hornet CrestWeapon Dagger Cln | 55 | 0 |\n| Hornet CrestWeapon Drill Lance Cln | 39 | 0 |\n| Hornet CrestWeapon Scythe | 48 | 0 |\n| Hornet CrestWeapon Shaman Cln | 61 | 2 |\n| Hornet CrestWeapon Warrior Cln | 65 | 0 |\n| Hornet CrestWeapon Whip Cln | 16 | 0 |\n| Hornet Strung Up Cln | 25 | 0 |\n| Hornet_Needolin_Windy | 59 | 0 |\n| Knight | 1321 | 121 |\n| Knight Hatchling Cln | 28 | 6 |\n| Lilypad Plat Cln | 6 | 0 |\n| Lost Lace Cln | 62 | 0 |\n| Mite Trap Cln | 6 | 0 |\n| Phantom Cln | 14 | 4 |\n| Prompts Cln | 0 | 7 |\n| Rosary Thief Cln | 6 | 0 |\n| Silk Boss Cln | 55 | 0 |\n| Slab Cloak Scene Cln | 43 | 0 |\n| Slab Fly Large Cage Cln | 4 | 0 |\n| Slab Fly Vomit Cln | 16 | 0 |\n| Spinner Cln | 14 | 0 |\n| Sprintmaster Companion Cln | 4 | 0 |\n| Swamp Barnacle Cln | 21 | 0 |\n| Tools Cln | 146 | 21 |\n| Weaver WebShot Scene Cln | 6 | 0 |\n| Wisp Fireball Cln | 19 | 0 |\n| Wood Witch Cln | 19 | 0 |\n| Wraith Cln | 4 | 0 |

## How it was made

The sprites are generated, not hand-drawn. Vanilla tk2d atlases are read out
of the game's Addressable bundles; for each frame the alpha channel is kept
untouched and the RGB flattened to black, while Hornet's eyes are recovered by
finding the dark ellipses inside the convex hull of her pale mask.

The *list* of which frames contain Hornet is derived from the
[Nightmare Hornet](https://thunderstore.io/c/hollow-knight-silksong/p/MorningRain/NightmareHornet/)
pack by MorningRain, whose author had already worked out exactly which frames
in each NPC collection she appears in. No art from that pack is used -- every
pixel comes from the vanilla atlases.

## Credits

* [Ashiepaws](https://github.com/Ashiepaws/silksong-patchwork) -- Patchwork
* [FabulatorK](https://github.com/FabulatorK) and the Stitchwork team
* [MorningRain](https://www.youtube.com/@AM_Rain) -- Nightmare Hornet, used as the frame-coverage reference
