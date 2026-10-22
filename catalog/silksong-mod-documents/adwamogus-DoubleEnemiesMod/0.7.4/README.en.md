# Double Enemies

**Doubles (or multiplies) every enemy, arena, and boss fight!**  
Take on twice the challenge with synchronized enemies and configurable difficulty options.

Currently the most stable and feature-rich mod of its kind.

---

## What It Does

This mod clones all in-game entities that use a `HealthManager` component — effectively doubling (or multiplying) all enemies and bosses.

It includes options to:
- **Adjust the multiplier** for how many copies of each enemy are spawned.
- **Filter by type** (Normal Enemies, Arena Enemies, Bosses).
- **Enable Shared HP** between cloned bosses for synchronized fights.
- **Enable unsafe mode** for arenas to be affected by Shared HP.

---

## Shared HP

Shared HP is an optional feature that links the health of cloned bosses, making fights more dynamic and consistent.  
When enabled, all copies of a boss share the same effective health — damaging one affects the others.

This feature is **on by default** and can be disabled in the config if needed.  
A few special bosses are automatically excluded to maintain stability.

---

## Configuration

After launching the game once with the mod installed, you can edit the config file in  
`BepInEx/config/com.adwamogus.skdoubleenemiesmod.cfg`

Main options:
- **Multiplier:** Change how many clones are created.
- **Enemy Filters:** Toggle Arenas, Bosses, or Normal Enemies.
- **Shared HP:** Enable or disable synchronized boss health.

---

## Compatibility

- Works with most other enemy or spawning mods.  
- Fully compatible with BepInEx-based setups.

---

## Installation

1. Install **BepInEx** (see guide [BepInEx](https://docs.bepinex.dev/articles/user_guide/installation/index.html)).  
2. Download this mod and extract the `.dll` file into your  
   `BepInEx/plugins` folder.  
3. Launch the game and enjoy the chaos!

---

## Difficulty Notice

This mod **significantly increases difficulty** and can create extremely challenging scenarios.  
Consider using quality-of-life or balance mods (e.g. unlimited shards) if needed.

---

## Feedback & Bug Reports

If you encounter issues, crashes, or odd behavior, please report them on the **Nexus Mods page**:  
[Double Enemies Mod](https://www.nexusmods.com/hollowknightsilksong/mods/504)

That page is updated more frequently and contains the latest patch notes.

---

## Source Code

GitHub: [https://github.com/adwamogus/DoubleEnemiesMod](https://github.com/adwamogus/DoubleEnemiesMod)

---
