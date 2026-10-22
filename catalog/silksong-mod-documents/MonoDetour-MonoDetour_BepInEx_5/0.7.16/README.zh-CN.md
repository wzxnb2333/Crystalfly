# MonoDetour BepInEx 5

该插件提供：

- BepInEx 日志集成对于 MonoDetour
- HarmonyX 插件支持 MonoDetour
- 在初始化时作为副作用提前加载 MonoDetour

MonoDetour 提前初始化意味着之后的所有内容都将获得以下内容：

- `ILHook`s（包含 HarmonyX 转译器）在抛出一个 `InvalidProgramException` 时，会对目标方法的编译进行 MonoDetour 的 CIL 分析
- 如果一个`ILHook`操纵器方法在旧版本中抛出异常MonoMod.RuntimeDetour将被禁用，以便目标方法可以被ILHooked成功地随后
- MonoMod的`ILLabel`在某些`Mono.Cecil.Cil.Instruction`方法中不会导致InvalidCastExceptions，例如`ToString`。
