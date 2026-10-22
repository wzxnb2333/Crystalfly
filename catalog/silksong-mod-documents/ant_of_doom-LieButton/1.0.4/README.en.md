# Silksong-Lie-Button-Mod
A simple but charming mod for Silksong, perfect for streamers. Click a certain key to lie down or wake up (like a toggle). Default is the keyboard `L`, but you may easily adapt it to another key inside the config, or even a controller button. The config also got an option to make Hornet wake up upon pressing `Attack` or `Jump`.

Without known bugs.

# Installation
Take `LieButton.dll` out of `ant_of_doom-LieButton-1.0.4.zip` and put it into `BepInEx\plugins`. 

Done.

# Changing the button
You can change the default button by launching the mod at least once, after what it creates a config in the `BepInEx\config` folder. The config is called `org.sealofapproval.liebuttonmod.cfg`. It's easy to change the default button, just open it and replace the `L` in the line `lieButton = L` with anything of your own choice.

In the same place, you may enable the wakeOnAttackOrJump feature by changing the `false` in the line `wakeOnAttackOrJump = false` to `true`.