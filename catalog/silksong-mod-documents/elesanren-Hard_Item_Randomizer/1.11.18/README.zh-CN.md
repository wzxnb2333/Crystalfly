带有 SceneRandom 的硬核物品随机化是将 Hard_Item_Randomizer 与 BloodDestroier 的场景随机化整合在一起的配置包。房间随机借用了 BloodDestroier 的场景随机化核心逻辑，陷阱随机由本模组独立实现。安装后可同时体验 物品随机 房间随机 陷阱随机 的完整丝之歌随机挑战。

注意事项：本模组已自带物品随机、房间随机功能，无需额外下载对应模组。房间随机使用了门黑名单（BlacklistedExits）来过滤部分连接，名单已内置，不需要手动配置。使用本模组需要安装 Architect 以支持陷阱与场景对象的动态放置。

---

带有 SceneRandom 的硬核物品随机化是将 Hard_Item_Randomizer 与 BloodDestroier 的场景随机化整合在一起的配置包。房间随机借用了 BloodDestroier 的场景随机化核心逻辑，陷阱随机由本模组独立实现。安装后可同时体验 物品随机 房间随机 陷阱随机 的完整丝之歌随机挑战。

注意事项：本模组已自带物品随机、房间随机功能，无需额外下载对应模组。房间随机使用了门黑名单（BlacklistedExits）来过滤部分连接，名单已内置，不需要手动配置。使用本模组需要安装 Architect 以支持陷阱与场景对象的动态放置。

---

## 安装 / Installation

1. 确保已安装 BepInEx 5.x。

2. 将模组文件放入 `BepInEx/plugins` 文件夹。

1. 确保已安装 BepInEx 5.x。

2. 将模组文件放入 `BepInEx/plugins`。

3. 安装 Architect（必需）。其他所有依赖项均已包含在内。

4. 启动游戏；模组将自动加载。

---

## 配置 / Configuration

模组的所有配置均在游戏内菜单中完成，无需手动编辑配置文件。

如需临时调整按 F7

- \*\*物品随机\*\*：控制检查点、拾取点、商店、石碑等奖励是否随机化。
- \*\*房间随机\*\*：控制场景之间的连接是否重新编排。
- \*\*怪物随机\*\*：控制敌人是否被替换为随机敌人。
- \*\*陷阱随机\*\*：控制场景中是否额外生成陷阱机关。

所有配置均通过游戏内菜单完成——无需手动编辑配置文件。

###### 若需在游戏中临时调整，请按 F7

- \*\*物品随机\*\*：控制检查点、拾取点、商店和石碑等奖励是否随机化。
- \*\*房间随机\*\*：控制场景之间的连接是否重新编排。
- \*\*怪物随机\*\*：控制敌人是否被替换为随机敌人。
- \*\*陷阱随机\*\*：控制场景中是否额外生成陷阱机关。

---

## 存档 / Saves

模组会在 `BepInEx/config/SilksongItemRandomizer/global\_save.json` 中持久化随机映射数据。该文件与种子绑定，同一种子下所有奖励分配固定。

模组将在 `BepInEx/config/SilksongItemRandomizer/global\_save.json` 中持久化随机映射数据。该文件与种子绑定，相同种子下的所有奖励分配均固定。

---

## 依赖 / Dependencies

- \*\*必需 / Required\*\*: BepInEx 5.x
- \*\*推荐 / Recommended\*\*: Benchwarp（长椅传送），DebugMod（调试工具）

---

## 反馈 / Feedback

如遇到问题或有改进建议，欢迎反馈（请附上 `global\_save.json` 和 `BepInEx/LogOutput.log`）。

如遇问题或有改进建议，请提供 `global\_save.json` 和 `BepInEx/LogOutput.log`。
