<h1>描述：</h1>  
我主要使用鼠标和键盘来玩几乎所有游戏。

空洞骑士（以及丝之歌）是为手柄设计的，鼠标和键盘只是后续添加的功能。

这个模组旨在让游戏更适配鼠标和键盘操作。<br />  
（如果你已经拥有这些工具，为什么不使用它们呢？）

然而，这并不是一个简单的工作，需要花费较长时间。<br />  
因此，我决定分阶段发布，边开发边推出。

故此，使用“工作进行中”（WIP）标签。

目前可使用功能：<br />  
- 在屏幕两侧点击箭头切换标签/面板（v0.0.1）  
- 在右上角点击标签（v0.0.3）  
- 鼠标悬停并点击大部分物品栏/纹章/任务/猎人日志元素（v0.0.4）  
- 点击地图区域（存在卡顿），并使用右键返回（v0.1.0）  
- 使用鼠标滚轮缩放地图（v0.1.1）  
- 地图点击并拖动（v0.1.2）  
- 鼠标悬停在滚动箭头区域以滚动（物品栏、纹章、任务、猎人日志）（v0.1.3）  
- 鼠标悬停在滚动区域时滚动（存在卡顿）（v0.1.4）  
- 可通过鼠标放置标记。使用普通确认键、鼠标侧键或双击进行导航。点击标记可更改，或使用鼠标滚轮，或使用“快速地图”按钮。（v0.1.5）  
- 任务标签中可点击“显示/隐藏已完成任务”（v0.1.6）  
- 修复：防止在悬停于念珠炮时右键关闭物品栏，以避免兼容性问题（v0.1.7）

<h1>链接：</h1>  
GitHub：<a href = "https://github.com/Bigfootmech/Silksong_mouse_inventory">https://github.com/Bigfootmech/Silksong_mouse_inventory</a> <br />  
NexusMods：<a href = "https://www.nexusmods.com/hollowknightsilksong/mods/517">https://www.nexusmods.com/hollowknightsilksong/mods/517</a> <br />  
Thunderstore：<a href = "https://thunderstore.io/c/hollow-knight-silksong/p/Bigfootmech/Silksong_Mouse_Inventory_Use/">https://thunderstore.io/c/hollow-knight-silksong/p/Bigfootmech/Silksong_Mouse_Inventory_Use/</a>  

<h1>安装方法：</h1>  

<h3>Thunderstore：</h3>  
所有操作都会自动完成。

即：我将此模组的依赖项设置为 BepInEx。

<h3>手动安装：</h3>  
首先，在丝之歌的文件夹中安装 BepInEx。  
（注意：这会破坏 Thunderstore 的正常操作流程）

你可以从以下位置找到它：  
https://github.com/BepInEx/BepInEx/releases  
当前最新稳定版本为 5.4.23.4  

解压后，运行游戏一次，以生成 BepInEx 的文件夹结构  
（即：除了“core”文件夹外，还存在其他文件夹）

然后将此 DLL 或包含 DLL 的文件夹放入  
空洞骑士 <code>丝之歌\BepInEx\plugins</code>  

<h3>混合安装：</h3>  
如果你意外使用了 Thunderstore，  
并且通过 Thunderstore 安装了 BepInEx，  
但此模组的依赖项出现异常或错误，

你应该能在以下路径找到 Thunderstore 的 BepInEx 插件文件夹：  

<code>C:\\Users\\{username}\\AppData\\Roaming\\Thunderstore Mod Manager\\DataFolder\\{game}\\profiles\\{profile_name}\\BepInEx</code>  

如果你在非 Windows 系统上使用 Thunderstore，且我打包出错，又不是技术爱好者……天助我也。
