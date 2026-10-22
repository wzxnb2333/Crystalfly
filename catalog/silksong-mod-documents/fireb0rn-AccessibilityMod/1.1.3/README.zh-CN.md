# 可访问性模组

赋予玩家无需手动执行技巧即可达到速通技术速度的能力。

## 特性

### 沉入冲刺速度  
当你拥有冲刺能力且装备了猎人纹章时，地面奔跑会加速你达到沉入冲刺的最高速度。

### 中立跳跃速度  
在获得冲刺能力之前，地面行走将赋予你中立跳跃技巧的速度。

这两种效果互斥：中立跳跃速度在获得冲刺能力前生效。沉入冲刺速度在获得冲刺能力后生效。

## 配置

所有设置都会保存到标准的 BepInEx 配置文件 `BepInEx/config/fire.AccessibilityMod.cfg` 中，可使用任意文本编辑器在该文件中进行修改。

| 区域 | 配置键 | 默认值 | 含义 |
| --- | --- | --- | --- |
| `General` | `Enabled` | `true` | 主控开关。开启或关闭整个模组。|
| `General` | `Pause Label Position` | `TopRight` | 暂停面板角落：`TopRight`、`TopLeft`、`BottomRight` 或 `BottomLeft`。|
| `Speed` | `Neutral Pogo Speed` | `0.25` | 在获得冲刺能力之前，地面奔跑速度的加成（0 = 关闭）。|
| `Speed` | `Dive-dash Speed` | `0.23` | 获得冲刺能力后，在冲刺状态下的速度加成（0 = 关闭）。|
| `Hotkey` | `Speed Toggle Hotkey` | `false` | 启用一个按键，可临时开启或关闭速度加成。|
| `Hotkey` | `Key` | `None` | 执行切换的按键。|

### 屏幕上的 UI 界面

存在一个用户界面，用于让玩家验证该模组在竞技 bingo 游戏中的使用情况。

- **暂停菜单面板**（默认位于右上角；可配置）：显示与默认值不同的速度和快捷键设置。如果所有设置均为默认值，则显示“AccessibilityMod 设置为默认”。  
- **快捷键提示**（默认位于右下角）：当按下速度切换快捷键时，会短暂弹出确认信息。  
- **主控开关**：若模组的主控 `Enabled` 开关关闭，这些 UI 元素将被隐藏。

暂停面板会自动清除底部可见的 BingoUI 计数行。当 Benchwarp 可见时，该面板位于右上角的“页面”按钮下方，或位于左上角的丝线条下方。无需额外设置即可使用。

## 安装

1. 安装 [BepInEx for Silksong](https://thunderstore.io/c/hollow-knight-silksong/)。
2. 将 `fire.AccessibilityMod.dll` 放入 `BepInEx/plugins/`。
3. 启动游戏以生成配置文件。

## 问题反馈表

如果你有任何漏洞报告、功能请求、无障碍性问题，或任何其他想反馈的内容：[use this form](https://forms.gle/6cw29RaCm2vBZrXa9)。

## License

PolyForm Noncommercial License 1.0.0. Free to use, modify, and share for noncommercial purposes; selling and sublicensing are not permitted. LICENSE information is included in the package.
