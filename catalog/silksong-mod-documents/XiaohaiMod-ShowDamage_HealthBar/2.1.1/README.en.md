# ShowDamage&HealthBar MOD | 伤害显示&敌人血条模组

## Video Tutorial | 视频教程

**B站视频教程**: https://www.bilibili.com/video/BV1kNaizqEFD

**English community tutorial**: https://www.youtube.com/watch?v=afsNVFw0tVM

## Description | 简介

**English**: A mod that displays enemy health bars and damage numbers when attacking enemies in the game. All features are fully configurable to suit your preferences.

**中文**: 在攻击敌人后显示血条以及玩家对其造成的伤害数字的模组。所有功能都可以根据您的喜好进行配置。

### Version 2.1.1 Compatibility | 2.1.1 版本兼容说明

- Revalidated against the latest supplied game API and updated for the Unity 6 / .NET Standard 2.1 runtime.
- 已根据最新提供的游戏 API 重新验证，并更新为适配 Unity 6 / .NET Standard 2.1 运行环境。
- Thunderstore installations use the maintained [`silksong_modding-BepInExPack_Silksong`](https://thunderstore.io/c/hollow-knight-silksong/p/silksong_modding/BepInExPack_Silksong/) dependency.
- Thunderstore 安装使用当前维护的 [`silksong_modding-BepInExPack_Silksong`](https://thunderstore.io/c/hollow-knight-silksong/p/silksong_modding/BepInExPack_Silksong/) 依赖包。

## Download | 下载  [Thunderstore](https://thunderstore.io/c/hollow-knight-silksong/p/XiaohaiMod/ShowDamage_HealthBar/ )

## Features | 功能

### English
1. **Enemy Health Bar**: Display health bars based on enemy health percentage after attacking. Health bars maintain fixed orientation (left to right) regardless of enemy facing direction
2. **Damage Text Display**: Show damage numbers briefly when dealing damage to enemies
3. **Health Numbers Display**: Show "Current HP/Max HP" text above health bars with customizable font size and color
4. **Boss Health Bar**: Special health bar for high-health enemies with customizable threshold, colors, and positioning
5. **Custom Texture Support**: Support for custom health bar textures with automatic loading from DLL/Texture/ folder. Place HpBar.png for normal health bars and HpBar_Boss.png for boss health bars. Features configurable scaling modes and automatic fallback to default textures
6. **In-Game GUI Configuration**: Press HOME to open configuration panel with real-time settings adjustment and bilingual support (Chinese/English)
7. **Fully Configurable**: All features can be customized through configuration files or in-game GUI
8. **Accurate Player Damage**: Display actual player damage only, including redirected damage, without counting environmental or non-player hits
9. **Optimized Runtime**: Lazily create health bars after the first valid hit and reuse up to 32 damage-text objects
10. **Independent Controls**: Toggle normal enemy bars, boss bars, and damage numbers independently
11. **Texture Accuracy**: Automatically trim transparent borders from fill textures and optionally use `BG_Enemy.png` / `BG_Boss.png` backgrounds

### 中文
1. **敌人血条**: 攻击敌人后，显示基于敌人生命值百分比的血条，血条不受敌人翻转方向影响，始终保持固定方向（从左向右）
2. **伤害文本显示**: 攻击敌人后，短暂地显示本次伤害值文本
3. **血量数值显示**: 血条上方可显示"当前血量/最大血量"的数值文本，支持自定义字体大小和颜色
4. **BOSS血条**: 针对高血量敌人的特殊血条，支持自定义阈值、颜色和位置
5. **自定义材质支持**: 支持自定义血条材质，自动从DLL/Texture/文件夹加载。放置HpBar.png用于普通血条，HpBar_Boss.png用于BOSS血条。支持可配置的缩放模式和自动回退到默认材质
6. **游戏内GUI配置**: 按HOME打开配置面板，支持实时调整设置和中英文双语切换
7. **完全可配置**: 所有功能都可以通过配置文件或游戏内GUI进行自定义
8. **准确的玩家伤害**: 只显示玩家实际造成的伤害，包含转发伤害，不计入环境和非玩家攻击
9. **运行时优化**: 首次有效攻击后才创建血条，并复用最多 32 个伤害数字对象
10. **独立显示开关**: 普通敌人血条、BOSS 血条和伤害数字可分别启用或关闭
11. **材质显示修复**: 自动裁掉填充图外围透明像素，并可为普通敌人和 BOSS 分别使用 `BG_Enemy.png` / `BG_Boss.png` 背景

## Screenshots | 截图

![MOD Preview](https://i.postimg.cc/qvX098tT/28-1757356236-21140228919.png)
![Health Numbers Display](https://i.postimg.cc/fb8sbDjq/443509ea6a9130a42571fa32af4c05ac.png)
![Health Numbers Display](https://i.postimg.cc/N0sYY5Tr/f146eb8be1104ce1ddada58d4b7d0471.png)
![Health Numbers Display](https://i.postimg.cc/TY1t8GpY/2.png)
*Preview of the mod in action | 模组效果预览*

![Imgur](https://i.postimg.cc/bJLKjTLf/3.png) ![Imgur](https://i.postimg.cc/fyL1W9gt/4.png) 
*Config GUI*


## Configuration | 可配置项

### Display Settings | 显示设置

| 配置项 | 类型 | 默认值 | 说明 |
|--------|------|--------|------|
| **ShowEnemyHealthBar** | Boolean | `true` | 启用/禁用普通敌人血条<br>Enable/disable normal enemy health bars |
| **ShowBossHealthBar** | Boolean | `true` | 启用/禁用 BOSS 血条<br>Enable/disable boss health bars |
| **ShowDamageText** | Boolean | `true` | 启用/禁用伤害文本显示<br>Enable/disable damage text display |
| **ConfigGUI_Hotkey** | KeyCode | `Home` | 配置面板热键<br>Hotkey to toggle config GUI |

### Damage Text Settings | 伤害文本设置

| 配置项 | 类型 | 默认值 | 说明 |
|--------|------|--------|------|
| **DamageTextDuration** | Float | `2.0` | 伤害文本显示持续时间（秒）<br>Damage text display duration (seconds) |
| **DamageTextFontSize** | Integer | `55` | 伤害文本字体大小<br>Damage text font size |
| **DamageTextColor** | String | `"#DC143CFF"` | 伤害文本颜色（十六进制格式）<br>Damage text color (hex format) |
| **DamageTextUseSign** | Boolean | `true` | 伤害文本是否显示符号(+/-)<br>Whether to show signs in damage text (+/-) |

### Health Bar Settings | 血条设置

| 配置项 | 类型 | 默认值 | 说明 |
|--------|------|--------|------|
| **HealthBarFillColor** | String | `"#beb8b8ff"` | 血条填充颜色（十六进制格式）<br>Health bar fill color (hex format) |
| **HealthBarBackgroundColor** | String | `"#00000085"` | 血条背景颜色（十六进制格式）<br>Health bar background color (hex format) |
| **HealthBarWidth** | Float | `135` | 血条宽度（像素）<br>Health bar width (pixels) |
| **HealthBarHeight** | Float | `25` | 血条高度（像素）<br>Health bar height (pixels) |
| **ShowHealthBarNumbers** | Boolean | `true` | 是否显示血量数值（当前HP/最大HP）<br>Whether to show health numbers (current HP / max HP) |
| **HealthBarNumbersFontSize** | Integer | `32` | 血量数值文本字体大小<br>Health numbers text font size |
| **HealthBarNumbersColor** | String | `"#0E0404FF"` | 血量数值文本颜色（十六进制格式）<br>Health numbers text color (hex format) |
| **HealthBarHideDelay** | Float | `1.5` | 血条无变化后自动隐藏延迟时间（秒）<br>Auto-hide delay after no changes (seconds) |
| **HealthBarNumbersVerticalOffset** | Float | `0.3` | 血量数值文本垂直偏移值<br>Vertical offset of health numbers |
| **HealthBarNumbersInsideBar** | Boolean | `false` | 是否将血量数值显示在血条内部<br>Whether to display health numbers inside the health bar |
| **HealthBarNumbersAutoWhiteOnLowHealth** | Boolean | `true` | 低血量时自动变白色文本<br>Auto white text on low health |
| **HealthBarShape** | Integer | `1` | 血条形状（1=长方形，2=圆角）<br>Health bar shape (1=Rectangle, 2=Rounded) |
| **HealthBarCornerRadius** | Integer | `30` | 血条圆角半径（像素）<br>Health bar corner radius (pixels) |

### Boss Health Bar Settings | BOSS血条设置

| 配置项 | 类型 | 默认值 | 说明 |
|--------|------|--------|------|
| **BossHealthThreshold** | Integer | `105` | BOSS血量阈值（超过此值显示BOSS血条）<br>Boss health threshold (show boss health bar when HP exceeds this) |
| **BossHealthBarFillColor** | String | `"#beb8b8ff"` | BOSS血条填充颜色（十六进制格式）<br>Boss health bar fill color (hex format) |
| **BossHealthBarBackgroundColor** | String | `"#00000080"` | BOSS血条背景颜色（十六进制格式）<br>Boss health bar background color (hex format) |
| **BossHealthBarWidth** | Float | `900` | BOSS血条宽度（像素）<br>Boss health bar width (pixels) |
| **BossHealthBarHeight** | Float | `25` | BOSS血条高度（像素）<br>Boss health bar height (pixels) |
| **BossHealthBarBottomPosition** | Boolean | `true` | BOSS血条位置（true=底部，false=顶部）<br>Boss health bar position (true=bottom, false=top) |
| **BossHealthBarNameColor** | String | `"#0E0404FF"` | BOSS名字文本颜色（十六进制格式）<br>Boss name text color (hex format) |
| **ShowBossHealthBarNumbers** | Boolean | `true` | 是否显示 BOSS 血量数值<br>Whether to show boss health numbers |
| **BossMaxHealth** | Float | `3000` | BOSS最大生命值上限（防止异常显示）<br>Boss maximum health limit (prevents abnormal display) |
| **BossHealthBarNumbersColor** | String | `"#000000FF"` | BOSS血量数值文本颜色（十六进制格式）<br>Boss health numbers text color (hex format) |
| **BossHealthBarShape** | Integer | `1` | BOSS血条形状（1=长方形，2=圆角）<br>Boss health bar shape (1=Rectangle, 2=Rounded) |
| **BossHealthBarCornerRadius** | Integer | `30` | BOSS血条圆角半径（像素）<br>Boss health bar corner radius (pixels) |

### Custom Texture Settings | 自定义材质设置

| 配置项 | 类型 | 默认值 | 说明 |
|--------|------|--------|------|
| **Enabled** | Boolean | `false` | 启用/禁用自定义填充材质<br>Enable/disable custom fill textures |
| **ScaleMode** | Integer | `1` | 材质缩放模式（1=拉伸适应，2=保持比例）<br>Texture scaling mode (1=Stretch to fit, 2=Maintain aspect ratio) |
| **TrimTransparentMargins** | Boolean | `true` | 自动裁剪填充图外围透明像素，使可见填充比例与实际血量一致<br>Trim transparent outer pixels so visible fill matches actual health |
| **EnemyBackgroundEnabled** | Boolean | `false` | 是否使用普通敌人背景 `BG_Enemy.png`<br>Use normal-enemy background `BG_Enemy.png` |
| **BossBackgroundEnabled** | Boolean | `false` | 是否使用自定义 BOSS 背景 `BG_Boss.png`<br>Use custom boss background `BG_Boss.png` |

**Custom Texture Usage | 自定义材质使用说明:**
- Place custom texture files in the `DLL directory/Texture/` folder | 将自定义材质文件放置在`DLL目录/Texture/`文件夹中
- Use `HpBar.png` for normal health bars | 使用`HpBar.png`作为普通血条材质
- Optionally use `BG_Enemy.png` for the normal-enemy background | 可选使用`BG_Enemy.png`作为普通敌人血条背景
- Use `HpBar_Boss.png` for boss health bars | 使用`HpBar_Boss.png`作为BOSS血条材质
- Optionally use `BG_Boss.png` for the boss-bar background | 可选使用`BG_Boss.png`作为BOSS血条背景
- Textures will automatically fallback to default if custom files are not found | 如果找不到自定义文件，将自动回退到默认材质
- Recommended texture size: 256x64 pixels for optimal quality | 推荐材质尺寸：256x64像素以获得最佳质量
- Each PNG must be no larger than 8 MiB and 4096×4096 pixels | 每张 PNG 不得超过 8 MiB，尺寸不得超过 4096×4096 像素

```text
BepInEx/plugins/HealthBar/
├─ Silksong_HealthBar.dll
└─ Texture/
   ├─ HpBar.png
   ├─ BG_Enemy.png       (optional / 可选)
   ├─ HpBar_Boss.png
   └─ BG_Boss.png        (optional / 可选)
```

## Configuration File Location | 配置文件位置

**Path | 路径**: `BepInEx/config/com.Xiaohai.HealthbarAndDamageShow.cfg`

The configuration file will be automatically generated after the first run. You can modify the settings and restart the game to apply changes.

配置文件将在首次运行后自动生成。您可以修改设置并重启游戏以应用更改。

## Installation | 安装方法

### English
1. Install the maintained [BepInExPack for Silksong](https://thunderstore.io/c/hollow-knight-silksong/p/silksong_modding/BepInExPack_Silksong/) if you haven't already
2. Download the latest release from Thunderstore
3. Extract the mod files to your `BepInEx/plugins` folder
4. Launch the game and enjoy!

### 中文
1. 如果尚未安装，请先安装当前维护的[丝之歌 BepInExPack](https://thunderstore.io/c/hollow-knight-silksong/p/silksong_modding/BepInExPack_Silksong/)
2. 从 Thunderstore 下载最新版本
3. 将模组文件解压到 `BepInEx/plugins` 文件夹
4. 启动游戏并享受！

### Linux / Proton

- Native Linux: use the maintained pack's `run_bepinex.sh`. | Linux 原生版：使用维护包内的 `run_bepinex.sh`。
- Steam Proton: install the Windows x64 pack and set launch options to `WINEDLLOVERRIDES="winhttp=n,b" %command%`. | Steam Proton：安装 Windows x64 包，并设置该启动参数。

## Troubleshooting | 故障排查

- If the mod does not appear, check `BepInEx/LogOutput.log` for a `Healthbar&DamageShow v2.1.1` load message.
- 如果模组没有出现，请检查 `BepInEx/LogOutput.log` 中是否存在包含 `Healthbar&DamageShow v2.1.1` 的加载信息。
- `BepInEx/plugins`, `BepInEx/config`, and `BepInEx/LogOutput.log` are generated after BepInEx loads successfully for the first time. If all are missing, repair the BepInEx installation instead of creating only an empty `plugins` directory.
- `BepInEx/plugins`、`BepInEx/config` 和 `BepInEx/LogOutput.log` 会在 BepInEx 首次成功加载后生成；如果三者都不存在，请修复 BepInEx 安装，而不是只新建一个空的 `plugins` 文件夹。
- Existing configuration files are retained across upgrades. Delete only this mod's configuration file if you intentionally want to restore defaults.
- 升级时会保留已有配置；仅当您确实需要恢复默认值时，才删除本模组自己的配置文件。



## Author | 作者

**Name | 姓名**: Xiaohai 小海  
**Email | 邮箱**: 515250418@qq.com  
**Bilibili | B站**: https://space.bilibili.com/2055787437

## 打赏 

如果您喜欢这个MOD，可以考虑打赏支持开发者继续创作更多优质内容！



### 微信 
![WeChat QR Code](https://i.postimg.cc/kD3tGxFc/20250907074753-17-40.jpg)

### 支付宝  
![Alipay QR Code](https://i.postimg.cc/NGyJyBk9/20250907074248-16-40.jpg)



## Future Plans | 更新计划

### English
Currently, only core functionality has been implemented. Future updates will include UI styling and beautification to better match the game's aesthetic.

### 中文
暂时只做了核心功能，以后会进行UI的风格化和美化，使其更符合游戏的风格。

## License | 许可协议 

本MOD基于 **知识共享署名 4.0 国际许可协议 (CC BY 4.0)** 发布。

您可以自由地：
- **共享** — 在任何媒介以任何形式复制、发行本作品
- **演绎** — 修改、转换或以本作品为基础进行创作，包括商业性使用

**惟须遵守下列条件：**
- **署名** — 您必须给出适当的署名（@小海 Xiaohai）。

**以下行为必须标记作者署名：**
- 在视频平台进行介绍推广
- 其他模组平台引用
- 对MOD进行二次开发和引用
- 任何商业用途或修改
- 本说明的主要用途是敬告某些平台,不要再偷我MOD但是说是自己的了!这样只会打击模组开发者的热情!



---

