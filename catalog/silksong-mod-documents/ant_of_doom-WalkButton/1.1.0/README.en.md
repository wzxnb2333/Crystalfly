# Silksong-Walk-Button-Mod
A simple mod for Silksong. Hold a certain button to walk slowly anywhere. Default is the keyboard `L`, but you may easily adapt it to another key.

It works while the key is held, but you can reconfigure it to be working on toggle instead.

# Installation
Take `WalkButton.dll` out of `ant_of_doom-WalkButton-1.1.0.zip` and put it into `BepInEx\plugins`. 

Done.

# Changing the button
You can change the default button by launching the mod at least once, after what it creates a config in the `BepInEx\config` folder. The config is called `org.sealofapproval.walkbuttonmod.cfg`. It's easy to change the default button, just open it and replace the `L` in the line `walkButton = L` with anything of your own choice.

In the same place, you can change the toggle/hold setting: replace `hold` in the line `mode = hold` with `toggle`, or vice versa.