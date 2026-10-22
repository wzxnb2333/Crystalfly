# MultiCustomizer

一个插件，允许加载多个纹理包并在条件下切换它们

## 它是如何工作的

AddTexturePack 函数允许你注册一个新的纹理包以加载到系统中。\\
请确保你的精灵包位于 `BepInEx/plugins/<name>/Customizer` \\
然后通过设置 IsActive 是否为 true，来激活该纹理包，其中 MaterialConditional 是使用 AddTexturePack 获取到的对象。\\

查看演示，请参考 [WandererTexturePack](https://github.com/unniisme/SilkMods/tree/master/WandererTexturePack)

------
感谢 RatherChaotic 及其 [Customizer](https://github.com/RatherChaotic/SSCustomizer)，本模组正是基于该作品开发的。
