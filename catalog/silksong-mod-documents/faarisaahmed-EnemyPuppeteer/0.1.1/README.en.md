# Enemy Puppeteer

> **Experimental.** A test rig, not a gameplay mod. Expect rough edges.

Take manual control of any enemy in Hollow Knight: Silksong — click one, walk it around, and fire
its real attacks yourself.

## Requirements

- **[BepInEx for Silksong](https://thunderstore.io/c/hollow-knight-silksong/p/BepInEx/BepInExPack_Silksong/)**
- **[Enemy Behavior API](https://github.com/faarisaahmed/enemy-behavior-api/releases)** — a hard
  requirement, not an optional extra

**Without the API this mod will not load at all.** It is a compile-time assembly reference, so
BepInEx fails to construct the plugin and logs an error; you will not see the window, and F8 will
do nothing. If that happens, check `BepInEx/LogOutput.log`.

The API is not on Thunderstore yet, so it has to be installed by hand: download
`EnemyBehaviorApi.dll` from its releases and drop it in `BepInEx/plugins/EnemyBehaviorApi/`.
BepInEx scans `plugins/` recursively, so any subfolder works.

## Install

Install BepInEx and the Enemy Behavior API first, then install this. Press **F8** in game.

## Use

| | |
|---|---|
| **Right-click** an enemy | select it |
| **Take control** | claim it |
| **Left-click** | move the marker — the enemy walks there |
| **Attack buttons** | fire that enemy's real attacks |
| **F8** | open/close, which also releases control |

Closing the window always hands the enemy back, so you can't strand one.

## Puppet mode

Taking control makes Hornet an **invisible marker**. She is invulnerable, cannot act, and a
crosshair shows where she is standing. The enemy walks to her; you decide everything else.

### It walks instead of sliding

Steering sets the enemy's **facing** and runs its own patrol walk — the unhurried back-and-forth
it does before noticing you. Only facing and whether the walk is running are driven; speed,
animation and ground handling stay with the enemy, so it moves like itself.

Which sign of scale means "facing right" is per-enemy art, so there's a **Facing** toggle if
something moonwalks.

### Only the enemy you control reacts

The marker is a real, targetable Hornet, so by default every enemy in the room converges on it.
**Bystanders: held** (the default) stops the others acting on her. They are not blinded — they
are prevented from choosing to chase or attack, which is a thing that can actually be guaranteed.

Only their decisions are suppressed, so a stray enemy can still be killed and still flinches.

## Attacks the game never shows you

Most enemies have states you will never see in normal play. This reads the FSM graph, not the play
history, so every state the developers authored is listed and firable.

They are mostly *reachable but conditionally gated* — caged variants whose trigger event only a
cage broadcasts, arena-specific setups, guards on distance or phase. Firing one bypasses the
guard, which is why an attack fired out of context sometimes looks broken: it was authored to run
only after something else set it up.

## Known rough edges

- Stationary enemies have no walk state; the UI says so rather than ignoring your clicks
- An attack can fire with nothing visible happening, if the classifier wrongly called a state an attack
- An obvious attack can be missing from the menu, for the same reason in reverse
- If an enemy under control will not die, that is the worst bug this can have — please report it

Source and issues: **https://github.com/faarisaahmed/enemy-puppeteer**
