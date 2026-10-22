# 显示碰撞框

一个简单的模组会开启默认的调试碰撞框。

![icon.png](https://raw.githubusercontent.com/T2PeNBiX99wcoxKv3A4g/HKSS.ShowHitbox/refs/heads/master/icon.png)

## 控制方式

* 默认按 `F11` 键切换碰撞框。

## 安装

1. 下载 [BepInEx](https://github.com/BepInEx/BepInEx) 和 [install](https://docs.bepinex.dev/articles/user_guide/installation/index.html)。
2. 下载 [BepinEx-Utils](https://github.com/T2PeNBiX99wcoxKv3A4g/BepinEx-Utils/releases/latest)。
3. 将所有 .dll 文件提取到 `game folder/BepInEx/plugins`。
4. 启动游戏

## 配置

模组配置文件名为 `io.github.ykysnk.HKSS.ShowHitbox.cfg`，位于 `game folder/BepInEx/config` 内部。  
如果你没有使用任何模组管理器，可以直接手动修改该值；此外，如果你安装了 [BepinEx Configuration Manager](https://github.com/BepInEx/BepInEx.ConfigurationManager)，你也可以在游戏内更改任意设置。

* `ToggleKey`
    * 类型：`keybind`
    * 默认值：`F11`
    * 描述：
        * 用于切换显示碰撞框的按键。
* `ShowHitbox`
    * 类型：`boolean`
    * 默认值：`false`
    * 描述：
        * 显示碰撞框。
* `MoreInfos`
    * 类型：`boolean`
    * 默认值：`true`
    * 描述：
        * 显示更多信息。