# FlintSlatePlus Mod / 燧石板增强模组

A powerful enhancement mod for Hollow Knight: Silksong. Extend FlintSlate and Flea Brew (Quickening) duration.  
空洞骑士：丝之歌的燧石板增强模组，延长燧石板和跳蚤秘酿（加速）持续时间。

**强化燧石板，追着小怪砍。**

## Features / 功能

- **Customize FlintSlate BUFF duration / 自定义燧石板BUFF持续时间** (0.1-9999s, default/默认: 60s, vanilla/原版: ~8s)
- **Support FlintSlate (Fire) / 支持燧石板（火元素）**
- **Support Pollip Pouch (Poison) / 支持花心囊（毒元素）**
- **Customize Flea Brew (Quickening) duration / 自定义跳蚤秘酿（加速）持续时间** (0.1-9999s, default/默认: 60s, vanilla/原版: ~10s)
- **Visual effects control / 视觉效果控制** - Disable flashing effects to reduce visual clutter / 禁用闪烁效果以减少视觉干扰
- **Independent control for each feature / 每个功能独立控制**

## Installation / 安装

1. Install BepInEx / 安装BepInEx  
2. Put `FlintSlatePlus.dll` in `BepInEx/plugins/` / 将dll文件放入plugins文件夹  
3. Start game / 启动游戏

## BUFF Interruption / BUFF中断条件

**English**: FlintSlate BUFF will end early in these situations:
- **Resting at bench**
- **Death/Respawn**
- **Tool equipment changes**
- **Entering water** (fire extinguished)
- **Natural expiration**

**中文**: 燧石板BUFF在以下情况会提前结束：
- **在椅子休息时**
- **死亡重生时**
- **工具装备改变时**
- **进入水中时**（火被扑灭）
- **自然到期时**

**💡 Tip**: If you set a very long duration, you can manually end the BUFF by resting at a bench.  
**💡 提示**：如果设置了很长的持续时间，可以通过在椅子休息来手动结束BUFF。

## Config / 配置

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
