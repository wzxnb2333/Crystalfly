Multiplies damage inflicted by the player by 2. The multiplier number can be configured.

To install:
1. Install BepInEx 5. Instructions here: https://docs.bepinex.dev/articles/user_guide/installation/index.html (Tested with 5.4.23.3; other versions may also work.)
2. Unzip DoubleDamage.zip and put DoubleDamage.dll in BepInEx/plugins folder.
3. Start the game. Player's damage should be doubled.
4. After running the game once, it will create a config file at BepInEx/config/stebars.silksong.double-damage.cfg. Edit the config file to change the damage multiplier. After changing the config, restart the game to apply the change.

Setting the multiplier to 1 will make damage the same as in the base game. You can set it to fractional numbers like 1.5 (for only +50% damage) or 0.5 (for half damage, so ants will take 8 hits to kill).

It should apply to all damage inflicted by the player (by nail, tools, etc.) but not to damage inflicted by enemies. Multiplier is applied after all other modifiers. Doesn't apply to some environment obstructions (like the vines made by Sister Splinter).

Source code: https://github.com/StephenBarnes/Silksong-DoubleDamage
