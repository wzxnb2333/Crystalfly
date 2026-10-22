将部分骨底帐篷替换为比基尼海滩主题的帐篷。

使用由 [Su4enka](https://next.nexusmods.com/profile/Su4enka/mods?gameId=8136) 创建的工具，该工具可实现背景图像的替换。本模组已捆绑此工具，并获得授权使用。衷心感谢他们的贡献，请查看他们制作的其他模组！

在使用 Customizer\CustomizerT2D 插件时，\Mods\Customizer 文件夹中只能存在一个自定义精灵图文件夹。

安装步骤：

1. 通过配置管理器下载 BepInEx 5 -> 解压至游戏根目录 -> 运行游戏并退出（此时会生成一个 BepInEx 文件夹）。

2. 从本模组页面下载 CustomizerT2D 插件，并放入相同的 \plugins\ 文件夹中，路径如下：
\Hollow Knight Silksong\BepInEx\plugins\CustomizerT2D\CustomizerT2D.dll

CustomizerT2D 插件会替换 Texture2D（包括 sactx 大图集；匹配时忽略末尾的 -HEX），而原始的 Customizer 仍负责处理 Collections。
插件在主菜单界面进行一次替换，随后在首次进入存档时使用一个短暂的监控窗口。这既减少了微秒级卡顿，又能捕捉到延迟加载的纹理（如脚本动画、UI 元素）。

3. 将 BonekiniBottom 文件夹放入
\Hollow Knight Silksong_Data\Mods\Customizer
（如果不存在则先创建 Mods/Customizer 文件夹）
路径如下：
\Hollow Knight Silksong\Hollow Knight Silksong_Data\Mods\Customizer\BonekiniBottom\Texture2D\....pngs

Hollow Knight Silksong\
├─ BepInEx\
│  └─ plugins\
│     └─ CustomizerT2D\
│        └─ CustomizerT2D.dll      （来自本页面）
└─ Hollow Knight Silksong_Data\
   └─ Mods\
      └─ Customizer\
         └─ BonekiniBottom\            （模组文件夹，或直接包含在现有文件夹中，仅允许一个文件夹）
            ├─ Texture2D\          （PNGs 为 Texture2D 文件夹）

若已使用自定义精灵图，请将 Texture2D 文件夹放入你的模组包文件夹 \Mods\Customizer\<yourpack, CuteHornet for
e.p.>\Texture2D\....pngs