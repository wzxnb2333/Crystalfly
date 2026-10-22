# MonorReaperBalance

Rebalances the **Reaper crest**'s bind-empowered state ("Reaper mode") only.
Other crests are untouched.

Idea by **Monor** (`monor0365` on Discord) — this mod implements their
original rebalance proposal.

## What it changes

- **Reaper mode no longer ends when you take a hit** — optionally, a hit
  instead shaves seconds off the remaining duration (the same pattern the
  game itself uses for Warrior rage).
- **Duration is 7.5 s by default** (vanilla: 10 s), configurable from
  2.5 to 15 s.
- **Minimum 2 silk bundles per hit dealt** while in Reaper mode
  (vanilla rolls 1–3; with the min-2 clamp the effective table becomes
  2 @ 85%, 3 @ 15%).

## Configuration

All settings are in `BepInEx/config/io.github.fabulatork.monorreaperbalance.cfg`
and are live-tunable mid-game via
[ConfigurationManager](https://thunderstore.io/c/hollow-knight-silksong/p/jakobhellermann/BepInExConfigurationManager/)
(F1 menu — each setting renders as a slider plus an editable number box).

| Setting | Default | Range | Effect |
| --- | --- | --- | --- |
| `ModeDurationSeconds` | 7.5 | 2.5–15 | Reaper mode length in seconds (mod default: 7.5, vanilla: 10). Values snap to 0.1 s steps. |
| `PersistThroughHits` | true | — | Taking a hit no longer ends Reaper mode. |
| `HitDurationPenalty` | 0 | 0–5 | Seconds removed from remaining Reaper time per hit taken. 0 = no penalty. Only applies while `PersistThroughHits` is on. |
| `MinOrbsPerHit` | 2 | 1–3 | Minimum silk bundles per hit while in Reaper mode (vanilla rolls 1–3). |

## Credits

- **Idea:** Monor (`monor0365` on Discord) — original proposer of this rebalance.
- **Mod:** Fabulator_K
