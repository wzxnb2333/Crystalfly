# Accessibility Mod

Grants players speedrunning-tech speeds without performing the techniques by hand.

## Features

### Dive-dash Speed
While you have the dash ability and the Hunter's Crest equipped, sprinting on the ground accelerates you toward dive-dash top speed.

### Neutral Pogo Speed
Before you have the dash ability, walking on the ground grants the speed of the neutral-pogo technique.

The two effects are mutually exclusive: Neutral Pogo Speed applies before dash. Dive-dash speed applies after.

## Configuration

All settings are saved to the standard BepInEx config file `BepInEx/config/fire.AccessibilityMod.cfg` and can be edited there with any text editor.

| Section | Key | Default | Meaning |
| --- | --- | --- | --- |
| `General` | `Enabled` | `true` | Master toggle. Turns the entire mod on or off. |
| `General` | `Pause Label Position` | `TopRight` | Pause panel corner: `TopRight`, `TopLeft`, `BottomRight`, or `BottomLeft`. |
| `Speed` | `Neutral Pogo Speed` | `0.25` | Boost added to ground run speed before you have dash (0 = off). |
| `Speed` | `Dive-dash Speed` | `0.23` | Boost added while sprinting after you have dash (0 = off). |
| `Hotkey` | `Speed Toggle Hotkey` | `false` | Enable a key that temporarily turns the speed boosts on/off. |
| `Hotkey` | `Key` | `None` | The key that performs the toggle. |

### On-screen HUD

A user interface exists to allow players to verify how this mod is being used for competitive bingo gameplay.

- **Pause-menu panel** (top-right by default; configurable): Shows speed and hotkey settings that differ from defaults. If everything is at default, displays "AccessibilityMod set to Default."
- **Hotkey toast** (bottom-right): When you press the speed-toggle hotkey, a brief confirmation message appears.
- **Master toggle**: If the mod's master `Enabled` toggle is off, these UI elements are hidden.

The pause panel automatically clears visible BingoUI counter rows at the bottom. With Benchwarp visible, it sits below the Page button at top right, or below the silk bar at top left. No extra settings are needed.

## Install

1. Install [BepInEx for Silksong](https://thunderstore.io/c/hollow-knight-silksong/).
2. Drop `fire.AccessibilityMod.dll` into `BepInEx/plugins/`.
3. Launch the game once to generate the config.

## Request Form

If you have any glitch reports, feature requests, accessibility concerns, or anything else you want to report: [use this form](https://forms.gle/6cw29RaCm2vBZrXa9).

## License

PolyForm Noncommercial License 1.0.0. Free to use, modify, and share for noncommercial purposes; selling and sublicensing are not permitted. LICENSE information is included in the package.
