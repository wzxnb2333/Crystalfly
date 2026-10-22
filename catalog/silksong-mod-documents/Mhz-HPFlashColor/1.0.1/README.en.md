# HP Flash Color MOD

Enemy hit flash color changes based on their remaining HP percentage.

## 🎮 Usage

**HP Gradient Effect**: When enemies take damage, their hit flash color transitions from the initial color (full HP) to the target color (low HP), providing visual feedback for enemy health status.

> Default: White (255,255,255) → Red (255,0,0)<br>
> Both colors are fully configurable via BepInEx config file.

**Debug Mode**: Enable detailed logging to monitor the color calculation and HP tracking.

## Configuration

Edit `BepInEx/config/Mhz.hpflashcolor.cfg`:

- `InitialColor`: RGB values for full HP (default: "255,255,255" for white)
- `TargetColor`: RGB values for low HP (default: "255,0,0" for red)
- `EnableDebugLog`: Debug logging (default: false)

## Installation

1. Install BepInEx
2. Put `HPFlashColor.dll` in `BepInEx/plugins/` folder
3. Start the game

---

# 血量闪烁颜色MOD

根据敌人剩余血量百分比改变受击闪烁颜色。

## 🎮 使用说明

**血量渐变效果**：敌人受到攻击时，闪烁颜色从初始颜色（满血）渐变到目标颜色（低血量），提供敌人血量的视觉反馈。

> 默认：白色 (255,255,255) → 红色 (255,0,0)<br>
> 两种颜色均可通过BepInEx配置文件自定义。

**调试模式**：启用详细日志记录，监控颜色计算和血量追踪过程。

## 配置设置

编辑 `BepInEx/config/Mhz.hpflashcolor.cfg`：

- `InitialColor`：满血时RGB颜色值（默认："255,255,255" 白色）
- `TargetColor`：低血量时RGB颜色值（默认："255,0,0" 红色）
- `EnableDebugLog`：调试日志（默认：false）

## 安装方法

1. 安装 BepInEx
2. 将 `HPFlashColor.dll` 放入 `BepInEx/plugins/` 文件夹
3. 启动游戏