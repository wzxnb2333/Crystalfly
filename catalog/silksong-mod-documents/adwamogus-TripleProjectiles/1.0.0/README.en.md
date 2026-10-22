# Triple Projectiles

**Multiplies most projectiles in the game and spreads them in configurable arcs.**  
Turn every fight into chaos with adjustable projectile counts.

Designed to be flexible, safe, and highly configurable.

---

## What It Does

This mod hooks into Silksong’s projectile spawning and **clones most enemy projectiles**.

It includes options to:
- **Set a projectile multiplier** (e.g. 3x, 5x, etc.)
- **Control spread angle** for cloned projectiles
- **Enable or disable player projectiles**
- **Limit cloning to physics-based projectiles only**

Extensive blacklists are used to avoid particles, UI elements, effects, and objects that would otherwise break fights.

---

## Configuration

After launching the game once, edit the config file at:  
`BepInEx/config/com.adwamogus.tripleprojectiles.cfg`

Main options:
- **Multiplier** : How many projectiles are spawned per original
- **MaxSpread** : Total spread angle (degrees)
- **EnablePlayerProjectiles** : Include player tools/projectiles
- **OnlyClonePhysicsProjectiles** : Ignore static / scripted objects

---

## Compatibility

- BepInEx 5.x  
- Compatible with Double Enemies Mod

---

## Difficulty Notice

This mod can **dramatically increase difficulty** and visual intensity.  
High multipliers may impact performance or fight readability.