# Pale Automaton
Pale Automaton turns Second Sentinel into a 4 phase parry focused fight, kinda like auric aegis in fact this is supposed to be a rework/remake of that mod.

# Installation
Unzip the contents of PaleAutomaton.zip into your BepInEx/plugins directory
## Dependencies:
> Warning! If you install these manually, make sure to install each listed dependency and place their contents in the respective folders, for example if the downloaded zip contains "core" or "patchers" folders, the contents of those go into the "core" and "patchers" folders from the bepinex directory, do NOT put everything in the plugins directory!

FSMUtil: https://thunderstore.io/c/hollow-knight-silksong/p/silksong_modding/FsmUtil

AssetHelper: https://thunderstore.io/c/hollow-knight-silksong/p/silksong_modding/AssetHelper
## Recommended:
Debug Mod: https://thunderstore.io/c/hollow-knight-silksong/p/hk_speedrunning/DebugMod

Lets you use noclip, invincibility, see hitboxes, camera zoom etc for testing purposes.

This build of Godhome Mod: https://astrum-nova.github.io/PaleAutomatonPlaytest/Godhome.dll

Lets you fight any boss you want, (put it in silksong installation folder/bepinex/plugins/here), press B to open the interface and click "Second Sentinel".
> The latest builds of Godhome or Pantheon Of Pharloom teleport you away after beating a boss, this breaks the death sequence.

# General Info
The mod was designed with no contact damage and no parry freeze frames in mind, never invincibile, parry focused. The arena will become an infinite walkway during the bossfight, and revert back once the boss is defeated or the player dies.

## Mechanics
Phase 3 and 4 feature a custom pogo hitbox, a circle around the player. This makes aerial pogo parries more consistent.

Pogo > up/directional slash parry sequence is required for aerial combat, because pogo parries do reset attack cooldown, but not pogo cooldown, so to parry a fast aerial combo you will want to do something like pogo > directional slash > up slash

## Phase 1 (1800 HP):
The boss will have mostly vanilla behaviour, with only the first attack of the combo being modified, This phase is meant to be a tutorial for the timings with ample time to recover

## Phase 2 (1600 HP):
The phase begins with the boss roaring, then a fake parry, then he will rapid slash at you. This phase continues the combos and followups of the first phase, each combo depends on the starting attack, and some of them have mixups but the parry timing should be the same for every mixup (i tested this thoroughly)

## Phase 3 (800 HP):
The phase begins with the boss teleporting away, spikes will appear on the ground, then the boss will come back. This phase features 5 completely new attack combos meant to replace the phase 2 ones, theyre designed with a fully aerial fight in mind.

## Phase 4 (400 HP):
The phase begins with the boss charging a massive windslash that destroys the ground and the spikes. This phase is fully aerial, nothing new from phase 3 though. The boss cannot be killed with a parry, you must get a direct hit to finish him off.

# Github
The code is on github, feel free to check it out and open PRs if you fix something
- https://github.com/astrum-nova/PaleAutomaton/tree/fixingthisshiiguess

# Config
## Accessibility
- Parry time freeze
- Camera zoom out
- Custom pogo hitbox
## Gameplay
- Infinite walkway
- Disable boss parrying you
## Visual Effects
- Bell bind effect on the boss (scrapped effect, can be enabled)
## Debug
- Debug mode
- Initial HP
- Phase 2 threshold
- Phase 3 threshold
- Phase 4 threshold
