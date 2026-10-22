# Void Crest Compatibility Patch

Expand Void Crest with functional base-crest movesets, restored crest mechanics, new Voidmass interactions, improved audio and animation support, safer crest switching, and in-game configuration.

Created by **Erymanthis** and published on Thunderstore under **ManthisCo**.

Source and releases: [github.com/Erymanthis/Void-Crest-Compatibility](https://github.com/Erymanthis/Void-Crest-Compatibility)

This is a compatibility and expansion patch. It is **not a standalone crest mod**.

## Credit and original mod

**DerVorce** is the creator of the original **Void Crest** mod on which this compatibility patch depends. Credit to DerVorce applies to their original mod, code, mechanics, and assets, including its Voidmass system, Shadow Dash, retaliation, Void Bind, Up-Bind special attack, Void Silk Skills, Void tool-slot system, recolor systems, and configuration.

The compatibility patch, its code, its redesigned crest artwork, and its six-slot visual layout are original work by **Erymanthis**. They are not assigned to or claimed as the property of DerVorce by this README. Each creator retains credit and ownership of their own contributions; no copyright is transferred by the credit language in this document.

Download and support the original mod here:

- [Void Crest by DerVorce on Nexus Mods](https://www.nexusmods.com/hollowknightsilksong/mods/978)
- [Void Crest by DerVorce on Thunderstore](https://thunderstore.io/c/hollow-knight-silksong/p/DerVorce/Void_Crest/)

Void Crest also credits VoidBaroness for Needleforge, Kaykao for development help, Entwined Spectre for the original mod's crest sprites, King Furgo for testing, and the Hollow Knight modding community. Those credits describe contributions to DerVorce's original release; they do not claim the replacement crest artwork included with this compatibility patch. Please visit the original mod page for DerVorce's complete credits and documentation.

## New features

### Redesigned Void Crest artwork and slot layout

The compatibility patch includes a new Void Crest design created for this release. It replaces the original in-menu crest sprite and silhouette presentation while continuing to use Void Crest's registered crest, Void color category, and all-colors-valid Void slots.

Its six Void slots use the following layout:

- top-middle: Cast + Up;
- center: Cast;
- bottom: Cast + Down;
- left, right, and lower-right: neutral Cast slots.

The replacement artwork and slot placement design are original contributions by **Erymanthis**, not assets credited or assigned to DerVorce or the original crest-sprite artist.

### Functional Base Crest selection

Void Crest can use another crest as the foundation for its regular attacks. This patch turns that setting into a complete gameplay system instead of copying only a basic attack configuration at startup.

Supported menu choices:

- Hunter
- Wanderer
- Reaper
- Architect
- Shaman
- Beast
- Witch
- Cloakless

The selected crest supplies its complete regular attack set, including applicable:

- normal and alternate slashes;
- up and alternate-up slashes;
- down and alternate-down attacks;
- wall attacks;
- Charge Slash;
- Taunt/Challenge Slash;
- crest-specific attack objects, animation data, and audio.

Void Crest's unique **Shadow Dash remains active**. This patch does not restore or add Dash Slash because Shadow Dash is its intended replacement.

Void's normal Bind and Up-Bind also remain the foundation of the crest. Base-crest Bind effects are restored only where the compatibility design explicitly calls for them.

### Hunter — Focus and Voidmass damage

Selecting Hunter automatically follows the player's real Hunter progression:

- base Hunter before either upgrade;
- Hunter v2 when the first evolution is unlocked;
- Hunter v3 when the final evolution is unlocked.

The real vanilla Hunter combo meter and Focus thresholds are used. Voidmass only increases eligible Nail damage while the correct Focus level is actually active.

- Above 2 Masks: each Voidmass adds 25% to the Voidmass damage multiplier.
- At 2 Masks or fewer: each Voidmass adds 35%.
- Tools, Void retaliation, and Void's special Up-Bind attack are excluded.
- Against recognized named bosses, the combined Hunter Focus and Voidmass result is capped at 3× base Nail damage.

Boss recognition supports normal game systems, boss-title and journal data, and community-recognized boss identities.

**Void Hunter currently recognizes bosses added or modified by Lost Sinner, Awakened Grand Mother Silk, and Lost and Chained. These boss mods are optional compatibility targets and are not dependencies.**

### Wanderer — Voidmass luck

Void Wanderer converts Voidmass into critical-hit chance while retaining Wanderer's vanilla requirements and luck handling.

- The normal Wanderer chance starts at 2%.
- Each Voidmass adds 3.1 percentage points.
- At 10 Voidmass, the chance is 33% before external luck modifiers.
- The vanilla 9-Silk requirement remains active.
- Magnetite Dice and other luck modifiers are applied once by the game rather than being accidentally multiplied twice.

### Reaper — Voidmass harvest

Void Reaper changes how Voidmass is earned:

- Being hit during Bind still causes retaliation damage.
- Bind retaliation no longer generates Voidmass while Reaper is selected.
- Completing a normal Void Bind activates Reaper's vanilla harvest mode.
- Reaper's normal Silk bundles are visually Voidified and become Voidmass orbs.
- The orbs grant no Silk.
- Each orb is worth 0.5 Voidmass.
- Two orbs complete one full Voidmass.
- A banked half unit appears as a half-opacity pip on the Voidmass spool.
- Orbs collected at the 10-Voidmass cap grant no additional resource.

Vanilla Reaper remains responsible for harvest duration, payout rolls, enemy eligibility, orb direction, fling speed, and Reap hit effects.

### Beast — Rage restoration

Completing a normal Void Bind with Beast selected activates vanilla Rage.

The patch restores the game's normal Rage systems, including:

- Rage duration and effects;
- Rage damage;
- Rage-hit healing and heal limits;
- Rage duration extensions from successful hits;
- Rage attack audio;
- Warrior-specific Bounce Pod behavior;
- Beast/Warrior growls and Challenge audio routing.

Void's special Up-Bind does not activate Rage.

### Shaman — spell bonus and Bind behavior

Selecting Shaman restores its vanilla Skill damage multiplier for Silk Skills while preserving Void Crest's own Silk Skill mechanics.

The grounded Shaman Bind route is also restored with Void-specific rules:

- it heals zero Masks, as intended for Void Crest;
- it receives 8 frames of recovery invincibility after the Bind ends;
- those recovery frames cannot trigger retaliation;
- Void's Up-Bind remains the Void sphere special rather than becoming Shaman's normal special Bind.

### Witch attack family

The player-facing Witch selection is connected to the game's internal Witch/Whip attack family where required for attacks and audio. Cursed is not exposed as a selectable base crest or treated as Witch identity. Void Crest 0.4.4 still receives its proven Cursed HeroConfig as a private bootstrap, after which this compatibility patch resolves and transplants the real Witch/Whip provider.

This restores the intended attack objects, recoil behavior, animation flag, Charge Slash, and attack audio without replacing Void's Bind with Witch roots.

### Architect and Cloakless

Architect restores its complete attack tree and wall-attack behavior. Cloakless can also provide its complete attack configuration.

This update does not add a new Voidmass passive or custom Bind overhaul for either selection.

## Quality of life and ModMenu compatibility

### Credit to ModMenu

In-game configuration support is provided through **Silksong.ModMenu** by the **silksong_modding** team.

- [ModMenu on Thunderstore](https://thunderstore.io/c/hollow-knight-silksong/p/silksong_modding/ModMenu/)
- [Silksong.ModMenu source on GitHub](https://github.com/silksong-modding/Silksong.ModMenu)

Thank you to the ModMenu contributors for providing the standard in-game configuration interface used by this patch.

### Base Crest menu

Void Crest's original Base Crest setting is converted from an error-prone free-text field into a scrollable in-game choice menu.

Older Hunter v2/v3 text values are presented as `Hunter`, which then follows the player's real Hunter progression. The menu presents `Toolmaster`, `Spell`, `Warrior`, and the private Cursed bootstrap as the in-game names `Architect`, `Shaman`, `Beast`, and `Witch`, while preserving the provider IDs required by Void Crest 0.4.4.

### Safe crest switching

Base Crest changes are bench-gated to protect active attack objects and save state.

- Change the option away from a bench: the new base is applied on the next bench rest.
- Change it while sitting at a bench: the new base is applied when leaving the bench.
- If a switch cannot be completed, the current moveset remains active and the requested change stays queued.

### Tint Hijacking

The `Tint Hijacking` option controls slash color only while Void Crest is equipped.

- **On:** Void controls the color of Nail slashes.
- **Off:** slashes use vanilla textures, the current Nail imbuement color, or the active skin mod's visuals.

Vanilla crests are untouched by this option.

### Debug and testing options

The patch includes optional test controls:

| Option | Purpose |
|---|---|
| Wanderer 50 Percent Crit Chance | Sets Void Wanderer's final chance to 50% without allowing Dice to multiply it again |
| Wanderer Luck Tracker | Displays Voidmass, readiness, raw/final chance, luck modifier, RNG rolls, crit count, observed rate, and the last result |
| Set Voidmass Count | Immediately sets live Voidmass from 0–10 and updates the Void spool |
| Set Masks to Critical Damage | One-shot Hunter test action that sets current red health to 2 Masks |
| Activate Focus Level | Sets the real Hunter combo meter to Off, Focus 1, or Focus 2 when that evolution is unlocked |
| Shaman Spell Damage Bonus | Enables or disables the restored Shaman Skill multiplier for testing |

The Wanderer panel hides automatically while paused or when Void Wanderer is not active.

### Audio restoration

Base-crest attack sounds are copied into the transplanted Void attack tree. The patch also restores the crest checks used by Charge Slash, Challenge, Beast growls, and other attack-audio state machines.

Void Dash and Void Bind audio remain owned by Void Crest.

### Skin and Nail imbuement support

When Tint Hijacking is Off, the patch updates live Slash, Downspike, and Charge Slash visuals across several animation frames. This helps skin mods and Nail imbuements keep their intended colors instead of being immediately repainted by Void's continuous tint system.

### Diagnostic logging

The BepInEx log records:

- selected and queued Base Crest changes;
- Hunter evolution resolution;
- source and transplanted attack objects;
- Charge Slash structure and animation components;
- audio sources copied or missing;
- restored crest/audio FSM branches;
- recognized bosses used by Hunter's damage cap;
- Reaper half/full Voidmass awards;
- debug actions and safe failures;
- caught compatibility errors and fallback behavior.

## Bug fixes

### Void tool and Silk Skill placement

- Fixed Void Crest requiring red tools and Silk Skills to be inserted in a specific slot configuration before their Cast bindings worked correctly.
- Up to three red tools and/or Silk Skills can now be equipped in any of the six Void slots without depending on insertion order.
- When those tools and skills are assigned Cast bindings, they take priority along the three-slot center spine: Cast + Up at the top, Cast in the center, and Cast + Down at the bottom.

### Base Crest persistence

- Fixed Void reverting to Hunter or the game-start crest after changing base crests.
- Fixed selected base-crest state being lost during `ResetAllCrestState` and `UpdateConfig`.
- Fixed live switches leaving the old attack tree active or selecting the wrong runtime config group.
- Fixed Hunter requiring separate internal v2/v3 config values.

### Attack and hitbox fixes

- Fixed transplanted downslashes using incompatible custom types or stale FSM events.
- Fixed Silk Skills losing hitboxes when Shaman's multiplier was applied too broadly.
- Fixed Charge Slash objects being omitted when the source attack lived outside the main crest hierarchy.
- Preserved Void's Shadow Dash object, invincibility, hitbox, and replacement of Dash Slash.

### Visual and animation fixes

- Fixed Void Crest's Orange Hornet Model Shader so it can be turned both on and off during gameplay without reloading the save.
- Fixed missing left/right Shadow Dash animations by safely falling back to normal Dash animation data.
- Fixed the fallback restarting at frame zero every update by preserving the requested Shadow Dash clip name.
- Fixed broken Downspike, extra-slash, and Charge Slash tint initialization.
- Fixed skin-mod combinations that could render Hornet or slash effects as static black shapes.

### Charge Slash and tint fixes

- Fixed Charge Slash tint not applying consistently to Spell/Shaman.
- Fixed tint state changing midway through Charge Slash initialization as far as the current per-crest handling allows.
- Fixed Tint Hijacking affecting vanilla crests.
- Fixed the Tint Hijacking Boolean behaving opposite to its displayed meaning.
- Fixed disabling Tint Hijacking failing to restore the active skin or Nail imbuement color.

### Audio fixes

- Fixed missing Charge Slash release sounds across transplanted crests.
- Fixed missing Hornet voice lines attached to Charge Slash routes.
- Fixed Beast/Warrior growls and Challenge sounds not recognizing Void's selected base crest.
- Fixed transplanted AudioSource settings and custom audio curves being lost.

### Bind fixes

- Fixed base-crest restoration breaking Void Bind invincibility.
- Fixed restored base Bind routes healing when Void should heal zero.
- Fixed Shaman recovery invincibility causing retaliation.
- Fixed Void's Up-Bind being replaced by unwanted Shaman, Witch, or Architect Bind behavior.
- Fixed normal Void Bind failing to activate Warrior Rage or Reaper harvest mode.

### Reaper fixes

- Fixed Reaper retaliation continuing to generate Voidmass after the harvest economy was moved to orbs.
- Fixed Reaper orbs continuing to award Silk.
- Fixed Reaper orb visuals remaining Silk-colored.
- Fixed pooled Void-colored bundles leaking into later vanilla Reaper usage.
- Fixed the banked 0.5 value having no visual representation on the Voidmass spool.

### Startup and scene safety

- The compatibility implementation avoids initializing Void Crest's color utility during the pre-menu loader.
- The half-pip correction runs in a normal late-frame update, preventing the startup failure that could interfere with room-entry/respawn placement.

## Conclusion

Void Crest Compatibility keeps DerVorce's original Void Crest mod as its required gameplay foundation while allowing the Base Crest setting to behave like a real gameplay branch.

DerVorce's mod still supplies Shadow Dash, retaliation, Void Bind, the Void Up-Bind sphere, Void Silk Skills, Voidmass, the Void spool, and the underlying Void tool-slot type. This patch supplies the replacement crest design and layout, moveset transplantation, vanilla crest recognition, branch mechanics, audio, animation, configuration, and safety work needed for those systems to coexist.

The project focuses on a stable playable foundation. Future versions may finish additional crest overhauls and address remaining compatibility bugs while continuing to credit each contributor only for their own work.

## Experimental crest-framework work

This repository now contains an experimental, non-runtime foundation for generalizing the compatibility work into a reusable crest-composition framework. It defines feature metadata, mix-and-match recipes, dependency validation, structured diagnostics, and a true rollback transaction before any of those APIs are wired into the released plugin.

- [Reverse-engineering evidence and current limits](docs/REVERSE_ENGINEERING_CREST_SYSTEM.md)
- [Framework architecture and migration plan](docs/CREST_FRAMEWORK_FOUNDATION.md)
- [Generated managed-IL crest inventory](docs/evidence/VANILLA_CREST_INVENTORY.md)
- [Gameplay validation matrix](docs/GAMEPLAY_VALIDATION_MATRIX.md)

These APIs are not stable yet and registration in the experimental catalog does not imply that a gameplay adapter has been implemented or verified.

## Technical explanations

### Version and audited dependencies

- Tested base implementation: Void Crest `0.4.4`
- Compatibility plugin ID: `io.github.erymanthis.voidcrestmovesetcompat`
- Void Crest plugin ID: `io.dervoce.voidcrest`
- Last published 1.0.0 SHA-256: `2FF64872574A228356D4CB2398C51F59FD7DB4ADC189C948ECD11068AD03C05E`

The compatibility plugin has hard BepInEx dependencies on Void Crest and Needleforge. It uses Silksong.ModMenu for the Base Crest choice element and in-game configuration presentation.

### What the compatibility layer does not replace

The following remain implemented by Void Crest:

- creation and registration of the Void crest;
- its HUD frame and Void color category;
- its all-colors-valid Void slot behavior;
- the Shadow Dash attack and retaliation hitbox;
- normal Bind duration and base retaliation damage;
- Bind invulnerability;
- Up-Bind sphere cost, scaling, damage, healing, and Voidmass consumption;
- Void Silk Skill cost and multiplier;
- Plasmium conversion;
- integer Voidmass storage and the cloned Void spool;
- Hornet and aura recolor settings;
- configured retaliation/dash tool synergies.

The compatibility layer intercepts only the behavior described in this README.

### Replacement crest artwork and slots

The compatibility DLL embeds the replacement crest artwork directly, so it does not depend on the Downloads folder, USB storage, or another loose image path at runtime. It replaces the live crest sprite, silhouette, and glow presentation after Void Crest registers its `CrestData`.

The patch repositions the six existing slots rather than creating a second set. Their `ToolItemType` remains Void. The vertical bindings are explicitly assigned as Up, Neutral, and Down from top to bottom; the three remaining slots are Neutral. Needleforge then receives the completed slot array and regenerated directional navigation.

### Moveset transplantation

Needleforge's `MovesetMaker.InitializeMoveset` is patched after initialization. When the initialized moveset is Void Crest's moveset, the compatibility plugin:

1. resolves the selected vanilla or Needleforge source crest;
2. finds its live `HeroController.ConfigGroup`;
3. clones its `HeroControllerConfig` into a `HeroConfigNeedleforge`;
4. clones the source attack root;
5. remaps Normal, Alternate, Up, Alternate Up, Down, Alternate Down, Wall, Charge, and Taunt attack references;
6. separately clones source objects that are outside the main attack root;
7. normalizes compatible Downspike/Slash downslash types;
8. copies attack AudioSource settings;
9. preserves Void Crest's Dash Slash FSM edit;
10. recreates and reconnects Void Crest's custom Shadow Dash as `DashStab`;
11. sets up and activates the completed replacement root;
12. retires the previous transplanted root.

Before `HeroController.Start`, Void's `ToolCrest.heroConfig` is pointed at the resolved base crest. After crest-state resets, the cloned compatibility config is restored. After `UpdateConfig`, the resolved live config group is forced back into place while Void is equipped.

### Functional crest-equipment proxy

Void is always the actually equipped crest. Inside a controlled set of vanilla methods:

- `ToolBase.IsEquipped` returns true for the selected base crest while Void is equipped;
- exact crest-ID comparisons treat Void and the selected base ID as equivalent;
- unrelated tools, other methods, and vanilla crest gameplay remain unchanged.

The targeted methods are:

- `HeroController.IsWandererLucky`
- `HeroController.Attack`
- `HeroController.NailHitEnemy`
- `HeroController.BindCompleted`
- `HeroController.Recoil(bool,bool)`
- `HeroController.RecoilDown`
- `HeroController.FallCheck`
- `DamageEnemies.DoDamage(GameObject,bool)`
- `HealthManager.TakeDamage(HitInstance)`
- `HeroAnimationController.UpdateToolEquipFlags`
- `SurfaceWaterRegion.OnTriggerEnter2D`
- `BouncePod.WillRespond`
- `BouncePod.Hit`

Postfixes also restore the results of `IsHunterCrestEquipped`, `IsArchitectCrestEquipped`, and `IsShamanCrestEquipped` for the matching selected base.

### Damage multiplier safety

The Shaman and Hunter additions temporarily modify `DamageEnemies.DamageMultiplier` immediately before damage calculation.

The exact original value is restored in both postfix and finalizer paths. This prevents a modified multiplier from leaking into pooled damage objects or later unrelated attacks, including when the original damage method throws an exception.

Void Crest's own Void Silk multiplier remains a separate `DamageStack` multiplier and is not replaced.

### Hunter calculation

The compatibility multiplier is:

```text
Normal health:   1 + (Voidmass × 0.25)
2 Masks or less: 1 + (Voidmass × 0.35)
```

It is applied only when vanilla Hunter Focus is active. Vanilla applies the Focus multiplier independently.

For a recognized boss, the compatibility multiplier is limited to:

```text
3 ÷ active vanilla Focus multiplier
```

This makes the combined Focus and Voidmass result no greater than 3× base Nail damage.

Boss detection uses `BossSceneController`, boss-title FSM actions, one-kill journal records, normalized community names, and exact identities for supported boss-overhaul mods. Recognized runtime instances are cached. Lost Sinner, Awakened Grand Mother Silk, and Lost and Chained are recognized when present but are not required or declared as dependencies.

### Wanderer calculation

The compatibility plugin replaces the chance getter only inside the Wanderer critical-hit calculation:

```text
raw chance = vanilla Wanderer chance + (Voidmass × 0.031)
final chance = raw chance × vanilla luck modifier
```

This produces 33% at 10 Voidmass before Dice/luck when the vanilla base is 2%.

The 50% debug option divides out the current luck modifier before returning its test chance, causing the game's later multiplication to land at an actual final 50%.

### Reaper orb conversion

Vanilla Reaper's payout logic still spawns `GlobalSettings.Gameplay.ReaperBundlePrefab` through `FlingUtils.SpawnAndFling`.

The compatibility plugin records only those exact prefab spawns and applies Void Crest's `Voidify` behavior to the resulting pooled objects. When the same pool is later used outside Void Reaper, `UnvoidifyRoot` restores the original appearance.

The bundle's PlayMaker FSM normally reaches `Get Silk`, where `CallMethodProper` invokes `HeroController.AddSilkParts`. The patch intercepts only that exact method/behavior combination on the Reaper bundle, awards 0.5 Voidmass, calls `Finish`, and skips the Silk call.

Void Crest stores Voidmass as an integer. Fractional Reaper state therefore uses:

- one runtime Boolean for a pending half;
- Void Crest's normal integer property after the second half;
- a temporary extra `SilkChunk` on `Spool_VoidVersion`;
- 50% sprite opacity for that final chunk;
- compatibility `LateUpdate` to reapply the opacity after Void Crest's continuous recolor pass.

The half is runtime-only and is not written to the save file.

### Reaper retaliation suppression

Void Crest's `HeroController.TakeDamage` prefix directly increases `voidspool.voidMass` during Bind before creating retaliation damage.

The compatibility prefix runs first and establishes a narrow guard only when:

- Void is equipped;
- Reaper is selected;
- Hornet is currently binding.

The next increasing write to the Voidmass property is changed to the current value, then the guard immediately clears. The rest of Void Crest's retaliation prefix continues normally, preserving damage, audio, hitbox, cooldown, and tool modifiers.

### Bind routing

The plugin tracks the transition into and out of `cState.isBinding` and separates a normal Bind from `VoidCrestPlugin.doingSpecialAttack`.

- Warrior and Reaper call vanilla `BindCompleted` after a normal Bind if their corresponding state is not already active.
- Shaman's recognized grounded normal route is enabled through its specific `Spell Control` FSM crest check.
- `HeroController.AddHealth(int)` is blocked only during that tracked Shaman grounded Bind.
- At Bind exit, the Shaman healing block is removed and an independent invincibility source is held for 8 rendered frames.
- Because recovery begins after binding is false, Void retaliation cannot run during it.
- Special Up-Bind is excluded from all three outcomes.

### Audio routing

AudioSources are copied by relative transform path and component index from each source attack object to its transplanted counterpart. Clip, mixer, flags, playback settings, 3D settings, distance/rolloff, and custom curves are copied.

`CheckIfCrestEquipped.IsTrue` is also post-processed only inside:

- `crestAttacksFSM`;
- `silkSpecialFSM`;
- the `Nail Arts` FSM;
- the recognized Shaman grounded route in `Spell Control`.

Arbitrary `spellControl` checks are intentionally not proxied. This prevents Witch roots, Architect Craft Bind, and unrelated Bind branches from replacing Void's Bind behavior.

### Animation fallback

While Void is equipped:

- `Shadow Dash` falls back to a clone of `Dash` when needed;
- `Shadow Dash Down` falls back to a clone of `Dash Down`.

The cloned clip is renamed to the originally requested Shadow Dash name. Returning a clip still named `Dash` would make the animation controller restart frame zero continuously because its requested animation name never appears to be playing.

### Tint implementation

When Tint Hijacking is Off, the desired slash color comes from the active `NailImbuementConfig.NailTintColor`, or white when no imbuement exists.

Retint hooks run on:

- `NailSlash.StartSlash`;
- `NailAttackBase.OnSlashStarting`;
- `NailAttackBase.SetNailImbuement`;
- `Downspike.StartSlash`;
- `HeroExtraNailSlash.OnEnable`.

A transient runtime component applies the color immediately and for 12 `LateUpdate` frames to survive animation and sprite initialization. It recolors child tk2d sprites and SpriteRenderers but rejects the Hero root.

Charge Slash roots are added to or removed from Void Crest's exemption arrays according to the current Tint Hijacking policy. Spell/Shaman receives special handling because its Charge Slash is initialized and retinted at different times than the other crest implementations.

### Orange Hornet shader live refresh

Void Crest already refreshes **Enable Orange Aura Shader** in both directions. Its separate **Enable Orange Hornet Model Shader** setting is normally sampled by the color monitor: turning it on can recolor Hornet during gameplay, but turning it off does not restore colors already changed by the monitor.

This patch subscribes to the Hornet shader setting while the compatibility plugin is loaded. When Void Crest is equipped:

- turning the setting on immediately asks Void Crest to apply its own color system to Hornet;
- turning the setting off immediately asks Void Crest to restore the colors it saved for Hornet's renderers;
- if the independently configured Orange Aura remains enabled, the patch reapplies it to `HeroLight` after restoring Hornet.

The handler is removed when the compatibility plugin unloads. It does not replace Void Crest's shader or alter the setting while another crest is equipped.

### Runtime-only state and reset rules

The compatibility plugin keeps runtime state for:

- active, observed, pending, and previous Base Crest choices;
- bench-enter and bench-exit application;
- normal/special Bind transitions;
- Shaman healing suppression and recovery protection;
- Reaper's banked half and displayed half-pip;
- the one-write Reaper retaliation guard;
- Wanderer tracker counters;
- Hunter debug selection;
- recognized boss instances;
- bounded diagnostic-log deduplication.

The Reaper half is cleared when Voidmass becomes 0, Voidmass reaches 10, debug Voidmass is set, Reaper becomes inactive, the base changes away from Reaper, or the plugin unloads.

### Harmony hook ledger

Version 1.1.0 contains 22 Harmony patch classes:

1. `ShamanNormalBindHealingPatch` → `HeroController.AddHealth(int)`
2. `MovesetMakerInitializePatch` → `MovesetMaker.InitializeMoveset`
3. `HeroControllerStartPreparePatch` → `HeroController.Start` prefix
4. `HeroControllerStartLogPatch` → `HeroController.Start` postfix
5. `HeroControllerResetAllCrestStatePatch` → `HeroController.ResetAllCrestState(bool)`
6. `HeroControllerUpdateConfigPatch` → `HeroController.UpdateConfig`
7. `VanillaBaseCrestFunctionPatch` → 13 targeted vanilla methods
8. `CrestDamageCompatibilityPatch` → `DamageEnemies.DoDamage(GameObject,bool)`
9. `ReaperBundlePickupPatch` → `CallMethodProper.OnEnter`
10. `ReaperBundleAppearancePatch` → `FlingUtils.SpawnAndFling`
11. `ReaperRetaliationVoidmassGuardPatch` → `HeroController.TakeDamage(...)`
12. `VoidmassResetClearsReaperFractionPatch` → `voidspool.voidMass` setter
13. `ReaperFractionalVoidmassSpoolPatch` → `SilkSpool.ChangeSilk`
14. `VanillaBaseCrestQueryPatch` → three vanilla crest-query methods
15. `CrestAttackAudioFsmCheckPatch` → `CheckIfCrestEquipped.IsTrue`
16. `HeroAnimationControllerGetClipPatch` → `HeroAnimationController.GetClip`
17. `NailSlashStartPatch` → `NailSlash.StartSlash`
18. `NailAttackBaseSlashStartingPatch` → `NailAttackBase.OnSlashStarting`
19. `NailAttackBaseImbuementPatch` → `NailAttackBase.SetNailImbuement`
20. `DownspikeStartPatch` → `Downspike.StartSlash`
21. `HeroExtraNailSlashEnablePatch` → `HeroExtraNailSlash.OnEnable`
22. `HeroExtraNailSlashChargeLogResetPatch` → `HeroExtraNailSlash.OnEnable`

Additional non-Harmony behavior runs through plugin `Update`, `LateUpdate`, `OnGUI`, `OnDestroy`, ModMenu registration, config change handlers—including the Orange Hornet shader refresh—and the transient live-slash retint component.

### Current boundaries and future work

- The Reaper half is not save-persistent.
- Hunter boss recognition remains heuristic until a universal game/community boss API exists.
- Moveset and audio transplantation depend on current vanilla/Needleforge attack-tree structure.
- Tint restoration uses a 12-frame correction window rather than permanently owning every sprite.
- Witch retaliation roots, Reaper features beyond the current harvest conversion, projectile Overclock, and unfinished crest overhauls are not part of 1.1.0.
- Base Crest changes remain intentionally bench-gated for stability.

## Clean-room component experiment

[`CrestComponentLab`](src/CrestComponentLab/README.md) is a separate Needleforge-only test mod. It
creates a new inert-by-default crest and independently configures its core moveset, dash slash,
charge slash, normal Bind provider, and special Up-Bind provider. It contains no dependency on
Void Crest or this compatibility plugin.

See [`CREST_COMPONENT_LAB_TEST.md`](docs/CREST_COMPONENT_LAB_TEST.md) for the installation and
gameplay matrix.
