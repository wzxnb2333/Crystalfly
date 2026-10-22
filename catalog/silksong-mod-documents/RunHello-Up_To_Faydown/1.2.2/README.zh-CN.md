<h1>UpToFaydown</h1>

玩家拥有 Faydown Cloak 后，该效果仅在向上+跳跃时触发，而非普通跳跃输入。这保留了普通跳跃功能供 Drifter's Cloak/Float 使用。

存在一个配置键用于开启或关闭此效果。

<h1>来源：</h1>

GitHub: <a href = "https://github.com/mcclure/Silksong.UpToFaydown">https://github.com/mcclure/Silksong.UpToFaydown</a>

This is based on <a href="https://github.com/DemoJameson/Silksong.MakeFloatGreatAgain">a mod by DemoJameson</a> which does the opposite (forces hover when down is held). I release my changes to this plugin as [0-BSD](https://opensource.org/license/0bsd) (public domain) but DemoJameson's license on the basic code still applies.

图标是通过截取 https://gamepadviewer.com 的截图生成的。

<h1>构建方法：</h1>

我使用了一个 `makezip.sh` 来制作 zip 包，但你可能需要将 "dotnet.exe" 修改为 "dotnet" 才能在你的机器上运行。

<h1>安装方法：</h1>

<h3>Thunderstore:</h3>  
应能自动完成所有操作。

<h3>手动安装：</h3>  
首先将 BepInEx 安装到你的 Silksong 文件夹中，  
（注意：这会破坏 Thunderstore 的正常流程）

你可以在以下位置找到它：  
https://github.com/BepInEx/BepInEx/releases  
当前最新稳定版本为 5.4.23.4

解压后，运行游戏一次，以生成 BepInEx 文件夹结构  
（即：文件夹中除了 `core` 之外还存在其他文件夹）

然后将此 DLL 或包含 DLL 的文件夹拖入  
<code>Hollow Knight Silksong\BepInEx\plugins</code>
