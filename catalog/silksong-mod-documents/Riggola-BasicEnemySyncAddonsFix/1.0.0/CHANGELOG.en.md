# Changelog

## 1.0.0 - 2026-09-27

- Rename the separate companion plugin to `BasicEnemySyncAddonsFix`, with BasicEnemySync as a required dependency.
- Show the local cocoon immediately and rebroadcast cocoon state so players entering the scene can see it.
- Prevent repeated cocoon packets from recreating the remote visual and guard cocooned players from ordinary damage.
- Keep the existing short protection after revival.
- Stop verified dead enemies from remaining active, reject position updates for recorded deaths, and prevent the original watchdog from reviving them.
- Rebroadcast recorded enemy deaths for players who join the scene later.
- Disable contact damage on named falling rocks once they have landed.
