# 缝线切换器

一个小型模组，允许你更改大黄蜂使用的缝线升级版本。

## 为什么？
我想能够降低我的针的等级，以免首领过快死亡！

*(e.g. 清理可选首领，[Godhome](https://thunderstore.io/c/hollow-knight-silksong/p/Cuckson/Godhome/))*

## 如何操作？

- 在物品菜单中选择针：
    - 使用冲刺输入 *(默认: `c`; 使用控制器时使用攻击输入)* 来“降级”针
    - 使用跳跃输入 *(默认: `z`; 控制器上等效)* 来“升级”针
<!--  -->
- 升级范围如下（从降级到升级）：
    - 原始针 *(i.e. 未使用模组时的状态)* ← **默认设置**
    - 针
    - 锋利针
    - 发光针
    - 蜂巢钢针
    - 淡钢针
<!--  -->
- 完全降级会使用你的默认针升级状态
- 默认情况下，无法“升级”超过当前最大升级等级
    - *e.g. 如果你通过原版的“针匠”(i.e. 原始版本) 升级到了发光针，那么在获得蜂巢钢针之前将被限制在发光针状态*
        - ***即使未安装此模组，升级针也是安全的***
            - *e.g. 如果你拥有蜂巢钢针但通过模组使用了锋利针，当向针匠提供淡油时，针匠仍会解锁淡钢针*
    - 模组内部的追踪系统仍可提升至最高可能等级，因此你可能需要多次点击“降级”输入才能看到变化
        - *e.g. 如果你被限制在发光针状态但频繁点击了“升级”，你可能需要多次“降级”才能使发光针变为锋利针*
        - 这是因为 `targetNeedleUpgradeLevel` 在所有存档文件间是共享的
        - 这个问题在很大程度上由 `capTargetToObtainedUpgradeLevel` 解决
    - 你可以通过将配置选项 `allowTargetAboveUpgradedLevel` 设置为 `true` 来移除此限制
        > *注意：配置选项仅在游戏启动时读取，因此你可能需要 **重新启动游戏以应用更改**。或者，你可以使用键盘快捷键 `home`+`end`（用于调试）在游戏运行中重新加载配置。*
<!--  -->
- 仅修改物品外观和伤害计算
    - *i.e. 不会影响完成度 / 游戏进度；为非永久性效果*
    - 注意：在丝之歌中，针的伤害计算因你所拥有的针升级类型而异，会乘以敌方特定的倍率因子（详见 [damage calculation](https://hollowknight.wiki/w/Damage_Values_and_Enemy_Health_(Silksong)#Damage_Calculation）
<!--  -->
- **此模组应安全安装和卸载，不会影响你的存档文件**
    - **不过，建议你始终备份你的存档文件以防万一**
    - 已进行测试，但我只是一个人

## 问题？疑问？修复？
- 在 [GitHub repository](https://github.com/itsschwer/silksong-needle-swapper/issues) 上打开一个 issue！
    - 缺陷报告
    - 建议 *(e.g. README 可读性)*
- 修改 BepInEx 配置选项（`BepInEx.cfg`）*`Logging.Console` → `LogLevels`* 和 *`Logging.Disk` → `LogLevels`* 以包含 `All` 可让您查看默认 `BepInExPack_Silksong` 配置中被省略的调试级别日志
    - 这可能有助于调查问题（并附上 `LogOutput.log`）

### 已知问题

<details>  
<summary><b>一些无关紧要的问题</b> <i>(点击显示/隐藏)</i></summary>

- “变换”按钮提示“降级”或“升级”针头时，如果在选择针头期间切换键盘与手柄控制，提示不会正确更新
    - 这种情况在正常游戏过程中通常不会出现，但取消选择并重新选择针头会将提示与当前控制方案同步
    - 目前实现似乎没有更优雅的解决方案

</details>

## 待办事项
- 仅在长椅上允许
    - 可复制 `global::InventoryItemToolManager.CanChangeEquips`
        - 但如何实现“坐在长椅上的提示”？
    - **这是否真的必要/理想？**
