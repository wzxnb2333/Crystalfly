# Replenish tools over time
This mod changes the way red tools are handled.  
Hornet will now replenish her red tools over time without needing to sit on a bench.

## Specifics
- Tools replenish over time off the bench
- Replenish speeds are based on the current capacity for any red tool.
  - Tool pouches increase capacity and thus increase replenish speed
  - The straight pin at full capacity replenishes a single pin every 7.5 seconds.
- Striking an enemy doubles the replenish speed for 1.5 seconds.
  - Replenish boost resets when striking an enemy before the boost runs out.
- Plasmium Phial & Flea Brew do not consume any plasmium or flea brew when off a bench.
  - This was entirely accidental, but having to refill was an annoying game mechanic, so I am just leaving it in.
- Tool pouch capacity has been halved, rounding up as needed.

## Installation
1) Download a preconfigured modloader from ThunderStore: https://thunderstore.io/c/hollow-knight-silksong/p/BepInEx/BepInExPack_Silksong/
2) Find your Silksong installation folder:
   1) In Steam: Right-click Silksong -> Properties -> Installed Files -> Browse
3) Extract the contents of the BepInEx zip to this folder.
4) Open and close the game once
   1) This lets BepInEx generate some files it needs to insert mods/plugins
5) Download the `ReplenishTools.zip` file
6) Extract the zip file to `<Your Silksong Folder>/BepInEx/plugins`
7) Run the game.
