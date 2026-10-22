# Always Mist

A mod lets you enter [the mist area](https://hollowknightsilksong.wiki.fextralife.com/The+Mist) again

![icon.png](https://raw.githubusercontent.com/T2PeNBiX99wcoxKv3A4g/Always-Mist/refs/heads/master/icon.png)

## What does this mod do?

* The mod will reopen [the mist area](https://hollowknightsilksong.wiki.fextralife.com/The+Mist) even you finish the
  area.
* Random the maze value makes the mist more different.
* Have a true always mist option can be turned on, then enter any room always enter the mist first.

## Installation

1. Download [BepInEx](https://github.com/BepInEx/BepInEx)
   and [install](https://docs.bepinex.dev/articles/user_guide/installation/index.html).
2. Download [BepinEx-Utils](https://github.com/T2PeNBiX99wcoxKv3A4g/BepinEx-Utils/releases/latest).
3. Extract all the .dll file to `game folder/BepInEx/plugins`
4. Launch game

## Configuration

The mod configuration file name is `io.github.ykysnk.AlwaysMist.cfg` inside `game folder/BepInEx/config`,
If you are not using any mod manager, you can manually change the value, also if you
installed [BepinEx Configuration Manager](https://github.com/BepInEx/BepInEx.ConfigurationManager),
you can change any values in game instead.

**The `TrueAlwaysMist` config will ignore `ResetMazeSaveData`, `RandomNeededCorrectDoors`, `RestBenchInMist` config
values.
Also turn on the config, enter any doors, or any room will enter the mist area first!**

* `ResetMazeSaveData`
    * Type: `boolean`
    * Default: `false`
    * Description:
        * Always reset maze save data when enter even if you're not dead.
* `RandomNeededCorrectDoors`
    * Type: `boolean`
    * Default: `false`
    * Description:
        * Random the correct doors are needed when enter every single time.
* `MaxRandomNeededCorrectDoors`
    * Type: `integer`
    * Default: `10`
    * Min: `2`
    * Max: `100`
    * Description:
        * The max value of random the correct doors needed.
* `MinRandomNeededCorrectDoors`
    * Type: `integer`
    * Default: `2`
    * Min: `2`
    * Max: `100`
    * Description:
        * The min value of random the correct doors needed.
* `TrueAlwaysMist`
    * Type: `boolean`
    * Default: `false`
    * Description:
        * Always enter the mist maze first when entering any rooms or any doors.
* `RestBenchInMist`
    * Type: `boolean`
    * Default: `false`
    * Description:
        * Turn on the rest bench in the mist maze.

## Known issue

* The mist area is only one side. after passing through the mist area, you won't back to the mist-last hall.
  I'm lazy, So I won't fix this.
* When a player passes through the mist area, enter again will exit immediately, until player death.
  They can be easily fixed by turn on `ResetMazeSaveData` config, also turn on `TrueAlwaysMist` config will not have the
  issue.
* You can turn on `TrueAlwaysMist` config then start new game, but will be very painful if you don't have any abilities.
  So recommend installing some abilities unlocked mod.