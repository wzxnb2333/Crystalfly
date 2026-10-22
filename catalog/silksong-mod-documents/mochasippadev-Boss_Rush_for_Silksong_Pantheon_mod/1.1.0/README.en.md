# Boss Rush for Silksong (Pantheon mod)

Adds a Pantheon-style boss rush menu to Silksong!
Play through all the game's bosses, each act, or make a custom list!

The Boss Rush Menu can be accessed from the main menu.

Choose between Full game mode, Acts 1, 2, or 3 or a custom list. Mandatory only or Mandatory + Optional.

Make a custom starter loadout for Hornet. Modify her tools, crest, needle, mask count and silk meter!

Includes lots of customization options, like disabling Groal's enemy preamble or healing your masks after every boss!

A statistics screen at the end shows you how well you did on your run. Includes damage dealt, binds used and more!

Includes an optional speedrun timer! Can you beat your best time?

## Getting started

Install with a Thunderstore mod manager, start the game and choose **Boss Rush** on the main menu.

## Modes

- **Boss Rush**: bosses back to back. Pick Act 1, Act 2, Act 3 or the Full Game, mandatory bosses only or the optional
  ones too, or build your own order. A run saves its progress as you go, so you can leave and pick **Continue** later.
- **Single Boss**: choose one boss from the list and fight just that one.

All 49 bosses are in, from Act 1 to Act 3.

## Your loadout

Choose what Hornet starts with: masks, silk, silk hearts, needle upgrade, tool pouch and crafting kit, Vesticrests,
crest and tools, and abilities. Set **Loadout Changes** to **Before each boss** to change it between fights.

## Difficulty

- **Boss HP** from 0.1x to 5.0x. At 2.0x every hit on a boss does half damage. Fights with several phases or several
  bosses scale the same way. (Father of the Flame isn't affected.)
- **Extra damage taken**: +1 to +4 masks on every enemy hit. Spikes and other hazards are unchanged.
- **One-hit death**: any hit kills Hornet. Magnetite Dice, Fractured Mask and Warding Bell can't save her.

## Options

- Heal masks and refill silk between bosses
- Speedrun timer: counts only while you're fighting, not loads or cutscenes
- On death: end the run, end just that fight, or retry the boss
- Skip the waves of enemies that come before some bosses

## Results

At the end of a run you get the speedrun time and real time, deaths, damage dealt and taken, hits taken, binds and silk
gained, a per-boss table, and a history of past runs. Runs that used a skip or were continued after quitting are marked.

## If a fight breaks

Bosses run on the game's own scripts, and a fight can occasionally get stuck when it's played out of its usual order.
Press **F8** during the fight to skip that boss (the skip is marked on the results). The pause menu shows a reminder.
Bug reports are welcome: please include `BepInEx/LogOutput.log`.

## Your save is safe

- A run plays on a fresh save that exists only in memory. While a run is on, nothing is written to your save slots,
  achievements or Steam Cloud.
- Outside Boss Rush the mod switches itself off and removes all its changes, so the normal game plays exactly as it
  does without it.
- Boss Rush keeps its own data (loadout, saved run, history) in `BepInEx/config/BossRush`.

## Languages

English, Français, Deutsch, Español, Italiano, Português, Русский, 日本語, 한국어, 简体中文 and 繁體中文. The mod follows the
game's language setting and uses the game's own names for bosses, items and abilities.

## Manual install

Needs BepInEx 5 ([BepInExPack Silksong](https://thunderstore.io/c/hollow-knight-silksong/p/silksong_modding/BepInExPack_Silksong/)).
Put `BossRush.dll` in `BepInEx/plugins/BossRush/`.

Built for Silksong 1.0.30000.
