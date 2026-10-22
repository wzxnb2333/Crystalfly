# GameAdjustments —— 丝之歌的全能增强模组
*GameAdjustments – The Ultimate Silksong Overhaul*

GameAdjustments 是一款基于 .NET Framework 4.7.2、运行在 BepInEx 上的全能插件，它将经济、战斗、地图、随机化、调试等功能全部折叠进一个可视化面板中，让你随时重塑 Silksong 的玩法。  
GameAdjustments is a .NET Framework 4.7.2 plugin for BepInEx that bundles economy tweaks, combat rewrites, map helpers, randomizers, and dev tools into a single GUI so you can reshape Silksong on the fly.

## 为什么要装 GameAdjustments？ / Why Install GameAdjustments?
- **全局调教 / Total Control**：Geo、碎片、丝线、敌人 AI、饰品常驻等所有系统都能随时调节；几分钟就能做出一套专属规则。  
  Everything from Geo, shards, silk, enemy AI, to permanent charms is configurable, so you can craft a custom ruleset within minutes.
- **创意库存与秒刷流程 / Creative Inventory & Instant Progression**：内置 Creative Inventory 支持搜索、分类、直接授予任意物品，测试套路或回档极其方便。  
  The Creative Inventory lets you search, browse, and grant any item instantly, perfect for testing builds or recovering lost progress.
- **随机化 + 刷刷刷 / Randomization Meets Grinding**：独立随机模式涵盖掉落、商店、获取日志，还能与掉落倍数、无限补给叠加，保持挑战新鲜感。  
  A dedicated Random Mode affects drops, shops, and acquisition logs, and stacks with drop multipliers or infinite stock for endless replayability.
- **强大 UI 与多语言 / Rich UI & Localization**：配置窗口支持浮动/最大化、字体缩放、收藏夹、键鼠/手柄热键，并预置中文、英文、西语文本。  
  The UI offers floating/maximized layouts, font scaling, favorites, keyboard/gamepad hotkeys, and ships with zh-CN, en-US, and es-ES strings.
- **开发者级调试工具 / Developer-Grade Tooling**：调试日志、碰撞盒可视化、多敌人克隆、PlayMaker FSM 调试等工具一应俱全，适合创作者深入研究。  
  Debug logging, collider visualization, multi-enemy cloning, and PlayMaker FSM inspection make it a full toolkit for modders and researchers.

## 功能详解 / Feature Highlights

### 经济与掉落（Currency Settings） / Economy & Drops
- **无限货币 / Unlimited Currencies**：可解除 Geo、碎片、丝线的上限并设置拾取倍率或完全免税（`NoCurrencyCap`, `AddGeoMultiply`, `NoTakeGeo`, `AddShardsMultiply`, `NoTakeShards`, `AddSilkMultiply`, `NoTakeSilk`）。  
  Remove currency caps and tune pickup multipliers or taxes for Geo, shards, and silk.
- **死亡惩罚反向化 / Death Penalty Reversal**：自定义死亡后保留比例，甚至让尸体返还 Geo（`HeroCorpseAddGeo`, `DeathGeoRetainPercent`）。  
  Decide how much Geo survives death—or make your corpse pay you back.
- **血量掉率挂钩 / HP-Based Drops**：按敌人最大生命值掉海量 Geo（`AllEntityDropMaxHpGeo`, `MaxHpDropGeoRatio`）。  
  Scale Geo rewards with enemy max HP for wild farming sessions.

### 道具与背包（Item Settings） / Items & Inventory
- **奖励倍增 / Reward Multipliers**：批量提升奖励与掉落数量或禁止删物品（`AddItemMultiply`, `EntityDropMultiply`, `NoRemoveItem`）。  
  Multiply rewards and drops, or prevent the game from removing items.
- **堆叠解锁 / Infinite Stack**：解除堆叠上限，囤物不再受限（`UnlockItemStackMax`）。  
  Remove stack caps so you can hoard freely.

### 地图与传送（GameMap + MapPin） / Map & Teleport
- **永远开图 / Always-On Map**：永远显示地图与跳蚤，开启自定义 Pin 及缩放（`AlwaysDrawMap`, `ShowFleaPin`, `EnableMapPin`, `MapPinScale`）。  
  Keep the map revealed, show flea pins, and add your own scalable markers.
- **Pin 传送 / Pin Teleportation**：在地图 Pin 上瞬移，彻底告别跑路（`EnableMapMarkerTeleport`）。  
  Teleport between markers straight from the map.

### 饰品与工具（Charm Settings） / Charms & Tools
- **补给自动回充 / Auto Refill Tools**：工具使用后立刻回充或完全免素材（`ToolItemUseReplenish`, `NoLiquidCostOnRefill`）。  
  Instantly replenish consumable tools or remove material costs.
- **关键饰品常驻 / Permanent Utilities**：罗盘、磁力、死囊、幸运骰等始终生效（`AlwaysEquippedCompassTool`, `AlwaysEquippedMagnetTool`, `AlwaysEquippedDeadPurseTool`, `AlwaysEquippedLuckyDiceTool`）。  
  Keep navigation and loot charms active by default.
- **远程速射调参 / Quick Sling Shot**：定义全局装备状态与速射次数/冷却（`GeneralEquippedToolItems`, `QuickSlingShotCount`, `QuickSlingShotCD`）。  
  Force all tools equipped and tune quick-shot count or cooldown.

### 任务与活动（Quest Settings） / Quests & Minigames
- **Task QoL**：任务物品无敌、猎人日志全亮、跳过跳蚤小游戏（`QuestItemInvincible`, `HunterLogUnlockAll`, `SkipFleaGame`）。  
  Make quest items invulnerable, auto-fill Hunter Logs, or skip the flea minigame entirely.

### 药剂与异常效果（Effect Settings） / Brews & Status Effects
- **增益倍率 / Buff Scaling**：单独调整跳蚤药剂、火石、战士之怒的倍率。  
  Scale flea brews, flintstones, and warrior rage bonuses per buff.  
- **中毒调速 / Poison Customization**：Tick 数、间隔、伤害量均可定义（`PoisonDamageTicks`, `PoisonDelay`, `PoisonDamageAmount`）。  
  Redesign poison pacing via custom ticks, delays, and damage.

### 战斗与移动（Game Settings） / Combat & Mobility
- **伤害与范围 / Damage & Reach**：修改伤害倍率、攻击范围长宽高、暴击概率、蓄力时间。  
  Re-tune damage multipliers, hitbox dimensions, crit chance, and charge times.
- **生存安全网 / Defensive Toggles**：无攻击冷却、无限跳、免死模式、一次只受 1 点伤害、自定义受击恢复与无敌时间。  
  Enable no attack cooldown, unlimited jumps, non-lethal mode, single-damage hits, or custom heal-on-hit/iframes.
- **移动无敌流 / Mobility Invincibility**：冲刺无敌、持续/冷却/速度调节、全程无冷却，加速跑步/行走/冲刺。  
  Grant dash invincibility, tweak duration/cooldown/speed, or remove dash cooldown altogether.

### 自动战斗与丝线获取（AutoAttack + Silk Settings） / Auto Combat & Silk Gains
- **自动攻击 / Auto Attacks**：开启自动攻击，设置扫描半径并排除特殊区域（`AutoAttackEnable`, `AutoAttackRadius`, `AutoAttackExcludeCoalRegion`）。  
  Toggle automated strikes with a configurable detection radius and blacklist.
- **全来源回丝 / Silk from Everything**：让普攻、魔法、酸液、陷阱、火焰、电击乃至陷阱都能回丝（`Generic`, `Spell`, `Acid`, `Trap`, `Fire` 等 17 项）。  
  Mark any damage type—from melee to lightning—to refund silk on hit.

### 持续恢复与维修（Recover Settings） / Passive Recovery & Repairs
- **定时回血 / Timed Regeneration**：独立控制生命、丝线、工具的恢复频率与是否免费（`HPRecoverEnable`, `SilkRecoverEnable`, `ToolItemRepairEnable`, `NoMaterialCostForTools`）。  
  Schedule health, silk, and tool regeneration, optionally with zero costs.
- **破碎面具秒修 / Instant Fractured Mask Repair**：连破碎面具都能自动修好（`ToolItemRepairIncludingFracturedMask`）。  
  Automatically restore fractured masks via the same system.

### 多实体与敌人 AI（Entity Settings） / Multi-Entities & AI
- **复制敌人 / Clone Enemies**：根据数量与生成节奏批量克隆敌人，打造弹幕战斗（`MultipleEnemiesEnabled`, `MultipleEnemiesSpawnCount`, `MultipleEnemiesSpawnInterval`）。  
  Spawn waves of duplicate enemies to stress test encounters.
- **Aggressive AI**：让敌人更主动、更凶狠（`AggressiveEnemies`）。  
  Flip enemies into overdrive for hardcore sessions.

### 随机模式（Random Mode Settings） / Random Mode
- **一键随机化 / One-Switch Randomizer**：开启后控制敌人掉落概率、商店最多显示数量、价格浮动区间。  
  A single toggle randomizes drop chances, shop slots, and price multipliers.
- **优化体验 / Better Pools**：自动移除糟糕奖励，保持池子干净（`RemoveBadExperienceItems`）。  
  Filter out frustrating rewards from the pool.  
- **获取日志 / Acquisition Log**：在屏幕上展示抽到的战利品，支持自定义数量与透明度（`ShowAcquisitionLog`, `AcquisitionLogMaxCount`, `AcquisitionLogOpacity`）。  
  Overlay a configurable log of your latest finds.

### 地图资源（MapResource Settings） / Map Resource Tracking
- **资源跟踪 / Trackables**：标记面具碎片、丝卷、跳蚤、可破坏墙，调节连线宽度与最大追踪距离。  
  Highlight mask shards, silk spools, fleas, and breakable walls with custom line widths and ranges.
- **丝球伴随 / Silk Orb Companion**：让丝球跟随玩家并设置速度（`SilkOrbFollowPlayer`, `SilkOrbFollowSpeed`）。  
  Turn silk orbs into loyal followers with adjustable chase speed.

### 自定义血条（HealthBar Settings） / Custom Health Bar
- **样式切换 / Style Switch**：Normal 与 Simple 任意切换，并为 Boss 设置独立尺寸。  
  Swap between Normal or Simple styles with separate boss sizing.
- **像素级调校 / Pixel-Perfect Control**：宽高、边框厚度、圆角、文字间距、飘字数量、动画速度、自动隐藏延迟全部可改。  
  Tune every aspect—shape, typography, floating numbers, smoothing, and hide delays.
- **视觉特效 / Visual Flair**：支持噪点纹理、受击/治疗闪烁、屏幕边缘留白、越界销毁等效果。  
  Add noise overlays, damage/heal flashes, viewport margins, and offscreen cleanup rules.

### Creative Inventory 与 UI 体验 / Creative Inventory & UI UX
- **三页签布局 / Triple-Tab Layout**：All Items、Inventory、Categories 三个页签 + 搜索 + 详情面板，点击即可授予物品。  
  Browse All, Inventory, or Categories, search in real time, and grant items directly from the detail pane.
- **窗口模式 / Window Modes**：浮动或最大化窗口、字体缩放、自动 UI 缩放、收藏夹、热键自定义（默认 `F11`/`JoystickButton8`）。  
  Choose floating/maximized modes, scale fonts/UI, manage favorites, and rebind hotkeys (default F11 / JoystickButton8).
- **功能覆盖层 / Feature Overlay**：可在左上角显示“已启用功能”列表，随时了解当前外挂组合（`ShowEnabledFeaturesOverlay`）。  
  Optionally render a top-left overlay listing active boosts.

### 调试与创作者工具（Debug Settings） / Debug & Creator Tools
- **日志与监控 / Logging & Monitoring**：启用全局、攻击类型、自动攻击日志，打印物品池，查看启用功能。  
  Toggle global, attack-type, and auto-attack logs or dump item pools for analysis.
- **可视化工具 / Visualization**：多敌人生成调试、敌人状态面板、PlayMaker FSM 调试、Collider2D Viewer。  
  Spawn debugger, enemy overlays, PlayMaker FSM tools, and 2D collider visualization streamline research.
- **实验开关 / Experiment Toggles**：强制击晕、硬直、无敌、超级幸运骰、跳过开场动画（可调倍速）等一键控制。  
  Flip switches for forced stuns, invincibility, super lucky dice, or fast-forwarded intros.

## 安装与使用 / Installation & Usage
1. **准备环境 / Prepare Environment**：确认在《Hollow Knight: Silksong》目录中安装 BepInEx（推荐 6.x）。  
   Make sure BepInEx (6.x recommended) is installed inside your Silksong folder.
2. **放置插件 / Drop the Plugin**：将编译好的 `GameAdjustments.dll`（或整个 `GameAdjustments` 文件夹）放进 `BepInEx/plugins/GameAdjustments/`。  
   Copy the built `GameAdjustments.dll` or its folder into `BepInEx/plugins/GameAdjustments/`.
3. **启动确认 / Launch Check**：进入游戏后检视 `BepInEx/LogOutput.log` 是否出现 `GameAdjustments` 横幅，确认成功加载。  
   Launch the game and verify the `GameAdjustments` banner appears in `BepInEx/LogOutput.log`.
4. **打开配置 / Open Config**：按 `F11` 或手柄 `JoystickButton8` 打开 GUI，`Enter` 修改，`↑↓` 选择，`Tab` 切换焦点，`F10/ESC` 关闭。  
   Use `F11`/`JoystickButton8` to open the GUI, `Enter` to edit, arrows to move, `Tab` to switch focus, and `F10/ESC` to close.
5. **收藏常用项 / Favorite Hot Toggles**：在任意项上加入 Favorites，常用组合（如冲刺无敌 + 无限跳）可一键启用。  
   Favorite your go-to toggles to bundle combos like Dash Invincible + Unlimited Jumping.
6. **Creative Inventory**：HUD 上点击 Creative 图标，按左列分类 → 中央网格 → 右侧详情的流程即可授予物品。  
   Open the HUD Creative icon, pick a category, select an item, then grant it with one click.

## 提示与玩法灵感 / Tips & Play Ideas
- **极速动作流 / Hyper Action Build**：冲刺无敌 + 冲刺无冷却 + 自动攻击 = 无锁高速战斗体验。  
  Combine dash invincibility, dash no cooldown, and auto attack to mimic stylish action games.
- **随机 Roguelite / Random Roguelite Runs**：打开 Random Mode + RemoveBadExperienceItems，再看 Acquisition Log 追踪抽卡历史。  
  Enable Random Mode with bad rewards filtered out, then track your pulls with the acquisition log.
- **自定义难度 / DIY Difficulty**：配合 Aggressive Enemies，再调低伤害倍率或减少恢复量，构建玻璃大炮或肉盾流。  
  Pair aggressive AI with lower player damage or healing to simulate glass cannon/tank extremes.
- **创作者演示 / Creator Showcase**：用 ShowEnabledFeaturesOverlay 显示配置、Collider Viewer 捕捉碰撞截图，方便录制或撰写文档。  
  Overlay enabled features and capture collider visuals for tutorials or trailers.

## 多语言与后续计划 / Localization & Future Plans
- **内置语言 / Built-in Locales**：包含简体中文、英语与部分西语译文，欢迎在 `LanguageDefaults` 中增补更多语言。  
  Ships with Simplified Chinese, English, and partial Spanish; extend `LanguageDefaults` to add more locales.
- **持续拓展 / Ongoing Expansion**：未来会接入更多 PatchFunction 功能并扩展 Creative Inventory 分类。提交 Issue 时请附 `AdvancedConfigSystem` 导出的配置，便于复现需求。  
  More PatchFunction features and Creative Inventory tabs are on the roadmap—attach your exported `AdvancedConfigSystem` configs when filing issues.

下载并体验 GameAdjustments，让丝之歌真正变成你的游戏；  
Download GameAdjustments today and make Silksong truly yours.  
无论你是爽快向玩家、严谨的实验家，还是模组创作者，都能在这里找到属于自己的乐趣！  
Whether you play for fun, run experiments, or build mods, this plugin has the tools you need.

> 本 README 由 GPT-5 自动生成，如内容与实际功能存在出入，请以游戏内体验为准并及时反馈给我们。  
> This README was auto-generated by GPT-5. If any information diverges from in-game behavior, please trust the live build and let us know.
