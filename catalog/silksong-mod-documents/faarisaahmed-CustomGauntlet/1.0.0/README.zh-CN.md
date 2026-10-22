# 自定义手套

一个丝之歌模组，新增了**自定义手套**游戏模式。从新游戏菜单中选择该模式将跳过开场动画，并直接带入大议会区域，此时你已拥有所有能力与工具，可使用长椅休息，并可在自己建造的竞技场中进行波次挑战。

## 状态 — 请先阅读此内容

**这只是一个功能性的玩具，而非完整模组。** 场地、波次编辑器和战斗机制均能正常运行，你构建的流程也会被正确播放。但以下内容尚未实现：

- **仅部分敌人可用。** 丝之歌中没有可加载预制体的敌人——它们仅存在于房间内部。因此该模组会从实际加载的房间中收集这些敌人。大论坛种子在进入时会为选择器提供大约 37 种类型的敌人，而 `Load missing` 通过加载一份手写编写的 32 房间列表来获取更多敌人。超出该范围的敌人虽然会显示在选择器中（例如整个猎人日志中的所有敌人），但无法被生成，且场地触发器会一直保持关闭状态，直到你波次中所有敌人都已被加载。
- **没有敌人到房间的索引关系。** `Load missing` 采用排除法工作——加载一个房间后，查看缺失列表是否缩小；因为日志条目存储的是显示名称，而与场景中的对象名不一致。因此部分敌人根本无法被找到。
- **导入的房间会占用内存。** 每个产生敌人的房间在会话期间都会持续驻留，因为其资源包持有该敌人的美术资源。`F9 → Waves` 会显示当前被持有的房间数量。
- **存在一些粗糙边缘情况。** 歌唱团的敲击器有时会停留在地板下方（未粘附检测通常能处理大部分问题）；少数敌人在非原生场地中表现异常。

首领、每波规则、计分系统和难度调节均未实现。开发菜单（`F9`）存在是因为你将需要它。

该模组已测试于作者提供的丝之歌版本；游戏补丁可以移动任何此模组可触及的内容。本模组不涉及真实存档——挑战模式运行属于独立的存档槽位。

## 安装

**使用模组管理器**（r2modman、Gale、Thunderstore 模组管理器）—— 从 Thunderstore 安装 CustomGauntlet。BepInEx 作为依赖项自动安装。

**手动安装** — 安装 [Silksong 的 BepInEx 5][bepinex]，然后从 `CustomGauntlet-<version>.zip` 中获取 [Releases](../../releases)，并将其中的 `plugins` 和 `patchers` 文件夹合并到你的 `BepInEx` 文件夹中，最终得到：

```
BepInEx/plugins/GauntletMod/GauntletMod.dll
BepInEx/patchers/GauntletMod/GauntletMod.Patcher.dll
```

[bepinex]: https://thunderstore.io/c/hollow-knight-silksong/p/BepInEx/BepInExPack_Silksong/

## 如何使用

1. **新游戏** → **自定义挑战**。你将从大论坛右侧末端的长椅开始，装备齐全，且该房间内的默认战斗已清除。
2. 按下 **F7** 进入波次编辑器。添加一个波次，然后从右侧列表中选择敌方单位。选择器会显示完整的猎人日志；灰色条目尚未加载。
3. 如果有任何项目是灰色的，请按 **加载缺失内容**，并稍等片刻——它会前往对应房间获取这些敌人。有些敌人永远无法找到；详见 **状态**。
4. 关闭编辑器。你的波次将被保存，并立即整合进竞技场中。
5. 走入竞技场，战斗即刻开始。死亡后你会回到长椅位置，并重置为第一波。

波次数据存储在 `BepInEx/config/GauntletMod.waves.txt` 中，该文件为纯文本格式，可直接手动编辑。

如果房间感觉让你感到被束缚，请按 **F9 → 房间 → 让我出去** 来解除相机锁定和大门封锁。

## 控制方式

| 键位 | 说明 |
| --- | --- |
| `F7` | 波浪编辑器 |
| `F9` | 开发菜单（包含“播放”、“房间”、“波浪”、“调试”选项卡，可拖动、可调整大小） |
| `1` | 自由移动 — 使用方向键或 WASD 移动，按住 Shift 可提升移动速度 |
| `2` | 掩码/丝线显示 |
| `-` / `=` | 相机缩放（缩小 / 放大） |

## 从源码构建  

```
dotnet build -c Release
```
  
同时编译两个项目，并将每个项目复制到指定位置：  

```
GauntletMod          -> BepInEx/plugins/GauntletMod/GauntletMod.dll
GauntletMod.Patcher  -> BepInEx/patchers/GauntletMod/GauntletMod.Patcher.dll
```
  
新机器唯一需要更改的是游戏的路径。无需编辑被追踪的文件，只需在 `LocalPaths.props` 旁边放置一个 `Directory.Build.props` —— 它被 git 忽略，并会覆盖默认设置：  

```xml
<Project>
  <PropertyGroup>
    <GamePath>C:\Program Files (x86)\Steam\steamapps\common\Hollow Knight Silksong</GamePath>
  </PropertyGroup>
</Project>
```

## 构建 Thunderstore 包  

```
./package.sh
```
  
构建、打包并压缩 `dist/CustomGauntlet-<version>.zip`，准备上传。版本信息存在于三个文件中 — `Thunderstore/manifest.json`、`GauntletMod/Plugin.cs` 和 `Directory.Build.props` — 脚本会拒绝构建如果这三个文件内容不一致。

压缩包的顶层目录 `plugins/` 和 `patchers/` 是 Thunderstore 的 BepInEx 安装程序所映射到 `BepInEx/` 的部分，因此该文件同样可以作为手动安装使用。

## 布局

```
GauntletMod.sln
Directory.Build.props        game path and version, shared by both projects
package.sh                   builds the Thunderstore zip
CHANGELOG.md
GauntletMod/                 the plugin
GauntletMod.Patcher/         the BepInEx preloader patcher
Thunderstore/                manifest.json, icon.png, make_icon.py and its source art
```

## 文件

所有文件均位于 `GauntletMod/` 下，除非特别注明。

| 文件 | 功能 |
| --- | --- |
| `Plugin.cs` | BepInEx 入口点，应用 Harmony 修补程序 |
| `GauntletConfig.cs` | 所有可调节参数 |
| `GauntletPatches.cs` | Harmony 的钩子函数 |
| `MenuInjector.cs` | 在游戏模式菜单中添加自定义手套按钮 |
| `GauntletMode.cs` | 模式状态及启动流程 |
| `GauntletRun.cs` | 单次运行：计时、重置、战斗间的工具自由使用 |
| `ArenaSetup.cs` | 清除、封印、生成点、HUD、房间诊断功能 |
| `ArenaTrigger.cs` | 模组自身的“进入即启动”触发器 |
| `EnemyLibrary.cs` | 可生成敌人的来源 |
| `EnemyImporter.cs` | 从其他房间获取敌人 |
| `WaveConfig.cs` | 战斗列表及其配置文件 |
| `WaveBuilder.cs` | 将该列表转化为实际战斗 |
| `GauntletPanel.cs` | 战斗编辑器窗口 |
| `GauntletHud.cs` | 掩码/丝线显示，运行结束卡片 |
| `BenchImporter.cs` | 将一个可用的长椅提升至原本没有长椅的房间中 |
| `PlayerBuffs.cs` | 最大化所有属性的装备配置 |
| `FreeMove.cs` | 无碰撞模式（noclip） |
| `DebugOverlay.cs` | 相机缩放功能 |
| `SceneCatalog.cs` | 在加载场景前检查场景是否存在，防止游戏卡死 |
| `GauntletManager.cs` | 场景监控、快捷键、开发菜单、看门狗机制 |
| `GauntletMod.Patcher/Patcher.cs` | 预加载修补程序 |

# 它是如何工作的

以下内容都是需要付出学习成本的决策。它们被记录下来，是为了让推理得以留存，而不仅仅是结果本身。

## 不播放开场动画启动

`GameManager.StartNewGame(permadeath, bossRush)` 已经有两个分支：

- `bossRush == false` → `RunStartNewGame()` → 加载 `Opening_Sequence`，即开场动画
- `bossRush == true` → `RunContinueGame()` → 直接加载 `playerData.respawnScene`

该路径选择的是第二个分支。它并非简单地附加一个跳过功能——而是游戏原本就包含的无开场动画路径，包括正确的全局池、英雄预制体和场景引用设置。

原版丝之歌仅在解锁了“钢铁之心”或“首领速通”时才会显示游玩模式菜单，因此对 `UIManager.StartNewGame` 前缀始终会引导你进入该菜单，而 `MenuInjector` 会复制现有的“普通”按钮，而非新建一个——从而免费继承菜单的字体、动画器、闪烁效果、音频和导航功能。

## 房间

`Hang_04` 是大议会厅，它**同时包含两个场景**：`Hang_04` 包含房间本身，`Hang_04_boss` 以叠加方式加载在上方，并携带 `BattleScene` 和波浪。早期版本通过“是否为当前活动场景”来过滤对象，这导致整个竞技场被排除在外。现在规则已反转——只要被加载的内容，都算作房间，除非它是持久态内容或来自我们导入的敌人所在的房间。

**家点固定在 `(53.5, 4.6)` 上**，每次加载时都会将玩家移动到该位置。自动定位仅是对房间形状的一个猜测。

## 战斗进入的波次

一个房间可以容纳多个 `BattleScene`，对应不同的剧情状态。`Hang_04` 会同时拥有一个未激活的 `Battle Scene Act3` 和一个正在运行的实例。

向未激活的实例中构建内容会导致一切问题被**静默地破坏**：`StartBattle` 会调用 `StartCoroutine`，Unity 不会在未激活对象上执行协程，因此战斗永远不会开始，敌人也不会切换出场。它们会正确地处于位置，但永久不可见。**当选择时，活跃状态优先于拥有最多波次**。

房间内的所有战斗都会被接管——论坛有三个战斗，若保留其中一个武装状态，则其默认战斗将与你的战斗并行运行。

## 波次

该模组**未实现**波次系统。`BattleScene` 已经锁定摄像机，关闭了大门，处理音乐提示，通过 `HealthManager` 死亡回调计数敌人，并推进波次。一个 `BattleWave` 仅仅是一个 GameObject，其子对象为敌人——因此模组保留房间自身的 `BattleScene` 并替换其 `waves` 列表。

玩家进入时能够生效，是因为模组在竞技场中放置了**自己的触发器**。一旦原版战斗被清除，就无法依赖房间原有的触发器。

第一波敌人受到 `FirstWaveDelay` 的延迟控制，因此在开场炫技之后才出现，而非在炫技期间出现。

### 敌人如何被放置

所有敌人都位于竞技场中心，地板上呈扇形分布。地面射线从你**所站地板的正上方**发射，而不是从天花板向下发射——从顶部向下射出会找到其下方的第一个物体，在 `Hang_04` 中该物体是一个距离地面二十单位高的平台，因此在该平台上生成的敌人将被遮挡。

`Collider2D.bounds` 仅在碰撞器**启用时**有意义，而一个 `BattleScene` 在战斗开始时会禁用自身的触发器。读取其为禁用状态时返回的是原点处的一个退化盒子，这会使所有生成点都位于房间角落。

那些挖掘后不再出现的潜行者，在几秒后会被移除——是被观察到而非预测到，因此合法的地下攻击将被保留。

## 敌人从哪里来

丝之歌在整个游戏中共有**六个**可寻址的预制体：三个核心管理器，`GlobalPool` 和 `Hero_Hornet`。没有敌人。它们仅存在于场景内部，有两种方式：

- **放置式** — 作为房间内的物体存在。较为罕见；手套关卡中的波次敌人就是这种形式，因此仅凭竞技场就可获得约37种类型。
- **池化式** — 更为常见。房间内携带一个 `PersonalObjectPool`，其 `startupPool` 列出敌人的*预制体*，在运行时实例化。

该库会读取**两者**，而池化的预制体列表才是关键所在。早期版本仅搜索 `HealthManager` 对象，在大多数房间中均找不到敌人，因为这些敌人只有在房间运行时才会生成——而导入器在进入时会隐藏房间，确保它们永远不会运行。读取预制体无需实例化，也无需运行房间。

模板会被克隆到一个**非激活**的父对象中，因此 Unity 会跳过 `Awake`，模板不会执行敌对逻辑，也不会注册到对象池中。

### 从其他房间导入

加载一个房间，复制并存放。两个丝之歌特有的陷阱：

**房间必须保持加载状态。** `UnloadSceneAsync(handle, autoReleaseHandle: false)` 并不固定捆绑包——该标志仅控制句柄包装器是否被回收。卸载场景会卸载其捆绑包，从而破坏克隆体精灵背后的序列化数据，即使这些克隆体已被移动到持久化场景中。这就是“隐形敌人”的来源。因此，能产出内容的房间必须保持加载状态，被移至远处并关闭；不产生内容的房间则会被释放。

**调色板必须被阻断而非修复。** 每个房间的 `SceneColorManager` 在加载时会将其调色板推入全局色彩曲线中，卸载时不会撤销这一操作。在导入运行期间，Harmony 前缀会跳过除竞技场自身以外的所有色彩管理器。

> 正确的解决方案是 **Silksong.AssetHelper**，它将单个场景资产重新打包到独立的捆绑包中，从而完全无需加载场景。此处未安装该组件。若将来安装，`EnemyImporter` 将被其替代——请注意，其请求会进入 `Awake`，因此波次文件将成为启动清单，且需要一个 `(scene, path)` 映射，而本模组并不包含此映射。

## 重生

`GameManager.FindEntryPoint` 有三个答案，具体取决于你如何到达：重生
死亡后显示标记，`playerData.hazardRespawnLocation` 表示坠入深渊或被尖刺击中时的情况，
一个过渡点，否则。仅修补标记查找可覆盖一个分支和
错过了其他人——并且 `hazardRespawnLocation` 在一个新存档中是 `(0,0,0)`，
房间的左下角。

更糟糕的是，本意是捕捉该读取操作的边界检查 `x >= 0 && y >= 0`，会调用 `(0,0)`
**在范围内**。它正经过最常见的不良重生点。那里有一个 3-单位
边距现在。

与其不断追逐重生路线，该模组会监控死亡标志的清除情况并
将你放置到配置的坐标位置——持续约一秒，因为
重生序列在旗帜清除后会不断重新定位你。

死亡也会**将关卡重置到第一波**。`BattleScene`会保留其波次计数器
否则，向后行走时会从中途恢复。

## 装备搭配

工具不是 `PlayerData` 布尔值——它们是 `ToolItem` ScriptableObjects，并通过进行跟踪
`ToolItemManager`，因此无论多少 `SetBool` 都无法获得一个。`UnlockAllTools()` 和
`UnlockAllCrests()`做它，再加上`UnlockCrestSockets()`用于通常需要花费的插槽
一个记忆锁扣（每个纹章的存档数据；`UnlockAllCrests` 不会触及它们）。

`maxHealth` 是 **衍生的** — `CharmUpdate` 字面意义上 `maxHealth = maxHealthBase` —
直接设置是没意义的。先设置基础值，然后跟随游戏自身路径进行操作
(`CharmUpdate` → `MaxHealth` → `HEALTH UPDATE`)，正是这一点使得HUD重新构建其
掩码行。

`ToolItemManager` 是一个不存在于菜单中的 `ManagerSingleton`，这就是为什么
装备配置会被应用两次：在新游戏时应用属性，到达游戏世界时再次应用所有内容。

## 界面显示

掩模/丝线界面独立使用自己的摄像机。`MoveMenuToMainCamera` 将该摄像机的 `cullingMask` 设置为 **0** —— 激活、启用、报告自身可见、不进行渲染。没有任何状态检查能够暴露其存在，且它会覆盖其他模组的界面切换功能。解决方案是 `MoveMenuToHUDCamera`，该方案重新指向 UI 图层并恢复掩模。

故意 **未** 与 `EnsureGameMapSpawned` 配对 —— 在独立空间中生成物品栏地图会在屏幕上产生一条黑色条带。

`2` 可切换模组绘制的掩模/丝线读数作为后备方案。

## 长椅

长椅无法被制作：`RestBench` 仅会翻转“靠近长椅”的标志，而坐下来、治疗、存档以及更换工具等操作均存在于其“长椅控制”FSM中。因此，长椅是从一个供体房间克隆出来的。

供体的选择很重要。贝尔韦的“铃铛长椅”是带有解锁弹窗的圣龛，克隆出的版本会显示弹窗而非长椅——空中悬浮的巨大发光符文。`Hang_06b` 是来自竞技场同一区域的普通长椅，因此匹配成功。

它被放置在地面上的方式是通过测量其**渲染边界**，而不是其锚点：锚点可能距离艺术素材数米之高，这会使整个物体被埋入地下。

## 门

该模组不会对它们进行任何修改。`SealExits` 默认为 **关闭**。早期版本会重写每个门的 `targetScene`，以判断是哪个房间，这破坏了该会话中房间的真实连接关系。`Hang_04` 恰好有两个门，通向 `Hang_06` 和 `Hang_12`。

如果竞技场感觉被封闭了，按 F9 → 房间 → **让我出去** 可以解除摄像机锁定和大门的封锁，这些封锁由一个 `BattleScene` 保持，直到其自身的结束序列运行完毕。

## 场景名称

请求丝之歌加载一个不在地址化目录中的场景**不会报错**——  
加载操作永远不会完成，游戏将永远停留在加载屏幕上，日志中无任何记录。每个场景名称都会首先与目录进行比对。

## 修补工具

一个 BepInEx 5 预加载器修补工具，会对模组所触及的内存中 Cecil 编译程序类型进行针对性公开。磁盘上的文件不会被修改。

**你并不严格需要它** —— 该插件是基于原生程序集构建，并使用反射技术。两个规则确保其安全性：仅对列出的类型进行操作，且 Unity 未已序列化的字段在被公开时会自动 `NotSerialized`（Unity 默认会序列化公共字段，因此直接公开一个普通私有字段将改变场景的反序列化行为）。

## 配置

`BepInEx/config/com.custom.gauntletmod.cfg`。波浪在独立的  
`BepInEx/config/GauntletMod.waves.txt` 中，这是一种纯文本格式，可手动编辑。

| 区段 | 键 | 默认值 |
| --- | --- | --- |
| 房间 | `GauntletScene` | `Hang_04` |
| 房间 | `HomePointSet` / `HomePointX` / `HomePointY` | `true` / `53.5` / `4.6` |
| 房间 | `AddBench` / `BenchDonorScene` / `BenchYOffset` | `true` / `Hang_06b` / `0` |
| 行为 | `StripArena` | `true` |
| 行为 | `SealExits` | `false` |
| 行为 | `KeepRoomClear` | `true` |
| 行为 | `RescueOutOfBounds` | `true` |
| 行为 | `SpawnAtArenaCentre` / `SpawnHeight` | `true` / `1.6` |
| 行为 | `HarvestEnemies` | `true` |
| 行为 | `ToolsBetweenFights` | `true` |
| 行为 | `FirstWaveDelay` | `2.5` |
| 行为 | `ShowClearMessage` | `false` |
| 模式 | `ShowModeMenuOnNewGame` / `MaxLoadoutOnStart` | `true` / `true` |
| 启动 | `SkipStartupLogos` | `true` |
| 菜单 | `ModeName` / `ModeDescription` | `CUSTOM GAUNTLET` / `Trials of your own making.` |
| 键位 | `WavePanelKey` `DevMenuKey` `FreeMoveKey` `ReadoutKey` `ZoomOutKey` `ZoomInKey` | `F7` `F9` `1` `2` `-` `=` |
| 数据记录 | `GauntletProfileIds` | *(自动)* |

`GauntletProfileIds` 记录哪些存档槽位是挑战模式的运行。模式由存档槽位识别，而非重生场景——否则在正常游戏过程中停留在同一房间时会重新激活该模式并移除真实存档。

## 已知的粗糙边缘

此列表的玩家界面版本位于顶部的**状态**部分。需要记录的细节如下：

- **合唱团拍手者**有时会停留在地板下方。未粘附处理可以解决大部分问题；它们的出现动画并未为未曾放置于其中的竞技场场景设计。
- **缺少敌人到房间的索引**。“加载缺失”功能是通过一个包含 32 房间列表的排除机制实现的，因为日志条目存储的显示名称与场景中的对象名称不匹配，且场景包被压缩。AssetHelper 的导出工具可以彻底解决此问题——同时也能消除需要持续保留导入房间以供加载的需求，而这正是内存消耗的主要来源。
