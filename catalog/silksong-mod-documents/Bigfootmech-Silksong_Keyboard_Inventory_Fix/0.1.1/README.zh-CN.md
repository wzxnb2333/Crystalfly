<h1>描述：</h1>  
我主要使用鼠标和键盘来玩几乎所有游戏。

《空洞骑士》（和《丝之歌》）是为手柄设计的，鼠标和键盘只是后续添加的功能，这大概可以接受。  
游戏的大部分操作控制都很流畅，我完成游戏时也没有遇到什么严重问题。

我真正感到不便的地方，是物品栏系统。  
它让玩家需要不断上下左右移动物品，而不是直接点击即可。

在《空洞骑士》中，只有一个按钮可以打开物品栏，  
而在《丝之歌》中，开发者额外提供了地图、任务等其他“标签”按钮，并在屏幕顶部提供一个小视图，显示当你按下左右箭头时会跳转到哪个标签。

但如今，所有这些额外的按钮都会打开各自的标签——而原本的“物品栏”按钮只是简单地打开或关闭整个视图。  
因此，想要进入包含游戏货币的“物品栏”标签，变得格外别扭。

这个模组会破坏手柄的使用功能，但修复了键盘操作的问题。  
现在，无论物品栏是打开还是关闭状态，点击物品栏按钮都会直接跳转到物品栏。

<h1>更新日志：</h1>  
- 0.1.0 = 基础版本发布  
- 0.1.1 = 修复在打开物品栏前与骨底任务板交互的问题  

<h1>链接：</h1>  
GitHub：<a href = "https://github.com/Bigfootmech/Silksong_Keyfix">https://github.com/Bigfootmech/Silksong_Keyfix</a> <br />  
NexusMods：<a href = "https://www.nexusmods.com/hollowknightsilksong/mods/359">https://www.nexusmods.com/hollowknightsilksong/mods/359</a> <br />  
Thunderstore：<a href = "https://thunderstore.io/c/hollow-knight-silksong/p/Bigfootmech/Silksong_Keyboard_Inventory_Fix/">https://thunderstore.io/c/hollow-knight-silksong/p/Bigfootmech/Silksong_Keyboard_Inventory_Fix/</a>  

<h1>安装方法：</h1>  

<h3>Thunderstore：</h3>  
该操作将由系统自动完成。  

即：我将此包的依赖项设置为 BepInEx。  

<h3>手动安装：</h3>  
首先，在《丝之歌》的文件夹中安装 BepInEx。  
（注意：这会破坏 Thunderstore 的正常操作方式）  

你可以从以下位置找到它：  
https://github.com/BepInEx/BepInEx/releases  
当前最新的稳定版本是 5.4.23.3  

解压后，运行游戏一次，以生成 BepInEx 的文件夹结构  
（即：除了“core”文件夹外，还存在其他文件夹）  

然后将此 DLL 文件，或包含 DLL 的文件夹，放入  
空洞骑士 <code>丝之歌\BepInEx\plugins</code>  

<h3>混合模式：</h3>  
如果你通过 Thunderstore 安装了 BepInEx，  
但此模组的依赖项出现了问题，  

你应该能在以下路径中找到 Thunderstore 的 BepInEx 插件文件夹：  

<code>C:\\Users\\{username}\\AppData\\Roaming\\Thunderstore Mod Manager\\DataFolder\\{game}\\profiles\\{profile_name}\\BepInEx</code>  

如果你在非 Windows 系统上使用 Thunderstore，且我打包时出错了，又不是技术高手……那真是天要亡你了。
