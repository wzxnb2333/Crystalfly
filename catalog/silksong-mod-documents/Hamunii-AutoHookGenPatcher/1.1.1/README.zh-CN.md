# AutoHookGenPatcher

在 [BepInEx](https://github.com/BepInEx/BepInEx) 预加载阶段自动生成 [MonoMod.RuntimeDetour.HookGen's](https://github.com/MonoMod/MonoMod) `MMHOOK` 文件。

手动安装：
将 ZIP 文件中的 `BepInEx` 文件夹移动到游戏的 `BepInEx` 文件夹中。

**该项目并未与 BepInEx 或 MonoMod 官方关联。**

本软件基于 [HarbingerOfMe](https://github.com/harbingerofme) 开发的 [HookGenPatcher](https://github.com/harbingerofme/Bepinex.Monomod.HookGenPatcher)，同样采用 MIT 许可证授权。

## 与原版 HookGenPatcher 的差异

- 不再仅针对固定的文件列表生成 MMHOOK 文件，AutoHookGenPatcher 会从已安装的插件中获取 MMHOOK 文件引用，并在可能的情况下生成这些 MMHOOK 文件。
- AutoHookGenPatcher 使用缓存文件来快速检查内容是否仍为最新状态，而无需对每个 MMHOOK 文件进行逐一验证。
- 钩子生成现已支持多线程，意味着生成多个 MMHOOK 文件所需时间更短。例如，在我的机器上，为 Lethal Company 中 `Managed` 目录下所有文件生成 MMHOOK 文件仅需约 **22.5 秒**，而若不使用多线程则需要 **40.0 秒**。

## 开发者使用说明

**注意：** 默认情况下，AutoHookGenPatcher 已经为 `Assembly-CSharp.dll` 生成了 `MMHOOK` 组件。因此，如果你只需要 `MMHOOK_Assembly-CSharp.dll`，无需进行任何操作。

使用 AutoHookGenPatcher 非常简单，你只需在最初告诉它需要生成哪些 MMHOOK 文件即可。这可以通过编辑配置文件 `AutoHookGenPatcher.cfg`，并将 `[Generate MMHOOK File for All Plugins]` 设置的 `Enabled` 值设为 `true` 来实现：

```toml
[Generate MMHOOK File for All Plugins]

## If enabled, AutoHookGenPatcher will generate MMHOOK files for all plugins
## even if their MMHOOK files were not referenced by other plugins.
## Use this for getting the MMHOOK files you need for your plugin.
# Setting type: Boolean
# Default value: false
Enabled = true

## Automatically disable the above setting after the MMHOOK files have been generated.
# Setting type: Boolean
# Default value: true
Disable After Generating = true
```
 当你发布模组时，请确保在上传的包中将 AutoHookGenPatcher 作为依赖项添加。

### 常见问题解答
#### AutoHookGenPatcher 是如何判断我的模组引用了哪些 MMHOOK 文件？
- 在 BepInEx 预加载阶段，AutoHookGenPatcher 会打开\* 并读取所有被引用组件 DLL 文件的元数据，查找以 `MMHOOK_` 开头的引用。然后它会检查是否存在与剩余名称匹配的已安装组件，并在这些组件存在时运行 MonoMod 的 HookGen。

\* 此操作仅在磁盘上组件的“最后修改时间”元数据比 AutoHookGenPatcher 缓存文件中记录的上次时间新时才会执行。被引用的 `MMHOOK` 组件也会被保存到缓存中。