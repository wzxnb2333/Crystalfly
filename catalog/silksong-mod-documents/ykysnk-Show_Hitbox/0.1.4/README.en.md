# Show Hitbox

Simple mod turns on the default debug hitbox.

![icon.png](https://raw.githubusercontent.com/T2PeNBiX99wcoxKv3A4g/HKSS.ShowHitbox/refs/heads/master/icon.png)

## Controls

* Toggle hitbox by pressing `F11` key by default.

## Installation

1. Download [BepInEx](https://github.com/BepInEx/BepInEx)
   and [install](https://docs.bepinex.dev/articles/user_guide/installation/index.html).
2. Download [BepinEx-Utils](https://github.com/T2PeNBiX99wcoxKv3A4g/BepinEx-Utils/releases/latest).
3. Extract all the .dll file to `game folder/BepInEx/plugins`
4. Launch game

## Configuration

The mod configuration file name is `io.github.ykysnk.HKSS.ShowHitbox.cfg` inside `game folder/BepInEx/config`,
If you are not using any mod manager, you can manually change the value, also if you
installed [BepinEx Configuration Manager](https://github.com/BepInEx/BepInEx.ConfigurationManager),
you can change any values in game instead.

* `ToggleKey`
    * Type: `keybind`
    * Default: `F11`
    * Description:
        * The toggle key to toggle the hitbox display.
* `ShowHitbox`
    * Type: `boolean`
    * Default: `false`
    * Description:
        * Show the hitbox.
* `MoreInfos`
    * Type: `boolean`
    * Default: `true`
    * Description:
        * Show more info.