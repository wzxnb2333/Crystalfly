# Silksong Loadout Randomizer

A BepInEx plugin for *Hollow Knight: Silksong* that randomizes your equipped tools and silk skills. This mod was inspired by MyPetCactus and his randomized build run on [YouTube](https://www.youtube.com/watch?v=zL1Bqa6Tgqs).

## Features
- **Bench Randomization**: Randomize your entire loadout every time you rest at a bench.
- **Boss Defeat Randomization**: Automatically randomize your loadout when you strike the final blow on a boss.
- **On-Demand Hotkey**: Press F10 (configurable) to instantly shuffle your loadout at any time.
- **In-Game Notifications**: Uses the native Silksong banner UI to notify you when a randomization occurs.
- **Safe Persistence**: Built-in save protection intercepts the game's background threading to ensure illegitimate or unowned items are **never** permanently written to your save file.

## Configuration
All options can be configured in-game via the BepInEx ConfigurationManager menu (F1 by default). 
You can customize exactly what gets randomized:
- Include/Exclude Crests, Silk Skills, Red Tools, Blue Tools, or Yellow Tools.
- Bypass unlock requirements to allow the randomizer to give you items you haven't found yet!
- Includes robust support for all 48 boss encounters, gracefully handling multi-boss fights without misfiring.

## Installation

**Recommended:** Download and install automatically via [Thunderstore / r2modman](https://thunderstore.io/c/hollow-knight-silksong/p/thecodeflayer/SilksongLoadoutRandomizer/).

**Manual Installation:**
1. Install [BepInEx 5](https://github.com/BepInEx/BepInEx) for Hollow Knight: Silksong.
2. Place `SilksongLoadoutRandomizer.dll` in your `BepInEx/plugins` folder.
3. Launch the game and enjoy the chaos!

## Source Code & Support
If you encounter any bugs, have feature requests, or want to view the source code, please visit the [GitHub Repository](https://github.com/thecodeflayer/SilksongLoadoutRandomizer).
