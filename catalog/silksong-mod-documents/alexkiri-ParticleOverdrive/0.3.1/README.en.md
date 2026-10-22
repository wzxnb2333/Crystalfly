# Particle Overdrive

A mod for Hollow Knight: Silksong that allows increasing the amount of particles to ridiculous levels


https://github.com/user-attachments/assets/55179fb4-7e0b-4c08-a33d-08f62880fcee



## Installation

### The Simple Way

Use r2modman or Gale.

### Manual

1. Download [BepInExPack Silksong](https://thunderstore.io/c/hollow-knight-silksong/p/BepInEx/BepInExPack_Silksong/) and extract it to the game folder, next to the game executable
2. Download [BepinExConfigurationManager](https://thunderstore.io/c/hollow-knight-silksong/p/jakobhellermann/BepInExConfigurationManager/)
2. Download from [github](https://github.com/alexkiri/ParticleOverdrivePlugin/releases), [thunderstore](https://thunderstore.io/c/hollow-knight-silksong/p/alexkiri/ParticleOverdrive/) or [nexusmods](https://www.nexusmods.com/hollowknightsilksong/mods/736) and extract it inside of `<game folder>/BepInEx/plugins`
3.
    - (Windows) Run the game normally
    - (Linux/MacOS) Run `run_bepinex.sh`


## Configuration

Use `BepinExConfigurationManager` (open with F1 by default) to adjust the parameter in realtime, or edit the `io.github.alexkiri.particleoverdrive.cfg` file inside `<game folder>/BepInEx/config`

- `ParticlesMultiplier`:
    - increases the amount of particles
    - decreases performance
    - default value: VanillaHigh
    - acceptable values: up to 16x
- `ExtremeMultiplier`:
    - ridiculously high values are allowed
    - recommended to disable the `UpgradeAllParticleSystems` option when using this multiplier, otherwise the game would be unplayable
    - can _greatly_ decrease performance
    - only available in "Advanced settings"
- `UpgradeAllParticleSystems`:
    - upgrades all the particle systems to be affected by the multiplier
    - requires quit & reload in order to be applied


The "Particle Effects" setting from the main menu does not have any effect with this mod enabled.
