# Changelog / 更新日志

## Version 2.1.1

### Fixes / 修复内容

1. Fixed the GUI fill-margin range mismatch: normal-enemy sliders now cover `0–40`, and boss sliders cover `0–80`.
   修复 GUI 填充边距范围不一致：普通敌人为 `0–40`，BOSS 为 `0–80`。
2. Added optional `BG_Enemy.png` background support for normal-enemy health bars.
   新增普通敌人血条的可选 `BG_Enemy.png` 背景支持。
3. Added default-on transparent-border trimming for custom fill textures, fixing delayed visible reduction and premature empty appearance.
   自定义填充图默认自动裁剪外围透明像素，修复受击后看似不减少以及未死亡就提前显示为空的问题。
4. Expanded hotkey, texture-directory, BepInEx first-run, Linux/Proton, and English installation guidance.
   补充热键、材质目录、BepInEx 首次运行、Linux/Proton 与英文安装说明。

---

## Version 2.1.0

### Updates / 更新内容

1. **Latest Game Compatibility / 最新游戏版本兼容**
   - Revalidated the health and damage call chain against the latest supplied game API and updated the build target to Unity 6-compatible .NET Standard 2.1.
   - 根据最新提供的游戏 API 重新核对血量与伤害调用链，并将构建目标更新为兼容 Unity 6 的 .NET Standard 2.1。

2. **Accurate Damage Routing / 准确的伤害归因**
   - Damage numbers now display only actual player damage, including damage redirected through `SendDamageTo`, while environmental and non-player damage is ignored.
   - 伤害数字现在只显示玩家实际造成的伤害，同时正确处理经 `SendDamageTo` 转发的伤害，并忽略环境及非玩家伤害。

3. **Performance and Lifecycle Improvements / 性能与生命周期优化**
   - Health bar UI is created only after the first valid player hit. Damage text uses a reusable pool capped at 32 entries, and rounded texture generation uses a bounded low-resolution cache.
   - 血条 UI 仅在首次有效玩家攻击后创建；伤害数字使用上限为 32 的复用池；圆角材质使用有界低分辨率缓存。

4. **Independent Display Controls / 独立显示控制**
   - Enemy bars, boss bars, and damage text can be enabled independently. Disabling an option immediately cleans up its active UI.
   - 普通敌人血条、BOSS 血条和伤害数字可独立开关；关闭选项时会立即清理对应 UI。

5. **Safer Custom Textures / 更安全的自定义材质**
   - Custom PNG files are validated before loading (maximum 8 MiB and 4096×4096), with reliable fallback and resource cleanup.
   - 自定义 PNG 会在加载前校验（最大 8 MiB、4096×4096），并提供可靠的默认回退和资源清理。

6. **Project Maintenance / 工程维护**
   - Rebuilt the maintainable project structure, removed decompiled source artifacts, and added reproducible build and release metadata.
   - 重建可维护的工程结构，移除反编译源码产物，并补充可复现的构建与发布元数据。

---

## Version 2.0.4

### Updates / 更新内容

1. **Boss Health Bar Auto-Hide / Boss血条自动隐藏**
   - Automatically hide the health bar when BOSS health drops to 0 or below. Note that for certain BOSSes that transition between phases, you may need to attack them again to reactivate the health bar display.
   - 当BOSS血量小于等于0时,自动隐藏血条。但是需要注意的是,某些BOSS转阶段后,需要重新攻击它来激活血条。

---

## Version 2.0.3

### Updates / 更新内容

1. **Silk Mother Boss Fix / 苍白之母Boss修复**
   - Completely fixed the health display issue of "Silk Mother" (Silk Mother). The current version can correctly display its health, but note that when this BOSS's health is less than or equal to 0, it needs to take damage again to enter the next phase.
   - 彻底修复了"苍白之母"(Silk Mother)的血量显示异常,当前版本可以正确的显示其血量,但是注意的是,该BOSS在血量低于等于0时,需要再次受到伤害才会进入下一阶段.

2. **Performance Optimization / 性能优化**
   - Create health bar components for BOSSes when scenes load, but keep them hidden temporarily to avoid slight stuttering that might occur when creating health bars for the first time when attacking BOSSes.
   - 在场景加载时就为BOSS创建血条组件,只是暂时隐藏,避免了首次攻击BOSS时临时创建血条可能会造成的轻微卡顿.

---

## Version 2.0.2

### Updates / 更新内容

1. **Silk Mother Boss Adaptation / 苍白之母Boss适配**
   - Added specialized adaptation for "Silk Mother" Boss health mechanism
   - 新增了对"苍白之母( Silk Mother )"Boss血量机制的专门适配

2. **Performance Optimization / 性能优化**
   - Fixed stuttering issues in certain areas
   - 修复了部分区域异常卡顿的问题

3. **UI Improvements / 界面改进**
   - Temporarily disabled rounded corner style selection for original textures
   - Fixed abnormal BOSS name display issues in certain areas
   - 暂时禁止了原始材质下圆角样式的选择
   - 修复了部分区域BOSS名字显示异常的问题

---

## Version 2.0.0

### Major Updates / 重大更新

1. **Bug Fixes / 问题修复**
   - Fixed bugs where certain BOSSes and normal enemies could not display health bars correctly
   - 修复了某些BOSS和普通敌人无法正确显示血条的BUG

2. **Enhanced Customization / 增强自定义功能**
   - Added fine-tuning for health bar positioning
   - Added BOSS health bar background color customization
   - Added toggle switch for BOSS health numbers display
   - 新增了血条位置的精细调准
   - 新增BOSS血条背景的颜色自定义
   - 新增BOSS血量数值显示开关

3. **Independent Display Controls / 独立显示控制**
   - Added separate toggle switches for BOSS and normal enemy health bars
   - 新增BOSS和普通敌人血条的单独显示开关

4. **Improved Texture Handling / 改进材质处理**
   - Changed health bar scaling mode from compression to clipping for better custom texture compatibility
   - 将血条缩小的模式从压缩改为剪切，可以适应更多的自定义材质

5. **Localization Improvements / 本地化改进**
   - BOSS names now display correct in-game names with multi-language support instead of GameObject.name
   - BOSS的名字将显示正确的游戏内名字，并支持多语言，而不是GameObject.name

---

## Version 1.0.9

### New Features / 新功能

1. **BOSS Health Bar Activation Mechanism / BOSS血条激活机制**
   - BOSS health bars now only appear after the BOSS takes damage for the first time
   - This prevents spoiling the presence of bosses before encounters begin
   - Improves gameplay experience by maintaining surprise elements
   - BOSS血条现在只有在BOSS首次受到伤害后才会显示
   - 防止在战斗开始前剧透BOSS的存在
   - 通过保持惊喜元素改善游戏体验

---

## Version 1.0.8

### Major Update / 重大更新

1. **Custom Texture Support / 自定义材质支持**
   - Added support for custom health bar textures
   - Load custom textures from DLL directory/Texture/ folder (HpBar.png for normal health bars, HpBar_Boss.png for boss health bars)
   - Automatic fallback to default textures when custom textures are not available
   - Configurable texture scaling modes: stretch to fit, maintain aspect ratio
   - 新增自定义血条材质支持
   - 从DLL目录/Texture/文件夹加载自定义材质（普通血条使用HpBar.png，BOSS血条使用HpBar_Boss.png）
   - 当自定义材质不可用时自动回退到默认材质
   - 可配置的材质缩放模式：拉伸适应、保持比例

2. **Code Structure Optimization / 代码结构优化**
   - Large-scale code refactoring for improved performance and maintainability
   - Optimized texture loading and caching system
   - Simplified material application logic
   - Reduced memory allocation and improved runtime performance
   - 大规模代码重构，提升性能和可维护性
   - 优化材质加载和缓存系统
   - 简化材质应用逻辑
   - 减少内存分配，提升运行时性能


---

## Version 1.0.7 (紧急修复版)

### Critical Performance Fix / 紧急性能修复

1. **Rounded Corner Algorithm Optimization / 圆角算法优化**
   - **URGENT FIX**: Completely resolved severe frame drops (30-50 FPS loss) when displaying health bars with rounded corners
   - Removed complex algorithm and redundant logging that caused performance bottlenecks
   - Optimized Update method calculations and distance checking algorithms
   - Cached frequently used components to avoid repeated parsing
   - **紧急修复**: 彻底解决了显示圆角血条时严重掉帧(损失30-50帧)的问题
   - 移除了导致性能瓶颈的复杂算法和冗余日志记录
   - 优化了Update方法计算和距离检查算法
   - 缓存常用组件避免重复解析

2. **Alternative Solution / 备用解决方案**
   - If performance issues persist, press HOME key to switch to rectangular mode for complete resolution
   - Rectangular mode provides optimal performance with zero frame loss
   - 如果依然存在性能问题，按HOME键切换成长方形模式即可彻底解决
   - 长方形模式提供最佳性能，零帧数损失

---

## Version 1.0.6

### New Features / 新功能

1. **Health Threshold Protection / 血量阈值保护**
   - Added monster health upper limit threshold judgment. When enemy health exceeds this threshold, no processing will be performed
   - Solved the issue of unknown enemies with huge health values displaying health bars
   - Prevented some BOSS phase transition issues where health instantly exceeds 99999, causing maximum health record anomalies and incorrect health bar proportions
   - 增加怪物血量上限阈值判断，当敌人血量超过该阈值则不进行任何处理
   - 解决了巨大血量的未知敌人显示血条的问题
   - 防止了一些BOSS转阶段时瞬间超过99999血量后使最大生命值记录异常导致血条比例错误的问题

2. **Custom Health Bar Dimensions / 自定义血条尺寸**
   - Added custom width and height (pixel values) settings for both health bar types
   - 新增2种血条的具体长宽(像素值)自定义

3. **Rounded Corner Support / 圆角支持**
   - Added rounded corner display support for both health bar types!
   - You can switch back to rectangle mode at any time~
   - 新增2种血条支持圆角显示了！
   - 你也可以随时切换回矩形模式~

4. **Auto White Text on Low Health / 低血量自动白色文本**
   - Added controllable toggle for "automatically change health text color to white when enemy health is below 49%"
   - This setting allows you to see specific values clearly even when enemies have low health!
   - 新增可控开关的"当敌人血量低于49%时自动将血量文本颜色改成白色"的设置
   - 该设置可以让你在敌人低血量时也能看清具体的数值！

5. **Reset to Defaults / 恢复默认值**
   - Added "Reset to Defaults" button in configuration GUI
   - One-click restoration of all settings to default values
   - 在配置GUI中添加了"恢复默认值"按钮
   - 一键恢复所有设置为默认值



---

## Version 1.0.5
- **Major Fix**: Fixed multiple BOSS health bars overlapping issue with intelligent position management
- **Enhancement**: Implemented BOSS health bar manager for automatic position calculation
- **New Feature**: Added in-game GUI configuration panel (Press HOME to open)
- **New Feature**: Real-time settings adjustment with instant health bar recreation
- **Enhancement**: All configuration options now available through intuitive GUI interface
- **重大修复**: 修复多个BOSS血条重叠问题，新增智能位置管理系统
- **改进**: 实现BOSS血条管理器，自动计算位置排列
- **新增**: 按HOME(可配置)可以打开菜单,实现实时动态调整配置项.

## Version 1.0.4
- **Major Fix**: Fixed health bar remnant issues with triple protection mechanism
- **New Feature**: Added BOSS health bar with customizable threshold, colors, and positioning
- **New Feature**: Added NumbersInsideBar and NumbersVerticalOffset configuration options
- **Enhancement**: Improved default configuration to better match game aesthetics
- **重大修复**: 修复血条残留问题，新增三重保护机制
- **新功能**: 新增BOSS血条，支持自定义阈值、颜色和位置
- **新功能**: 新增NumbersInsideBar和NumbersVerticalOffset配置项
- **改进**: 改善默认配置以更好匹配游戏风格

## Version 1.0.3
- **New Feature**: Fixed health bar direction and added health numbers display
- **Enhancement**: Dynamic Canvas sizing and real-time health updates
- **Bug Fix**: Fixed health bar remnants and boss phase transition issues
- **新功能**: 修复血条方向并新增血量数值显示
- **改进**: 动态Canvas大小和实时血量更新
- **修复**: 修复血条残留和BOSS阶段转换问题

## Version 1.0.2
- **New Feature**: Added health bar color and scale customization
- **Enhancement**: Improved health bar appearance configuration
- **新功能**: 新增血条颜色和大小自定义
- **改进**: 改善血条外观配置

## Version 1.0.1
- **Initial Release**: Basic enemy health bar and damage text display functionality
- **初始版本**: 基本的敌人血条和伤害文本显示功能

---

*For more information, please refer to the README.md file / 更多信息请参考 README.md 文件*
