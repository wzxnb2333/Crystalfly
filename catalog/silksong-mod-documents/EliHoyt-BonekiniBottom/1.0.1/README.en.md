Replaces some Bone Bottom tents with Bikini Bottom themed tents.

Utilizes a utility created by [Su4enka](https://next.nexusmods.com/profile/Su4enka/mods?gameId=8136) which allows replacement of background images. Bundled into this mod with permission. Big thanks to them, please check out their mods!


There can be only one folder with custom sprites in \Mods\Customizer when using Customizer\CustomizerT2D plugin. 

Installation:

1. Download BepInEx 5 with Configuration Manager -> extract into root game folder -> run the game and exit (a BepInEx folder will appear).

2. Download CustomizerT2D plugin from this mod page. Put into same \plugins\ folder like so:
\Hollow Knight Silksong\BepInEx\plugins\CustomizerT2D\CustomizerT2D.dll

The CustomizerT2D plugin replaces Texture2D (including sactx atlases; matching ignores the final
-HEX), while the original Customizer continues to handle Collections. 
The plugin applies replacements once on the main menu screen and then uses a
short watch window on the first entry into a save. This minimizes
micro-stutters while still catching late-loaded textures (scripted
animations, HUD).

3. Put the BonekiniBottom folder into 
\Hollow Knight Silksong_Data\Mods\Customizer
(create the Mods/Customizer folders if doesn’t exist)
Like so:
\Hollow Knight Silksong\Hollow Knight Silksong_Data\Mods\Customizer\BonekiniBottom\Texture2D\....pngs


Hollow Knight Silksong\
├─ BepInEx\
│  └─ plugins\
│     └─ CustomizerT2D\
│        └─ CustomizerT2D.dll      (from this page)
└─ Hollow Knight Silksong_Data\
   └─ Mods\
      └─ Customizer\
         └─ BonekiniBottom\            (pack folder or include into existing one, there can be only one folder)
            ├─ Texture2D\          (PNGs for Texture2D)

If already using custom sprites throw Texture2D folder into your modpack folder \Mods\Customizer\<yourpack, CuteHornet for
e.p.>\Texture2D\....pngs 