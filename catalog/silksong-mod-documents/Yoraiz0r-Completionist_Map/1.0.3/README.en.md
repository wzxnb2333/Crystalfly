# Completionist Map
Completionist Map is a Hollow Knight: Silksong mod that adds automatically tracked pins for almost all items, interactables & enemies on the map. Completionist Map is based on the game's data, with filters & other conveniences to help collect every item, journal entry, and stray rosary bead.

# Features
- Automatically tracked map pins for all content considered desirable.
- Map pins are categorized and can be toggled for visibility, and pinged for discoverability.
- Vast improvements to the map when used with the mouse, including smooth dragging, focus zoom, infinite zoom, expanded map borders, and pin placement on cursor position.
- Hornet's compass pin now animates at all times, for visibility. 
- Animated HUD pins that lead to rough positions of items without repeatedly checking the map.
- Immersive mode to reveal icons in areas as you explore.
- Humble attempt at matching pin visibility to game progression & Steel Soul mode.
- A variety of conveniences for different playstyles:
- Allow Hornet's compass pin to display without being equipped
- Reveal Flea pins early
- Allow map usage in the Abyss
- Journal completion preferences (Get one / Get full entry)
- Show completion percentiles on the save files early
- Debug Teleport onto every map pin or scene by right clicking them on the map (requires enabling first)

# Installation
1. Download [BepInEx](https://github.com/BepInEx/BepInEx)
   and [install it](https://docs.bepinex.dev/articles/user_guide/installation/index.html) to silksong's folder.
2. Download this mod's zip and extract the whole `CompletionistMap` folder into `Hollow Knight Silksong/BepInEx/plugins`. (So you have `Hollow Knight Silksong/BepInEx/plugins/CompletionistMap/CompletionistMapMod.dll` & accompanying json files) 
3. (Optional) Download and install the [BepInEx Configuration Manager](https://github.com/BepInEx/BepInEx.ConfigurationManager).
4. Run the game & enjoy! (Also, read about usage!)


# Upgrading
1. Remove the whole `Hollow Knight Silksong/BepInEx/plugins/CompletionistMap` folder.
2. Download this mod's latest zip and extract the whole `CompletionistMap` folder into `Hollow Knight Silksong/BepInEx/plugins`. (So you have `Hollow Knight Silksong/BepInEx/plugins/CompletionistMap/CompletionistMapMod.dll` & accompanying json files) 

# Usage
Once you load Silksong with completionist map, it may take a few seconds until it appears. While it loads you may see "Loading completionist map..." on the top left when the map is open. For most users it will finish loading before you enter your save. (The BepInEx console will say when that is done)

## Categories
![Categories](https://files.catbox.moe/0hmeyh.webp)
Once loading is over, a list of categories will appear on the left of the detailed map screen. 
* Hover: See cateogry name.
* Left click: Show/hide category.
* Left hold & drag: Show/hide multiple categories at once.
* Right click: Ping collectable pins of this category.
* Middle click: Ping all pins of this category. (including collected / unobtainable missed pins)

## Map pins
![map pins](https://files.catbox.moe/be9vsy.webp)
Depending on your progress in the game, many pins will appear on the map.
* Hover: See pin name/details.
* Hold left shift: See advanced pin details.
* Right click (With Teleport option enabled): Teleport to pin location (You may get stuck in walls! Teleport somewhere else to resolve that!)

## HUD pins
![hud pins](https://files.catbox.moe/6a8i65.webp)
Whenever you enter a new room, or open the map/quickmap in a room that is not cleared out, a set of HUD pins will appear & guide you to their respective items. These hud pins will safely hide themselves after a customizable span of time, or whenever you interact with NPCs/other content.
* Issue: Because Silksong saves most things during room transitions, there may be cases where after collecting an item, the HUD pin stays. Simply leave the room after collecting to trigger the save.
* Issue: Some logic objects in silksong are actually outside the bounds of the map they operate in. I tried to manually correct and minimize those cases, but there may still be a few situations where a pin appears in an incorrect position on the map. The advanced details will say exactly where it is.

## Config
After running silksong with Completionist map, you'll be able to find the config file for completionist map at `Hollow Knight Silksong/BepInEx/config/yorai.completionistmap.cfg`. 
If you've opted to install the [BepInEx Configuration Manager](https://github.com/BepInEx/BepInEx.ConfigurationManager), then simply hit F1 in-game. All options are customizable in the menu.

### General
* `Area display limitation`: Allows limiting the area where completionist pins show to only your current area, instead of the whole map. 
* `Area display mode`: Controls whether Hornet needs to have gotten the map of an area, actually traversed it, or whether the pins are visible at all times regardless.
* `Journal entry display mode`: Controls whether the journal category counts entries as complete based on if they're discovered, or thoroughly researched.
* `Journal entry completion mode`: Controls whether the journal category will count all entries, or just entries required for the `True Hunter` achievement.

### Quality of life
* `Scene map markers enabled`: Enables/disables HUD pins entirely.
* `Scene map markers when entering scene` (v1.0.1+): Should HUD pins appear when entering a new scene? 
* `Allow scene map markers to animate`: (v1.0.1+): Should HUD pins animate towards their target?
* `Duration for scene map markers`: How long should HUD pins last before fading out? (0 = infinite, they'll still disappear when Hornet interacts with the environment)
* `Allow flea pins without items`: Whether to display flea pins before you've obtained them in the game. Useful to avoid backtracking in new runs.
* `Allow compass without items`: Whether to show Hornet's pin on the map without the compass equipped. 
* `Allow compass pin to animate` (v1.0.1+): Whether Hornet's compass pin should animate.
* `Map pin size`: (v1.0.1+): Size multiplier for pins in the detailed map.
* `Allow player pins to be placed on the mouse cursor` (v1.0.1+): Whether to move the player pin placement cursor onto the mouse cursor.
* `Allow map in abyss`: Whether to allow Hornet to view the map in the Abyss before certain events unfold.
* `Show save completion percentile`: Whether the main menu should show completion percentiles on your saves. Useful for keeping track of things early.

### Debugging
* `Allow teleporting`: Enables the usage of Right Click on a pin to teleport to it. Also works on scenes & vanilla pins. 
* `Ignore hide reasons`: Some pins may hide themselves because they're not currently obtainable, this will let you see them on the map so you can explore what went wrong
* `Show all map zone names`: Display zone names at all times. Useful for understanding certain data tests.

## Misc
* To make icons on the edges of the map easier to access, Completionist Map expands the detailed map borders.
* Because Silksong typically locks the mouse cursor to window center, Completionist Map frees it when the detailed map is shown.
* Hornet's custom pins can now be positioned using the mouse cursor, however, I've opted to keep the actions for placing / removing the pin untouched (keyboard based), this is still a work in progress.

# Issues 
* Completionist Map relies on silksong information collected from the assetbundles ahead of time, meaning if the game updates in the future, the new content will not appear on its own. 
* Some pins only appear after certain conditions have been met, but I've yet to figure out a way to relay that information nicely, currently. I've personally verified all pins can be collected and a save can reach 100%, but I'm working to reduce the earlygame frustartions (Tested without Steel Soul, though)
* Although the mod was written with localization in mind, the ability to localize entries is incomplete. Also, certain items that do have localizations are only partially loaded and as such fail to show their localized names. This is something I hope to remedy in the near future.

# Credits
* Completionist Map is written by Yoraiz0r
* The json file creation project relies on [AssetTools.NET](https://github.com/nesrak1/AssetsTools.NET) & [FSMExpress](https://github.com/nesrak1/FSMExpress/tree/skong) by [Nes](https://github.com/nesrak1) who has been incredibly helpful!
* A lot of the early research & info gathering was done using [UABEANext](https://github.com/nesrak1/UABEANext), also by Nes.
* Special thanks to the Hollow Knight Modding discord community for their assistance in figuring out the many weird issues presented by the data!