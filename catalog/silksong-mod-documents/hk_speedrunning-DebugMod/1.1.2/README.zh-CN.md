# DebugMod 为丝之歌

空洞骑士速通玩家的主要练习工具，具备完整的存档/即时存档、作弊功能以及众多其他特性。  
基于 [Hollow Knight's DebugMod](https://github.com/TheMulhima/HollowKnight.DebugMod/) 开发，拥有全新直观的用户界面，并对存档功能进行了进一步优化。

按 `F2` 可显示用户界面。

如有关于问题或错误报告，请加入 [Modding Discord](https://discord.gg/F6Y5TeFQ8j) 或 [Speedrunning Discord](https://discord.gg/3JtHPsBjHD)。

---
- [DebugMod for Silksong](#debugmod-for-silksong)
  - [Features](#features)
  - [Upcoming Features](#upcoming-features)
  - [Installation](#installation)
    - [Manual Installation](#manual-installation)
  - [Credits](#credits)
    - [Hollow Knight DebugMod](#hollow-knight-debugmod)
    - [Silksong DebugMod](#silksong-debugmod)
---

## 特性

- 完全重新设计的现代化用户界面，可在游戏中便捷地使用模组的所有功能，且不会干扰游戏体验。
- 所有功能均可通过游戏内UI绑定快捷键，或通过鼠标操作实现。
- 可在叠加层中查看调试信息。
- ### 存档/即时存档（Savestates）：
    - 设置并加载完整的存档，回滚所有对大黄蜂背包、愿望与日记进度、世界状态等的更改。
  - 死亡后自动加载存档以实现立即重试。
  - 将存档保存至文件，并使用第三方存档包轻松进行练习。
  > [!WARNING]
  > 加载存档**会覆盖你的主存档文件，且无法撤销！**
- ### 游戏玩法：
  - 无限生命值/丝线、无敌、无碰撞（noclip）等效果。
  - 可调节并冻结时间流逝速度，逐帧前进。
  - 内置击中框可视化功能，按击中框类型进行颜色区分。
  - 预览玩家死亡时茧的生成位置。
  - 对大黄蜂造成伤害或将其击杀。
  - 设置并传送至上一次危险事件重生点。
  - 放大与缩小摄像机视角。
  - 实现视觉效果变化，例如隐藏大黄蜂、HUD界面、关闭其周围光照等。
  - 切换关卡状态 3（效果在下次场景加载时生效）。
  - 阻止对场景数据的修改（如念珠线、可破坏墙壁等），并将其恢复为默认值。
- ### 物品：
  - 给予或移除玩家技能。
  - 设置玩家生命值、丝线、面罩、卷轴、针具伤害值。
  - 提供无限工具、念珠与贝壳碎片。
  - 补充工具使用次数。
  - 解锁所有工具、纹章、猎人纹章升级及纹章槽位。
- ### 敌人：
  - 查看所有敌人的生命值。
  - 删除、克隆或为任意敌人 9999 设置生命值。
  - 在敌人上方添加生命条。
  - 击杀所有敌人。

## 即将推出的功能

- 空洞骑士 DebugMod 兼容性：目前需要以下功能；
  - 首领重生（+竞技场）。_此功能可能已由场景数据覆盖，但首领的实现方式可能存在差异，依赖于 PlayerData._
  - 给予特定按键/物品以及通往铃道 / 通风室的权限
- 提供一种方式让扩展模块能够在主面板中展示自己的界面。目前它们仅被追加到快捷键列表中。

## 安装

DebugMod 在 [Thunderstore](https://thunderstore.io/c/hollow-knight-silksong/p/hk_speedrunning/DebugMod/) 上分发，因此任何兼容的安装程序均可完成安装。  
我们推荐使用 [Gale](https://github.com/Kesomannen/gale/releases/tag/1.11.1)，您可以在其中搜索 DebugMod 并直接将其安装到个人配置文件并启动。

其他兼容的安装程序包括 [r2modman]() 与 [Thunderstore](https://get.thunderstore.io)（Overwolf）。

### 手动安装

在 MacOS 上必须手动安装，因为当前安装程序似乎不支持此功能。以下步骤适用于使用 Windows 的 Steam 安装方式。

1) 从此处下载 BepInEx：https://thunderstore.io/c/hollow-knight-silksong/p/BepInEx/BepInExPack_Silksong/  
1) 在 Steam 中右键点击 “Silksong” -> 属性 -> 已安装文件 -> 浏览...  
1) 将 BepInEx 压缩包的内容复制到该文件夹中（如果提示覆盖文件，请选择覆盖）  
1) 启动并关闭游戏一次，以便 BepInEx 生成初始文件  
1) 从此处下载 `DebugMod_MANUAL.zip`：https://github.com/hk-speedrunning/Silksong.DebugMod/releases/latest  
1) 将 DebugMod 和 Silksong.ModList 文件夹复制到游戏文件夹中的 `BepInEx/plugins` 目录下（如果提示覆盖文件，请选择覆盖）  
1) 该模组不会对存档造成负面影响，但为了安全起见，建议仍进行备份。  
   存档位于 `%AppData%/../LocalLow/Team Cherry/Hollow Knight Silksong`

> [!IMPORTANT]  
> 因内容审核原因，此模组需要安装 [Silksong.ModList](https://github.com/silksong-modding/Silksong.ModList)。该模组已包含在 [release download](<https://github.com/hk-speedrunning/Silksong.DebugMod/releases/latest>) 中。  
> 如果未安装 Silksong.ModList，则 DebugMod 将静默失败加载。

## Credits

### Hollow Knight DebugMod
- Coding - Serena
- SaveStates/Old Current Patch - 56
- UI design and graphics - The Embraced One
- Assistance with canvas - KDT
- 1.5 and A lot of Changes - Mulhima
- Multiple SaveStates/Minimal info panel- Cerpin
- Improve hitbox viewer - DemoJameson
- Multiple SaveState Pages - Magnetic Pizza (and jhearom for porting to lp)
- Additional Glitched functionality - pseudorandomhk
- Additional Bindable Functions, and Fix Stuff - Flib
- Buttons to directly run bindable actions - flukebull
- Add frame by frame advance code - SFGrenade

### Silksong DebugMod
- Initial Port, Core development & UI Overhaul - spacemonkeyy
- Minor bugfixing, nitpicking & maintenance - Jamie
- Additional fixes & assets - Flib, cometcake575, Freya Holmér, Foxyrobo, enadormi
- Translation credits:
  - Chinese: DUUScarlet & yuniBiscuit
