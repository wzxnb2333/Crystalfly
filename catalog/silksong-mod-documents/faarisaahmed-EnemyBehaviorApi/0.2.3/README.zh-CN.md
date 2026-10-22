# 敌人行为 API

**这是一个专为模组开发者设计的库。单独使用它，你无法看到任何可见效果。**  
安装它，是因为另一个模组将其列为依赖项 —— 或者因为你正在开发该模组。

它通过反射读取任意敌人的 PlayMaker FSMs，判断哪些状态是攻击行为，哪些是移动行为，并将这些信息以结构化的格式提供给其他模组进行观察、调整或驱动。

其中没有任何针对单个敌人的列表。当你将其指向一个 GameObject 时，它会告诉你该敌人能够做什么 —— 即使是从未见过的敌人，其行为也与已知敌人完全一致，因此基于此 API 开发的模组无需为每种新生物单独添加支持。

## 对玩家而言

安装后没有任何变化。没有快捷键，没有菜单，也没有行为上的差异。它会位于其他模组之下，并为它们提供信息。如果某个模组提示你需要安装这个模组，那么你只需完成这一步即可。

它会对 PlayMaker 进行补丁处理，因此如果你在调试冲突问题，测试这一点是合理的选择。

## 为模组开发者

三种访问层级，均限定在单个敌人实例范围内——从不作用于敌人的种类，也从不全局生效。两个模组可以在同一房间中持有不同的敌人而不会发生冲突。

| 等级 | 可执行操作 | 是否独占 |
|---|---|---|
| **观察** | 读取配置文件，订阅状态变更 | 否 |
| **影响** | 调整速度、时间、伤害值、分支权重 | 否 |
| **覆盖** | 抑制敌人的决策并触发转换 | 是，按实例独占 |

```csharp
[BepInDependency("com.faaris.enemybehaviorapi")]
public class MyMod : BaseUnityPlugin
{
    void Awake()
    {
        EnemyBehavior.EnemyAppeared += enemy =>
        {
            var handle = EnemyBehavior.Claim(enemy, MyGuid, AuthorityTier.Observe);
            _handles[enemy.InstanceId] = handle;

            foreach (var attack in handle.Profile.Actions)
                Logger.LogInfo($"{attack.DisplayName}: {attack.Shape}, {attack.DamageAmount} dmg");
        };
    }
}
```

务必释放句柄——释放句柄会撤销该句柄所写入的所有参数，并释放覆盖权限。

完整文档、用于修正误分类首领的注解格式，以及一个演示所有三个层级操作的示例：
**https://github.com/faarisaahmed/enemy-behavior-api**

## 状态 — 在构建前请阅读此内容

**Alpha版本。该架构尚未冻结；预期会出现破坏性变更。** 启动时请检查 `SchemaVersion.IsCompatible`。

发现机制与“观察”层级是基于 18 种敌对单位、16 场景和 2 首领进行衡量的。影响与覆盖功能已实现，但实际效果较弱——每项机制均仅通过手动操控敌对单位发现并修复了一个真实问题。

已知存在错误的情况，因此不会让你感到意外：

- `DamageAmount` 对大多数攻击读取 0。丝之歌将英雄伤害保留在持久性 `DamageHero` 组件中，而非 FSM 行为中。
- 速度在约三分之一的移动行为中生效。
- 分类机制是针对早期游戏敌人进行调优的。后期游戏内容及第 3 关卡尚未测量。
- 接触伤害敌对单位（骨滚轮）报告 0 攻击——根据当前定义是正确的，但架构目前尚无法表达“触摸即受伤”这一概念。
- `Fsm.SetState` 会绕过覆盖抑制机制，因此通过这种方式自行驱动的敌人即使在 `SuppressAll` 下仍会持续运行。

如果你熟悉某个敌对单位，发现其分类明显错误，请在 GitHub 问题中提出——这种判断是分类器调优所依据的真实标准。

## 多人模式

如果你的模组从其他地方重放敌人的状态——例如应用权威主机的FSM状态、重放记录或存档恢复——请将该操作封装起来，否则会触发覆盖声明并导致敌人不同步：

```csharp
using (EnemyBehavior.AuthoritativeScope())
{
    fsm.SendEvent(replicatedEvent);
}
```
