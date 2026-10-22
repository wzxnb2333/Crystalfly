# Pantheon of Pharloom

A mod that lets you re-fight bosses and take on boss rush challenges similar to Godhome.  
It also includes useful features such as the ability to switch your nail upgrade and certain abilities directly from the mod’s UI menu.


**NOTE: Please read the changelog for updates in newer versions.**

If you run into any issues, easiest way to reach out would be to join the [Hollow Knight Modding](https://discord.com/servers/hollow-knight-modding-879125729936298015) discord server and look for the "Pantheon of Pharloom" thread.

---

## Manual Installation

1. Install [BepInEx](https://docs.bepinex.dev/articles/user_guide/installation/index.html#installing-bepinex-1).
2. Download **Pantheon of Pharloom** from **Thunderstore**.  
3. Unzip the downloaded file.  
4. Place the included `.dll` file into `BepInEx/plugins` folder.
    - **NOTE**: you will have to lauch the game once after installing BepInEx for the plugins folder to show up.
    - On Linux, you have to do some additional setup as shown [here for Bepinix](https://docs.bepinex.dev/articles/user_guide/installation/index.html?tabs=tabid-nix#tabpanel_bHGHmlrG6S-2_tabid-nix).

## How to Use

- After loading a save file, press **B** to toggle the Boss Rush Menu on or off.  
- From the menu, you can:
  - Select and re-fight individual bosses  
  - Start a boss rush sequence  
  - Adjust your nail upgrades and abilities
  - Bind nail, health, silk, skill, and tools.
      - Nail binding: Reduces nail damage by 40%.
      - Health binding: Reduces health to just 4 masks.
      - Silk binding: Reduces total silk to 9. (Enough for one bind)
      - Skill binding: Prevents skill use.
      - Tools binding: Prevents tool use.

## Important Notes

- I patch the save game routine to do nothing while the mod is active to prevent players from accidently messing up the save file (because the boss fights set a lot of flags/triggers). Therefore, you won't be do normal progression while mod is active for now.
    - **NOTE:** I'd still highly recommend making  a backup of your save file, just in case.
- For the best experience, use a **100% save file** (or one where you’ve already fought all bosses). 
  - You can’t currently re-fight bosses you haven’t defeated yet, but the boss rush should still be functional regardless if you've fought the boss already or not.
  - A 100% save file is included in the mod's zip file if manually downloaded.

---

## Customize Your Boss Rush [Optional]

This mods allows you to **customize boss encounters** using simple text files.


### Folder Structure

Place your text files inside the `boss_list` folder under your BepInEx plugins directory:

```
BepInEx/
├─ plugins/
│  ├─ PantheonOfPharloom.dll
│  └─ boss_list/
│     ├─ act1.txt
│     ├─ act2.txt
│     ├─ act3.txt
```

- `act1.txt` → Bosses for 1st boss rush.
- `act2.txt` → Bosses for 2nd boss rush.
- `act3.txt` → Bosses for 3rd boss rush.

* The final boss rush will always be the first three combined.
* All these files are optional and the mod will just fallback to the default list if not found in that path. This allows you to just change the order of just one boss rush and keep the others as default.

### Boss Rush File Format

- One boss per line
- Lines starting with `#` are ignored (comments)
- Empty lines are ignored
- **IMPORTANT NOTE**: The mod will break right now if specify invalid boss name or if you put a Bench at the very end of your boss rush.

**Example:**

```
# Custom Act 1 Bosses
Phantom
Bench
Widow
```

* This will create a boss rush: Phantom -> Bench -> Widow -> End.

### Boss List [SPOILER WARNING]

#### I. Judge

```
Moss Mother
Skull Tyrant
Bell Beast
Savage Beastfly
Fourth Chorus
Fire Lace
Bench
Double Conchfly
Moorwing
Sister Splinter
Widow
Phantom
Last Judge
```

#### II. Sinner

```
Garmond and Zaza
Cogwork Dancers
Trobbio
Chef Lugoli
Broodmother
Groal
Bench
Moss Mother 2
Single Conchfly
Zango
Voltvyrm
Savage Beastfly 2
Shakra
Bench
Father of Flame
Signis & Gron
Second Sentinel
Unravelled
Flower Lace
First Sinner
```

#### III. Void

```
Bell Eater
Crawfather
Gurr
Lost Garmond
Pinstress
Watcher
Tormented Trobbio
Bench
Palestag
Clover Dancer
Seth
Nyleth
Khann
Kramelita
Bench
Grandmother
Lost Lace
```

* Your must use these names exactly in your custom list. `Bench` is the bench scene in bellhome.

## Screenshots and Videos

[Quick Demo](https://youtu.be/ABDr8a4e1rc)

## Status

Planned additions:
- Additional UI polish and improvements  
- Boss rush Stats
- HUD display to show bindings
- Void version of fights in the final pantheon

---

## Feedback

If you encounter issues or bugs or have suggestions, please let me know.
