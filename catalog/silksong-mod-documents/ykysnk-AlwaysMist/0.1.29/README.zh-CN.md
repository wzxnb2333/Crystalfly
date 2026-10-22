# 永恒迷雾

一个模组让你可以再次进入 [the mist area](https://hollowknightsilksong.wiki.fextralife.com/The+Mist)

![icon.png](https://raw.githubusercontent.com/T2PeNBiX99wcoxKv3A4g/Always-Mist/refs/heads/master/icon.png)

## 这个模组的作用是什么？

* 模组会在你完成区域后仍然重新开启 [the mist area](https://hollowknightsilksong.wiki.fextralife.com/The+Mist)。
* 随机生成迷宫数值，使雾气更加多样化。
* 提供一个始终进入雾气的选项（true），开启后无论进入哪个房间都会先进入雾气。

## 安装

1. 下载 [BepInEx](https://github.com/BepInEx/BepInEx) 和 [install](https://docs.bepinex.dev/articles/user_guide/installation/index.html)。
2. 下载 [BepinEx-Utils](https://github.com/T2PeNBiX99wcoxKv3A4g/BepinEx-Utils/releases/latest)。
3. 将所有 .dll 文件提取到 `game folder/BepInEx/plugins`。
4. 启动游戏

## 配置

模组的配置文件名为 `io.github.ykysnk.AlwaysMist.cfg`，位于 `game folder/BepInEx/config` 内部。  
如果你没有使用任何模组管理器，可以直接手动修改该值；此外，如果已安装 [BepinEx Configuration Manager](https://github.com/BepInEx/BepInEx.ConfigurationManager)，你也可以在游戏内更改任意设置。

**`TrueAlwaysMist` 配置将忽略 `ResetMazeSaveData`、`RandomNeededCorrectDoors`、`RestBenchInMist` 配置的值。  
同时开启该配置后，进入任何门或房间时，会先进入迷雾区域！**

* `ResetMazeSaveData`  
    * 类型：`boolean`  
    * 默认值：`false`  
    * 描述：  
        * 即使未死亡，每次进入房间时都会重置迷宫存档数据。  
* `RandomNeededCorrectDoors`  
    * 类型：`boolean`  
    * 默认值：`false`  
    * 描述：  
        * 每次进入房间时，随机确定正确的门需要开启。  
* `MaxRandomNeededCorrectDoors`  
    * 类型：`integer`  
    * 默认值：`10`  
    * 最小值：`2`  
    * 最大值：`100`  
    * 描述：  
        * 随机确定正确门所需的最大数值。  
* `MinRandomNeededCorrectDoors`  
    * 类型：`integer`  
    * 默认值：`2`  
    * 最小值：`2`  
    * 最大值：`100`  
    * 描述：  
        * 随机确定正确门所需的小数值。  
* `TrueAlwaysMist`  
    * 类型：`boolean`  
    * 默认值：`false`  
    * 描述：  
        * 每次进入任何房间或门时，始终先进入迷雾迷宫。  
* `RestBenchInMist`  
    * 类型：`boolean`  
    * 默认值：`false`  
    * 描述：  
        * 在迷雾迷宫中开启休息长椅功能。

## 已知问题

* 雾之区域仅有一侧。穿过雾之区域后，将无法返回之前的雾之大厅。  
  我懒，所以不会修复这个问题。

* 当玩家穿过雾之区域后再次进入时，会立即退出，直到玩家死亡。  
  这个问题可以通过开启 `ResetMazeSaveData` 配置项来解决，同时开启 `TrueAlwaysMist` 配置项也不会出现此问题。

* 可以开启 `TrueAlwaysMist` 配置项然后开始新游戏，但如果没有任何能力，则体验会非常痛苦。  
  因此建议安装一些已解锁能力的模组。