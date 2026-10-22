# Silk Suture

**Bind healing, re-timed.** The moment a healing bind starts, you
receive **1 mask immediately** — the rest arrives when the bind
completes. Total healing always matches vanilla: Silk Suture changes
*when* you heal, never *how much*.

## Why?

In vanilla, an interrupted bind costs ALL your silk and gives nothing
back. With Silk Suture, the mask you received up front may save you from certain doom.

## Every situation handled

Not just the basic bind — every crest, tool, and status has correct,
conserved behavior (you can opt in/out of each via configs):

| Situation | Vanilla | Silk Suture |
|---|---|---|
| Normal bind | 3 at completion | **1 instantly + 2 at completion** |
| Multibinder | 2 + 2 | 1 instantly, then 1 + 2 (or 2 + 1, configurable) |
| Shaman crest | 3 | 1 instantly + 2 (incl. the mid-air held bind) |
| Architect crest (healing bind) | 3 | 1 instantly + 2 |
| Architect crest (crafting) | no heal | unchanged |
| Witch crest | lifesteal up to 3/4 | 1 instantly + lifesteal up to 2/3 |
| Beast crest | rage heals up to 3/4 | 1 instantly + rage up to 2/3 |
| Maggoted | cleanse only, 0 heal | 1 instantly + cleanse (the one opt-out bonus) |
| Maggoted + Multibinder | cleanse, then 2 | 1 + cleanse + 1 (total unchanged) |
| Lifeblood state | heals 1 | unchanged (lifeblood masks are their own system) |
| Cursed binds | no heal | unchanged |
| Interrupted bind | all silk lost, no heal | all silk lost, **keep the instant mask** |

Full details in the bundled `HEALING.md`.

## Configurable to taste

Every special case has its own switch, and every switch's off-position
is vanilla behavior: `WitchAdvance`, `BeastAdvance`, `ShamanAdvance`,
`MaggotAdvance`, `MaggotMultibindAdvance`, `LifebloodAdvance`, plus the
Multibinder split pattern. Config file:
`BepInEx/config/com.fabulator_k.silksuture.cfg` (generated on first
launch).

## Install

Use a mod manager, or manually: copy the `plugins/` contents into
`BepInEx/plugins/SilkSuture/`.

## Feedback welcome

This is a 0.5.x release: the mechanics are complete and every edge case
is handled, but a few of the rarer combinations have had limited field
testing. If anything heals a mask more or less than the table says —
especially **Maggoted + Multibinder** or the **Witch crest's glowing
"potential heal" masks** — please report it with your
`BepInEx/LogOutput.log` (it resets each launch, so grab it right after).

## Credits

- Mod Icon by **Fabulator_K**
- Built on [BepInEx](https://github.com/BepInEx/BepInEx) and the
  Silksong modding community's tooling.
