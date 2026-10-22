# Camera Tools

A mod that for creating cinematic camera shots. These shots are meant for B-roll type footage in Silksong content. Applies filters to enhance the game's visuals and decrease blur on background objects. Each camera effect lasts 25 seconds by default and can optionally be lengthened.

To use this mod, it is recommended to enable the OBS "Replay Buffer" feature. Under Settings > Output > Replay Buffer, check "Enable Replay Buffer", and set Maximum Replay Time to 25s. Under Hotkeys, set a hotkey for "Save Replay". Set up your scene, turn on Start Replay Buffer. In game, when you hit your hotkey, it will clip the previous 25 seconds.

Debug is recommended to remove your HUD, Hide Hero (if desired), and remove Hero Light within page 5 of Binds. Keep in mind that you can also change timescale on page 1, but then you may want to also change your maximum replay time.

## Controls

- 1: Pan Right
- 2: Pan Left
- 3: Zoom In
- 4: Zoom Out
- 5: Pan Right (Large)
- 6: Pan Left (Large)
- 8: Add 5 seconds to next camera movement.
- 9: Toggle Blur (adds a Blur effect to the outside of the screen)
- 0: Toggle Vignette
- U: Pan Up
- I: Pan Left (while keeping character left)
- O: Pan Diagonally Up Right
- P: Pan Diagonally Up Left
- B: Loads surrounding scenes into the current scene. This is an experimental feature (very buggy).

## Known Issues

This mod is being presented as is, and isn't being supported as it was just a quick mod I had someone whip up for a video. Apologies if you run into any frustrating issues, but I hope it serves its purpose for you!

- After loading a file, saving and quitting and entering another save breaks the mod until the game is restarted.
- Sometimes, black boxes appear in the foreground of a scene during camera effects. This can be dealt with by fully restarting the game. It affects scenes individually, so you can also just move to another scene.
- Certain lighting effects cause a red glow, including the particle effect from smacking or pogoing into terrain. If it's being caused by a breakable object, such as a lamp, you can break the object to avoid the glow.

*Please credit *Clove* for developing the mod! I only commissioned it.*