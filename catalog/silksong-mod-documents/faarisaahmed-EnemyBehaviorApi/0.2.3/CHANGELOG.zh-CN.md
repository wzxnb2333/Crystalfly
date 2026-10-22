# Changelog

## 0.2.3

**如果与 SSMP 或任何多人游戏模组一起使用，则必需。**

复制的转换不再被抑制。从其他地方重放敌人状态的行为，并非由敌人“主动决定”，因此阻止这种行为总是错误的——但在门（gate）接收到这些信息时，它无法区分这是否是敌人的自主决策。

SSMP 通过 `Fsm.Event` 和 `PlayMakerFSM.SendEvent` 在非主机客户端复制敌人的状态，两者最终都会到达 `Fsm.DoTransition`。只要本地持有任何 Override 权限，门就会阻止这些操作，导致敌人无声地停止接收状态并出现不同步——这会被误认为是网络故障，而非权威冲突。

请将复制的转换用 `EnemyBehavior.AuthoritativeScope()` 或 `EnemyBehavior.RunAuthoritative(...)` 包裹。务必严格限定作用范围：内部内容确实会绕过权威控制。

## 0.2.2

**关键修复：覆盖操作不再导致游戏卡死。**

以前，压制（Suppression）会阻止 `Fsm.SwitchState` 的执行。PlayMaker 的更新循环是 `while (IsSwitchingState) SwitchState(...)`，而 `IsSwitchingState` 只有在 `SwitchState` 完成后才会被清除——因此，一个 Harmony 前缀返回 false 会留下待定状态，并导致主线程无限循环。

现在，压制操作发生在 `Fsm.DoTransition`，该操作返回布尔值，并在任何内容入队之前执行，因此在此处拒绝意味着转换永远不会发生。

后果：`Fsm.SetState` 不再经过 `DoTransition`，因此即使在 `SuppressAll` 下，使用 `SetState` 自行驱动的敌人也不会被压制。这是安全的错误方向——控制略微减弱，而非游戏卡死。

## 0.2.1

首次独立发布。将调查拆分为单独的程序集，使得“不会触碰敌人”成为二进制文件本身的属性，而非一种承诺。

在 18 种敌人、16 个场景和 2 个首领上，对敌情发现、FSM扫描、状态/动作/参数提取、实例权限以及事件流进行了测量。
