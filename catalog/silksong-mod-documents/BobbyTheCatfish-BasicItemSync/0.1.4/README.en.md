# BasicItemSync
Syncs items and other key completion between players in SSMP.
See the [commands section](#commands) for a list of all the things this mod syncs.

> This mod is very much in beta. Please report bugs here: https://github.com/BobbyTheCatfish/Silksong-BasicItemSync/issues

## IMPORTANT USAGE NOTE
BasicItemSync currently only supports completely synchronous playthroughs.
This means that **all players need to start from completely fresh save files**, and **new players should not join** midway through a playthrough.
Items that are obtained will only be sent to players currently connected to the server. **They will not be re-sent for new players.**

## Installation
BasicItemSync can be automatically installed by any Thunderstore compatible mod manager.

### Manual Installation
1) Install [SSMP](https://thunderstore.io/c/hollow-knight-silksong/p/SSMP/SSMP) and [AssetHelper](https://thunderstore.io/c/hollow-knight-silksong/p/silksong_modding/AssetHelper/).
2) Download and extract BasicItemSync
3) Navigate to your BepInEx plugins folder
4) Copy the extracted mod folder to the plugins folder (Example: `plugins/BasicItemSync/BasicItemSync.dll`)


## Commands
```
/sync-ui
```
Shows a graphical interface, equivalent to `/sync`, allowing multiple settings to be set at once.

```
/sync [module] [true/false]
```
Enables or disables syncing one of the following modules. Unless specified, modules are on by default.
- Abilities
	- Movement
	- Needolin
	- Needle Strike
- Maps
- Pins
	- Shakra Pins
	- Flea Pins
- Currency
	- Shell Shards
	- Rosaries
	- Chests
	- NOT gains from cocoon
- SpendingCurrency (Default: false)
	- All money spent in Shops and Donations
	- Snitchflies
	- NOT losses on death
- Quests
	- Quest completion
	- Quest rewards
- QuestItems (Default: false)
- Bosses
	- + Void Masses
- Arenas
- Progression
	- Key Items (Keys, Melodies, etc.)
	- Bellshrines
	- Toll Benches
	- Important NPCs
- Fleas
- Collectables
	- Common Items
	- Relics
	- Mementos
	- Bellhome Decor
- Upgrades
	- Masks
	- Silk Spools
	- Silk Hearts
	- Crafting Kits
	- Tool Pouches
	- Needle Upgrades
- Tools
	- Colored tools
	- Silk Skills
- Crests
	- Vesticrest
- Transit
	- Bellways
	- Ventricas
- Shortcuts
	- Levers
	- Pressure Plates
	- Breakable Walls