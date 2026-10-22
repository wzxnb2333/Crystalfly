# DetourContext.Dispose 修复

此 BepInEx 补丁应用了来自 [Fix DetourContext.Dispose #102](https://github.com/MonoMod/MonoMod/pull/102) 的修复，使 `MonoMod.RuntimeDetour.DetourContext.Dispose` 能够正常工作。仅当您使用的是不包含该补丁的 **MonoMod.RuntimeDetour** 版本时才需要此功能。

如果补丁未找到其目标的 IL 代码，它会在 BepInEx 控制台中输出 `Nothing to patch.`。若出现这种情况，则说明无需使用此补丁。

## 为什么这很重要

如果你正在使用 `DetourContext`、e.g。:
```cs
using(new DetourContext(){ Priority = 100 })
{
    On.StartOfRound.Awake += Hook_2;
}
On.StartOfRound.Awake += Hook_1;
```
该 `DetourContext` 将不会自行销毁，且 `Hook_1` 会在 `Hook_2` 之前执行，因为它也被应用到了该对象上。  
通过此补丁，`DetourContext` 将自行销毁，且 `Hook_2` 会在 `Hook_1` 之前正常执行。