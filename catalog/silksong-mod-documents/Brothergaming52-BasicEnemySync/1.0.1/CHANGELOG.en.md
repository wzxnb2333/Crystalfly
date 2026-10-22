# Changelog

All notable changes to the **SSMP Enemy Syncing Addon** will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

---

## [1.0.1] - 2026-08-18

### Added
- **Native BepInEx Plugin Loader (`EnemySyncingPlugin`)**:
  - Registered as `bg52.enemysync.ssmp` with dependency on `ssmp`.
  - Enables automatic addon discovery and seamless 1-click installation via Thunderstore / r2modman into `BepInEx/plugins/`.
- **Boss Target HUD Indicators**:
  - Dynamically changes the active boss's target player nametag to **Red** across UI Text, 3D TextMesh, and TextMeshPro components.
  - Reverts back to original color when the boss switches targets or dies.
- **Corpse Snapshot & Death Synchronization**:
  - Added corpse position, rotation, and knockback velocity synchronization on enemy defeat.
- **Scene Owner Death Verification**:
  - Added `VerifyEnemyDieRequest` and `SceneOwnerNotify` packets to prevent phantom duplicate spawns and desyncs.
- **Enhanced Projectile Synchronization**:
  - Added projectile animation clip and `ScaleX` scale synchronization across network.
- **Anti-Duplication Watchdog**:
  - Background entity monitor to restore disabled alive entities and clean up dead entities across scenes.

### Changed
- Overhauled PlayMaker FSM distance and position overrides (`GetDistance`, `GetXDistance`, `GetYDistance`, `GetPosition`) for precise target tracking.
- Improved Downed Cocoon mechanics, suicide keybind prompt UI, and team wipe resolution.

### Fixed
- Fixed desync where enemies would fail to register deaths from non-host clients.
- Fixed boss targeting becoming stuck after player death or scene transitions.

---

## [1.0.0] - 2026-07-26

### Added
- **Initial Release of SSMP Enemy Syncing Addon**:
  - Real-time position, rotation, and animation state synchronization for enemies and mini-bosses.
  - Multi-phase boss state synchronization and PlayMaker FSM action mirroring.
  - Proximity and FOV dynamic enemy authority claiming system.
  - Host fallback simulation for unclaimed or out-of-range enemies.
  - Co-op Downed Cocoon & Teammate Revive mechanics.
  - Quick Map hold-to-give-up suicide action for trapped cocooned players.
  - Team wipe check and coordinated respawns.
