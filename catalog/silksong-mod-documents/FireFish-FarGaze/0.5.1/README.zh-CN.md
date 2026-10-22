# 远眺

转向风景，更清晰地观察《空洞骑士：丝之歌》的世界。

## 特性

- 当大黄蜂静止站立时，按下远望键可转向背景，并平滑清除模糊效果。
- 向上或向下查看可移动相机，而不会中断大黄蜂的背对姿态。
- 自定义独立的键盘和控制器绑定。选择切换模式或保持模式。

## 安装

在 Far Gaze 的 Thunderstore 页面选择 **使用模组管理器安装**。管理器将安装声明的 [BepInExPack Silksong](https://thunderstore.io/c/hollow-knight-silksong/p/silksong_modding/BepInExPack_Silksong/) 和 [ModMenu](https://thunderstore.io/c/hollow-knight-silksong/p/silksong_modding/ModMenu/) 依赖项，包括 ModMenu 自身的要求。

对于手动安装，请先安装这些包以及 ModMenu 列出的依赖项，然后将 `FarGaze.dll` 放置在游戏目录下的 `BepInEx/plugins/FarGaze/` 中。

## 控制与设置

默认键盘按键是 `B`。首次连接控制器类型时，默认为按下左摇杆（PlayStation 上为 `L3`，Xbox 上为 `LS`）。

打开 **选项 → 模组 → 远视** 可以更改键盘绑定、控制器绑定或输入模式。每种控制器类型会记住自己的绑定设置。若存在与游戏操作冲突的绑定，则会被拒绝，且不会覆盖当前设置。

- **切换**：按下一次进入远视模式，再次按下退出。
- **持续按住**：只要按键被持续按住，就保持在远视模式中。

远视模式只能在静止状态下启动。行走、跳跃、攻击或受到伤害都会结束该模式。正常退出时会播放返回动作动画，并平滑地恢复背景模糊效果。本模组不会修改存档文件。

## Compatibility and credits

Version **0.5.1** uses BepInEx 5 and was verified with the Steam release of Silksong, build 22479045. Keyboard and PS4 controllers received the most testing. PS5, Xbox, and Nintendo-style controllers have their own prompts and bindings.

Controller prompts come from xelu's [Free Keyboard and Controllers Prompts Pack](https://opengameart.org/content/free-keyboard-and-controllers-prompts-pack), released under [CC0](https://creativecommons.org/publicdomain/zero/1.0/). PlayStation face-button prompts use the game's own icons.

---

## 中文说明

站定后按下远眺键，大黄蜂会转身望向背景，背景模糊逐渐消退。远眺时仍可向上或向下移动镜头，大黄蜂保持背身姿势。支持分别自定义键盘和手柄按键，以及点按、长按两种模式。

在 Thunderstore 页面选择 **Install with Mod Manager**，管理器会自动安装清单声明的 BepInExPack Silksong、ModMenu 及后者的依赖。手动安装时，请先安装这些依赖，再将 `FarGaze.dll` 放入游戏目录的 `BepInEx/plugins/FarGaze/`。

默认键盘按键为 `B`；首次连接某种手柄时，默认按键为按下左摇杆（PlayStation 为 `L3`，Xbox 为 `LS`）。在 **选项 → Mods → 远眺** 中可修改按键和模式。冲突按键会被拒绝，每种手柄类型分别记住绑定。

- **点按**：按一次进入，再按一次退出。
- **长按**：按住时远眺，松开时退出。

只有站定时才能进入远眺；行走、跳跃、攻击或受击会结束远眺。正常退出会播放回身动画，背景模糊平滑恢复。本模组不修改存档。

当前版本为 **0.5.1**，使用 BepInEx 5，已在 Steam 版《丝之歌》build 22479045 上验证；重点测试键盘与 PS4 手柄。手柄提示图采用 xelu 的 CC0 素材包；PlayStation 面键提示使用游戏自身的图标。
