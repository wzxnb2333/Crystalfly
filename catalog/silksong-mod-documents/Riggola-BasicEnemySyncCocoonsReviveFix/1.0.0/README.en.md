# BasicEnemySync Cocoons Revive Fix

Unofficial **FIX Attempt** companion for [BasicEnemySync](https://github.com/Brothergaming52/ssmp-enemy-sync). It tries to prevent a revived player from immediately dying to lingering damage, especially during closed-door encounters and boss fights. It is experimental and is not guaranteed to work in every situation; related fixes may be added in the future.

After a successful cocoon revive (`ExitCocoonState(true)`), the plugin attempts to suppress calls to `HeroController.TakeDamage` and `PlayerData.TakeHealth` for 1.5 seconds. It applies after every successful cocoon revive; it does not detect boss rooms or closed doors, and it does not replace or modify BasicEnemySync. The fix is not guaranteed and may be expanded to address related issues later.

## Dependencies

- [BepInExPack Silksong 1.0.3](https://thunderstore.io/c/hollow-knight-silksong/p/silksong_modding/BepInExPack_Silksong/)
- [BasicEnemySync 1.0.1](https://github.com/Brothergaming52/ssmp-enemy-sync)

Every player in the SSMP session should use the same plugin version. The package is not published to Thunderstore yet; Gale search will find it only after upload.

## License and credits

The companion code is MIT-licensed; see [LICENSE](LICENSE). The included icon comes from BasicEnemySync and retains its original MIT notice in [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md) and [THIRD-PARTY-LICENSE.txt](THIRD-PARTY-LICENSE.txt). This is an unofficial project and is not affiliated with the BasicEnemySync or BepInEx authors.

## Changelog

See [CHANGELOG.md](CHANGELOG.md) for changes in each released version.
