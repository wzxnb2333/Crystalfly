# 简单死亡计数器 (BepInEx) — v0.2.0

一个小巧的屏幕死亡计数器，功能简单且有效，死亡时带有轻微动画效果，配置可在实时中更新，无需重启游戏即可生效

## ✨ 特性

* **实时计数** — 游戏进行时会显示 `Deaths: N`。
* **死亡动画** — 死亡时会有短暂动画（可开启/关闭）。
* **智能缩放** — 根据屏幕高度自动调整，或使用固定字体大小。
* **可配置位置** — 可通过 `PosX`/`PosY` 移动计数器（负 Y 值表示从底部开始）。
* **热重载配置** — 保存 `.cfg` 后，游戏内会立即更新。

## 📦 要求

* **BepInEx 5 (Mono)** 已安装于游戏之中。

## 🛠️ 安装

### 使用模组管理器（r2modman / Thunderstore 模组管理器）

1. 安装 **BepInEx 5** 以供游戏使用。
2. 在你的配置文件中添加/启用 **简单死亡计数器**。
3. 启动游戏。

### 手动安装

1. 安装 **BepInEx 5**。
2. 将 `SimpleDeathCounter.dll` 复制到：

   ```
   <GameFolder>\BepInEx\plugins\
   ```

3. 运行游戏一次以生成配置文件。

## ⚙️ 配置

**文件：**  

```
<GameFolder>\BepInEx\config\Nordbo.SimpleDeathCounter.cfg
```
  
**热重载：** 游戏运行时保存文件——更改会立即生效。

**按键：**  

```ini
[UI]
PosX = 24                ; pixels from the left
PosY = -72               ; pixels (negative = distance from bottom)
FontSize = 26            ; used when AutoFontSize = false
AutoFontSize = true      ; if true, ~3% of screen height
EnableDeathAnimation = true

[Data]
DeathCount = 0           ; current total (you can edit/reset)
```
  
**提示**

* 希望尺寸固定？设置 `AutoFontSize = false` 并调整 `FontSize`。  
* 将计数器从底部向上移动，可使用负值的 `PosY`（e.g.，`-72`）。

## 🧪 使用方法

* 屏幕叠加显示 `Deaths: 1234`。
* 每次死亡都会增加计数，并触发短暂的弹跳动画（如果已启用）。

## 🔁 重置/编辑计数

* 打开配置文件并设置：

  ```
  [Data]
  DeathCount = 0
  ```
* 保存文件——叠加层会立即更新。

## ❌ 卸载

删除：

```
<GameFolder>\BepInEx\plugins\SimpleDeathCounter.dll
```

（可选）移除配置文件：

```
<GameFolder>\BepInEx\config\Nordbo.SimpleDeathCounter.cfg
```

---

## 📝 注意事项

* 仅计算模组激活期间的死亡次数（不进行回溯计数）。
* 如果遇到问题，请检查 `BepInEx\LogOutput.log` 并在报告时附上日志。
