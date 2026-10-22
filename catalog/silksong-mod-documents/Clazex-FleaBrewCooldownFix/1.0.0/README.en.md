# Flea Brew Cooldown Fix

Make attack cooldown use Flea Brew reduced attack duration instead of the original duration as lower cap when Flea Brew is active.

## Explanation

As an example, when Beast Crest activates rage, its attacks have duration 0.3s and cooldown 0.32s.

Flea Brew should theoretically reduce theduration to 0.2s and the cooldown to 0.16s.

However, the cooldown is lower-capped by the *original* attack duration to 0.3s, which results in the final speed up being merely 6.25%.

This mod changed it such that it is instead lower-capped by the *reduced* duration, and all crests gain benefits more or less.


# 跳蚤秘酿冷却修复

跳蚤秘酿生效时，使攻击冷却以跳蚤蜜酿减少后的攻击间隔而非原始攻击间隔作为下限。

## 解释

以野兽纹章狂暴状态为例，其攻击持续时间为 0.3 秒，冷却时间为 0.32 秒。

跳蚤秘酿理论上应当将持续时间缩短为 0.2 秒，冷却时间缩短至 0.16 秒。

然而，冷却时间以*原始的*攻击间隔作为下限，实际变为 0.3 秒，导致跳蚤秘酿最终的加速仅有 6.25%。

本 mod 使得冷却时间改为以跳蚤秘酿*减少后的*攻击间隔作为下限，各纹章都有不等的收益。
