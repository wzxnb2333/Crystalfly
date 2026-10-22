# Glissando
*or: "Even a Wyrm will turn"*

A Hollow Knight: Silksong mod that completely replaces jumping with flipping gravity.

Inspired by the game "VVVVVV" by Terry Cavanagh. Sponsored and playtested by TherminatorX.

Double jumping is also changed in this mod. And to make traversal a little more reasonable, once float and double jump are both unlocked, you can always press down and jump in the air to float.

Included in the mod's options is the ability to force a hazard respawn, with either menu button or keybind, in case Hornet ever gets stuck somewhere or goes out of bounds.

Acts 1 and 2 have been playtested, but you may still encounter some problems. If you find a glitch, please [open an issue on the Github repository](https://github.com/kaycodes13/Silksong.Glissando/issues).

**Therm's challenge:** how much of this mod can be beaten with the Mount Fay upgrade disabled?

Donation Link: https://ko-fi.com/kaykao

## Installation

Install via r2modman or Thunderstore Mod Manager. Glissando's dependencies should be installed automatically.

For manual installation, extract the `.zip` file and place the resulting **folder** in the `BepInEx/plugins` directory for your Silksong install. You'll also have to manually install:
* [FsmUtil](<https://thunderstore.io/c/hollow-knight-silksong/p/silksong_modding/FsmUtil/>)
* [I18N](<https://thunderstore.io/c/hollow-knight-silksong/p/silksong_modding/I18N/>)
* [ModMenu](<https://thunderstore.io/c/hollow-knight-silksong/p/silksong_modding/ModMenu/>) and its dependencies

---

## For Mod Developers

If you're developing a mod that changes Hornet's velocity at any point, the y velocities you set *may not* automatically flip when Glissando flips gravity. This is especially important for custom crests and tools, as any new FSM actions you add **will not** be affected by this mod.

If you want your mod to be fully compatible with Glissando, you can check the value of the static boolean property `GlissandoPlugin.GravityIsFlipped` and invert any relevant y velocities or movement when it's true.
