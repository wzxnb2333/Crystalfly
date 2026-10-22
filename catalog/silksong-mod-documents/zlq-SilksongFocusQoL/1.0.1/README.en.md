# Silksong Focus QoL

A mod for **Hollow Knight: Silksong** that improves window focus behavior.  
Automatically pauses and mutes the game when the window loses focus, and unpauses when focus is restored within specified time window.

## Features
- Pause the game when unfocused  
- Mute audio while unfocused  
- Auto-unpause when focus returns  
- Configurable time window for quick tab-outs
- Supports real-time configuration via **[Configuration Manager](https://www.nexusmods.com/hollowknightsilksong/mods/26)**
## Configuration
After the first launch, the configuration file is created: `BepInEx/config/com.zlq.silksongfocusqol.cfg`

You can modify the settings either manually in the `.cfg` file **(requires restarting the game)**, or use the **[Configuration Manager](https://www.nexusmods.com/hollowknightsilksong/mods/26)** to adjust settings **in real-time while the game is running**.

#### Example config file
```ini
## Settings file was created by plugin Silksong Focus QoL v1.0.1
## Plugin GUID: com.zlq.silksongfocusqol

[Settings]

## Mute audio when window loses focus
EnableAutoMute = true

## Pause game when window loses focus
EnableAutoPause = true

## Unpause when focus returns
EnableAutoUnpause = true

## Time window to auto-unpause
## 0 = always unpause when focus returns; >0 = only if refocus occurs within this many seconds
AutoUnpauseWindow = 3
```
## Installation

**Requirements:** [BepInEx 5](https://github.com/BepInEx/BepInEx/releases) is required for this mod.

1. In Steam, right-click **Hollow Knight: Silksong** → **Manage → Browse local files**.  
2. Download BepInEx `.zip` and extract its contents into the game folder.  
3. Launch the game once, then exit.  
4. Open the `BepInEx` folder in the game directory. You should see `plugins` and `config` folders — this confirms BepInEx loaded correctly.  
5. Place this mod's `.dll` file into `BepInEx/plugins`.  

For easier configuration in-game, you can also use BepInEx 5 with [Configuration Manager](https://www.nexusmods.com/hollowknightsilksong/mods/26) from Nexus Mods.


## Changelog

See the full changelog [here](https://github.com/zlq23/SilksongFocusQoL/blob/main/CHANGELOG.md).
