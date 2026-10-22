# GlitchDebug

Glitched extensions for Silksong's DebugMod, for practicing All Glitches speedruns. 

_Normal users likely do not need this & should not install it,_ as it may cause unexpected behaviour.

## Features

- Adds duped & glitch storage to savestates:
  - Pogo Storage, Invulnerability, Noclip & Bench Storage all saved.
- Adds keybinds to toggle glitched states.
- Adds info panel entry for a complete list of all loaded scenes.

## Usage

GlitchDebug adds the following settings, editable in BepInEx config or ModMenu:
- Save Duped States: Enable this to enable the saving of duped savestates. Can also be accessed in Keybinds.
- Legacy Force Duped: Enable this to load duped states created in GlitchDebug v0.1. _This will be removed in future, so migrate your savestate packs!_

All functions are added to the bottom of the Keybinds panel:
- Toggle Bench Storage, Pogo Storage, Noclip
- Dupe/undupe active room & MMS dupe to bench
- Reset all scene data

## Credit
- Staxis & Peekagrub: Implementations for DebugMod v0.2 & v0.3
- Jamie: Update for DebugMod v1.0.3