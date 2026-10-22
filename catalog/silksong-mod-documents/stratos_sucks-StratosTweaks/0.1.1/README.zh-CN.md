# 丝之歌模组调整

空洞骑士：丝之歌插件。需要 [BepInExPack Silksong](https://thunderstore.io/c/hollow-knight-silksong/p/silksong_modding/BepInExPack_Silksong/) 和 [ModMenu](https://thunderstore.io/c/hollow-knight-silksong/p/silksong_modding/ModMenu/)。

通过 **选项 → 模组 → 丝之歌模组调整** 进行配置。

- **启用场景注入** — 替换小图（贝尔哈特、骨底、歌之圣殿）。默认关闭。
- **教程文字** — Tut_01 叠加显示。默认开启；不跟随场景注入。
- **启用位置追踪** — CSV 日志记录（默认开启）

插件中硬编码了高级 Addressables 路径（在 ModMenu 中不会显示）。

## 位置追踪

每个会话 CSV 文件中的事件（`utc,event,scene,x,y,z`）：

| 事件 | 发生时机 |
|--------|----------|
| `enter` | 进入房间（场景切换前的最近移动） |
| `bench_warp` | 长椅传送/重生传送（需安装 Benchwarp） |
| `door_warp` | 长椅门传送（需安装 Benchwarp） |
| `warp` | 其他传送 — 作为启发式回退方案，或大范围同房间跳跃 |
| `exit` | 离开房间 |
| `sample` | 在房间内的周期性位置记录 |

在地图上拼接房间时，请配对 **`exit` → `enter`**，并忽略 **`warp` / `bench_warp` / `door_warp`** 行。

调整 BepInEx 配置文件（`Tracking` 部分）：`WarpDistance`、`MovementWindowSeconds`、`WalkThreshold`。
