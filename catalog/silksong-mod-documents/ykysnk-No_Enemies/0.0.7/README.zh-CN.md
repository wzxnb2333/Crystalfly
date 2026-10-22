# 无敌人

在 `Hollow Knight: Silksong` 中没有任何事情发生

![icon.png](https://raw.githubusercontent.com/T2PeNBiX99wcoxKv3A4g/HKSS.NoEnemies/refs/heads/master/icon.png)

## 这个模组的作用是什么？

* 安装该模组后，敌人将消失。
* 在 `KillWhenTryAttack` 模式下尝试攻击时，任何敌人将会被击杀。

## 安装

1. 下载 [BepInEx](https://github.com/BepInEx/BepInEx) 和 [install](https://docs.bepinex.dev/articles/user_guide/installation/index.html)。
2. 下载 [BepinEx-Utils](https://github.com/T2PeNBiX99wcoxKv3A4g/BepinEx-Utils/releases/latest)。
3. 将所有 .dll 文件提取到 `game folder/BepInEx/plugins`。
4. 启动游戏

## 配置

模组配置文件名为 `io.github.ykysnk.HKSS.NoEnemies.cfg`，位于 `game folder/BepInEx/config` 内部。  
如果你没有使用任何模组管理器，可以直接手动修改该值；此外，如果你安装了 [BepinEx Configuration Manager](https://github.com/BepInEx/BepInEx.ConfigurationManager)，你也可以在游戏中直接更改各项数值。

* `Mode`
    * 类型：`NoEnemiesMode`
    * 默认值：`BeGone`
    * 描述：
        * 所有敌人均不生效。
* `RandomCorpseSplat`
    * 类型：`boolean`
    * 默认值：`false`
    * 描述：
        * 地图中随机出现尸体溅射效果。

## 已知问题

* 部分敌人可能存在一些问题。例如，物品可能完全不会掉落。  
  你可以尝试将工作模式更改为 `KillWhenTryAttack` 或 `Off` 来避免此问题。
* 有时你可能会卡在战斗场景中，请返回主菜单或重新启动游戏。