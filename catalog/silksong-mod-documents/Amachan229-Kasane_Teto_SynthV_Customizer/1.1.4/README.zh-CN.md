空洞骑士：丝之歌的外观模组。包含大黄蜂角色的替身披风颜色与发型 Kasane Teto 的精灵图集。

版本：1.1.4

安装步骤：
1. 使用配置管理器 DownloadBepInEx 5，解压至游戏根目录后运行游戏并退出（此时会生成一个 BepInEx 文件夹）。
2. 下载丝之歌自定义工具 → 放入  
\Hollow Knight Silksong\BepInEx\plugins\Customizer  
（若不存在则创建 Customizer 文件夹）→ 运行游戏一次并退出（此时会生成一个 Mods 文件夹）。
路径如下：
\Hollow Knight Silksong\BepInEx\plugins\Customizer\Customizer.dll
4. 从本模组页面下载 [CustomizerT2D.zip plugin](https://www.nexusmods.com/hollowknightsilksong/mods/203?tab=files)（若跳过此步骤，部分图集将无法更新）。放入相同 \plugins\ 文件夹中，路径如下：
\Hollow Knight Silksong\BepInEx\plugins\CustomizerT2D\CustomizerT2D.dll
空洞骑士：丝之歌\
├─ BepInEx\
│  └─ plugins\
│     ├─ Customizer\
│     │  └─ Customizer.dll         （来自丝之歌自定义工具）
│     └─ CustomizerT2D\
│        └─ CustomizerT2D.dll      （来自本页面）
└─ Hollow Knight Silksong_Data\
   └─ Mods\
      └─ Customizer\
         └─ CuteHornet <or your different modpack folder name>\            （包文件夹或合并到现有文件夹中，仅允许一个文件夹）  
            ├─ Texture2D\          （PNGs 为 Texture2D 文件）
            └─ ... 其他包含精灵图集等的文件夹\
5. 替换场景动画（目前仅有两个铃兽相关的场景）
从本模组页面下载 [CustomizerCinematic plugin](https://www.nexusmods.com/hollowknightsilksong/mods/354?tab=files) 插件  
放入相同 \plugins\ 文件夹中，路径如下：
\Hollow Knight Silksong\BepInEx\plugins\CustomizerCinematic\CustomizerCinematic.dll
空洞骑士：丝之歌\
并放入相同的 \config\ 文件夹中，路径如下：
\Hollow Knight Silksong\BepInEx\config\su4enka.customizer.cinematic.overlayplayer
空洞骑士：丝之歌\
6. 下载文本模组（用于替换游戏内文字）→ 放入  
\Hollow Knight Silksong\BepInEx\plugins\TextModder  
（若不存在则创建 TextModder 文件夹）→ 运行游戏一次并退出  
路径如下：
\Hollow Knight Silksong\BepInEx\plugins\TextModder\SilkSongTextModder.dll
空洞骑士：丝之歌\
├─ BepInEx\
│  └─ plugins\
│     ├─ Customizer\
│     │  └─ Customizer.dll          （来自丝之歌自定义工具）
│     └─ CustomizerT2D\
│        └─ CustomizerT2D.dll         （来自本页面）
│     ├─ TextModder\
│        └─ SilkSongTextModder.dll         （来自 TextModder ）
│
│
└─ Hollow Knight Silksong_Data\
   └─ Mods\
      └─ TextModder\            （文件夹路径）
       │     └─ ... 文本文件
      ├─ Customizer\
         └─ CuteHornet <or your different modpack folder name>\    （包文件夹或合并到现有文件夹中，建议使用单个模组文件夹）  
*(感谢 Amachan Suzuki 制作的 [Cute Hornet mod](https://www.nexusmods.com/hollowknightsilksong/mods/203) 安装说明)*
*COPYRIGHT All character assets and sprites belong to Team Cherry. I simply modified the sprites myself. I do not claim any of the used assets as my own creation. The assets are not to be used outside of the game. This modification is fan-made and strictly non-commercial and is created solely for entertainment and personal use.

你可以在 Steam 中自定义游戏界面。

设置 → 属性 → 自定义  
点击“更改”按钮并选择一张图片。
