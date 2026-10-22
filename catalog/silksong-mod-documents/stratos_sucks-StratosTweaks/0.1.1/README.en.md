# Stratos Tweaks

Hollow Knight: Silksong plugin. Requires [BepInExPack Silksong](https://thunderstore.io/c/hollow-knight-silksong/p/silksong_modding/BepInExPack_Silksong/) and [ModMenu](https://thunderstore.io/c/hollow-knight-silksong/p/silksong_modding/ModMenu/).

Configure from **Options → Mods → Stratos Tweaks**.

- **Enable Scene Injection** — chibi replacements (Bellhart, Bone Bottom, Songclave). Off by default.
- **Tutorial Text** — Tut_01 overlay. On by default; does not follow scene injection.
- **Enable Position Tracking** — CSV logging (on by default)

Advanced Addressables paths are hardcoded in the plugin (not shown in ModMenu).

## Position tracking

Events in each session CSV (`utc,event,scene,x,y,z`):

| Event | When |
|--------|------|
| `enter` | Walked into a room (recent movement before scene change) |
| `bench_warp` | Benchwarp bench/respawn warp (requires Benchwarp installed) |
| `door_warp` | Benchwarp door transition warp (requires Benchwarp installed) |
| `warp` | Other teleport — heuristic fallback, or large same-room jump |
| `exit` | Left a room |
| `sample` | Periodic position while in a room |

For stitching rooms on a map, pair **`exit` → `enter`** and ignore **`warp` / `bench_warp` / `door_warp`** rows.

Tune in BepInEx cfg (`Tracking` section): `WarpDistance`, `MovementWindowSeconds`, `WalkThreshold`.
