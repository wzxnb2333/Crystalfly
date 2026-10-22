# Changelog

## 0.1.0

First release.

### Added
- Enemies are shared between both players: one enemy, one pool of health, and it dies once for
  both of you. Bosses included.
- Turns on SSMP's entity system, which ships complete but has the three calls that start it
  commented out in `ClientManager`.
- Registers Silksong's enemies, since SSMP's built-in entity registry is Hollow Knight 1's and
  matches nothing in this game.
- Health synchronisation, which SSMP's entity system never had.

### Notes
- Reads SSMP's internals directly, so it is tied to **SSMP 0.3.1.0**. On a different build it
  disables itself at startup and logs why, rather than breaking your game.
- The scene host is authoritative. Clients report the damage they deal and it is applied there,
  so a kill by either player counts.
- Death is routed through the game's own death path rather than by zeroing health, so the correct
  effects play and SSMP replicates it.
