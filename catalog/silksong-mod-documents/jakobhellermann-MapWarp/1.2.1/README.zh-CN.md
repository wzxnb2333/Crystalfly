<p align="center"><img src="https://raw.githubusercontent.com/jakobhellermann/silksongmod-MapWarp/main/thunderstore/icon.png" alt="MapWarp icon" width="128"></p>

# MapWarp

在地图上任意位置右键点击即可传送至该位置，支持鼠标平移和缩放操作。此外还提供显示完整地图、始终显示指南针以及显示房间轮廓的选项。

<p align="center"><img src="https://raw.githubusercontent.com/jakobhellermann/silksongmod-MapWarp/main/docs/demo.png" alt="MapWarp demo" width="640"></p>

## 特性

- **缩放与平移**：滚动可将视角向鼠标位置聚焦，点击并拖动可进行平移。
- **点击传送**：右键点击一个房间即可传送到大黄蜂所在的位置。默认情况下，你会落在最近的过渡点或安全重生点；按住 **Shift** 键则会直接降落在你点击的具体位置。

## 配置

可在 `BepInEx/config/io.github.jakobhellermann.mapwarp.cfg` 中编辑（或通过  
[Configuration Manager](https://thunderstore.io/c/hollow-knight-silksong/p/jakobhellermann/BepInExConfigurationManager/)）：

**传送**

- **启用传送** *(默认开启)*：右键点击地图上的房间，可传送到最近的安全位置（按住 Shift 可传送到精确位置）。
- **显示重生点** *(默认开启)*：当鼠标悬停在地图上的房间时，显示安全的重生点。

**地图**

- **解锁整个地图** *(默认开启)*：即使未购买，也显示完整地图。
- **在快速地图中显示全图** *(默认关闭)*：在快速地图中显示整个地图，而非仅当前区域。
- **立即打开地图** *(默认开启)*：立即打开快速地图，而不是等待动画加载。
- **始终显示指南针** *(默认关闭)*：始终在地图上显示当前位置，如同已装备指南针工具。

**调试**

- **显示房间边界** *(默认关闭)*：在地图上勾勒出每个房间的轮廓，并标注其场景名称。
