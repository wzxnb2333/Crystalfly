# Better Reaper

Strengthens the Reaper crest in many aspects.

## Features

- Increases stun accumulation from side and up attacks from `1.0` to `1.5`
  + That is, Bosses require `1/3` less hits to be stunned (rounding is needed in actual cases), similar for the following
  + Increases stun accumulation from down and sprint attacks from `1.0` to `1.75`
  + Increases stun accumulation from the Needle Strike from `2.0` to `3.5`
- Enlarges hitbox of the Needle Strike slightly (to `29/26` times both horizontally and vertically)
- Enhances the ability to generate Silk fragments when in Reaper mode
  + Increases the expectation of number of fragments generated per attack from `1.8` to `2`
    - Decreases the probability to generate `1` fragment from `35%` to `25%`
    - The probability to generate `2` fragments remains `50%`
    - Increases the probability to generate `3` fragmentg from `15%` to `25%`
  + Decreases the initial speed of the fragments by `25%`
    - Which also decreases the total movement distance by `25%`
  + Automatically collect the fragments when they expire naturally, or after completing Binding
- When Reaper mode expires naturally, or when it's activated again before ending, heals `1` Mask
  + Or grants `3` Silk instead if at full health
- When taking damage in Reaper mode, drain Silk to prevent health loss, at the cost of `3` Silk per Mask
  + Applies after the effects of Magma Bell, Warding Bell and Magnetite Dice
  + Affects only the actual amount of health loss, the amount of damage taken is still considered to be its original value
    - Attack cancellation and recoil remains the same
    - Specially, Reaper mode is not ended if there is no actual health loss


# 更好的收割者

从多个方面加强收割者纹章。

## 功能

- 侧向攻击与上劈造成的硬直累计从 `1.0` 提升至 `1.5`
  + 即 Boss 进入硬直状态需要的攻击次数减少 `1/3`（实际计算时需要考虑四舍五入），以下同理
  + 冲刺攻击与下劈造成的硬直累计从 `1.0` 提升至 `1.75`
  + 蓄力劈砍造成的硬直累计从 `2.0` 提升至 `3.5`
- 小幅增大了蓄力劈砍的碰撞箱（水平与竖直方向均扩大至 `29/26` 倍）
- 收割状态下攻击产生灵丝团的能力增强
  + 每次攻击产生的灵丝团的数量的期望从 `1.8` 个提升至 `2` 个
    - 产生 `1` 个的概率从 `35%` 降低至 `25%`
    - 产生 `2` 个的概率为 `50%` 不变
    - 产生 `3` 个的概率从 `15%` 提升至 `25%`
  + 灵丝团的初始移动速度降低 `25%`
    - 总移动距离亦降低 `25%`
  + 灵丝团在持续时间自然结束时或缚丝后自动被收集
- 收割状态持续时间自然结束时，或期间重复进入收割状态时，回复 `1` 点生命值
  + 生命满时改为回复 `3` 点灵丝
- 收割状态持续期间受到伤害时，消耗灵丝抵消生命值损失，每 `3` 点灵丝抵消 `1` 点
  + 生效于熔岩钟、护佑钟及磁石骰的判定之后
  + 仅影响实际损失的生命值，不改变伤害量相关的判定
    - 仍会受到打断、击退等效果
    - 特别地，实际未损失生命值不会导致收割状态结束
