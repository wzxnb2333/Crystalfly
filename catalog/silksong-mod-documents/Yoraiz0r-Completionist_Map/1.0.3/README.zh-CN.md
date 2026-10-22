# Completionist Map
Completionist Map 是一款《空洞骑士：丝之歌》模组，为地图上几乎所有的物品、可交互对象及敌人添加了自动追踪的标记点。Completionist Map 基于游戏本体数据，配备过滤功能及其他便利特性，帮助玩家收集每一件物品、每一条日记条目以及散落的念珠。

# Features
- 所有被视为理想的内容的地图标记均会自动追踪。
- 地图标记被分类，并可以切换可见性，也可进行探测以实现发现。
- 使用鼠标时地图性能大幅改进，包括平滑拖动、聚焦缩放、无限缩放、扩展的地图边界以及将图钉放置在光标位置。
- 大黄蜂的指南针针头现在始终会动画，以增强可见性。
- 带动画效果的HUD标记，可指向物品的大致位置，而无需反复查看地图。
- 沉浸模式，在探索过程中揭示区域中的图标。
- 对pin可见性与游戏进度及Steel Soul模式的匹配做出的谦逊尝试。
- 为不同玩法风格提供多种便利功能：
- 允许大黄蜂的指南针标记在未装备时显示
- 早期揭示跳蚤徽章
- 允许在深渊中使用地图
- 日志完成偏好设置（获取一条 / 获取完整条目）
- 在存档文件早期显示完成百分位数
- 通过在地图上右键点击每个地图标记或场景来调试传送（需先启用）

# 安装  
1. 下载 [BepInEx](https://github.com/BepInEx/BepInEx) 和 [install it](https://docs.bepinex.dev/articles/user_guide/installation/index.html) 并放入丝之歌的文件夹中。  
2. 下载本模组的 zip 文件，并将整个 `CompletionistMap` 文件夹提取到 `Hollow Knight Silksong/BepInEx/plugins` 中。（这样你将拥有 `Hollow Knight Silksong/BepInEx/plugins/CompletionistMap/CompletionistMapMod.dll` 及其配套的 json 文件）  
3. （可选）下载并安装 [BepInEx Configuration Manager](https://github.com/BepInEx/BepInEx.ConfigurationManager)。  
4. 启动游戏并享受！（同时，请阅读使用说明！）

# 升级  
1. 移除整个 `Hollow Knight Silksong/BepInEx/plugins/CompletionistMap` 文件夹。  
2. 下载本模组的最新 zip 包，并将整个 `CompletionistMap` 文件夹提取到 `Hollow Knight Silksong/BepInEx/plugins` 中。（这样你将拥有 `Hollow Knight Silksong/BepInEx/plugins/CompletionistMap/CompletionistMapMod.dll` 及相应的 json 文件）

# 使用方法  
加载丝之歌模组时，如果启用了完整地图功能，可能需要几秒钟才能显示出来。在加载过程中，当地图打开时，左上角可能会出现“正在加载完整地图...”的提示。对于大多数用户来说，地图会在你进入存档之前完成加载。（控制台中的 BepInEx 会显示加载完成的时间）

## 类别  
![Categories](https://files.catbox.moe/0hmeyh.webp)  
加载完成后，详细地图界面左侧会出现一系列类别列表。  
* 鼠标悬停：显示类别名称。  
* 左键点击：显示/隐藏该类别。  
* 左键按住并拖动：一次性显示或隐藏多个类别。  
* 右键点击：对本类别的收集点进行探测。  
* 中键点击：探测该类别的所有点位（包括已收集的和无法获取的遗漏点位）。

## 地图标记  
![map pins](https://files.catbox.moe/be9vsy.webp)  
根据你在游戏中的进度，地图上会出现许多标记。  
* 鼠标悬停：查看标记名称/详细信息。  
* 按住左 Shift 键：查看高级标记详情。  
* 右键点击（需开启传送功能）：传送到标记位置（你可能会被墙壁困住！请尝试传送到其他地方解决这个问题！）

## HUD标记  
![hud pins](https://files.catbox.moe/6a8i65.webp)  
每当您进入一个新房间，或在未清理的房间中打开地图/快速地图时，会出现一组HUD标记，并引导您前往对应物品。这些HUD标记会在自定义的时间间隔后安全隐藏，或者在您与NPCs/其他内容互动时自动消失。  
* 问题：由于丝之歌在房间切换过程中会保存大部分内容，可能存在收集物品后HUD标记仍残留的情况。只需在收集完成后离开房间即可触发保存。  
* 问题：丝之歌中部分逻辑对象实际上位于其操作地图范围之外。我已尝试手动修正并最小化此类情况，但仍可能有少数情况下HUD标记出现在错误的位置。详细信息会明确指出具体位置。

## 配置  
在使用丝之歌搭配完成主义地图后，你可以在 `Hollow Knight Silksong/BepInEx/config/yorai.completionistmap.cfg` 找到完成主义地图的配置文件。  
如果你选择了安装 [BepInEx Configuration Manager](https://github.com/BepInEx/BepInEx.ConfigurationManager)，只需在游戏中按下 F1 即可。所有选项均可在菜单中自定义。

### 通用设置  
* `Area display limitation`：限制完成主义标记仅显示在当前区域，而非整个地图。  
* `Area display mode`：控制大黄蜂是否需要获取某个区域的地图，或实际穿越该区域，还是说标记始终可见。  
* `Journal entry display mode`：控制日志分类是否将“发现”作为完成标准，还是以“彻底研究”为准。  
* `Journal entry completion mode`：控制日志分类是否统计所有条目，或仅统计用于达成 `True Hunter` 成就的条目。

### 使用体验优化  
* `Scene map markers enabled`：启用/禁用HUD标记。  
* `Scene map markers when entering scene`（v1.0.1+）：进入新场景时，HUD标记是否显示？  
* `Allow scene map markers to animate`（v1.0.1+）：HUD标记是否在移动过程中动画朝向目标？  
* `Duration for scene map markers`：HUD标记应在多长时间后淡出？（0 = 无限，即使大黄蜂与环境互动，仍会消失）  
* `Allow flea pins without items`：是否在未获得前就显示跳蚤标记。有助于避免新运行中的回溯。  
* `Allow compass without items`：是否在未装备指南针时，在地图上显示大黄蜂的标记。  
* `Allow compass pin to animate`（v1.0.1+）：大黄蜂的指南针标记是否应动画显示。  
* `Map pin size`（v1.0.1+）：详细地图中标记的尺寸倍率。  
* `Allow player pins to be placed on the mouse cursor`（v1.0.1+）：是否将玩家标记放置光标移动到鼠标指针位置。  
* `Allow map in abyss`：是否允许大黄蜂在特定事件发生前查看深渊地图。  
* `Show save completion percentile`：主菜单是否显示存档的完成百分比。有助于早期跟踪进度。

### 调试功能  
* `Allow teleporting`：启用右键点击标记以传送至该位置的功能，也适用于场景和原版标记。  
* `Ignore hide reasons`：某些标记可能因不可获取而隐藏，此选项可让你在地图上看到它们，以便排查问题。  
* `Show all map zone names`：始终显示区域名称。有助于理解特定的数据测试结果。

## 其他功能
* 为了更方便地访问地图边缘的图标，完成主义地图扩展了详细地图的边界。
* 因为丝之歌通常会将鼠标光标锁定在窗口中心，详细地图显示时，完成者地图会释放该锁定。
* 大黄蜂的自定义徽章现在可以使用鼠标光标进行定位，不过我选择保留放置/移除徽章的操作（基于键盘），这一功能仍处于开发中。

# 问题  
* 完美主义地图依赖于在资源包加载前从资产包中收集的丝之歌信息，这意味着如果游戏未来更新，新内容将无法自动显示。  
* 某些标记点只有在满足特定条件后才会出现，但我尚未找到一种优雅的方式来传递这些信息，目前仍处于探索阶段。我已亲自验证所有标记点均可收集，并且存档可以达到 100%，但正在努力减少早期游戏的挫败感（测试时未启用钢铁之心）。  
* 尽管该模组在设计之初就考虑了本地化，但本地化功能仍不完整。此外，某些具有本地化的物品仅部分加载，因此无法显示其本地化名称。我希望在未来不久能解决这个问题。

# Credits
* Completionist Map is written by Yoraiz0r
* The json file creation project relies on [AssetTools.NET](https://github.com/nesrak1/AssetsTools.NET) & [FSMExpress](https://github.com/nesrak1/FSMExpress/tree/skong) by [Nes](https://github.com/nesrak1) who has been incredibly helpful!
* A lot of the early research & info gathering was done using [UABEANext](https://github.com/nesrak1/UABEANext), also by Nes.
* Special thanks to the Hollow Knight Modding discord community for their assistance in figuring out the many weird issues presented by the data!