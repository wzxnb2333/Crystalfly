# Fifth Chorus

A Silksong mod that reworks the **Fourth Chorus** boss, adding a faster fight flow, extra phases, and some quality-of-life features when fighting it.

---

## Features

### 1. Reworked Fourth Chorus Boss Fight
The Fourth Chorus encounter is reworked to create a more difficult, but fun fight.  
Patterns are adjusted to avoid repeats, boss logic is smarter, and the boss has a completely new 4th phase.

[Watch the showcase on YouTube](https://www.youtube.com/watch?v=qyYajaUgmEk)

### 2. In-Game Boss Config Menu
Press **F6** (configurable) to open a custom **Boss Config** menu:

- Choose a difficulty, see a description, then press **Start** to jump into the fight

### 3. Difficulty Presets (Free Play / Easy / Medium / Hard)
Each difficulty configures the player’s stats before teleporting:

- **Free Play**  
  Use your current save’s gear, tools, masks, and silk. No changes applied.
- **Easy**  
  7 masks, 9 silk, no upgrades.
- **Medium**  
  6 masks, 9 silk, no upgrades, no non-combat silk skills (except dash and cloak).
- **Hard**  
  5 masks, 9 silk, no tools, no upgrades, no non-combat silk skills (except dash and cloak).

### 4. Instant Teleport to Fourth Chorus Arena
Upon starting the fight, the mod warps directly to the Fourth Chorus room:

- Scene: `Bone_East_08`
- Gate: `right1`

This can be done at any time once loaded in, as long as the game is not paused.

### 5. Save Blocking (Safe Sandbox Mode)
All calls to `GameManager.SaveGame` are intercepted and skipped:

- No autosaves or manual saves while the mod’s patch is active
- Your real save file won’t be altered by using this mod

As a result, you do not need a dedicated save file to play with this mod. Any save you have will work.  
It is still always recommended to back up your save when playing with mods.

---

## Installation

1. Install **BepInEx** for Silksong (follow the standard setup for your platform).
2. Build this project to produce `FifthChorus.dll` (or grab it from releases).
3. Drop `FifthChorus.dll` into:
   - `Silksong/BepInEx/plugins/` (or any folder inside `plugins`).
4. Launch the game.

---

## Usage

- Load your save as normal.
- Once in-game, press **F6** (default) to open the **Fifth Chorus Config Menu**.
- Pick a difficulty:
  - Free Play / Easy / Medium / Hard
- Read the description, then press **Start** to:
  - Apply the preset stats
  - Teleport to the Fourth Chorus arena

