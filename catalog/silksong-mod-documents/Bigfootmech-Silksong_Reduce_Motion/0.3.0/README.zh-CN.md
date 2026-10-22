<h1>描述：</h1>  
我对那些本应静态、无需交互的游戏元素感到困扰。<br />  
然而这些元素会做出某些动作，需要立即关注（例如闪烁/脉动/……）。

当前禁用内容：<br />  
- 主菜单丝之歌主题粒子效果  
- 背包选择时“光标”的脉动效果  
- 地图标记的闪烁效果  

<h1>链接：</h1>  
GitHub：<a href = "https://github.com/Bigfootmech/Silksong_Reduce_Motion">https://github.com/Bigfootmech/Silksong_Reduce_Motion</a> <br />  
NexusMods：<a href = "https://www.nexusmods.com/hollowknightsilksong/mods/556">https://www.nexusmods.com/hollowknightsilksong/mods/556</a> <br />  
Thunderstore：<a href = ""></a>  

<h1>安装方法：</h1>  

<h3>Thunderstore：</h3>  
这将由系统自动完成。  

例如：我将此包的依赖项设置为 BepInEx。  

<h3>手动安装：</h3>  
首先，请在 Silksong 文件夹中安装 BepInEx。  
（注意：这会破坏 Thunderstore 的正常操作方式）  

您可以在以下位置找到它：  
https://github.com/BepInEx/BepInEx/releases  
当前最新稳定版本为 5.4.23.4  

解压后，运行游戏一次，以生成 BepInEx 的文件夹结构  
（即：除了 'core' 文件夹外，还存在其他文件夹）  

然后将此 DLL 文件，或包含 DLL 的文件夹，放入  
空洞骑士 <code>丝之歌\BepInEx\plugins</code>  

<h3>混合安装方式：</h3>  
如果您意外使用了 Thunderstore，  
并且通过 Thunderstore 安装了 BepInEx，  
但此模组的依赖项出现异常或错误，  

您应该可以在以下路径找到 Thunderstore 的 BepInEx 插件文件夹：  

<code>C:\\Users\\{username}\\AppData\\Roaming\\Thunderstore Mod Manager\\DataFolder\\{game}\\profiles\\{profile_name}\\BepInEx</code>  

如果您在非 Windows 系统上使用 Thunderstore，且我打包时出错，又不是技术爱好者……天啊，祝你好运。
