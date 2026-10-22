# Silk Suture — Changelog

## 0.5.1 (2026-07-26)

Packaging fix — no gameplay changes.

- The plugin ID changed to `com.fabulator_k.silksuture`. **Your settings
  will reset to defaults**: a fresh
  `BepInEx/config/com.fabulator_k.silksuture.cfg` is generated on
  launch, and the config file from 0.5.0 is left unused and can be
  deleted. Sorry for the churn — worth doing now rather than later.
- Documentation updated to reference the new config path.

## 0.5.0 (2026-07-26) — first public release

Bind healing re-timed: **1 mask the instant a healing bind starts**, the
remainder at completion. Totals match vanilla everywhere, so this
changes *when* you heal, not *how much*. This aims to mitigate some of
the frustrations with an interrupted bind.
Every crest, tool and status combination is handled and individually
configurable. See `HEALING.md` for the full behavior table.

**Highlights since the private tester builds:**

- Advance amount fixed at 1 mask (a configurable amount could exceed
  vanilla totals on the Witch/Beast heal-cap paths).
- Per-case toggles: `WitchAdvance`, `BeastAdvance`, `ShamanAdvance`,
  `MaggotAdvance`, `MaggotMultibindAdvance`, `LifebloodAdvance`, plus
  the Multibinder split pattern. Every toggle's off-state is vanilla.
- Maggoted binds heal 1 instead of vanilla's 0 (the one deliberate
  bonus, and it is optional); the Maggoted + Multibinder combination
  stays at the vanilla total instead of stacking.
- Witch HUD fix: the glowing "potential heal" masks could show one too
  few on the second and later witch binds of a session (visual only).
- Reload insurance: the mod re-verifies its changes after every scene
  load and re-applies them if the game rebuilt the bind state machine.
- Icon by Fabulator_K; `HEALING.md` behavior guide bundled.

**Known gaps:** two of the rarer combinations — Maggoted + Multibinder
totals, and the Witch HUD indicator count — are correct by design and
by code review, but have had limited in-game verification. Reports
welcome (include `BepInEx/LogOutput.log`).

## 0.4.6 (2026-07-19, tester build)

- Fixed the witch HUD showing one glowing mask too few on the second and
  later witch binds of a session (visual only — healing was correct).

## 0.4.5 (2026-07-19, tester build)

- Advance amount fixed at 1 mask; `ImmediateHeal` config removed.
- New `ShamanAdvance` toggle (default on), incl. the mid-air held bind.
- Maggoted + Beast: instant mask granted, no rage activation (vanilla).
- Reload insurance after every scene load.
- `HEALING.md` bundled.

## 0.4.2 – 0.4.4 (2026-07-19, internal)

- Maggoted + Multibinder conserved advance; `MaggotAdvance` added, then
  its balance corrected: the +1 on a maggoted bind is intended, only the
  Multibinder combination stacking to 3 was too strong.
- `LifebloodAdvance` added (default off — lifeblood stays vanilla).

## 0.4.1 (2026-07-19, tester build)

- The advance model: instant mask deducted from the bind's own healing
  budget, with Witch and Beast handled through their C# heal caps.
  Fixed from the earliest prototypes: Multibinder totals, lifeblood and
  witch over-heals, the Architect heal-bind shortfall, a free heal while
  maggoted, and a Beast rage-cap nerf that gave nothing back.

## 0.2.0 – 0.3.0 — internal prototypes
