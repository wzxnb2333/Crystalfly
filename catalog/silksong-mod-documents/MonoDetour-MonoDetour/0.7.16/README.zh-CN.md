# MonoDetour

简单且方便的 .NET 重定向库，基于 MonoMod。RuntimeDetour。

- GitHub: <https://github.com/MonoDetour/MonoDetour>
- 文档：<https://monodetour.github.io/>

注意：MonoDetour 是一个为开发者提供的库。在被运行时加载后，它会执行以下操作：

- 当抛出 `InvalidProgramException` 时，包含 HarmonyX 转译器的 `ILHook` 会在目标方法编译时接受 MonoDetour 的 CIL 分析
- 如果在旧版 MonoMod.RuntimeDetour 上抛出异常，会禁用 `ILHook` 操作器方法，以确保后续目标方法能够成功被 ILHooked
- 在某些 `Mono.Cecil.Cil.Instruction` 方法中，MonoMod 的 `ILLabel` 不会导致 InvalidCastExceptions，例如 `ToString`。

注意：不要直接依赖此包。应依赖 `MonoDetour_BepInEx_5` 或 `MonoDetour_BepInEx_6`，因为它会将 MonoDetour 的日志器与 BepInEx 集成，并提供 HarmonyX 的互操作支持。

## 为什么使用 MonoDetour？

MonoDetour 是 HarmonyX 或 MonoMod 的替代方案。RuntimeDetour 用于函数跳转。它尽可能地改进了挂钩体验。详见 <https://monodetour.github.io/getting-started/why-monodetour/> 以了解更多信息。
