# QuestMod

BepInEx 模组适用于空洞骑士：丝之歌。按 **F9** 可打开一个用于管理任务（愿望）的面板：接受、完成、撤销、调整目标数量、绕过剧情关卡，以及应用自定义规则。

当前版本：v2.2.0

## 它的功能

- **所有愿望模式**（工具）：禁用（原版），**调整后**（推荐——开启愿望但保持链式顺序、排除项和边缘情况修复），纯模式（原始，无检查；仅高级用户使用）。详见 [docs/AllWishesModes.md](docs/AllWishesModes.md)。
- 每个任务的 **接受 / 放弃 / 完成 / 取消完成**，并支持可用性 + AutoAccept 开关，配合批量重置。
- **远程完成**（工具，按存档）：开启时，完成操作会同步NPC交任务行为（扣除目标数量、发放奖励、触发连锁反应）。关闭时（默认），完成仅是**标记翻转**——仅 QuestData 标记发生变化。详见 [docs/CompleteAndUndo.md](docs/CompleteAndUndo.md)。
- **自动接受可用愿望**（工具）：每次场景加载时，会接受符合自然条件的可获取愿望（链式逻辑感知）。与“全部接受”不同的是，剧情锁定的愿望需等待其门禁开启。
- 链式任务合并为一行，并支持步骤导航。
- 使用“撤销上一次批量操作”功能批量接受或完成可用愿望。
- 每个存档可进行细粒度前置条件跳过：Fleatopia、强制愿望、Faydown斗篷、Needolin、Bonebottom任务板，以及**任务板始终可用**（三个愿望墙）。
- 目标数量覆盖（目标标签页）：滑块调节、预设（设置为 1、一半、默认值），类别倍率。
- 多目标/顺序性任务的检查清单界面（如Mr Mushroom等）。
- 交付标签页：Gourmand计时器冻结，任务物品无敌状态。
- 工具栏中的自定义条件预设；可选的 `BepInEx/config/QuestMod/QuestRequirements.user.json` 叠加层。详见 [docs/CustomRequirements.md](docs/CustomRequirements.md)。
- 通过剪贴板实现存档/即时存档的复制与粘贴。

破坏性功能均被限制在每个存档的**单向安全覆盖**之下。旧版存档将保持原版状态，直到你确认启用。

### 在 2.2.0 中移除

- 丝线与灵魂标签页/点数覆盖  
- 标签编辑标签页（规则仍可通过预设 + 用户JSON生效）  
- 愿望位置重新分配框架（从未实现）  
- 单击进入章节 3（`blackThreadWorld`）工具开关

## 安装

使用 [Gale](https://github.com/Kesomannen/gale) 或任何 Thunderstore 模组管理器。依赖项会自动下载（BepInExPack_Silksong、DataManager、FsmUtil、UnityHelper）。

手动安装：将 `QuestMod.dll` 放入 `BepInEx/plugins/QuestMod/`（或 `DatUub-QuestMod/`）。

## 键位

**F9** 打开任务管理器。可在 BepInEx 配置中重新绑定（`GuiToggleKey`）。

## 配置

`BepInEx/config/com.silkmod.questmod.cfg` 或 **工具** 选项卡。

| 区域 | 示例 |
|------|------|
| 通用设置 | EnableCompletionOverrides, OnlyDiscoveredQuests, QuestItemInvincible, ShowQuestDisplayNames, GuiToggleKey, GuiScale, GuaranteedSilverBells |
| 自定义条件 | EnableCustomRequirements（每存档）、ActivePreset（每存档） |
| 完成任务 | EnableFullRemoteComplete（每存档；也可通过工具 → **远程完成**） |
| 交付设置 | GourmandStopDecay, GourmandDecaySeconds |
| 高级选项 | DebugLogging, DevRemoveLimits, DevForceOperations, ShowPureWishesMode |

配置文件中为每个条目提供了完整描述。

## 选项卡

| 选项卡 | 功能说明 |
|--------|----------|
| **任务** | 链式任务 + 愿望清单；**进行中 / 已完成 / 全部** 过滤器；接受 / 放弃 / 完成 / **撤销完成**；仅标记状态 vs 远程完成状态 |
| **目标** | 目标数量覆盖和预设设置 |
| **交付** | 食客计时器 + 任务物品无敌机制 |
| **检查清单** | 多目标 / 顺序阶段触发 |
| **工具** | 存档安全、批量操作、模式切换、远程完成、自动接受可用任务、前置条件绕过、规则预设、存档复制/粘贴 |

## 完成 / 撤销（支持）

| 情况 | 应该怎么做 |
|------|------------|
| 正常的物品收集愿望 | 仅使用标记完成即可；通过 **已完成 → 撤销完成** 来取消 |
| 终极狂喜 / 蚂蚁集市（及其他类似场景） | 仅标记完成会被**阻止**——需在世界中完成。远程完成可能帮助物品交还，但**不会运行小游戏** |
| “我完成了某项内容却找不到撤销选项” | 任务 → **已完成** → 蓝色 **撤销完成** |
| 希望有NPC风格的交还方式 | 工具 → 启用 **远程完成** |

详细说明： [docs/CompleteAndUndo.md](docs/CompleteAndUndo.md)。

默认世界状态列表（仅标记拒绝）： `Flea Games`, `Flea Games Pre`。可通过嵌入的 `QuestCapabilities.json` 中的 `worldStateComplete` 扩展，如果你维护一个分支版本。

## 已知问题

- 当启用 **任务板始终可用** 时，修复者NPC可能会短暂播放他的锤击动画，直到FSM重写将其捕获（Addressables加载；补丁尝试重试约30秒）。该自助终端仍可交互。
- 仅标记完成的功能不会推进小游戏或世界FSM的进度——详见上方“完成 / 撤销”部分。
- 撤销完成只会清除完成标志，不会退还能力或小游戏的进度。

## 文档

| 文档 | 主题 |
|------|--------|
| [CHANGELOG.md](CHANGELOG.md) | 版本更新历史 |
| [docs/AllWishesModes.md](docs/AllWishesModes.md) | 禁用 / 调整 / 纯模式 |
| [docs/CompleteAndUndo.md](docs/CompleteAndUndo.md) | 仅标志位 vs 远程完成、撤销 |
| [docs/CustomRequirements.md](docs/CustomRequirements.md) | 标签、预设、每关规则 |
| [docs/quests/](docs/quests/) | 每关能力说明 |

## 建造

```sh
git clone <repo>
# Optional: SilksongPath.props with your install path for post-build copy
dotnet build -c Release
```

Thunderstore 包名：

```sh
dotnet build -c Release -t:ThunderstorePack
```

### 开发工具（不包含在 Thunderstore 构建中）

SelfTest + GuiShots 仅在显式启用时编译：

```sh
dotnet build -c Release -p:COMPILE_SELFTEST=true
```

| 环境 | 效果 |
|-----|--------|
| `QUESTMOD_SELFTEST=1` | SelfTest IPC + GuiShots 文件触发（不自动退出） |
| `QUESTMOD_SELFTEST_FULL=1` | 也运行自动 ContinueGame / 断言测试套件（加载存档，然后退出） |

不要将 SelfTest 版本发布到 Thunderstore。本地Harness源被git忽略。

## 贡献

问题和 PRs 欢迎。目标为 `main`。游戏内测试；当出现异常时检查 `BepInEx/LogOutput.log`。

## 感谢

TheMythical2046 提供了原始需求、任务研究和测试支持。
