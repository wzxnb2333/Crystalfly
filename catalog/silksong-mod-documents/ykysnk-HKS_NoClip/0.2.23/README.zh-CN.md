# 空洞骑士：丝之歌 NoClip

非常简单的模组，按 `b` 键切换无碰撞模式  
支持手柄操作

![icon.png](https://raw.githubusercontent.com/T2PeNBiX99wcoxKv3A4g/HKS-NoClip/refs/heads/master/icon.png)

## 控制方式

* 默认通过按 `b` 键来切换无碰撞模式。  
* 使用方向键在任何方向移动。  
* 按减号键可加速。  
* 通过同时按住跳跃键和上箭头键，在 2 秒内快速切换无碰撞模式，然后通过持续按住跳跃键，在 2 秒后关闭无碰撞模式。你需要启用 `QuickToggleNoClip` 配置项。

## 安装

1. 下载 [BepInEx](https://github.com/BepInEx/BepInEx) 和 [install](https://docs.bepinex.dev/articles/user_guide/installation/index.html)。
2. 下载 [BepinEx-Utils](https://github.com/T2PeNBiX99wcoxKv3A4g/BepinEx-Utils/releases/latest)。
3. 将所有 .dll 文件提取到 `game folder/BepInEx/plugins`。
4. 启动游戏

## 配置

模组配置文件名为 `io.github.ykysnk.HKS-NoClip.cfg`，位于 `game folder/BepInEx/config` 内部。  
如果你没有使用任何模组管理器，可以直接手动修改该值；同时，如果你安装了 [BepinEx Configuration Manager](https://github.com/BepInEx/BepInEx.ConfigurationManager)，也可以在游戏中直接更改各项数值。

* `NoClipToggleKey`
    * 类型：`Keybind`
    * 默认值：`B`
    * 描述：
        * 用于在游戏内切换无碰撞模式的按键。
* `TurnOffCol2d`
    * 类型：`Boolean`
    * 默认值：`false`
    * 描述：
        * 当启用无碰撞模式时关闭玩家碰撞体。（若碰撞体开启可能会导致一些问题）。
* `Invincible`
    * 类型：`Boolean`
    * 默认值：`false`
    * 描述：
        * 在启用无碰撞模式时使玩家无敌。
* `QuickToggleNoClip`
    * 类型：`Boolean`
    * 默认值：`true`
    * 描述：
        * 通过持续按住跳跃键和上箭头键，快速切换无碰撞模式。
* `Speed`
    * 类型：`Float`
    * 默认值：`30`
    * 描述：
        * 无碰撞模式下玩家的移动速度。
* `QuickToggleNoClipWaitTime`
    * 类型：`Float`
    * 默认值：`2`
    * 描述：
        * 快速切换无碰撞模式所需的持续时间。