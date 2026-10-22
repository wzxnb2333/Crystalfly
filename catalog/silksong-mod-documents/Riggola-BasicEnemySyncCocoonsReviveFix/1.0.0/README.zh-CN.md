# BasicEnemySync 蜕皮重生修复

非官方的 **修复尝试**，作为 [BasicEnemySync](https://github.com/Brothergaming52/ssmp-enemy-sync) 的配套工具。它试图防止玩家在重生后立即受到残留伤害，尤其是在封闭房间和首领战中。该功能仍处于实验阶段，并不能保证在所有情况下都有效；未来可能会加入相关的修复内容。

在玩家成功完成蜕皮重生（`ExitCocoonState(true)`）之后，插件会尝试抑制对 `HeroController.TakeDamage` 和 `PlayerData.TakeHealth` 的调用，持续时间长达 1.5 秒。该功能在每次成功的蜕皮重生后都会生效；它无法检测到首领房间或封闭门，也不会替代或修改 BasicEnemySync。此修复并非绝对可靠，未来可能会扩展以解决相关问题。

## 依赖项

- [BepInExPack Silksong 1.0.3](https://thunderstore.io/c/hollow-knight-silksong/p/silksong_modding/BepInExPack_Silksong/)
- [BasicEnemySync 1.0.1](https://github.com/Brothergaming52/ssmp-enemy-sync)

《SSMP》会话中的每位玩家都应使用相同的插件版本。该包尚未发布到 Thunderstore；在上传之后，Gale 搜索才会找到它。

## License and credits

The companion code is MIT-licensed; see [LICENSE](LICENSE). The included icon comes from BasicEnemySync and retains its original MIT notice in [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md) and [THIRD-PARTY-LICENSE.txt](THIRD-PARTY-LICENSE.txt). This is an unofficial project and is not affiliated with the BasicEnemySync or BepInEx authors.

## 更新日志

详见 [CHANGELOG.md](CHANGELOG.md) 以了解每个发布版本的变更内容。
