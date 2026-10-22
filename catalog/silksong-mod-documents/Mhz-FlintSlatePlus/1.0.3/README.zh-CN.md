# FlintSlatePlus Mod / 燧石板增强模组

一款强大的增强模组，适用于空洞骑士：丝之歌。延长 FlintSlate 和 跳跃酿造（迅捷）的持续时间。
空洞骑士：丝之歌的燧石板增强模组，延长燧石板和跳蚤秘酿（加速）持续时间。

**强化燧石板，追着小怪砍。**

## 特性 / 功能

- **自定义燧石板BUFF持续时间** / 自定义 FlintSlate BUFF 持续时间 (0.1-9999s， 默认/默认: 60s， 原版/原版: ~8s)
- **支持 FlintSlate（火元素）/ 支持燧石板（火元素）**
- **支持花心囊（毒元素）**
- **自定义跳蚤秘酿（加速）持续时间** (0.1-9999s，默认：60s，原版：~10s)
- **视觉效果控制 / 视觉效果控制** - 禁用闪烁效果以减少视觉干扰 / 禁用闪烁效果以减少视觉干扰
- 每个功能独立控制

## 安装 / 安装

1. 安装 BepInEx / 安装BepInEx  
2. 将 `FlintSlatePlus.dll` 放入 `BepInEx/plugins/` / 将dll文件放入plugins文件夹  
3. 启动游戏 / 启动游戏

## BUFF中断条件

**中文**: FlintSlate 增益效果将在以下情况下提前结束：
- 在长椅上休息
- **死亡/重生**
- **工具装备变更**
- **进入水中**（火焰熄灭）
- **自然过期**

燧石板BUFF在以下情况下会提前结束：
- **在椅子休息时**
- **死亡重生时**
- **工具装备改变时**
- **进入水中时**（火被扑灭）
- **自然到期时**

**💡 提示**：如果你设置了很长的持续时间，可以通过在长椅上休息来手动结束BUFF。
**💡 提示**：如果设置了很长的持续时间，可以通过在长椅上休息来手动结束BUFF。

## 配置 / 配置

```ini
[General / 通用设置]
# Enable FlintSlate duration modification / 启用燧石板持续时间修改
EnableFlintSlateMod = true
# Enable Flea Brew (Quickening) duration modification / 启用跳蚤秘酿（加速）持续时间修改
EnableFleaBrewMod = true

[Settings / 设置] 
# FlintSlate BUFF duration in seconds (vanilla: ~8s) / 燧石板BUFF持续时间（秒）（原版约8秒）
FlintSlateDuration = 60.0
# Flea Brew (Quickening) duration in seconds (vanilla: ~10s) / 跳蚤秘酿（加速）持续时间（秒）（原版约10秒）
FleaBrewDuration = 60.0

[Visual Effects / 视觉效果]
# Enable FlintSlate flashing effect / 启用燧石板闪烁效果
EnableFlintSlateVisualEffects = false
# Enable Flea Brew flashing effect / 启用跳蚤秘酿闪烁效果
EnableFleaBrewVisualEffects = false
```
