# SpeedrunGym

包含用于速通练习的工具。欢迎提出功能建议。

可通过 BepInEx 配置、e.g 在运行时使用 [ConfigurationManager](https://thunderstore.io/c/hollow-knight-silksong/p/jakobhellermann/BepInExConfigurationManager/) 进行配置。

## 随机数归一化

### 咬咬跳跃

归一化首次咬咬下潜的时机。

- `Early` - 尽快下潜
- `Late1` - 在多一个拍翼周期后下潜
- `Late2` - 在多两个拍翼周期后下潜

## 末段滞空检测

启用后，将显示为何末段滞空取消失败，或距离最优状态有多接近。

![pogo endlag ui](https://raw.githubusercontent.com/jakobhellermann/silksongmod-SpeedrunGym/main/docs/pogo-endlag.png)

可能的弹窗信息：

- `repress +Xms` - 成功，方向在中立落地后 X 毫秒再次按下。
- `jump +Xms` - 在最早可跳时间点之后 X 毫秒跳跃，第二行显示。
- `dir repress Xms early` - 失败，你过早按下了方向，提前了 X 毫秒。
- `neutral Xms late` - 你中立落地延迟了 X 毫秒。
- `no neutral` - 你从未进入中立落地状态。

## 跳跃时机

两个独立可切换的空中跳跃提示，显示在大黄蜂旁边作为弹窗。

![jump timing ui](https://raw.githubusercontent.com/jakobhellermann/silksongmod-SpeedrunGym/main/docs/jump-timing.png)

- `Sprintjump Release` - 从冲刺跳跃中退出时，显示你释放跳跃瞬间的垂直速度。`yvel ≈ 0` 表示你恰好在最高点松开，`-` = 仍处于上升状态，`+` = 正在下落。
- `Repress` - 如果你通过按压跳跃来实现漂浮，显示按压跳跃所花费的时间。

## 训练用木偶

在大黄蜂旁边生成一个被中和的巨型芦苇，作为训练用木偶。

- `Contact damage` - `Off`, `EffectOnly`（普通攻击，但不损失生命值）或 `Damage`。
- `Take knockback` - 攻击是否会使木偶后退。
