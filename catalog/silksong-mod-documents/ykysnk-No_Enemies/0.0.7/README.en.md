# No Enemies

Nothing happened in `Hollow Knight: Silksong`

![icon.png](https://raw.githubusercontent.com/T2PeNBiX99wcoxKv3A4g/HKSS.NoEnemies/refs/heads/master/icon.png)

## What does this mod do?

* The enemies will be gone when you install the mod.
* Any enemies will be killed when trying to attack in `KillWhenTryAttack` mode.

## Installation

1. Download [BepInEx](https://github.com/BepInEx/BepInEx)
   and [install](https://docs.bepinex.dev/articles/user_guide/installation/index.html).
2. Download [BepinEx-Utils](https://github.com/T2PeNBiX99wcoxKv3A4g/BepinEx-Utils/releases/latest).
3. Extract all the .dll file to `game folder/BepInEx/plugins`
4. Launch game

## Configuration

The mod configuration file name is `io.github.ykysnk.HKSS.NoEnemies.cfg` inside `game folder/BepInEx/config`,
If you are not using any mod manager, you can manually change the value, also if you
installed [BepinEx Configuration Manager](https://github.com/BepInEx/BepInEx.ConfigurationManager),
you can change any values in game instead.

* `Mode`
    * Type: `NoEnemiesMode`
    * Default: `BeGone`
    * Description:
        * No enemies working modes.
* `RandomCorpseSplat`
    * Type: `boolean`
    * Default: `false`
    * Description:
        * Random corpse splat in the maps.

## Known issue

* Some enemies may have some problems. Like items will not drop at all.  
  You can try to change the working mode to `KillWhenTryAttack` or `Off` to avoid this issue.
* Some time you may stick in the battle scene, try back to the main menu or restart the game.