# SSMPEnemySync

An addon for [SSMP](https://github.com/Extremelyd1/SSMP) (Hollow Knight: Silksong multiplayer) that
makes enemies **shared between both players**.

One enemy. One pool of health. Either player can hit it, both hits come off the same health bar,
and it dies once for both of you. Bosses included.

Without this, each player fights their own private copy of every enemy, and only one of you can
actually kill anything.

## Requirements

- **[BepInEx for Silksong](https://thunderstore.io/c/hollow-knight-silksong/p/BepInEx/BepInExPack_Silksong/)**
- **[SSMP](https://github.com/Extremelyd1/SSMP) 0.3.1.0**

> **Both players need this addon, at the same version.** SSMP pairs networked addons by name and
> version, so a mismatched pair will not connect at all.

## Install

Install BepInEx and SSMP first, and confirm multiplayer works on its own.

Then drop `SSMPEnemySync.dll` into **the folder that already contains `SSMP.dll`**:

```
Hollow Knight Silksong/
└── BepInEx/plugins/
    └── SSMP/                 ← whatever folder your SSMP.dll lives in
        ├── SSMP.dll
        └── SSMPEnemySync.dll ← here
```

**That exact folder matters.** SSMP's addon loader only scans the directory its own assembly sits
in. Anywhere else and the addon is silently never loaded — no error, it simply does nothing.

If you install through a mod manager, check afterwards that the DLL ended up beside `SSMP.dll`
rather than in its own folder.

Start the game. The BepInEx log should show `[EnemySync] Initialised.`

## What it actually does

SSMP already ships a complete host/client entity system that replicates enemy position, animation,
FSM state and death. Three things stop it working in Silksong, and this addon supplies all three
from outside:

1. **It is switched off.** The calls that start the entity manager are commented out in
   `ClientManager`.
2. **Full synchronisation is off by default**, and it gates every entity packet handler on both
   ends. The menu toggle that used to set it is commented out too.
3. **The entity registry is Hollow Knight 1's**, so none of Silksong's enemies qualify as entities.

On top of that it adds health synchronisation, which the entity system never had.

### How health stays honest

The **scene host** is authoritative. Clients detect hits they landed, report them, and undo them
locally so nothing is counted twice; the host applies the damage and broadcasts the result.

Death is routed through the game's own death path rather than by zeroing health, so the correct
effects play, the fight's scripting is respected, and SSMP's own death replication sees it.

Health is re-broadcast periodically as well as on change, so a dropped update heals itself rather
than leaving one player looking at a stale bar.

## Uninstalling

Delete `SSMPEnemySync.dll`. SSMP returns to exactly its previous behaviour — this addon modifies
none of its files.

## Limitations

- **Tied to SSMP 0.3.1.0.** It reaches SSMP's internals by reflection, because its entity system is
  entirely `internal` and the addon API deliberately exposes nothing about entities. On a different
  SSMP build it disables itself at startup and logs the reason instead of misbehaving.
- **The enemy registry covers early-game enemies.** Enemies it does not recognise are not treated as
  entities at all, so they stay unsynced.

Source and issues: **https://github.com/faarisaahmed/SSMPEnemySync**
