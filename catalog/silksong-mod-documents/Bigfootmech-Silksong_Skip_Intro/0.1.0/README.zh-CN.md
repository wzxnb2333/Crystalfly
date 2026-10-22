<h1>描述：</h1>  
跳过每次游戏启动时的“Team Cherry”和“Loading Symbol”提示信息。

为频繁打开游戏的用户（例如模组开发者）提供便利。

<h1>链接：</h1>  
GitHub：<a href = "https://github.com/Bigfootmech/Silksong_Skipintro">https://github.com/Bigfootmech/Silksong_Skipintro</a> <br />  
NexusMods：<a href = "https://www.nexusmods.com/hollowknightsilksong/mods/82">https://www.nexusmods.com/hollowknightsilksong/mods/82</a>

<h1>安装方法：</h1>

<h3>Thunderstore：</h3>  
该操作将由系统自动完成。

例如：我将此包的依赖项设置为 BepInEx。

<h3>手动安装：</h3>  
首先将 BepInEx 安装到你的 Silksong 文件夹中，  
（注意：这会破坏 Thunderstore 的正常操作流程）

你可以在以下位置找到它：  
https://github.com/BepInEx/BepInEx/releases  
当前最新的稳定版本是 5.4.23.3

解压后，运行游戏一次，以生成 BepInEx 的文件夹结构  
（即：除了“core”文件夹外，还存在其他文件夹）

然后将此 DLL 文件，或包含 DLL 的文件夹，放入  
空洞骑士 <code>丝之歌\BepInEx\plugins</code>

<h3>混合安装：</h3>  
如果你通过 Thunderstore 安装了 BepInEx，  
但此模组的依赖项出现了异常或错误，

你应该能在以下路径中找到 Thunderstore 的 BepInEx 插件文件夹：  
<code>C:\\Users\\{username}\\AppData\\Roaming\\Thunderstore Mod Manager\\DataFolder\\{game}\\profiles\\{profile_name}\\BepInEx</code>

如果你在非 Windows 系统上使用 Thunderstore，且我打包时出错了，又不是技术爱好者……天啊，祝你一切顺利。
