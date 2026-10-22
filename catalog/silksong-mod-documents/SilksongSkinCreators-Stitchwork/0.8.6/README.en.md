# About Stitchwork

>forked and maintained by [FabulatorK](https://github.com/FabulatorK)

## Quick Introduction

Stitchwork is a robust asset replacement mod and creator enhancement tool kit.\
(It helps with making and loading skins, similar to using resource packs in Minecraft.)\
It's a humble fork of **[Patchwork](https://github.com/Ashiepaws/silksong-patchwork/)** by **[Ashiepaws](https://github.com/Ashiepaws)**, to whom this project owes great gratitude.\
Most features in this project are built from prior feedback for Patchwork and collaborations with skin creators.

Want to play the game with a skin?\
Install Stitchwork and at least one skin pack, then open the Pack Manager GUI in game with hotkey `1` (alpha 1, not numpad) to manage which skin pack to activate (or disable).\
The skin packs you designate will load after a scene reload in-game without having to restart the game process.

If you'd like to make a skin for the game using Stitchwork, please refer to the [wiki section](https://thunderstore.io/c/hollow-knight-silksong/p/SilksongSkinCreators/Stitchwork/wiki) on this mod page.

## Install the Mod

- **Mod managers:**\
  We recommend [Cogfly](https://github.com/Nix-main/Cogfly), [Gale](https://thunderstore.io/c/hollow-knight-silksong/p/Kesomannen/GaleModManager/), or [r2modman](https://thunderstore.io/c/hollow-knight-silksong/p/ebkr/r2modman/), make a profile in the manager, and install `Stitchwork` from search.\
  Consult the respective mod manager pages for detailed guides.
- **Manual:**\
  Put `Stitchwork.dll` from the downloaded and extracted file into `BepInEx/plugins/StitchworkRoot`.\
  **That's the whole install** — since 0.8.1 the shader bundle and localization
  tables ride inside the DLL, albeit a restart might be required for first-time users.\
  (Companion files beside the DLL still work and take precedence if present, they are still shipped in the mod package as `stitchwork.assetbundle` and the `Lang/` folder.)

## Patchwork by Ashiepaws

[Patchwork](https://github.com/Ashiepaws/silksong-patchwork/) pioneered live asset replacement for *Hollow Knight: Silksong* — sprites, audio, text, video, all swapped on the fly without touching a game file.\
It defined a new granularity standard of asset replacement mods at a per-sprite level, this helps skins survive game updates better and more controls for artists over their work.\
Stitchwork inherited its loading model, its pack idea, and its
*replace-don't-modify* approach, and ideas from Ashiepaws continue to shape
the roadmap to 1.0 and beyond.

Two behaviors follow from that lineage:

- **Patchwork packs load unchanged.**\
  Stitchwork discovers `Patchwork/` folders exactly as it discovers its own — a pack authored for the
  original framework needs no re-authoring.
- **If Patchwork itself is installed, Stitchwork goes inert** for the
  session rather than fight over the same game hooks.

As of **version 0.8.4**, old skin packs made for Customizer mods can be discovered by Stitchwork without moving from their existing install path (`.../Hollow Knight Silksong/Hollow Knight Silksong_Data/Mods/Customizer`), they are properly tagged with the "Customizer" badge.\
In addition, Customizer skin packs downloaded into the `BepInEx/plugins` folder by mod managers are recognized and they wear the "wayward" badge in the Pack Manager GUI.

As of **version 0.8.6**, Stitchwork/Patchwork skin packs can be dropped into the `<GameRoot>/Packs` folder in their normal shape (`<GameRoot>/Packs/<PackName>/Stitchwork/...`) and are discovered globally. This helps users who juggle multiple profiles with a mod manager.

### Where to report problems

Stitchwork is a separate project from Patchwork.\
**Do not submit Stitchwork issues to Patchwork's GitHub page.**\
Ashiepaws does not maintain this fork, and a report there does not reach us.\
Report Stitchwork problems through the Stitchwork channels:\
Find the `#Stitchwork` channel in the community modding Discord server, [Hollow Knight Modding](https://discord.gg/6KRPJCQFg).\
(You may need to assign yourself necessary roles in `#Channels&Roles`.)\
Or open a support post in the `#silk-support` channel with Stitchwork in the title of your post.\
Please include your `BepInEx/LogOutput.log` file in your post and provide as much relevant information as you can.

## What the fork adds

On that foundation, Stitchwork consolidates some of the core mechanics and builds out its own **creator toolset**: the Dev
Hub (find any asset by pointing at it in the game world), the animation
console and onion-skin canvas workflow, per-clip animation art (Appliqué) to bypass reused animation frames across clips,
the Pack Doctor, boot health checks, interface localization for simplified Chinese, and more.

## New in 0.8.6

- A pack apply no longer freezes the game as much.\
A progress bar at the top of the screen shows the sprite reload process.
- Animation canvas: extract or compile every listed clip in one batch.\
A progress bar shows the batch.
- Export GIF: the Whole Clip panel writes an animated GIF of the animation clip with your art.
- Global packs folder: put packs in `<GameRoot>/Packs/`.\
Every mod manager profile sees them.
- A short toast message appears when a saved file is applied.\
It also tells you when a sprite file was not applied.
- Text Merger: Push writes resolved text into the pack.\
Pull writes it into the working folder.
- Particle textures can be edited from the T2D browser.
- Video: replaced cinematics no longer chop the audio.\
Team Cherry's color grading over a replaced video is skipped.\
Both have config options.
- Fonts: `FontFallbackScale` and the `@s` file name tag set the fallback glyph size.
- T2D: sheet-to-sprite conversion is fixed. Sheet replacement pairs atlas pages by index.\
The browser selection and preview stay correct after scene changes and reloads.
- Dev Hub toggles are now buttons with a clear on and off state.
- (WIP) `StitchworkApi` adds the instance-skins contract for other mods. (The mechanism comes later.)

## New in 0.8.5

- Black screen fix. Version 0.8.4 could show a black screen at startup on some game builds. Fixed.
- Skins work on macOS and Linux. Rotated sprites no longer need a platform-specific shader.
- Animation canvas: compile skips the frames you did not change. Replaced files are backed up first.
- Audio: a log warning names a replacement clip with a different sample rate, and the fix.
- Log writing pauses while a cinematic plays. Replaced cutscenes play with less stutter. Config: `SuppressLogDuringCinematics`.
- World Pick: particle systems are opt-in. A pick logs the object's draw order.
- Appliqué: the index line reports the clip override count, split by pack and workspace.
- The install root is logged at startup. Packs with a deeper folder layout are no longer adopted twice. The sprite editor opens your existing file in instance-specific folders.

## Wiki

There is a wiki section on the mod page providing essential information with text only.\
The bundled HTML wiki pages are in progress.\
Since there are new features, wiki contents are being adjusted to reflect the current state of the mod.

## Credits

Beyond its predecessor, Stitchwork is shaped by many hands:

- The **skin creators and testers** whose packs, bug reports, and
  screenshots drove nearly every fix and feature in this mod — the change
  history is a conversation with them.
- The **localization helpers**: the Chinese terminology work that the
  in-game glossary and the wiki's zh pages are built on, and the German
  translation effort underway.
- Fellow toolmakers whose published work taught us techniques: **dakusan**
  (SilkDev — the world-pick projection approach), the **DebugMod** authors
  (cursor handling, and the pause workflow every creator uses), **YLTai**
  and **CarrieForle** (whose font mods shaped Stitchwork's font-handling
  boundaries), and **Custom Knight Creator** from the Hollow Knight era —
  the animation canvas's spiritual ancestor.
- The lineage before the lineage, inherited with Patchwork's own thanks:
  **Douglas Gregory** (the GPU performance work the tk2d sprite path is
  built on), **RatherChaoticDev** (Silksong Customizer, which Patchwork
  grew from), and **Su4enka** (CustomizerT2D, which shaped the T2D
  support Stitchwork extends).

### Special thanks

- To **MorningRain**.\
For localization assists, feedbacks, and moral support.\
Check out their incredible **[Nightmare Hornet](https://thunderstore.io/c/hollow-knight-silksong/p/MorningRain/NightmareHornet/)** skin project here.

- To **MeteoRa_** on Bilibili.\
They pushed the boundaries with the debug process and shaped experimental features, contributed greatly to the UI layout and mod icon design.\
They are working on a Second Sentinel skin and a Lies of P inspired skin.

- To **InkBlight(三只墨荒)** on Bilibili, whose Lace skin pack and Night Heron asset pack replace the Moorbugs.\
They helped improve the T2D support features of Stitchwork significantly with valuable findings and reports.

- To **Snek**, who offered contribution to technical documentation and promised localization of Stitchwork's wiki pack into the German language.
