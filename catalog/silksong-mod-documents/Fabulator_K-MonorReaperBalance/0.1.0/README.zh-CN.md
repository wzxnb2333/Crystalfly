# MonorReaperBalance

仅重新平衡 **纹章** 的缚丝强化状态（“纹章模式”）。  
其他纹章保持不变。

想法来自 **Monor**（`monor0365` 在 Discord）—— 本模组实现了他们最初的重平衡提案。

## 它改变了什么

- **收割模式不再在受到攻击时结束** — 可选地，受到攻击后只会减少剩余时间的几秒（游戏本体对战士怒气机制采用相同模式）。
- **默认持续时间为 7.5 秒**（原版为 10 秒），可配置范围为 2.5 到 15 秒。
- **在收割模式下，每次攻击至少获得 2 束丝线**（原版随机生成 1–3；加上最小值 2 限制后，实际概率表变为 2 @ 85%，3 @ 15%）。

## 配置

所有设置均位于 `BepInEx/config/io.github.fabulatork.monorreaperbalance.cfg`  
并在游戏过程中通过  
[ConfigurationManager](https://thunderstore.io/c/hollow-knight-silksong/p/jakobhellermann/BepInExConfigurationManager/)  
（F1 菜单 — 每个设置以滑块和可编辑的数值框形式显示）进行实时调节。

| 设置 | 默认值 | 范围 | 效果 |
| --- | --- | --- | --- |
| `ModeDurationSeconds` | 7.5 | 2.5–15 | 复活模式持续时间（模组默认值：7.5，原版值：10）。数值会以 0.1 秒为步长对齐。 |
| `PersistThroughHits` | true | — | 受到伤害不再结束复活模式。 |
| `HitDurationPenalty` | 0 | 0–5 | 每受到一次伤害，减少剩余复活时间的秒数。0 = 无惩罚。仅在 `PersistThroughHits` 开启时生效。 |
| `MinOrbsPerHit` | 2 | 1–3 | 复活模式中每受到一次伤害的最低丝线数量（原版随机生成 1–3）。 |

## Credits

- **Idea:** Monor (`monor0365` on Discord) — original proposer of this rebalance.
- **Mod:** Fabulator_K
