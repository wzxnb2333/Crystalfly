# Changelog

## 1.1.1

Target version: `1.1.1`.

### Added

- Added the independent `CrestComponentLab` Needleforge test mod with genuinely inert attack
  defaults and separate core moveset, dash slash, charge slash, normal Bind, and Up-Bind provider
  configuration.
- Added clean-profile installation, static anti-dependency verification, and a focused component
  isolation gameplay matrix.

- Added an experimental crest-composition foundation with feature descriptors, recipes, dependency/conflict validation, structured diagnostics, and reverse-order rollback transactions.
- Added an evidence-backed vanilla feature catalog covering standard attack slots, animation/audio profiles, and the currently understood Hunter, Wanderer, Reaper, Beast, Shaman, and Witch behaviors.
- Added a reusable Mono.Cecil inspection tool and generated managed-IL inventory for crest state, config fields, and crest-dependent call sites.
- Added runtime config-group snapshots that distinguish the physical crest, active provider, transplanted host group, and current group by object identity.
- Added reverse-engineering and framework architecture documentation plus framework-only tests.

### Fixed

- Changed Void's Base Crest switcher to display Architect, Shaman, Beast, and Witch while storing the provider IDs required by Void Crest 0.4.4. Cursed is no longer selectable or treated as Witch identity; it remains only as the base mod's private Witch-family bootstrap before the real Witch/Whip runtime provider is applied.
- Replaced the abstract `ToolBase.IsEquipped` Harmony target with its concrete `ToolCrest`
  implementation so the lab loads without `BadImageFormatException`.
- Prevented the `None` core from falling through to Hunter downslash or leaving null slash/damage
  components that spam `HeroController.Attack` exceptions.
- Added live configuration detection with atomic, bench-entry/bench-exit gated recipe activation.
- Preinstalled the Special Bind FSM route so its provider can change live at the same gate.
- Removed a forgotten planning statement.

## 1.1.0

### Added

- Added the independent `CrestComponentLab` Needleforge test mod with genuinely inert attack
  defaults and separate core moveset, dash slash, charge slash, normal Bind, and Up-Bind provider
  configuration.
- Added clean-profile installation, static anti-dependency verification, and a focused component
  isolation gameplay matrix.

- Added an experimental crest-composition foundation with feature descriptors, recipes, dependency/conflict validation, structured diagnostics, and reverse-order rollback transactions.
- Added an evidence-backed vanilla feature catalog covering standard attack slots, animation/audio profiles, and the currently understood Hunter, Wanderer, Reaper, Beast, Shaman, and Witch behaviors.
- Added a reusable Mono.Cecil inspection tool and generated managed-IL inventory for crest state, config fields, and crest-dependent call sites.
- Added runtime config-group snapshots that distinguish the physical crest, active provider, transplanted host group, and current group by object identity.
- Added reverse-engineering and framework architecture documentation plus framework-only tests.

### Fixed

- Changed Void's Base Crest switcher to display Architect, Shaman, Beast, and Witch while storing the provider IDs required by Void Crest 0.4.4. Cursed is no longer selectable or treated as Witch identity; it remains only as the base mod's private Witch-family bootstrap before the real Witch/Whip runtime provider is applied.
- Replaced the abstract `ToolBase.IsEquipped` Harmony target with its concrete `ToolCrest`
  implementation so the lab loads without `BadImageFormatException`.
- Prevented the `None` core from falling through to Hunter downslash or leaving null slash/damage
  components that spam `HeroController.Attack` exceptions.
- Added live configuration detection with atomic, bench-entry/bench-exit gated recipe activation.
- Preinstalled the Special Bind FSM route so its provider can change live at the same gate.

## 1.0.0

- Added complete Base Crest attack-set transplantation with safe bench-gated switching.
- Restored Hunter Focus scaling, Wanderer critical chance, Reaper harvest conversion, Warrior Rage, and Shaman spell/Bind behavior while Void Crest is equipped.
- Restored compatible Charge Slash, Challenge, voice, growl, attack, and Bind audio routes.
- Fixed Shadow Dash animation fallback, transplanted attack hitboxes, Charge Slash tinting, crest persistence, and Void Bind invincibility/healing conflicts.
- Added ModMenu controls, gameplay debug options, and diagnostic logging.
- Added Erymanthis's redesigned Void Crest artwork and six-slot layout.
- Fixed red tools and Silk Skills requiring a specific insertion order; up to three can now use any Void slot while Cast priority follows the Up, Neutral, and Down center spine.
- Fixed Void Crest's Orange Hornet Model Shader only applying reliably at save load and failing to turn off during gameplay; live changes now preserve the independently configured Orange Aura.
