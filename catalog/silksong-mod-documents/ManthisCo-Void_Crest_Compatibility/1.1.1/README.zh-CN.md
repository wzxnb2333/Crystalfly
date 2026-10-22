# 空洞纹章兼容性补丁

为空洞纹章扩展功能性基础纹章动作集，恢复纹章机制，新增空洞质互动内容，提升音频与动画支持，增强纹章切换的安全性，并提供游戏内配置功能。

由 **Erymanthis** 制作，发布于 Thunderstore，归属 **ManthisCo**。

源码与版本发布：[github.com/Erymanthis/Void-Crest-Compatibility](https://github.com/Erymanthis/Void-Crest-Compatibility)

这是一个兼容性与扩展补丁。它 **不是独立的纹章模组**。

## 信用与原始模组

**DerVorce** 是原始 **空洞纹章** 模组的创作者，本兼容补丁依赖于该模组。对 DerVorce 原始模组、代码、机制及资源（包括其空洞质系统、影之冲刺、反击机制、缚丝、上位缚丝特殊攻击、空洞丝线技能、空洞工具槽系统、重新着色系统以及配置）的贡献，均应予以致谢。

The compatibility patch, its code, its redesigned crest artwork, and its six-slot visual layout are original work by **Erymanthis**. They are not assigned to or claimed as the property of DerVorce by this README. Each creator retains credit and ownership of their own contributions; no copyright is transferred by the credit language in this document.

原始模组的下载与支持请访问以下链接：

- [Void Crest by DerVorce on Nexus Mods](https://www.nexusmods.com/hollowknightsilksong/mods/978)
- [Void Crest by DerVorce on Thunderstore](https://thunderstore.io/c/hollow-knight-silksong/p/DerVorce/Void_Crest/)

空洞纹章也对 VoidBaroness 的针forge、Kaykao 提供的开发协助、Entwined Spectre 提供的原始纹章精灵图、King Furgo 的测试工作，以及空洞骑士模组社区表示感谢。这些致谢内容针对的是 DerVorce 原始版本的贡献；并不涉及本兼容补丁所包含的替换纹章艺术作品。有关 DerVorce 完整的致谢与文档，请访问其原始模组页面。

## 新功能

### 重新设计的空洞纹章艺术与插槽布局

兼容性补丁包含为本次发布特别制作的新版空洞纹章设计。它取代了原菜单中纹章的精灵图示和轮廓展示，同时继续使用空洞纹章注册的纹章、空洞颜色类别以及所有颜色有效的空洞插槽。

其六个空洞插槽采用以下布局：

- 中上：施法 + 向上；
- 中心：施法；
- 底部：施法 + 向下；
- 左侧、右侧和右下角：中立施法插槽。

该替换艺术与插槽布局设计由 **Erymanthis** 原创完成，未归属于 DerVorce 或原始纹章精灵艺术家。

### 可选基础纹章选择

空洞纹章可以使用其他纹章作为其常规攻击的基础。本补丁将该设置转变为一个完整的玩法系统，而不再仅复制启动时的简单攻击配置。

支持的菜单选项：

- 猎手
- 游荡者
- 死亡收割者
- 架构师
- 预言者
- 生物
- 女巫
- 无披风

所选纹章提供其完整的常规攻击组合，包括适用的：

- 普通与替代斩击；
- 向上与替代向上斩击；
- 向下与替代向下攻击；
- 墙面攻击；
- 冲刺斩击；
- 挑战/挑衅斩击；
- 纹章专属的攻击物体、动画数据和音效。

空洞纹章独有的 **暗影冲刺** 保持激活状态。本补丁未恢复或添加“冲刺斩击”，因为暗影冲刺是其设计上的替代方案。

空洞的普通缚丝与向上缚丝仍作为基础保留。仅在兼容性设计明确要求时，才会恢复基础纹章的缚丝效果。

### 猎手 — 聚焦与虚空质量伤害

选择猎手将自动跟随玩家实际的猎手进度：

- 在任何升级之前的基础猎手；
- 解锁首个进化后为猎手 v2；
- 解锁最终进化后为猎手 v3。

使用真实原版猎手的组合计数器和聚焦阈值。虚空质量仅在正确聚焦等级激活时提升可计入的钉刺伤害。

- 在 2 遮罩以上：每个虚空质量增加 25% 的虚空质量伤害倍率。
- 在 2 遮罩或更少时：每个虚空质量增加 35%。
- 工具、虚空反击以及空洞的特殊向上缚丝攻击被排除在外。
- 对于已识别的首领，猎手聚焦与虚空质量的总和上限为 3× 基础钉刺伤害。

首领识别支持正常游戏系统、首领称号及日志数据，以及社区公认的首领身份。

**当前虚空猎手识别由 Lost Sinner、Awakened Grand Mother Silk 和 Lost and Chained 添加或修改的首领。这些首领模组为可选兼容目标，并非依赖项。**

### 游荡者 — 虚空质量幸运值

虚空游荡者将虚空质量转化为暴击几率，同时保留游荡者的原版要求和幸运处理机制。

- 正常游荡者几率初始为 2%。
- 每个虚空质量增加 3.1 个百分点。
- 在 10 虚空质量时，暴击几率为 33%（在外部幸运值调整前）。
- 原版 9-丝线要求依然生效。
- 磁铁骰子及其他幸运值调整仅由游戏系统应用一次，而非被错误地叠加两次。

### 死亡收割者 — 虚空质量收获

虚空死亡收割者改变了虚空质量的获取方式：

- 在缚丝期间受到攻击仍会触发反击伤害。
- 选择死亡收割者时，缚丝反击不再生成虚空质量。
- 完成一次普通虚空缚丝将激活死亡收割者的原版收获模式。
- 死亡收割者的常规丝线包在视觉上被虚空化，并变为虚空质量球体。
- 球体不提供任何丝线。
- 每个球体价值 0.5 虚空质量。
- 两个球体构成一个完整的虚空质量。
- 积累的半单位会以半透明标记显示在虚空质量卷轴上。
- 在 10-虚空质量上限时收集的球体不提供额外资源。

原版死亡收割者仍负责收获持续时间、发放骰子、敌方资格判定、球体方向、抛掷速度以及收割命中效果。

### 生物 — 愤怒恢复

选择生物并完成一次普通虚空缚丝将激活原版愤怒机制。

本补丁恢复了游戏的正常愤怒系统，包括：

- 愤怒持续时间与效果；
- 愤怒伤害；
- 愤怒击中的治疗与治疗上限；
- 成功命中带来的愤怒持续时间延长；
- 愤怒攻击音效；
- 武士专属的弹跳舱行为；
- 生物/武士的咆哮及挑战音效路由。

虚空的特殊向上缚丝不会触发愤怒。

### 预言者 — 法术加成与缚丝行为

选择预言者将恢复其原版法术伤害倍率（适用于丝线技能），同时保留空洞纹章自身的丝线技能机制。

地面状态下的预言者缚丝路线也已恢复，并遵循虚空专属规则：

- 修复为零遮罩，符合空洞纹章的设计意图；
- 缚丝结束后的恢复帧为 8 帧；
- 这些恢复帧不会触发反击；
- 虚空的向上缚丝仍保持为虚空球体特殊攻击，而非变为预言者的普通特殊缚丝。

### 女巫攻击家族

玩家可选择的女巫选项与游戏内部女巫/鞭子攻击家族相关联，以确保攻击和音效正确。诅咒未作为可选基础纹章暴露或被视为女巫身份。空洞纹章 0.4.4 仍会接收其已验证的诅咒 HeroConfig 作为私有启动机制，之后本兼容性补丁将解决并移植真实的女巫/鞭子提供者。

这恢复了预期的攻击物体、反冲行为、动画标志、冲刺斩击以及攻击音效，而不会用女巫根系替换空洞的缚丝。

### 架构师与无披风

架构师恢复其完整的攻击树和墙面攻击行为。无披风也可提供其完整的攻击配置。

本次更新未为任一选择添加新的虚空质量被动或自定义缚丝重制。

## 使用体验与 ModMenu 兼容性

### 致谢 ModMenu

通过 **Silksong.ModMenu** 提供了游戏内的配置支持，由 **silksong_modding** 团队开发。

- [ModMenu on Thunderstore](https://thunderstore.io/c/hollow-knight-silksong/p/silksong_modding/ModMenu/)
- [Silksong.ModMenu source on GitHub](https://github.com/silksong-modding/Silksong.ModMenu)

感谢 ModMenu 贡献者提供了本补丁所使用的标准游戏内配置界面。

### 基础纹章菜单

空洞骑士原版的纹章设置从易出错的自由文本字段转换为可滚动的游戏内选择菜单。

较旧版本的猎手 v2/v3 的文本值表示为 `Hunter`，随后跟随玩家实际的猎手进度。菜单中显示 `Toolmaster`、`Spell`、`Warrior`，以及私有诅咒启动项，其游戏内名称分别为 `Architect`、`Shaman`、`Beast` 和 `Witch`，同时保留 Void Crest 0.4.4 所需的提供者 IDs。

### 安全纹章切换

基础纹章更改被长椅限制，以保护正在使用的攻击对象和存档/即时存档。

- 将选项改为远离长椅：新的基础会应用在下一个长椅上。
- 在长椅上时更改：当离开长椅时应用新的基础值。
- 如果无法完成切换，则当前动作集保持激活，所请求的变更将继续排队。

### 色彩劫持

`Tint Hijacking` 选项仅在装备纹章时控制斩击颜色。

- **开启：** 空洞骑士控制钉刺的色彩。
- **关闭：** 斜杠使用原版纹理、当前钉刺附魔的颜色，或当前皮肤模组的视觉效果。

原版纹章不会受到此选项的影响。

### 调试与测试选项

补丁包含可选的测试控制：

| 选项 | 目的 |
|---|---|
| 探索者 50 百分比暴击几率 | 设置空洞骑士探索者的最终暴击几率为 50%，且不允许可重复乘以骰子数值 |
| 探索者幸运追踪器 | 显示虚空质量、准备状态、原始/最终概率、幸运修正值、随机数生成结果、暴击次数、观测频率以及最后一次结果 |
| 设置空无质量数量 | 立即将 0–10 的活体空无质量设置，并更新空无卷轴 |
| 设置护甲为致命伤害 | 一次性猎手测试动作，将当前红色生命值设置为 2 护甲 |
| 激活专注等级 | 将猎手连招计数器设置为关闭、专注 1 或专注 2，当该进化解锁时 |
| 神职者法术伤害加成 | 开启或关闭神职者技能乘数以供测试使用 |

流浪者面板在暂停或Void流浪者未激活时会自动隐藏。

### 音频修复

基础纹章攻击音效被复制到移植的虚空攻击树中。该补丁还恢复了用于充能斩击、挑战、野兽咆哮以及其他攻击音效状态机的纹章检查。

空洞纹章仍拥有虚空冲刺与虚空缚丝的音效。

### 皮肤与指甲浸润支持

当禁用色调劫持时，补丁会更新多个动画帧中的活斩、下刺和蓄力斩的视觉效果。这有助于皮肤模组和指甲镶嵌保持其原本的颜色，而不是被虚空持续的着色系统立即覆盖。

### 诊断日志

BepInEx日志记录：

- 选中并加入队列的基底纹章变更；
- 猎手进化解析；
- 源攻击对象与移植攻击对象；
- 充能斩的结构与动画组件；
- 音频源被复制或缺失；
- 恢复了纹章/音频 FSM 分支；
- 被猎手使用的首领所触发的伤害上限；
- 复活者半/全虚空质量奖励；
- 调试动作和安全失败；
- 发现并修复了兼容性错误及降级行为。

## 修复内容

### 空洞工具与丝线技能的放置问题

- 修复了空洞纹章在需要红色工具和丝线技能插入特定槽位配置后，其施法绑定才能正常工作的缺陷。
- 现在最多可将三个红色工具和/或丝线技能装备到六个空洞槽位中的任意一个，不再依赖插入顺序。
- 当这些工具与技能被分配施法绑定时，它们会优先沿三槽中心脊柱生效：顶部为施法+上，中部为施法，底部为施法+下。

### 基础纹章的持久性问题

- 修复了空洞纹章在更改基础纹章后重新变为猎人或游戏初始纹章的问题。
- 修复了在 `ResetAllCrestState` 和 `UpdateConfig` 过程中选中的基础纹章状态丢失的问题。
- 修复了实时切换时旧攻击树仍被激活，或选择错误的运行时配置组的问题。
- 修复了猎人需要独立的内部 v2/v3 配置值问题。

### 攻击与碰撞盒修复

- 修复了移植后的斜线使用不兼容的自定义类型或过时的FSM事件的问题。
- 修复了当祭司乘数应用范围过广时，丝线技能丢失碰撞盒的问题。
- 修复了源攻击位于主纹章层级之外时，冲锋斜线对象被忽略的问题。
- 保留了空洞的影子冲刺对象、无敌状态、碰撞盒以及对冲刺斜线的替换。

### 视觉与动画修复

- 修复了空洞纹章橙色大黄蜂模型着色器，在游戏过程中可安全地开启或关闭，无需重新加载存档。
- 修复了缺失左右影子冲刺动画的问题，通过安全回退到正常冲刺动画数据解决。
- 修复了回退动画每次更新都从帧零开始的问题，通过保留请求的影子冲刺剪辑名称实现。
- 修复了下刺、额外斜线和冲锋斜线着色初始化异常的问题。
- 修复了皮肤模组组合导致大黄蜂或斜线效果呈现为静态黑色形状的问题。

### 冲锋斜线与着色修复

- 修复了冲锋斜线着色在法术/祭司上未能一致应用的问题。
- 修复了在冲锋斜线初始化过程中，着色状态中途发生变化的问题（受限于当前每个纹章的处理方式）。
- 修复了着色劫持影响原版纹章的问题。
- 修复了着色劫持布尔值行为与显示含义相反的问题。
- 修复了禁用着色劫持未能恢复激活皮肤或指甲灌注颜色的问题。

### 音效修复

- 修复了移植纹章中缺失冲锋斜线释放音效的问题。
- 修复了与冲锋斜线路径关联的大黄蜂语音未正确加载的问题。
- 修复了野兽/战士咆哮和挑战音效无法识别空洞所选基础纹章的问题。
- 修复了移植后的 AudioSource 设置及自定义音频曲线丢失的问题。

### 绑定修复

- 修复了基础纹章恢复导致空洞绑定无敌状态失效的问题。
- 修复了恢复的基础绑定路径在空洞本应无治疗时却错误地进行治疗的问题。
- 修复了祭司恢复无敌状态引发反击的问题。
- 修复了空洞的上方向绑定被错误替换为祭司、女巫或建筑师绑定行为的问题。
- 修复了普通空洞绑定无法激活战士狂怒或收割者收割模式的问题。

### 收割者修复

- 修复了收割者反击在收割经济移至能量球后仍继续生成虚空物质的问题。
- 修复了收割者能量球持续奖励丝线的问题。
- 修复了收割者能量球视觉仍保持丝线颜色的问题。
- 修复了聚集的虚空色包裹体泄漏到后续原版收割者使用中去的问题。
- 修复了银行存储的 0.5 值在虚空物质卷轴上无任何视觉表示的问题。

### 启动与场景安全

- 兼容性实现避免在菜单加载前初始化空洞纹章的颜色工具。
- 半管道校正运行于正常延迟帧更新中，防止启动失败干扰房间进入或重生位置的设置。

## 结论

空洞骑士纹章兼容性保持了 DerVorce 原始的空洞骑士纹章模组作为其核心游戏玩法基础，同时允许基础纹章设置表现得如同一个真实的游戏分支。

DerVorce 的模组仍然提供影之冲刺、反击、缚丝、缚丝上位球、虚空丝线技能、虚空质、虚空卷轴以及底层的虚空工具槽类型。本补丁提供了新的纹章设计与布局、动作集移植、原版纹章识别、分支机制、音效、动画、配置及必要的安全工作，以确保这些系统能够共存。

该项目专注于构建一个稳定可玩的基础。未来版本可能会完成更多纹章的全面重制，并解决剩余的兼容性问题，同时仅对每位贡献者本人的工作进行署名。

## 实验性纹章框架工作

本仓库现在包含一个实验性的、非运行时的基础架构，用于将兼容性工作泛化为可重用的纹章组合框架。它定义了功能元数据、模块化配方、依赖关系验证、结构化诊断，以及在任何 true 被接入已发布插件之前的一次 APIs 回滚事务。

- [Reverse-engineering evidence and current limits](docs/REVERSE_ENGINEERING_CREST_SYSTEM.md)
- [Framework architecture and migration plan](docs/CREST_FRAMEWORK_FOUNDATION.md)
- [Generated managed-IL crest inventory](docs/evidence/VANILLA_CREST_INVENTORY.md)
- [Gameplay validation matrix](docs/GAMEPLAY_VALIDATION_MATRIX.md)

这些 APIs 尚未稳定，实验目录中的注册并不意味着已实现或验证了游戏玩法适配器。

## 技术说明

### 版本和经过审核的依赖项

- 测试基础实现：空洞纹章 `0.4.4`
- 兼容性插件 ID：`io.github.erymanthis.voidcrestmovesetcompat`
- 空洞骑士纹章插件 ID: `io.dervoce.voidcrest`
- 最后发布 1.0.0 SHA-256: `2FF64872574A228356D4CB2398C51F59FD7DB4ADC189C948ECD11068AD03C05E`

兼容性插件具有硬性限制BepInEx对空洞骑士和针锋的依赖。它使用Silksong.ModMenu用于基础纹章选择元素及游戏内配置的展示。

### 兼容层不会替代的内容

以下内容仍由空洞纹章实现：

- 创建与注册纹章；
- 其HUD框架和虚空颜色类别；
- 全色相有效虚空插槽行为；
- 影之冲刺攻击及反击的碰撞框；
- 正常的缚丝持续时间与基础反击伤害；
- 绑定无敌状态；
- 提升上击缚丝的消耗、倍率、伤害、治疗效果以及虚空质量的消耗；
- 空洞骑士丝线技能消耗与倍率；
- 等离子转化；
- 整数空洞质量存储以及克隆的空洞线圈；
- 大黄蜂与光环颜色设置；
- 配置了反击/冲刺工具的协同效果。

兼容层仅拦截本README中描述的行为。

### 替换纹章艺术作品和插槽

兼容性DLL直接嵌入替换的纹章艺术，因此在运行时不再依赖下载文件夹、U盘存储或其他松散的图像路径。在空洞骑士纹章注册其 `CrestData` 后，它会替换当前正在使用的纹章精灵、轮廓和发光效果。

该补丁重新调整了六个现有插槽的位置，而非创建第二组。它们的 `ToolItemType` 仍为虚空。垂直方向的缚丝明确指定为从上到下依次为 上、中立 和 下；其余三个插槽均为中立。针forge随后接收到完整的插槽数组并重新生成了方向导航。

### 移动技能移植

针线匠的 `MovesetMaker.InitializeMoveset` 在初始化之后被补丁。当已初始化的动作集为虚空纹章的动作集时，兼容性插件：

1. 解除所选原版或 Needleforge 源头纹章；
2. 找到其生命 `HeroController.ConfigGroup`；
3. 复制其 `HeroControllerConfig` 为一个 `HeroConfigNeedleforge`；
4. 克隆源攻击根节点；
5. 重映射普通攻击、替代攻击、上击、替代上击、下击、替代下击、墙击、蓄力攻击和挑衅攻击的引用；
6. 分别克隆位于主攻击根节点之外的源对象；
7. 规范化兼容的下劈/斜斩/下割类型；
8. 复制攻击 AudioSource 设置；
9. 保留空洞骑士纹章的冲刺斩 FSM 编辑；
10. 重现并重新连接空洞骑士纹章的自定义影之冲刺作为 `DashStab`；
11. 设置并激活已完成的替换根节点；
12. 撤销先前移植的根部。

在 `HeroController.Start` 之前，空洞骑士的 `ToolCrest.heroConfig` 指向已解析的基础纹章。在纹章状态重置后，克隆的兼容性配置会被恢复。在 `UpdateConfig` 之后，当装备空洞骑士时，会强制将已解析的实时配置组重新定位。

### 功能性纹章装备代理

纹章始终是当前装备的纹章。在一组原版方法内部：

- `ToolBase.IsEquipped` 在装备了虚空时，对所选的基础纹章返回 true；
- 精确的纹章-ID 比较将空洞骑士与选定的基础 ID 视为等价；
- 无关工具、其他方法以及原版纹章玩法保持不变。

目标方法为：

- `HeroController.IsWandererLucky`
- `HeroController.Attack`
- `HeroController.NailHitEnemy`
- `HeroController.BindCompleted`
- `HeroController.Recoil(bool,bool)`
- `HeroController.RecoilDown`
- `HeroController.FallCheck`
- `DamageEnemies.DoDamage(GameObject,bool)`
- `HealthManager.TakeDamage(HitInstance)`
- `HeroAnimationController.UpdateToolEquipFlags`
- `SurfaceWaterRegion.OnTriggerEnter2D`
- `BouncePod.WillRespond`
- `BouncePod.Hit`

后缀也会为匹配的选定基础恢复 `IsHunterCrestEquipped`、`IsArchitectCrestEquipped` 和 `IsShamanCrestEquipped` 的结果。

### 伤害倍率安全

祭司与猎手的增益效果会在伤害计算前临时修改 `DamageEnemies.DamageMultiplier`。

原始值在后置处理和最终化路径中均被恢复。这可以防止修改后的乘数渗入池化的伤害对象或后续无关的攻击中，包括当原始伤害方法抛出异常时的情况。

空洞骑士自带的虚空丝线乘数仍为一个独立的 `DamageStack` 乘数，不会被替换。

### 射手计算

兼容性倍率是：

```text
Normal health:   1 + (Voidmass × 0.25)
2 Masks or less: 1 + (Voidmass × 0.35)
```

仅在原版猎手专注模式激活时生效。原版独立应用专注倍率。

对于已识别的首领，兼容性乘数受限于：

```text
3 ÷ active vanilla Focus multiplier
```

这使得焦点与虚空质量的叠加效果不会超过 3× 基础钉刺伤害。

首领检测使用 `BossSceneController`、首领标题FSM动作、一杀日志记录、标准化社区名称以及支持的首领重制模组的确切身份。识别出的运行时实例会被缓存。Lost Sinner、Awakened Grand Mother Silk 和 Lost and Chained 在存在时会被识别，但并非必需，也不会声明为依赖项。

### 游荡者计算

兼容性插件仅在流浪者暴击计算中替换几率获取器：

```text
raw chance = vanilla Wanderer chance + (Voidmass × 0.031)
final chance = raw chance × vanilla luck modifier
```

这在原版基础为 2% 时，会在掷骰/运气之前产生 33% 的 10 空虚质量。

调试选项 50% 在返回其测试概率之前会去除当前的幸运倍率，导致游戏后续的乘法运算最终得到实际的 50%。

### 魔鬼之球转换

原版收割者奖励逻辑仍会生成 `GlobalSettings.Gameplay.ReaperBundlePrefab` 至 `FlingUtils.SpawnAndFling`。

兼容性插件仅记录精确的预制体生成，并将空洞纹章的 `Voidify` 行为应用到生成的池化对象上。当该池在后续使用中脱离空洞收割者时，`UnvoidifyRoot` 恢复原始外观。

该包的 PlayMaker FSM 通常会到达 `Get Silk`，其中 `CallMethodProper` 调用 `HeroController.AddSilkParts`。补丁仅拦截了重斩包中的这一具体方法/行为组合，授予 0.5 空无质量，调用 `Finish`，并跳过丝线的调用。

空洞纹章将虚空质量存储为整数。因此，分数型复仇状态使用：

- 一个运行时的布尔值用于待处理的一半；
- 空洞骑士纹章的正常整数属性在第二半段之后；
- 一个临时的额外 `SilkChunk` 在 `Spool_VoidVersion` 上；
- 50% 该最终区块的精灵透明度；
- 兼容性 `LateUpdate` 在空洞骑士纹章的持续重色处理后重新应用透明度。

一半是仅在运行时生效的，并不会写入存档文件。

### 亡者反击压制

空洞骑士的 `HeroController.TakeDamage` 前缀在缚丝期间直接提升 `voidspool.voidMass`，并在生成反击伤害前生效。

兼容性前缀首先运行，并仅在以下条件下建立一个狭窄护盾：

- 已装备空洞纹章；
- 选择了收割者；
- 当前正在执行大黄蜂的缚丝动作。

当对 Voidmass 属性进行下一次递增写入时，其值被更改为当前值，随后护盾立即清除。其余关于空洞纹章反击的前缀行为正常运行，保留伤害、音效、碰撞框、冷却时间以及工具属性。

### 缚丝路由

插件追踪进入和退出 `cState.isBinding` 的过程，并将普通缚丝与 `VoidCrestPlugin.doingSpecialAttack` 分开处理。

- 战士和收割者在对应状态未激活时，会调用原版 `BindCompleted`。
- 术士的已知地面普通路径通过其特定的 `Spell Control` FSM 纹章检查启用。
- `HeroController.AddHealth(int)` 只在被追踪的术士地面缚丝期间被阻断。
- 在退出缚丝时，移除术士的治疗封锁，并为 8 帧渲染保留一个独立的无敌源。
- 因为恢复发生在缚丝结束后（即 false 之后），因此在缚丝期间无法执行空洞纹章的反击。
- 特殊上抬缚丝被排除在所有三种结果之外。

### 音效路由

每个来源攻击对象的 AudioSources 通过相对变换路径和组件索引复制到其移植后的对应体中。音效、混音器、标志位、播放设置、3D 设置、距离衰减/卷降以及自定义曲线均被复制。

`CheckIfCrestEquipped.IsTrue` 仅在以下场景中进行后处理：

- `crestAttacksFSM`；
- `silkSpecialFSM`；
- `Nail Arts` FSM；
- `Spell Control` 中识别的术士地面路径。

故意不代理任意 `spellControl` 检查。这防止了女巫根、建筑师工艺缚丝以及无关的缚丝分支替换空洞纹章的缚丝行为。

### 动画降级

当装备空洞纹章时：

- 当需要时，`Shadow Dash` 会降级为 `Dash` 的克隆；
- `Shadow Dash Down` 会降级为 `Dash Down` 的克隆。

克隆的动画片段会被重命名为最初请求的影之冲刺名称。若返回仍名为 `Dash` 的动画片段，动画控制器将不断从帧零重新开始，因为其请求的动画名称从未被识别为正在播放。

### 颜色渲染实现

当关闭“颜色劫持”时，期望的斩击颜色来自当前激活的 `NailImbuementConfig.NailTintColor`，若无附魔则为白色。

重染钩子在以下时机运行：

- `NailSlash.StartSlash`；
- `NailAttackBase.OnSlashStarting`；
- `NailAttackBase.SetNailImbuement`；
- `Downspike.StartSlash`；
- `HeroExtraNailSlash.OnEnable`。

一个临时运行时组件会立即应用该颜色，并持续 12 `LateUpdate` 帧以确保动画和精灵初始化完成。它会重染子级 tk2d 精灵和 SpriteRenderers，但拒绝对英雄根节点进行操作。

根据当前“颜色劫持”策略，空洞纹章的豁免数组会添加或移除斩击根节点。法术/术士因其斩击根节点初始化和重染时间与其它纹章实现不同而获得特殊处理。

### 橙色大黄蜂着色器实时刷新

空洞纹章已双向刷新 **启用橙色光环着色器**。其独立的 **启用橙色大黄蜂模型着色器** 设置通常由颜色监控器采样：开启时可在游戏过程中改变大黄蜂的颜色，关闭时不会恢复已被监控器更改的颜色。

本补丁在兼容性插件加载期间订阅大黄蜂着色器设置。当装备空洞纹章时：

- 开启该设置会立即要求空洞纹章将自身的颜色系统应用于大黄蜂；
- 关闭该设置会立即要求空洞纹章恢复其为大黄蜂渲染器保存的颜色；
- 如果独立配置的橙色光环仍处于开启状态，补丁在恢复大黄蜂颜色后会重新应用该着色器至 `HeroLight`。

当兼容性插件卸载时，移除该处理器。它不会替代空洞纹章的着色器，也不会在其他纹章装备时修改设置。

### 仅运行时状态与重置规则

兼容性插件会保留以下运行时状态：

- 当前、观察中、待处理及上一次基础纹章选择；
- 长椅进入和退出应用；
- 普通/特殊缚丝过渡；
- 术士治疗抑制与恢复保护；
- 收割者的储蓄半格与显示半格；
- 单次写入的收割者反击护盾；
- 游荡者追踪计数器；
- 猎手调试选择；
- 被识别的首领实例；
- 诊断日志去重。

当 Voidmass 变为 0，或达到 10，或设置调试 Voidmass，收割者变为非活动状态，基础纹章切换离开收割者，或插件卸载时，收割者的半格将被清除。

### Harmony 钩子记录

版本 1.1.0 包含 22 个 Harmony 补丁类：

1. `ShamanNormalBindHealingPatch` → `HeroController.AddHealth(int)`  
2. `MovesetMakerInitializePatch` → `MovesetMaker.InitializeMoveset`  
3. `HeroControllerStartPreparePatch` → `HeroController.Start` 前缀  
4. `HeroControllerStartLogPatch` → `HeroController.Start` 后缀  
5. `HeroControllerResetAllCrestStatePatch` → `HeroController.ResetAllCrestState(bool)`  
6. `HeroControllerUpdateConfigPatch` → `HeroController.UpdateConfig`  
7. `VanillaBaseCrestFunctionPatch` → 13 针对原版方法的靶向钩子  
8. `CrestDamageCompatibilityPatch` → `DamageEnemies.DoDamage(GameObject,bool)`  
9. `ReaperBundlePickupPatch` → `CallMethodProper.OnEnter`  
10. `ReaperBundleAppearancePatch` → `FlingUtils.SpawnAndFling`  
11. `ReaperRetaliationVoidmassGuardPatch` → `HeroController.TakeDamage(...)`  
12. `VoidmassResetClearsReaperFractionPatch` → `voidspool.voidMass` 设置器  
13. `ReaperFractionalVoidmassSpoolPatch` → `SilkSpool.ChangeSilk`  
14. `VanillaBaseCrestQueryPatch` → 三个原版纹章查询方法  
15. `CrestAttackAudioFsmCheckPatch` → `CheckIfCrestEquipped.IsTrue`  
16. `HeroAnimationControllerGetClipPatch` → `HeroAnimationController.GetClip`  
17. `NailSlashStartPatch` → `NailSlash.StartSlash`  
18. `NailAttackBaseSlashStartingPatch` → `NailAttackBase.OnSlashStarting`  
19. `NailAttackBaseImbuementPatch` → `NailAttackBase.SetNailImbuement`  
20. `DownspikeStartPatch` → `Downspike.StartSlash`  
21. `HeroExtraNailSlashEnablePatch` → `HeroExtraNailSlash.OnEnable`  
22. `HeroExtraNailSlashChargeLogResetPatch` → `HeroExtraNailSlash.OnEnable`

额外的非 Harmony 行为通过插件 `Update`、`LateUpdate`、`OnGUI`、`OnDestroy`、ModMenu 的注册，配置变更处理器（包括橙色大黄蜂着色器刷新）以及临时实时斩击重染组件实现。

### 当前边界与未来工作

- 收割者的半格不具有存档持久性。
- 猎手首领识别仍依赖启发式方法，直到出现通用游戏/社区首领 API。
- 移动技能和音效移植依赖于当前原版/针刺forge攻击树结构。
- 颜色恢复使用一个 12 帧修正窗口，而非永久拥有每个精灵。
- 女巫反击根、收割者超出当前收获转换的特性、弹幕过载以及未完成的纹章重制均不包含在 1.1.0 中。
- 基础纹章变更仍有意保持长椅门限，以确保稳定性。

## 清洁室组件实验

[`CrestComponentLab`](src/CrestComponentLab/README.md) 是一个仅限 Needleforge 使用的独立测试模组。它创建了一个默认不可激活的纹章，并独立配置了其核心动作集、冲刺斩击、蓄力斩击、普通缚丝提供者以及特殊向上缚丝提供者。该模组不依赖于虚空纹章或本兼容插件。

请参见 [`CREST_COMPONENT_LAB_TEST.md`](docs/CREST_COMPONENT_LAB_TEST.md) 以了解安装方法和游戏机制矩阵。
