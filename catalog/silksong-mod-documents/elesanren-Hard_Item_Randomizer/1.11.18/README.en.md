Hard\_Item\_Randomizer w/ SceneRandom 是将 Hard\_Item\_Randomizer 与 BloodDestroier 的 Scene Randomizer 整合在一起的配置包。房间随机借用了 BloodDestroier 的 Scene Randomizer 核心逻辑，陷阱随机由本模组独立实现。安装后可同时体验   物品随机   房间随机   陷阱随机  的完整丝之歌随机挑战。



注意事项：本模组已自带物品随机、房间随机功能，无需额外下载对应模组。房间随机使用了门黑名单（BlacklistedExits）来过滤部分连接，名单已内置，不需要手动配置。使用本模组需要安装 Architect 以支持陷阱与场景对象的动态放置。



\---



Hard\_Item\_Randomizer w/ SceneRandom is a configuration package that combines Hard\_Item\_Randomizer with BloodDestroier's Scene Randomizer. Room randomization uses the core logic of BloodDestroier's Scene Randomizer, while trap randomization is independently implemented by this mod. Once installed, you can experience a complete Silksong randomizer challenge featuring Item randomization,   Room randomization     Trap randomization



Notes: This mod includes Item Randomizer, Room Randomizer — no additional mods required for these features. Room randomization uses a built-in BlacklistedExits list to filter certain connections; no manual configuration is needed. Architect is required for dynamic placement of traps and scene objects.



\---



\## 安装 / Installation



1\. 确保已安装 BepInEx 5.x。

2\. 将模组文件放入 `BepInEx/plugins` 文件夹。





1\. Make sure BepInEx 5.x is installed.

2\. Place the mod files into `BepInEx/plugins`.

3\. Install Architect (required). All other dependencies are included.

4\. Launch the game; the mod will load automatically.



\---



\## 配置 / Configuration



模组的所有配置均在游戏内菜单中完成，无需手动编辑配置文件。

如需临时调整按F7

\- \*\*物品随机\*\*：控制检查点、拾取点、商店、石碑等奖励是否随机化。

\- \*\*房间随机\*\*：控制场景之间的连接是否重新编排。

\- \*\*怪物随机\*\*：控制敌人是否被替换为随机敌人。

\- \*\*陷阱随机\*\*：控制场景中是否额外生成陷阱机关。



All configuration is done via the mode menu – no manual file editing required.

###### If need adgustment in game,press F7 

\- \*\*Item Randomizer\*\*: Controls whether checkpoints, pickups, shops, and tablets are randomized.

\- \*\*Room Randomizer\*\*: Controls whether scene connections are rearranged.

\- \*\*Enemy Randomizer\*\*: Controls whether enemies are replaced with random ones.

\- \*\*Trap Randomizer\*\*: Controls whether extra traps are spawned in scenes.



\---



\## 存档 / Saves



模组会在 `BepInEx/config/SilksongItemRandomizer/global\_save.json` 中持久化随机映射数据。该文件与种子绑定，同一种子下所有奖励分配固定。



The mod persists random mapping data in `BepInEx/config/SilksongItemRandomizer/global\_save.json`. This file is tied to the seed – all rewards are fixed for a given seed.



\---



\## 依赖 / Dependencies



\- \*\*必需 / Required\*\*: BepInEx 5.x

\- \*\*推荐 / Recommended\*\*: Benchwarp（长椅传送）, DebugMod（调试工具）



\---



\## 反馈 / Feedback



如遇到问题或有改进建议，欢迎反馈（请附上 `global\_save.json` 和 `BepInEx/LogOutput.log`）。



For bug reports or suggestions, please include your `global\_save.json` and `BepInEx/LogOutput.log`.

