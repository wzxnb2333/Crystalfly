# GameAdjustments - Changelog

## v1.3.8 - 2025-05-10  
### 修复  
- 修复蓝血形态下，部分敌人皮肤显示异常。  
  修复蓝血色调在特定敌人上渲染为黑色或失真。  
- 尝试修复手柄控制异常问题。  
  尝试修复手柄输入的偶发性问题。  
- 尝试修复关闭菜单后角色卡住无法移动的问题。  
  尝试修复“关闭菜单后角色卡死”问题。  

### 新增  
- 黑化模式新增：可在玩家附近镜像攻击效果（可配置次数）。  
  进阶黑丝模式：可在玩家附近镜像攻击效果，支持自定义次数。  
- 狂暴模式新增难度选项：简单 / 困难 / 疯狂。  
  激进敌人：新增难度选择（简单 / 困难 / 疯狂）。  
- 蓝血模式新增：回血间隔、固定值或百分比回血配置。  
  蓝血模式：新增回血间隔、固定值或百分比回血设置选项。  

### 优化  
- 优化配置菜单在大批量配置项分类下的打开卡顿。  
  优化大量配置项分类下配置菜单的打开延迟。  
- 进一步调整狂暴模式逻辑，使行为更平滑。  
  进一步调优激进模式下的敌人行为，使其更加流畅。

## v1.3.7 - 2025-05-09
### 新增功能
- 更新了蓝血模式。  
  蓝血模式已更新。
- 优化了血条数字显示：按伤害类型显示不同颜色的飘字。  
  血条数值现已根据伤害类型显示不同颜色的浮动文字。
- 增强了狂暴模式。  
  激怒状态下的敌人变得更强大。

## v1.3.6 - 2025-05-08
### 新增
- 新增“跳跃”配置类（JumpSettings），包含以下配置项：  
  - EnableJumpAdjustment：跳跃调整总开关  
  - JumpSpeedMultiplier：跳跃速度倍率  
  - JumpHeightMultiplier：跳跃高度倍率  
  - JumpDurationMultiplier：跳跃持续时间倍率  
  - GravityMultiplier：重力倍率  
  - DoubleJumpSpeedMultiplier：二段跳速度倍率  
  - WallJumpSpeedMultiplier：蹬墙跳速度倍率  
  - EnableVariableJumpHeight：可变跳跃高度  
  - JumpReleaseSpeedMultiplier：跳跃释放速度倍率  
  - EnableInfiniteJump：无限跳  
  - EnableInfiniteDoubleJump：无限二段跳
- 新增“渐进式黑化”配置类（ProgressiveBlackThreadSettings），包含以下配置项：  
  - EnableProgressiveBlackThread：渐进式黑化总开关  
  - OverrideVanillaThreading：覆盖原版黑化逻辑  
  - UseProgressiveMode：渐进模式开关  
  - InitialProbability：初始黑化概率  
  - ProbabilityIncrement：每次概率增量  
  - CheckInterval：检测间隔  
  - MaxProbability：最大概率  
  - ThreadedHPMultiplier：黑化后血量倍率  
  - AttackMultiplier：黑化攻击倍率  
  - ExtraAttackDelay：额外攻击间隔  
  - AttackAngleSpread：攻击角度分散  
  - AttackPositionSpread：攻击位置分散

## v1.3.5 - 2025-12-07
### 修复
- 修复了在随机模式下拾取“黑色爪镜”后敌人不再掉落物品的问题。  
  修复了在随机模式下拾取“黑色爪镜”后敌人不再掉落物品的问题。

### 优化
- 优化了随机模式的逻辑，显著减少生成物品池和刷新商店时可能出现的卡顿。  
  优化了随机模式的逻辑，显著减少生成物品池和刷新商店时可能出现的卡顿。

## v1.3.4 - 2025-12-05
### 修改内容
- 正式发布版本，包含此前所有功能的稳定实现。  
  官方稳定版发布，包含此前所有功能的稳定实现。
