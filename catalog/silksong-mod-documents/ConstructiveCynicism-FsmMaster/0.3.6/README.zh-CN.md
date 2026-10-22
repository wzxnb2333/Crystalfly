# FsmMaster 用于空洞骑士和丝之歌

一个适用于空洞骑士和空洞骑士：丝之歌的 PlayMaker 状态机实时查看器与编辑器。可将游戏中的任意状态机以交互式图表形式展示，运行时可编辑其状态、变量及转换，并将修改保存以便后续重新应用。

按 `F3` 可切换叠加层。你可以为仅监控模式绑定一个按键。两种模式均可通过 BepInEx 的配置管理器（丝之歌）或模组 API 自身的模组设置菜单（空洞骑士 1.3.1.5/1.4.3.2/1.5.78）在游戏内重新绑定；对于空洞骑士 1.2.2.1，其模组 API 早于游戏内设置菜单，需直接编辑模组的保存设置文件进行重新绑定。

源代码及最新版本请访问 [FsmMaster Github Repository](https://github.com/ConstructiveCynicism/FsmMaster)

如有关于问题或错误报告，请加入 [Modding Discord](https://discord.gg/F6Y5TeFQ8j) 或 [Speedrunning Discord](https://discord.gg/3JtHPsBjHD)。

---
- [FsmMaster for Hollow Knight and Silksong](#fsmmaster-for-hollow-knight-and-silksong)
  - [Features](#features)
  - [Coming Soon](#coming-soon)
  - [Controls](#controls)
    - [Panel Controls](#panel-controls)
    - [Graph Controls](#graph-controls)
    - [State Panel](#state-panel)
    - [Sequencer](#sequencer)
  - [Installation](#installation)
  - [Credits](#credits)
---

## 特性  
- 图形化、可自定义的用户界面，支持性能切换选项，适用于配置较低的设备。  
- 支持FSM的编辑与查看，并实时显示信息——在FSM实际运行时，状态、变量和转换会即时更新。  
- 可重新绑定快捷键（具体方法请参见上方说明，按平台不同而异）。  
- 支持存档功能，包含 DebugMod - 当前FSM的编辑内容会自动保存至并从存档中恢复。  
- 自定义随机数生成序列——可替换FSM中的随机事件选择，使用由你控制的固定有序序列。  
- 隐藏变量编辑功能，包括 PlayMaker 在其自身编辑器中通常不暴露的变量。  
- 监控面板支持多个同时运行的 FSMs。  
- 可交互式图表——通过点击和拖拽方式浏览FSM中的状态、转换与事件，而非阅读原始数据。  
- 自定义配置文件的保存与加载，可选择在场景切换时自动加载。

## 即将推出  
- 自适应随机数生成，根据你正在困扰的攻击类型加快练习速度  
- 与计时器模组集成，根据 Fsm 值启动和停止计时器  
- 更多动作的序列功能

## 控制方式

### 面板控制
- **打开** - 选择要显示的FSM。
- **保存** - 将编辑内容保存到文件。
- **加载** - 从文件加载编辑内容。
- **撤销** - 撤销上一次编辑。
- **重置** - 将FSM恢复为默认状态。
- **隐藏** - 隐藏图表。
- **自动** - 在场景切换时自动加载上次保存的文件。

### 图表控制
- **滚动轮** - 放大或缩小。
- **拖动** - 平移。
- **点击** - 选择一个状态、事件或转换。
- **双击** - 更改状态。
- **右键点击** - 禁用一个状态或转换。
- **在转换上点击并拖动** - 将其目标重新指向另一个状态。

### 状态面板
- 可直接编辑大多数基本变量类型。
- 点击变量旁边的蓝色圆点，将其添加到监控面板中。

### 序列器
- 拖动事件以构建自定义序列，并重新排列事件，以用固定模式替代FSM中的随机事件选择。

## 安装

| 游戏 | 管理器 | 手动下载 | 安装路径 |
|---|---|---|---|
| 丝之歌 | [Thunderstore](https://thunderstore.io/c/hollow-knight-silksong/p/ConstructiveCynicism/FsmMaster) / [Cogfly](https://github.com/Nix-main/Cogfly/releases/latest) | [FsmMaster-silksong.zip](https://github.com/ConstructiveCynicism/FsmMaster/releases/latest/download/FsmMaster-silksong.zip) | `BepInEx/plugins/` |
| 空洞骑士 1.5.78 | [Lumafly](https://themulhima.github.io/Lumafly/) / [Scarab](https://github.com/fifty-six/Scarab) | [FsmMaster-hk1578.zip](https://github.com/ConstructiveCynicism/FsmMaster/releases/latest/download/FsmMaster-hk1578.zip) | `hollow_knight_Data/Managed/Mods/` |
| 空洞骑士 1.4.3.2 | — (仅手动安装) | [FsmMaster-hk1432.zip](https://github.com/ConstructiveCynicism/FsmMaster/releases/latest/download/FsmMaster-hk1432.zip) | `hollow_knight_Data/Managed/Mods/` |
| 空洞骑士 1.3.1.5 | — (仅手动安装) | [FsmMaster-hk1315.zip](https://github.com/ConstructiveCynicism/FsmMaster/releases/latest/download/FsmMaster-hk1315.zip) | `hollow_knight_Data/Managed/Mods/` |
| 空洞骑士 1.2.2.1 | — (仅手动安装) | [FsmMaster-hk1221.zip](https://github.com/ConstructiveCynicism/FsmMaster/releases/latest/download/FsmMaster-hk1221.zip) | `hollow_knight_Data/Managed/Mods/` |

> [!IMPORTANT]
> 由于审核原因，丝之歌版本需要安装 [Silksong.ModList](https://github.com/silksong-modding/Silksong.ModList)。该模组已包含在丝之歌的发布包下载中。
> 如果未安装 Silksong.ModList，FsmMaster 将静默失败加载。

> [!TIP]
> FsmMaster 可独立运行，但与 [DebugMod](https://github.com/hk-speedrunning/Silksong.DebugMod)（丝之歌）或 [DebugMod](https://github.com/TheMulhima/HollowKnight.DebugMod)（空洞骑士）同时安装，可获得存档/即时存档支持——在保存或加载存档时，会自动捕获并恢复活动的 FSM 编辑。
> 我推荐安装 DebugMod 以获得最佳体验。

## Credits

- Core development - ConstructiveCynicism
- FSM edit helpers derived from [Silksong.FsmUtil](https://github.com/silksong-modding/Silksong.FsmUtil) - silksong-modding (EUPL-1.2)
- Graph layout and FSM visualization conventions inspired by [FSMExpress](https://github.com/nesrak1/FSMExpress) - nesrak1
- Built on DebugMod's savestate system - [Silksong.DebugMod](https://github.com/hk-speedrunning/Silksong.DebugMod) / [HollowKnight.DebugMod](https://github.com/TheMulhima/HollowKnight.DebugMod)

## License
> FsmMaster
> Copyright (c) 2026 ConstructiveCynicism
> Licensed under the **[EUPL-1.2](LICENSE)**.

This product includes code derived from Silksong.FsmUtil
Copyright (c) silksong-modding
Licensed under the EUPL-1.2 - https://github.com/silksong-modding/Silksong.FsmUtil
The derived code has been modified by ConstructiveCynicism since 2026-07-20.

Graph layout and FSM visualization conventions inspired by FSMExpress (nesrak1).
Built on Silksong.DebugMod's savestate system.
