# 关于 Stitchwork

> 由 [FabulatorK](https://github.com/FabulatorK) 分支并维护

## 快速介绍

Stitchwork 是一个强大的资源替换模组以及创作增强工具包。  
（它可以帮助制作和加载皮肤，类似于在《我的世界》中使用资源包。）  
它是对 **[Patchwork](https://github.com/Ashiepaws/silksong-patchwork/)** 由 **[Ashiepaws](https://github.com/Ashiepaws)** 创建的项目的一个谦逊分支，本项目对此项目怀有深深的感激之情。  
本项目中的大多数功能均源自 Patchwork 的早期反馈，并与皮肤创作者们进行了合作。

想在游戏中使用皮肤吗？  
安装 Stitchwork 并至少一个皮肤包，然后通过快捷键 `1`（测试版 1，非数字键盘）打开游戏内的“包管理器”界面，以管理激活（或禁用）哪个皮肤包。  
你指定的皮肤包将在游戏场景重新加载后自动加载，无需重启游戏进程。

如果你想使用 Stitchwork 制作一个游戏皮肤，请参考本模组页面上的 [wiki section](https://thunderstore.io/c/hollow-knight-silksong/p/SilksongSkinCreators/Stitchwork/wiki)。

## 安装模组

- **模组管理器：**  
  我们推荐使用 [Cogfly](https://github.com/Nix-main/Cogfly)、[Gale](https://thunderstore.io/c/hollow-knight-silksong/p/Kesomannen/GaleModManager/) 或 [r2modman](https://thunderstore.io/c/hollow-knight-silksong/p/ebkr/r2modman/)，在管理器中创建一个配置文件，并从搜索功能中安装 `Stitchwork`。  
  详细操作指南请参考相应模组管理器的官方页面。

- **手动安装：**  
  将下载并解压后的 `Stitchwork.dll` 文件放入 `BepInEx/plugins/StitchworkRoot` 目录中。  
  **这就是全部的安装步骤** —— 自从 0.8.1，着色器包和本地化表已内嵌在 DLL 中，尽管首次使用可能需要重启。  
  （与 DLL 并列的辅助文件仍然有效，并且如果存在会优先使用，这些文件仍随模组包一同提供，位于 `stitchwork.assetbundle` 和 `Lang/` 文件夹中。）

## 丝之歌模组 Patchwork 由 Ashiepaws 开发

[Patchwork](https://github.com/Ashiepaws/silksong-patchwork/) 首次为 *空洞骑士：丝之歌* 实现了动态资源替换——角色图、音频、文本、视频等均在运行时实时切换，无需修改任何游戏文件。  
它确立了资产替换模组的新精细度标准，以单个精灵图级别进行替换，这使得皮肤在应对游戏更新时更加稳定，并赋予艺术家对其作品更充分的控制权。  
Stitchwork 继承了其加载模型、包结构设计以及“替换而非修改”的理念，Ashiepaws 的思想持续影响着 1.0 乃至后续版本的发展路线。

由此衍生出两种行为：

- **Patchwork 包在安装时保持不变。**  
  Stitchwork 正如发现自身包一样，会精确识别 `Patchwork/` 文件夹——为原始框架设计的包无需重新制作。
- **若 Patchwork 已安装，则 Stitchwork 在当前会话中处于静默状态**，而非与之争夺相同的游戏钩子。

自 **版本 0.8.4** 起，原本为 Customizer 模组设计的老皮肤包可被 Stitchwork 识别，无需从原有安装路径移动（`.../Hollow Knight Silksong/Hollow Knight Silksong_Data/Mods/Customizer`），这些包将正确标记“Customizer”标签。  
此外，由模组管理器下载至 `BepInEx/plugins` 文件夹的 Customizer 皮肤包也会被识别，并在包管理器界面中显示“wayward”标签。

自 **版本 0.8.6** 起，Stitchwork/Patchwork 的皮肤包可直接以正常形态（`<GameRoot>/Packs/<PackName>/Stitchwork/...`）放入 `<GameRoot>/Packs` 文件夹，并被全局识别。这有助于同时管理多个配置文件的用户。

### 问题反馈渠道

Stitchwork 是一个独立于 Patchwork 的项目。  
**请勿将 Stitchwork 的问题提交至 Patchwork 的 GitHub 页面。**  
Ashiepaws 不维护此分支，该页面的问题不会传递到我们这里。  
请通过 Stitchwork 的官方渠道反馈问题：  
在社区模组开发的 Discord 服务器中找到 `#Stitchwork` 频道，[Hollow Knight Modding](https://discord.gg/6KRPJCQFg)。  
（你可能需要在 `#Channels&Roles` 中分配必要的角色权限。）  
或在 `#silk-support` 频道中创建支持请求，并将“Stitchwork”作为帖子标题。  
请附上你的 `BepInEx/LogOutput.log` 文件，并尽可能提供相关详细信息。

## 什么内容被添加

在此基础上，Stitchwork 整合了一些核心机制，并构建了自身的**创作者工具集**：Dev Hub（在游戏世界中点击任意资源即可查找）、动画控制台和洋葱皮画布工作流程、每帧动画艺术（Appliqué）以避免不同片段间重复使用动画帧、Pack Doctor、启动健康检查、简体中文界面本地化，以及其他功能。

## 新增功能 0.8.6

- 模组应用不再频繁导致游戏卡死。  
  屏幕顶部出现进度条，显示精灵重载过程。
- 动画画布：可一次性批量提取或编译列表中的所有片段。  
  批量操作时会显示进度条。
- 导出GIF：在“完整片段”面板中，可将动画片段以动态GIF格式导出至你的美术资源。
- 全局模组文件夹：将模组放入 `<GameRoot>/Packs/`。  
  每个模组管理器配置文件均可看到这些模组。
- 当应用保存文件时，会弹出一条简短提示消息。  
  同时也会提示你某个精灵文件未被成功应用。
- 文本合并：Push操作将解析后的文本写入模组包中。  
  Pull操作则将其写入工作文件夹。
- 粒子贴图可在T2D浏览器中进行编辑。
- 视频：替换的场景动画不再切割音频。  
  替换视频时跳过Team Cherry的色彩校正。  
  两者均支持配置选项。
- 字体：`FontFallbackScale` 和 `@s` 文件名标签设置备用字形大小。
- T2D：解决了从图集到精灵的转换问题。图集替换会按索引匹配图页。  
  在场景切换和刷新后，浏览器的选择与预览仍保持正确。
- 开发中心的开关现在变为带明确开/关状态的按钮。
- （开发中）`StitchworkApi` 为其他模组添加了实例皮肤合约。（该机制后续会实现）

## 0.8.5 新内容

- 修复黑屏问题。版本 0.8.4 在某些游戏版本启动时可能出现黑屏，现已解决。
- 皮肤在 macOS 和 Linux 中正常工作。旋转精灵不再需要平台特定的着色器。
- 动画画布：编译会跳过你未修改的帧。替换文件首先会被备份。
- 音频：日志警告会标明采样率不同的替换音效，并提供修复方案。
- 日志写入在播放电影时会暂停。已优化切场景播放，减少卡顿。配置项：`SuppressLogDuringCinematics`。
- 世界选择：粒子系统为可选启用。选择物品时会记录对象的绘制顺序。
- 裁剪贴图：索引行会显示剪辑覆盖数量，并按包和工作区进行分类统计。
- 安装根目录在启动时会被记录。具有更深文件夹结构的包不再被重复加载。精灵编辑器会打开你已有的文件，保存至实例特定的文件夹中。

## 游戏百科

模组页面上有一个游戏百科部分，提供必要的纯文本信息。  
捆绑的 HTML 格式游戏百科页面正在开发中。  
由于新增了功能，游戏百科内容正在调整以反映模组当前的状态。

## Credits

Beyond its predecessor, Stitchwork is shaped by many hands:

- The **skin creators and testers** whose packs, bug reports, and
  screenshots drove nearly every fix and feature in this mod — the change
  history is a conversation with them.
- The **localization helpers**: the Chinese terminology work that the
  in-game glossary and the wiki's zh pages are built on, and the German
  translation effort underway.
- Fellow toolmakers whose published work taught us techniques: **dakusan**
  (SilkDev — the world-pick projection approach), the **DebugMod** authors
  (cursor handling, and the pause workflow every creator uses), **YLTai**
  and **CarrieForle** (whose font mods shaped Stitchwork's font-handling
  boundaries), and **Custom Knight Creator** from the Hollow Knight era —
  the animation canvas's spiritual ancestor.
- The lineage before the lineage, inherited with Patchwork's own thanks:
  **Douglas Gregory** (the GPU performance work the tk2d sprite path is
  built on), **RatherChaoticDev** (Silksong Customizer, which Patchwork
  grew from), and **Su4enka** (CustomizerT2D, which shaped the T2D
  support Stitchwork extends).

### Special thanks

- To **MorningRain**.\
For localization assists, feedbacks, and moral support.\
Check out their incredible **[Nightmare Hornet](https://thunderstore.io/c/hollow-knight-silksong/p/MorningRain/NightmareHornet/)** skin project here.

- To **MeteoRa_** on Bilibili.\
They pushed the boundaries with the debug process and shaped experimental features, contributed greatly to the UI layout and mod icon design.\
They are working on a Second Sentinel skin and a Lies of P inspired skin.

- To **InkBlight(三只墨荒)** on Bilibili, whose Lace skin pack and Night Heron asset pack replace the Moorbugs.\
They helped improve the T2D support features of Stitchwork significantly with valuable findings and reports.

- To **Snek**, who offered contribution to technical documentation and promised localization of Stitchwork's wiki pack into the German language.
