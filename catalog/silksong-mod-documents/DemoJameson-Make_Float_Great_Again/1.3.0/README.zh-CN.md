# 让浮空再次伟大  
重新添加浮空覆盖输入（下键 + 跳跃，玩家拥有 Faydown Cloak 后），并提供丰富的自定义选项。

## 特性

* 可自定义输入：配置是否在按住特定输入（向下、向上、梦境钉或快速地图）时触发漂浮（默认：按住向下）。
* 反转输入条件：反转输入条件逻辑。启用后，必须按住指定的其中一个输入（向下、向上、梦境钉或快速地图）才能触发漂浮，而不是要求不按住任何输入（默认：false）。
* 水平输入支持：允许在按住水平输入时触发漂浮（默认：false）。

## 配置
我们推荐使用 [BepInEx.ConfigurationManager](https://thunderstore.io/c/hollow-knight-silksong/p/jakobhellermann/BepInExConfigurationManager/) 来方便地在游戏中自定义设置。如果您已安装该插件（在游戏中按 F1 打开菜单），请前往“让浮点数再次伟大”部分，直接调整选项——无需手动编辑文件。

 alternatively，编辑位于 `BepInEx\config\com.demojameson.makefloatgreatagain.cfg` 的配置文件。

## 安装方法

### Thunderstore
一切都会被自动处理。

### 手动安装
首先将 BepInEx 安装到你的丝之歌文件夹中，  
（注意：这将导致 Thunderstore 的功能失效）

你可以从以下位置找到它：  
https://github.com/BepInEx/BepInEx/releases  
当前最新稳定版本为 5.4.23.4

解压后，运行游戏一次，以生成 BepInEx 的文件夹结构  
（即：文件夹中除了 `core` 之外，还应存在其他文件夹）

然后将此 DLL 文件，或包含 DLL 的文件夹，放入  
`Hollow Knight Silksong\BepInEx\plugins`

## 源代码  
GitHub: [https://github.com/DemoJameson/Silksong.MakeFloatGreatAgain](https://github.com/DemoJameson/Silksong.MakeFloatGreatAgain)