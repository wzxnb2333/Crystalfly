# Silk Soar Dash Mod

Silk Soar variant that lets you soar horizontally, like Crystal Dash.

## Description

Do you like Silk Soar? Do you miss Crystal Dash? Then this mod is exactly right for you!
This mod gives you a second way of using Silk Soar, by throwing the needle horizontally and letting you dash soar rooms!

Arguably this could also just have been called Super Clawline.

Installation instructions
The easiest way is to install it from Thunderstore with a mod manager like r2modman or Gale.

For a manual install you will need to have the BepInEx mod loader installed. Check out
the [installation instructions on the official website](https://docs.bepinex.dev/articles/user_guide/installation/index.html).
Once you got it installed, download this mod and unpack the zip into

```
\steamapps\common\Hollow Knight Silksong
```

## Main features

- Use SSD when standing on the ground
- Use SSD when on a wall (Cling Grip required)
- Throw needle to the next wall in the path and dash there
- Stick to the wall if you have Cling Grip
- Dash across door transitions
- Default combination: Harpoon (LT) + UP (opposite of Silk Soar)
- Configure in game by pressing F1 (BepInEx Config)
- Swap key combination:  Silk Soar becomes Harpoon + UP and Silk Soar Dash becomes Harpoon + Down
- Configure when this is available: With Silk Soar, with Clawline, always
- Configure Silk Cost: 1 (default), any other amount or none
- Silk Soar itself is still usable

## Requirements

No other mods are required.

## Known Limitations

**Room Transitions**
Currently there is no way for the game to tell me if in the next few rooms across, the needle would land on a valid
wall. So for now the dash will just happen and the wall check will happen once entering the room. Hornet will then
continue to dash right up until the obstacle.
In a future release we might be able to do some work upfront when loading the game, so that we can do the check properly
and stop the dash from starting, just like Silk Soar does it.
But honestly: I had a hard time even finding a room where this really matters.

**Animations**
The animations are wonky in some situations. Those are because I'm reusing
animations from Silk Soar and Clawline. Unless someone can and wants to do proper graphics for this mod, this will not
change.

**Balancing/Game play**
Of course this is not balanced game play wise and will very likely allow for some interesting interactions, sequence
breaks or just straight up make parts of the game easier. The config options let you add at least some balancing if you
want to.

## Note

I used AI to help me understand how the Final State Machines are working and to help me write code for it. While I did
some of the coding myself and ensured the code is clean and properly structured, most of it was still written by AI. It
was only used for coding tasks! I just want to be transparent about it.