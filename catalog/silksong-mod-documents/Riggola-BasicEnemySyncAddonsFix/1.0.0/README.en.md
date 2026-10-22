# BasicEnemySyncAddonsFix

Unofficial companion for [BasicEnemySync](https://thunderstore.io/c/hollow-knight-silksong/p/Brothergaming52/BasicEnemySync/). This is a separate BepInEx plugin and requires the original mod. It does not replace `EnemySyncing.dll`.

It makes the local cocoon appear immediately, repeats cocoon state for other players and late joins, preserves revive protection, and guards against ordinary damage during cocoon state. It also suppresses reappearance of recorded dead enemies and landed rock contact damage.

All players should install the same addon version. Disable the older `BasicEnemySyncCocoonsReviveFix` package when using this one.

These synchronization fixes need multiplayer testing across scene transitions. See [CHANGELOG.md](CHANGELOG.md) for the exact changes.

This project is unofficial and unaffiliated with the BasicEnemySync authors. Licensed under MIT; the included icon retains its upstream license and attribution in the notice files.
