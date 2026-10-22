# Changelog

## 0.1.1

Packaging only — **the mod itself is unchanged.** `EnemyPuppeteer.dll` is byte-for-byte identical
to 0.1.0, so there is nothing to gain by updating if it is already working for you.

### Changed
- New icon.
- The readme now states plainly that the Enemy Behavior API is a **hard requirement**. Without it
  the mod does not merely sit idle — it fails to load entirely, because the API is a compile-time
  assembly reference, so BepInEx cannot construct the plugin at all. The previous wording implied
  it would load and do nothing, which would have sent people hunting in the wrong place.

## 0.1.0

First release. Experimental test rig, not a gameplay mod.

### Added
- Select an enemy by clicking its hitbox, or from a list of everything in the scene
- Take control of it, fire any of its discovered attacks, and walk it by clicking
- Puppet mode: Hornet becomes an invisible, invulnerable marker that cannot act
- Bystander suppression, so only the enemy you control reacts to you
- Buttons for jump, dash and teleport states, for ledges and gaps
- Live readout of the enemy's PlayMaker state and whether each command landed
- Rescan, for enemies already awake before the API loaded

### Notes
- Enemies move using their own **patrol walk** — the unhurried back-and-forth they do before
  noticing you. Only facing and whether the walk is running are driven; speed, animation and
  ground handling stay with the enemy, so it moves like itself rather than sliding.
- Attacks are entirely manual. The enemy's own decision-making is suppressed while you drive it.
- Many enemies have states you never see in normal play. They are reachable but conditionally
  gated, and firing one bypasses the guard — so an attack fired out of context can look broken.
