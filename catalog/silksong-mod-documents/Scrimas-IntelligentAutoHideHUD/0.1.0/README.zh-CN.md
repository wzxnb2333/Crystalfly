## 描述

在探索 Pharloom 时，是否经常觉得屏幕过于杂乱？本模组为丝之歌添加了一个动态的、类似《艾尔登法环》的HUD：当你安全探索时，面具、丝线卷轴、工具图标和纹章会逐渐消失；一旦出现敌人靠近、受到伤害或消耗资源，它们会立刻重新显现——无需再在美丽的截图和探索过程中被永久性的生命值/丝线/工具信息所干扰，同时也不会错过战斗中的关键信息。

## 主要特性

- 战斗驱动的可见性——当有活体敌人靠近（可配置距离范围）或对你产生攻击时，HUD 立即出现；在剧本设定的竞技场/战斗之门遭遇中也会触发。背景与前景装饰类敌人（不同深度层级）会被正确忽略，不会 false 触发 HUD。
- 按资源类型智能显示——护甲、丝线和工具弹药各自独立地在发生变化时立即显示（受到伤害 → 血量数值出现；消耗丝线 → 丝线卷轴数值出现）。
- 资源不足锁定机制——每种元素都有自己的“始终显示低于 X%”阈值，确保你在资源不足时不会被突然惊到。

## 安装

1. 如果尚未安装，请先安装 [BepInEx 5 with Configuration Manager](https://thunderstore.io/c/hollow-knight-silksong/p/silksong_modding/BepInExPack_Silksong/)。
2. 下载本模组，并将 HideHudSilksong.dll 放入你的空洞骑士 丝之歌/Hollow Knight Silksong/BepInEx/plugins/ 文件夹中。
3. 启动游戏

## 配置

可通过 ConfigurationManager 进行配置：

- 接近/仇恨半径以及竞技场锁定距离上限
- 战斗退出后的滞留时长
- 每种元素（生命值/丝线/工具）的管理开关、始终显示低于%的阈值，以及变更滞留时长
- 淡入淡出时长及隐藏状态透明度（默认完全不可见），或设置一个淡出效果

## 链接

NexusMods 页面：https://www.nexusmods.com/hollowknightsilksong/mods/1190  
GitHub 页面：https://github.com/Scrimas/Silksong-Intelligent-AutoHide-HUD
