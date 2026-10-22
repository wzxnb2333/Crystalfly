# GameAdjustments —— 丝之歌的全能增强模组  
*GameAdjustments – The Ultimate Silksong Overhaul*

GameAdjustments 是一款基于 .NET Framework 4.7.2、运行在 BepInEx 上的全能插件，它将经济、战斗、地图、随机化、调试等功能全部折叠进一个可视化面板中，让你随时重塑 Silksong 的玩法。  
GameAdjustments 是一款基于 .NET Framework 4.7.2 的插件，专为 BepInEx 设计，将经济调整、战斗重写、地图辅助、随机化功能和开发工具整合到一个可视化面板中，让你随时重塑 Silksong 的玩法。

## 为什么要安装 GameAdjustments？ / Why Install GameAdjustments?
- **全局调教 / 总控**：地脉、贝壳碎片、丝线、敌人 AI、饰品常驻等所有系统均可随时调节；几分钟内即可构建专属规则。  
  地脉、贝壳碎片、丝线、敌人 AI、饰品常驻等所有系统均可随时调节，几分钟内即可构建专属规则。
- **创意库存与秒刷流程 / 创意库存与即时进度**：内置创意库存支持搜索、分类、直接授予任意物品，测试套路或回档极为方便。  
  内置的创意库存支持搜索、分类和直接授予任意物品，非常适合测试搭配或恢复丢失进度。
- **随机化 + 刷刷刷 / 随机化与重复挑战**：独立随机模式涵盖掉落、商店、获取日志，可与掉落倍数、无限补给叠加，持续保持挑战的新鲜感。  
  独立的随机模式影响掉落、商店和获取日志，并可与掉落倍数或无限补给叠加，确保无尽的可重玩性。
- **强大 UI 与多语言 / 强大的界面与本地化**：配置窗口支持浮动/最大化、字体缩放、收藏夹、键鼠/手柄热键，并预置中文、英文、西语文本。  
  界面支持浮动/最大化布局、字体缩放、收藏夹、键盘或手柄快捷键，内置中文化（zh-CN）、英语（en-US）和西班牙语（es-ES）文本。
- **开发者级调试工具 / 开发者级别的调试工具**：提供调试日志、碰撞盒可视化、多敌人克隆、PlayMaker FSM 调试等工具，适合创作者深入研究。  
  提供调试日志、碰撞盒可视化、多敌人克隆以及 PlayMaker FSM 调试等功能，是模组开发者和研究人员的完整工具集。

## 功能详解 / Feature Highlights

### 经济与掉落（Currency Settings） / 经济与掉落
- **无限货币 / 无限货币**：可解除 Geo、碎片、丝线的上限并设置拾取倍率或完全免税（`NoCurrencyCap`, `AddGeoMultiply`, `NoTakeGeo`, `AddShardsMultiply`, `NoTakeShards`, `AddSilkMultiply`, `NoTakeSilk`）。  
  移除货币上限，并可调节拾取倍率或完全免税（Geo、碎片、丝线）。
- **死亡惩罚反向化 / 死亡惩罚反向**：自定义死亡后保留比例，甚至让尸体返还 Geo（`HeroCorpseAddGeo`, `DeathGeoRetainPercent`）。  
  自定义死亡后剩余的 Geo 比例，甚至让尸体返还给你。
- **血量掉率挂钩 / 血量相关掉落**：按敌人最大生命值掉海量 Geo（`AllEntityDropMaxHpGeo`, `MaxHpDropGeoRatio`）。  
  根据敌人的最大生命值来调整 Geo 的掉落量，适合野耕玩法。

### 道具与背包（Item Settings） / 道具与背包
- **奖励倍增 / 奖励倍率**：批量提升奖励与掉落数量或禁止删物品（`AddItemMultiply`, `EntityDropMultiply`, `NoRemoveItem`）。  
  提升奖励和掉落数量，或防止游戏删除物品。
- **堆叠解锁 / 无限堆叠**：解除堆叠上限，囤物不再受限（`UnlockItemStackMax`）。  
  移除堆叠限制，可自由囤积。

### 地图与传送（GameMap + MapPin） / 地图与传送
- **永远开图 / 始终显示地图**：永远显示地图与跳蚤，开启自定义 Pin 及缩放（`AlwaysDrawMap`, `ShowFleaPin`, `EnableMapPin`, `MapPinScale`）。  
  始终保持地图可见，显示跳蚤标记，并可添加自定义缩放标记。
- **Pin 传送 / Pin 传送**：在地图 Pin 上瞬移，彻底告别跑路（`EnableMapMarkerTeleport`）。  
  直接从地图上通过标记进行瞬移。

### 饰品与工具（Charm Settings） / 饰品与工具
- **补给自动回充 / 工具自动回充**：工具使用后立刻回充或完全免素材（`ToolItemUseReplenish`, `NoLiquidCostOnRefill`）。  
  使用后立即恢复消耗性工具，或取消材料消耗。
- **关键饰品常驻 / 关键饰品永久生效**：罗盘、磁力、死囊、幸运骰等始终生效（`AlwaysEquippedCompassTool`, `AlwaysEquippedMagnetTool`, `AlwaysEquippedDeadPurseTool`, `AlwaysEquippedLuckyDiceTool`）。  
  默认保持导航与获取类饰品持续有效。
- **远程速射调参 / 快速发射调节**：定义全局装备状态与速射次数/冷却（`GeneralEquippedToolItems`, `QuickSlingShotCount`, `QuickSlingShotCD`）。  
  强制所有工具生效，并可调节快速射击的次数或冷却时间。

### 任务与活动（Quest Settings） / 任务与活动
- **Task QoL**：任务物品无敌、猎人日志全亮、跳过跳蚤小游戏（`QuestItemInvincible`, `HunterLogUnlockAll`, `SkipFleaGame`）。  
  让任务物品免疫伤害，自动填充猎人日志，或完全跳过跳蚤小游戏。

### 药剂与异常效果（Effect Settings） / 药剂与异常效果
- **增益倍率 / 增益倍率调节**：单独调整跳蚤药剂、火石、战士之怒的倍率。  
  分别调节跳蚤药剂、火石和战士之怒的增益倍率。
- **中毒调速 / 中毒自定义**：Tick 数、间隔、伤害量均可定义（`PoisonDamageTicks`, `PoisonDelay`, `PoisonDamageAmount`）。  
  通过自定义 Tick、延迟和伤害值来重新设计中毒效果。

### 战斗与移动（Game Settings） / 战斗与移动
- **伤害与范围 / 伤害与范围**：修改伤害倍率、攻击范围长宽高、暴击概率、蓄力时间。  
  调整伤害倍率、攻击框的长宽高、暴击几率和蓄力时间。
- **生存安全网 / 防御开关**：无攻击冷却、无限跳、免死模式、一次只受 1 点伤害、自定义受击恢复与无敌时间。  
  开启无攻击冷却、无限跳跃、非致命模式、单点伤害、或自定义受击后恢复和无敌时间。
- **移动无敌流 / 移动无敌**：冲刺无敌、持续/冷却/速度调节、全程无冷却，加速跑步/行走/冲刺。  
  赋予冲刺无敌状态，可调节持续时间、冷却时间与速度，甚至完全移除冲刺冷却。

### 自动战斗与丝线获取（AutoAttack + Silk Settings） / 自动战斗与丝线获取
- **自动攻击 / 自动攻击**：开启自动攻击，设置扫描半径并排除特殊区域（`AutoAttackEnable`, `AutoAttackRadius`, `AutoAttackExcludeCoalRegion`）。  
  开启自动化攻击，并可设定探测范围及黑名单区域。
- **全来源回丝 / 所有来源回丝**：让普攻、魔法、酸液、陷阱、火焰、电击乃至陷阱都能回丝（`Generic`, `Spell`, `Acid`, `Trap`, `Fire` 等 17 项）。  
  将所有伤害类型（从近战到闪电）设置为命中后返还丝线。

### 持续恢复与维修（Recover Settings） / 持续恢复与维修
- **定时回血 / 定时回复**：独立控制生命、丝线、工具的恢复频率与是否免费（`HPRecoverEnable`, `SilkRecoverEnable`, `ToolItemRepairEnable`, `NoMaterialCostForTools`）。  
  设置生命值、丝线和工具的自动回复频率，可选择是否免费。
- **破碎面具秒修 / 破碎面具即时修复**：连破碎面具都能自动修好（`ToolItemRepairIncludingFracturedMask`）。  
  通过相同系统自动修复破碎的面具。

### 多实体与敌人 AI（Entity Settings） / 多实体与敌人 AI
- **复制敌人 / 克隆敌人**：根据数量与生成节奏批量克隆敌人，打造弹幕战斗（`MultipleEnemiesEnabled`, `MultipleEnemiesSpawnCount`, `MultipleEnemiesSpawnInterval`）。  
  批量生成重复敌人以制造密集攻击场景。
- **Aggressive AI**：让敌人更主动、更凶狠（`AggressiveEnemies`）。  
  将敌人AI切换为激进模式，提升战斗难度。

### 随机模式（Random Mode Settings） / 随机模式
- **一键随机化 / 单次切换随机化**：开启后控制敌人掉落概率、商店最多显示数量、价格浮动区间。  
  开启后可随机调整敌人的掉落率、商店展示数量和价格波动范围。
- **优化体验 / 更优池子**：自动移除糟糕奖励，保持池子干净（`RemoveBadExperienceItems`）。  
  自动过滤掉令人沮丧的奖励，使奖励池更优质。
- **获取日志 / 获取记录**：在屏幕上展示抽到的战利品，支持自定义数量与透明度（`ShowAcquisitionLog`, `AcquisitionLogMaxCount`, `AcquisitionLogOpacity`）。  
  在界面上叠加可配置的最新收获记录。

### 地图资源（MapResource Settings） / 地图资源追踪
- **资源跟踪 / 可追踪资源**：标记面具碎片、丝卷、跳蚤、可破坏墙，调节连线宽度与最大追踪距离。  
  使用自定义线宽和范围高亮显示面具碎片、丝卷、跳蚤及可破坏墙壁。
- **丝球伴随 / 丝球跟随**：让丝球跟随玩家并设置速度（`SilkOrbFollowPlayer`, `SilkOrbFollowSpeed`）。  
  将丝球变为忠实随行者，并可调节追逐速度。

### 自定义血条（HealthBar Settings） / 自定义血条
- **样式切换 / 样式切换**：Normal 与 Simple 任意切换，并为 Boss 设置独立尺寸。  
  在 Normal 和 Simple 模式间自由切换，同时为首领设置独立大小。
- **像素级调校 / 像素级精确控制**：宽高、边框厚度、圆角、文字间距、飘字数量、动画速度、自动隐藏延迟全部可改。  
  可调节所有参数——形状、字体、浮动数字、平滑处理和隐藏延迟。
- **视觉特效 / 视觉效果**：支持噪点纹理、受击/治疗闪烁、屏幕边缘留白、越界销毁等效果。  
  添加噪点贴图、伤害/治疗闪烁、视口边距及越界清除规则。

### Creative Inventory 与 UI 体验 / 创意背包与 UI 体验
- **三页签布局 / 三标签布局**：All Items、Inventory、Categories 三个页签 + 搜索 + 详情面板，点击即可授予物品。  
  可浏览 All Items、Inventory 或 Categories，实时搜索，并直接从详情面板授予物品。
- **窗口模式 / 窗口模式**：浮动或最大化窗口、字体缩放、自动 UI 缩放、收藏夹、热键自定义（默认 `F11`/`JoystickButton8`）。  
  可选择浮动或全屏窗口，调整字体与 UI 大小，管理收藏夹，并自定义快捷键（默认 F11 / JoystickButton8）。
- **功能覆盖层 / 功能叠加层**：可在左上角显示“已启用功能”列表，随时了解当前外挂组合（`ShowEnabledFeaturesOverlay`）。  
  可选择在左上角显示当前激活的功能列表。

### 调试与创作者工具（Debug Settings） / 调试与创作者工具
- **日志与监控 / 日志与监控**：启用全局、攻击类型、自动攻击日志，打印物品池，查看启用功能。  
  可开启全局、攻击类型和自动攻击日志，或输出物品池用于分析。
- **可视化工具 / 可视化工具**：多敌人生成调试、敌人状态面板、PlayMaker FSM 调试、Collider2D Viewer。  
  提供敌人生成调试器、敌人状态叠加层、PlayMaker FSM 工具以及 2D 碰撞体可视化工具，便于研究。
- **实验开关 / 实验功能开关**：强制击晕、硬直、无敌、超级幸运骰、跳过开场动画（可调倍速）等一键控制。  
  可通过开关实现强制眩晕、无敌、超级幸运骰或快速跳过开场动画（支持变速）。

## 安装与使用 / Installation & Usage  
1. **准备环境 / Prepare Environment**：确认在《空洞骑士：丝之歌》目录中安装 BepInEx（推荐 6.x）。  
   确保 BepInEx（推荐 6.x）已安装在你的丝之歌文件夹内。  
2. **放置插件 / Drop the Plugin**：将编译好的 `GameAdjustments.dll`（或整个 `GameAdjustments` 文件夹）放进 `BepInEx/plugins/GameAdjustments/`。  
   将编译好的 `GameAdjustments.dll` 或其文件夹复制到 `BepInEx/plugins/GameAdjustments/` 中。  
3. **启动确认 / Launch Check**：进入游戏后检视 `BepInEx/LogOutput.log` 是否出现 `GameAdjustments` 横幅，确认成功加载。  
   启动游戏后检查 `GameAdjustments` 是否显示 `BepInEx/LogOutput.log` 横幅，以确认已正确加载。  
4. **打开配置 / Open Config**：按 `F11` 或手柄 `JoystickButton8` 打开 GUI，`Enter` 修改，`↑↓` 选择，`Tab` 切换焦点，`F10/ESC` 关闭。  
   使用 `F11`/`JoystickButton8` 打开 GUI，用 `Enter` 编辑，使用箭头键移动，用 `Tab` 切换焦点，用 `F10/ESC` 关闭。  
5. **收藏常用项 / Favorite Hot Toggles**：在任意项上加入 Favorites，常用组合（如冲刺无敌 + 无限跳）可一键启用。  
   将常用选项添加到 Favorites 中，以便一键启用如“冲刺无敌 + 无限跳跃”等组合。  
6. **Creative Inventory**：HUD 上点击 Creative 图标，按左列分类 → 中央网格 → 右侧详情的流程即可授予物品。  
   在 HUD 上点击 Creative 图标，选择左侧分类 → 中央网格中的项目 → 点击右侧详情即可授予物品。

## 提示与玩法灵感 / Tips & Play Ideas
- **极速动作流 / Hyper Action Build**：冲刺无敌 + 冲刺无冷却 + 自动攻击 = 无锁高速战斗体验。  
  结合冲刺无敌、冲刺无冷却和自动攻击，实现流畅的高速战斗体验。
- **随机 Roguelite / Random Roguelite Runs**：开启 Random Mode + RemoveBadExperienceItems，再通过 Acquisition Log 跟踪抽卡历史。  
  启用 Random Mode 并过滤掉不良奖励，然后使用获取日志追踪抽卡记录。
- **自定义难度 / DIY Difficulty**：搭配 Aggressive Enemies，再降低伤害倍率或减少恢复量，构建玻璃大炮或肉盾流。  
  结合激进的敌人AI，并降低玩家伤害或治疗值，模拟出玻璃炮或坦克类极端玩法。
- **创作者演示 / Creator Showcase**：使用 ShowEnabledFeaturesOverlay 显示配置、Collider Viewer 捕捉碰撞截图，便于录制或撰写文档。  
  叠加启用的功能并捕获碰撞视觉效果，用于制作教程或宣传片。

## 多语言与后续计划 / 定位与未来规划
- **内置语言 / 内置语言包**：包含简体中文、英语与部分西语译文，欢迎在 `LanguageDefaults` 中增补更多语言。  
  包含简体中文、英语和部分西班牙语翻译；欢迎在 `LanguageDefaults` 中添加更多语言。
- **持续拓展 / 持续扩展**：未来将接入更多 PatchFunction 功能并扩展 Creative Inventory 分类。提交 Issue 时请附 `AdvancedConfigSystem` 导出的配置，便于复现需求。  
  将增加更多 PatchFunction 功能，并扩展 Creative Inventory 分类——提交问题时请附上你的 `AdvancedConfigSystem` 配置导出文件以便重现需求。

下载并体验 GameAdjustments，让丝之歌真正变成你的游戏；  
下载 GameAdjustments 并立即体验，让丝之歌真正成为你的一部分。  
无论你是爽快向玩家、严谨的实验家，还是模组创作者，都能在这里找到属于自己的乐趣！  
无论是追求畅快游玩的玩家、严谨的实验者，还是模组开发者，都能在此找到专属的乐趣。

> 本 README 由 GPT-5 自动生成，如内容与实际功能存在出入，请以游戏内体验为准并及时反馈给我们。  
> This README was auto-generated by GPT-5. If any information diverges from in-game behavior, please trust the live build and let us know.
