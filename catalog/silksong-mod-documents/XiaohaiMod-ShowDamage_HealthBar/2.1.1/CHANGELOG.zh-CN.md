# Changelog / 更新日志

## 版本 2.1.1

### 修复内容 / Fixes / 修复内容

1. 修复了 GUI 填充边距范围不一致的问题：普通敌人的滑块现在覆盖 `0–40`，BOSS 的滑块覆盖 `0–80`。  
   修复 GUI 填充边距范围不一致：普通敌人为 `0–40`，BOSS 为 `0–80`。
2. 新增普通敌人血条的可选 `BG_Enemy.png` 背景支持。  
   新增普通敌人血条的可选 `BG_Enemy.png` 背景支持。
3. 自定义填充图默认自动裁剪外围透明像素，修复受击后看似不减少以及未死亡就提前显示为空的问题。  
   自定义填充图默认自动裁剪外围透明像素，修复受击后看似不减少以及未死亡就提前显示为空的问题。
4. 补充了热键、材质目录、BepInEx 首次运行、Linux/Proton 与英文安装说明。  
   补充热键、材质目录、BepInEx 首次运行、Linux/Proton 与英文安装说明。

## 版本 2.1.0

### 更新内容 / 更新内容

1. **最新游戏版本兼容性 / 最新游戏版本兼容**
   - 根据最新提供的游戏 API 重新核对血量与伤害调用链，并将构建目标更新为兼容 Unity 6 的 .NET Standard 2.1。
   - 根据最新提供的游戏 API 重新核对血量与伤害调用链，并将构建目标更新为兼容 Unity 6 的 .NET Standard 2.1。

2. **准确的伤害归因 / 准确的伤害归因**
   - 伤害数字现在只显示玩家实际造成的伤害，同时正确处理经 `SendDamageTo` 转发的伤害，并忽略环境及非玩家伤害。
   - 伤害数字现在只显示玩家实际造成的伤害，同时正确处理经 `SendDamageTo` 转发的伤害，并忽略环境及非玩家伤害。

3. **性能与生命周期优化 / 性能与生命周期优化**
   - 血条 UI 仅在首次有效玩家攻击后创建；伤害数字使用上限为 32 的复用池；圆角材质使用有界低分辨率缓存。
   - 血条 UI 仅在首次有效玩家攻击后创建；伤害数字使用上限为 32 的复用池；圆角材质使用有界低分辨率缓存。

4. **独立显示控制 / 独立显示控制**
   - 普通敌人血条、BOSS 血条和伤害数字可独立开关；关闭选项时会立即清理对应 UI。
   - 普通敌人血条、BOSS 血条和伤害数字可独立开关；关闭选项时会立即清理对应 UI。

5. **更安全的自定义材质 / 更安全的自定义材质**
   - 自定义 PNG 会在加载前校验（最大 8 MiB、4096×4096），并提供可靠的默认回退和资源清理。
   - 自定义 PNG 会在加载前校验（最大 8 MiB、4096×4096），并提供可靠的默认回退和资源清理。

6. **工程维护 / 工程维护**
   - 重建可维护的工程结构，移除反编译源码产物，并补充可复现的构建与发布元数据。
   - 重建可维护的工程结构，移除反编译源码产物，并补充可复现的构建与发布元数据。

## 版本 2.0.4

### 更新内容 / 更新内容

1. **首领血条自动隐藏 / 首领血条自动隐藏**
   - 当BOSS血量降至 0 或以下时，自动隐藏血条。需要注意的是，对于某些在阶段间转换的 BOSSes，可能需要再次攻击它们才能重新激活血条显示。
   - 当BOSS血量小于等于0时，自动隐藏血条。但是需要注意的是，某些BOSS转阶段后，需要重新攻击它来激活血条。

## 版本 2.0.3

### 更新内容 / 更新内容

1. **苍白之母Boss修复 / Silk Mother Boss Fix**
   - 彻底修复了"苍白之母"(Silk Mother)的血量显示异常，当前版本可以正确显示其血量，但是注意的是，该BOSS在血量低于等于0时，需要再次受到伤害才会进入下一阶段。
   - 完全修复了“苍白之母”（Silk Mother）的血量显示问题。当前版本能够正确显示其血量，但需要注意：当该 BOSS 的血量小于或等于 0 时，需要再次受到伤害才会进入下一阶段。

2. **性能优化 / Performance Optimization**
   - 在场景加载时就为BOSS创建血条组件，但暂时隐藏，以避免首次攻击BOSS时临时创建血条可能造成的轻微卡顿。
   - 在场景加载时即为BOSS创建血条组件，但暂不显示，从而避免了首次攻击BOSS时因首次生成血条而产生的轻微卡顿。

## 版本 2.0.2

### 更新内容 / Update Content

1. **苍白之母Boss适配 / Silk Mother Boss Adaptation**
   - 新增了对"苍白之母( Silk Mother )"Boss血量机制的专门适配
   - Added specialized adaptation for "Silk Mother" Boss health mechanism

2. **性能优化 / Performance Optimization**
   - 修复了部分区域异常卡顿的问题
   - Fixed stuttering issues in certain areas

3. **界面改进 / UI Improvements**
   - 暂时禁止了原始材质下圆角样式的选择
   - 修复了部分区域BOSS名字显示异常的问题
   - Temporarily disabled rounded corner style selection for original textures
   - Fixed abnormal BOSS name display issues in certain areas

## 版本 2.0.0

### 重大更新 / Major Updates

1. **问题修复 / Bug Fixes**
   - 修复了某些BOSS和普通敌人无法正确显示血条的BUG
   - Fixed bugs where certain BOSSes and normal enemies could not display health bars correctly

2. **增强自定义功能 / Enhanced Customization**
   - 新增了血条位置的精细调准
   - 新增BOSS血条背景的颜色自定义
   - 新增BOSS血量数值显示开关
   - Added fine-tuning for health bar positioning
   - Added BOSS health bar background color customization
   - Added toggle switch for BOSS health numbers display

3. **独立显示控制 / Independent Display Controls**
   - 新增BOSS和普通敌人血条的单独显示开关
   - Added separate toggle switches for BOSS and normal enemy health bars

4. **改进材质处理 / Improved Texture Handling**
   - 将血条缩小的模式从压缩改为剪切，可以适应更多的自定义材质
   - Changed health bar scaling mode from compression to clipping for better custom texture compatibility

5. **本地化改进 / Localization Improvements**
   - BOSS的名字将显示正确的游戏内名字，并支持多语言，而不是GameObject.name
   - BOSS names now display correct in-game names with multi-language support instead of GameObject.name

## 版本 1.0.9

### 新功能 / 新特性

1. **BOSS血条激活机制 / BOSS血条激活机制**
   - BOSS血条现在只有在BOSS首次受到伤害后才会显示
   - 这避免了战斗开始前剧透BOSS的存在
   - 通过保持惊喜元素改善游戏体验
   - BOSS血条现在只有在BOSS首次受到伤害后才会显示
   - 防止在战斗开始前剧透BOSS的存在
   - 通过保持惊喜元素改善游戏体验

## 版本 1.0.8

### 重大更新 / Major Update

1. **自定义材质支持 / 自定义材质支持**
- 增加了对自定义生命值条纹理的支持
- 从 DLL 目录/Texture/ 文件夹加载自定义纹理（HpBar.png 用于普通生命条，HpBar_Boss.png 用于首领生命条）
- 当自定义纹理不可用时，自动回退到默认纹理
- 可配置的纹理缩放模式：拉伸适配，保持宽高比
   - 新增自定义血条材质支持
- 从DLL目录/Texture/文件夹加载自定义材质（普通血条使用HpBar.png，BOSS血条使用HpBar_Boss.png）
   - 当自定义材质不可用时自动回退到默认材质
   - 可配置的材质缩放模式：拉伸适应、保持比例

2. **代码结构优化 / 代码结构优化**
- 大规模代码重构以提升性能和可维护性
- 优化了纹理加载和缓存系统
- 简化材质应用逻辑
- 减少内存分配并提升运行时性能
   - 大规模代码重构，提升性能和可维护性
   - 优化材质加载和缓存系统
   - 简化材质应用逻辑
   - 减少内存分配，提升运行时性能


---

## 版本 1.0.7（紧急修复版）

### 关键性能修复 / 紧急性能修复

1. **圆角算法优化 / Rounded Corner Algorithm Optimization**
- **URGENT FIX**: 完全解决了在显示带圆角的血条时出现的严重帧率下降（30-50 FPS 丢失）问题
- 移除了导致性能瓶颈的复杂算法和冗余日志记录
- 优化了更新方法的计算和距离检测算法
- 缓存常用组件以避免重复解析
   - **紧急修复**: 彻底解决了显示圆角血条时严重掉帧(损失30-50帧)的问题
   - 移除了导致性能瓶颈的复杂算法和冗余日志记录
- 优化了Update方法的计算和距离检测算法
   - 缓存常用组件避免重复解析

2. **备用解决方案 / Alternative Solution**
- 如果性能问题持续存在，请按 HOME 键切换到矩形模式以彻底解决
- 矩形模式提供最佳性能，且无帧丢失
- 如果仍然存在性能问题，按 HOME 键切换成长方形模式即可彻底解决
   - 长方形模式提供最佳性能，零帧数损失

---

## 版本 1.0.6

### 新功能 / 新特性

1. **血量阈值保护 / Health Threshold Protection**
   - 增加了怪物血量上限阈值判断。当敌人血量超过该阈值时，将不进行任何处理
   - 解决了巨大血量的未知敌人显示血条的问题
   - 防止了一些 BOSS 阶段转换时瞬间超过 99999 血量，导致最大生命值记录异常和血条比例错误的问题

2. **自定义血条尺寸 / Custom Health Bar Dimensions**
   - 新增两种血条的具体长宽（像素值）自定义功能

3. **圆角支持 / Rounded Corner Support**
   - 新增两种血条的圆角显示支持！
   - 你也可以随时切换回矩形模式~

4. **低血量自动白色文本 / Auto White Text on Low Health**
   - 新增可控制开关：“当敌人血量低于 49% 时，自动将血量文本颜色改为白色”
   - 该设置可在敌人血量较低时清晰显示具体数值

5. **恢复默认值 / Reset to Defaults**
   - 在配置 GUI 中新增了“恢复默认值”按钮
   - 一键将所有设置恢复为默认值

## 版本 1.0.5
- **重大修复**：修复了多个 BOSS 生命值条重叠的问题，通过智能位置管理实现
- **增强功能**：实现了 BOSS 生命值条管理器，用于自动计算位置
- **新功能**：新增游戏内 GUI 配置面板（按 HOME 打开）
- **新功能**：实时调整设置，并即时重建生命值条
- **增强功能**：所有配置选项现在均可通过直观的 GUI 界面访问
- **重大修复**：修复了多个首领血条重叠的问题，新增智能位置管理系统
- **改进**: 实现首领血条管理器，自动计算位置排列
- **新增**: 按 HOME（可配置）可以打开菜单，实现实时动态调整配置项。

## 版本 1.0.4
- **重大修复**：修复了三重防护机制下生命条残留的问题
- **新功能**：新增 BOSS 生命值条，支持自定义阈值、颜色和位置
- **新功能**：新增了 NumbersInsideBar 和 NumbersVerticalOffset 配置选项
- **增强**：优化了默认配置以更好地匹配游戏视觉风格
- **重大修复**: 修复血条残留问题，新增三重保护机制
- **新功能**: 新增首领血条，支持自定义阈值、颜色和位置
- **新功能**: 新增 NumbersInsideBar 和 NumbersVerticalOffset 配置项
- **改进**: 改善默认配置以更好匹配游戏风格

## 版本 1.0.3
- **新功能**: 修复血条方向并新增血量数值显示
- **改进**: 动态Canvas大小和实时血量更新
- **修复**: 修复血条残留和BOSS阶段转换问题

## 版本 1.0.2
- **新功能**: 新增血条颜色和大小自定义
- **改进**: 改善血条外观配置

## 版本 1.0.1  
- **首次发布**: 基本的敌人血条和伤害文本显示功能  
- **初始版本**: 基本的敌人血条和伤害文本显示功能  

---  

*更多信息，请参考 README.md 文件 / 更多信息请参考 README.md 文件*
