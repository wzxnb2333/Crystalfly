### 3.2.20

#### 多语言支持（i18n）

我们现在支持多种语言！通过贡献代码来添加您语言的支持，请前往：https://github.com/ebkr/r2modmanPlus/

目前在 3.2.20 上支持的语言包括：
- 英语（一如既往）
- 法语

法语仍处于开发阶段，但功能已完整。目前唯一例外是错误信息的翻译，这些内容仍为英文。

#### 性能优化

- 修复了由 3.2.19 中新增的“移除模组时提示”功能引发的回归问题。
- 在大型配置文件中安装/卸载模组时性能大幅提升。
- 模组排序速度更快。

#### 新增功能

- 增加刷新指示器，显示模组列表更新状态及进度百分比。

#### 新增游戏

- AI LIMIT
- Among Us 3D
- BOMBANANA!
- Cast N Chill
- Cult of PiN
- Cuphead
- Electoral Carnage
- Holdfast: Nations At War
- How to Fish
- Inferno Protocol
- Last Pirates: Die Together
- Lazy Witch's Factory
- Limitless Survivor
- Moonlighter
- Nucleares
- Offscreen
- Re:Legend
- Rhythm Doctor
- Stonewards
- Survival: Fountain of Youth
- The Message from Deep Space
- The Sexy Brutale
- Tiny Rogues
- Tunguska: The Visitation

### 3.2.19

#### Flatpak 远程仓库

在 [@TB516](https://github.com/TB516) 的大力支持下，Flatpak 远程仓库现已为 Linux 用户开放。

这意味着 Flatpak 用户现在可以像普通 Flatpak 应用程序一样更新 r2modman。通过 Flatpak 更新也更小、安装更快。

您可以通过包含的 `flatpak-setup.sh` 脚本进行安装，或手动执行以下命令：

```bash
flatpak remote-add --if-not-exists r2builds https://r2builds.ebkr.dev/flatpak/r2modman.flatpakrepo
flatpak install -y r2builds io.github.ebkr.r2modman
```

如需更多关于 Flatpak 的设置信息，请参考 README 文件。

#### 优化改进

- 重新设计了设置界面，包括：
    - 更多项目/操作被归为一组。
    - 智能搜索功能。
- 降低峰值内存使用量。
- 当您已安装的包从 Thunderstore 移除时，系统将向您发出提醒。
- 在线模组列表搜索功能优化：
    - “默认”选项已更名为“相关性”。
    - 相关性搜索现在会优先匹配名称/作者完全一致的结果。
- 搜索结果中已忽略空格和下划线，意味着您无需记住包名的精确拼写。
- 现在可通过设置打开包含特殊字符的文件夹。

#### 新增游戏

- A Dance of Fire and Ice
- Big Walk
- Block Story
- Cairn
- Grain Rot
- Home Safety Hotline
- Inside the Backrooms
- Iron Nest
- LumenTale: Memories of Trey
- Shift At Midnight
- Sineus Arena Survivors
- Slip & Skid
- Stick Fight: The Game
- SUPERHOT
- SUPERHOT: MIND CONTROL DELETE
- The Lab
- Waterpark Simulator
- Wobbly Life

### 3.2.18

#### 性能优化

- 内存使用性能显著提升。例如，在 Lethal Company 中空闲状态下的内存占用约为 ~400 MB。
- 同时 CPU 周期也已减少，任务执行更快，功耗更低。

#### 视觉调整

- “新”选项卡被添加到游戏选择界面中。
- “英雄”元素（标题区块）不再固定在屏幕顶部。
- “启动模组版”操作在进入在线模式时不再改变大小。

#### 新增游戏

- Bloons TD 6
- Blue Prince
- Book of Travels
- Burglin' Gnomes
- Everything is Crab
- Goblin Cleanup
- GRIME
- Lucky Shot
- MECCHA CHAMELEON
- Paralives
- Pikuniku
- Romestead

### 3.2.17

#### 自动游戏添加

长期以来，您需要更新管理器才能获取新游戏，且每次新增游戏都需要我手动发布一个版本。

现在已实现自动化。只要您的管理器版本支持，即可访问所有 **受支持** 的游戏。

### 3.2.16

#### 界面调整

- “游戏选择”界面已重新设计，包括：
  - 增加“收藏”组。
  - 让游戏名称更易查找和阅读。
  - 始终显示“收藏”操作按钮。
  - 即使未来某时刻游戏被隐藏，仍可访问。

#### 修复内容

- Titanfall 2 / Northstar 现在可以再次从管理器中启动（感谢 @TB516）。
- 修复了多个样式对齐问题。
- Beetleball 不再推荐固定版本的 MelonLoader。
- 关键模组加载文件现在在安装模组时同步，而非“启动模组版”时（感谢 @NikkelM）。
- 修复了 Scrap Mechanic 的启动问题（感谢 @BenMcAvoy）。

#### 新增游戏

- Sledding game
- Hot Lava
- Angry Birds VR: Isle of Pigs
- Project Gorgon
- Puttler
- Island Market Simulator
- SIDE EFFECTS
- Modulus: Factory Automation
- Roadside Research
- MINOS
- BRUTALISTICK VR
- Super Psycho Baseball
- Getting Over It
- The Spell Brigade
- Vampire Crawlers
- Voodoo Fishin
- Project Arrhythmia 重命名为 Afterbeat
- Cave Crawlers
- Gamble with your Friends
- Miside Zero
- Lootun
- Windrose

### 3.2.15

#### 界面调整

- 新增底部栏，可实现：
  - 更快的游戏切换。
  - 更快的配置文件切换。
  - 更便捷地导出配置文件。
- “启动”操作现在更易识别。
- 导航栏区域更加清晰。

#### 新增游戏

- Astroneer
- Deadly Delivery
- Granny: Chapter Two
- Soulcalibur VI
- A Gentleman's Dispute
- Super Battle Golf
- Cursed Words
- Ducks Can Drive
- Gorilla Tag
- Jump Scare Mansion
- Pit of Goblin
- Very Very Valet
- Cryo
- Crawlspace
- Big Ambitions

#### 修复内容

- 游戏选择界面中的操作已正确对齐。
- 修复了多个模组安装行为问题。

### 3.2.14

#### 新增游戏

- Carrier Deck
- Factory Planner
- On-Together: Virtual Co-Working
- Project Arrhythmia
- Stolen Realm
- The Farmer Was Replaced
- YAPYAP

#### 修复内容

- On Together 现在会自动解析正确的文件夹路径。
- 修复了支持 UMM 的 linux_wrapper 实现（感谢 @alexneargarder）。
- 修复了 Miside 图像问题（感谢 @Hythera）。

### 3.2.13

#### 修复内容

- Linux 系统曾自动设置 WINEDLLOVERRIDES，此行为现已恢复。
- 修复了帮助页面中启动参数的显示问题（感谢 @alexneargarder）。

### 3.2.12

#### 新增游戏

- KeyWe
- Smushi Come Home
- Crime Simulator
- Scrap Mechanic
- Baby Steps
- Chill with You : Lo-Fi Story
- Crawlspace 2
- Goblin Buster: Incremental Tower Defense
- MineMogul
- Nuclear Option
- Pushing it! Together - Sisyphus Co-op
- RAM: Random Access Mayhem
- Unfair Flips
- RV There Yet
- Crab Game
- Cloverpit
    - 支持 Xbox Game Pass PC 版本

#### 性能优化  
- Mods.yml 文件大小已减少 99.35%  
    - 这将极大提升配置文件的运行性能  
    - 需要对配置文件执行一次变更操作才能生效，例如：  
        - 模组重新排序  
        - 安装/卸载/启用/禁用  
    - 操作完成后，访问配置文件界面应几乎瞬间完成  
- 本地模组列表异步加载，显示速度远快于以往  
    - 显著减少了等待模组列表加载的时间  

#### 设计改进  
- 模组卡片按钮已统一设计，不再占据整个空间  
    - 更易于视觉扫描  
    - 减少了鼠标移动需求  
- 在线预览面板现在拥有不同的背景颜色  
- 滚动条现已采用应用级样式而非原生样式  
- 其他优化以提升用户体验  

#### Flatpak 构建版本  

Flatpak 构建现已在 r2modman GitHub 仓库中为 Linux 用户提供：  
https://github.com/ebkr/r2modmanPlus/releases/latest  

这些构建版本适用于 Steam Deck，可在游戏模式下运行。  

> 当前存在 Steam Deck 的 `Gamescope` 组合器问题，导致下拉菜单出现在窗口下方。  
> 你可以使用箭头键绕过此问题。我后续会研究解决方案。  
> 此问题在桌面模式下并不存在。

### 3.2.11  
#### 新增游戏  
- Aeruta  
- Beetleball（测试版）  
- Football Manager 26  
- Necropolis  
- Super Fantasy Kingdom  
- Vigil  

#### 修复内容  
- PEAK 图像已恢复  
- 对 Linux 平台的 Steam 目录解析进行了优化（感谢 @RossBrunton）  

### 3.2.10  
#### 新增游戏  
- Bendy and the Ink Machine  
- Forsaken Frontiers  
- Return From Core  
- Mimesis  
- Five Nights at Freddy's: Into the Pit  
  - 支持 EGS 平台  
- Broforce  
- Megabonk  
  - 现可在配置中选择 Linux 可执行文件  

#### 修复的 Bug  
- 离线状态不再阻止管理器进入游戏  
- 更新检查现在在应用启动时进行，而非游戏选择之后  
- EGS 游戏可再次启动  

### 3.2.9  
#### 新增游戏  
- Megabonk  
- An Unfinished Game  
- Easy Delivery Co  
- Jump Space  
- CloverPit  
- Grey Hack  
- Slasher's Keep  

#### 更新内容  
- 空洞骑士：丝之歌 Linux 现可在列表中选择  
- 在线部分的预览面板和模组列表行中新增 NSFW 提示标识  
- 预览元数据可折叠，以释放更多预览空间  

### 3.2.8  
#### 更新内容  
- 配置编辑器  
  - 编辑器标题栏现在更不突兀  
  - 配置编辑器现已支持多选功能  
  - 加载大量配置文件时会显示加载动画，而非卡死应用  
  - “显示更多”功能现限制每行长度，直到用户选择后才生效  
- Linux 原生/Proton 检测与安装问题修复  

### 3.2.7  
#### 更新内容  
- 阻止游戏启动的 GTK 版本问题已解决  
- 模组中 Resonite 渲染器路径（影响 AudioBridge）现在能正确解析  
- Linux 包装器首次安装时现可正确写入磁盘  

### 3.2.6  
#### 更新内容  
- 若之前未存在，Linux 包装器现在能被正确解析  

### 3.2.5  
#### 更新内容  
- 修复了多个导致渲染问题的 Bug  

### 3.2.4  
#### 新增游戏  
- 空洞骑士：丝之歌  
- Resonite  
- House of Legacy  
- Beton Brutal  
- Pair A Dice  
- Maltese's Fluffy Onsen  
- Paper Animal Adventure  
- PAC-MAN WORLD Re-PAC  
- Darkwater  

#### 更新内容  
- 预览面板现在可调整大小  
- 下载与安装进度显示更准确  
- 整个应用已升级至 Vue 3，同时部分内部工具也进行了优化  

### 3.2.3  
#### 新增游戏  
- Len's Island  
- Mage Arena  
- Ostranauts  
- Patapon 1+2 重播  

### 3.2.2  
#### 新增游戏  
- Bad North  
- Logic World  
- Lost Skies Island Creator  
- Mycopunk  
- Vellum  
- Word Play  

#### 其他更新  
本版本主要聚焦于改进 Linux 支持，具体包括：  
- `Other` 平台选项不再导致应用卡死在 Linux  
- 现可在配置中手动选择原生、Proton 或自动启动行为  

### 3.2.1  
#### 新增游戏  
- PEAK  
- Painting VR  
- DEPO : Death Epileptic Pixel Origins  
- 9 Kings  
- Guilty as Sock!  
- Return of the Obra Dinn  
- ASKA  
- PIGFACE（演示版）  

#### 其他更新  
- 一般 Bug 修复与功能增强  

### 3.2.0  
#### 重大更新  
- 现可在在线部分预览模组的 README、CHANGELOG 和依赖项。  
- 配置文件管理现在位于屏幕左下角。  
  - 可用于：  
    - 更改配置文件  
    - 导出配置文件  
    - 导入本地模组  

#### 新增游戏  
- ANEURISM IV  
- Lost Skies  
- Labyrinthine  

### 3.1.58  
#### 新增游戏  
- Schedule 1  
- Zort  
- Disco Elysium  
- Odd Remedy  
- Dusk  
- Monster Train 2  
- My Dream Setup  
- Hades 2（@xiaoxiao921）  
- Gatekeeper  
- Pulsar: Lost Colony  
- Songs of Conquest  
- White Knuckle  
- ENA: Dream BBQ  
- Human Fall Flat  
- Magicite  
- ASKA  

### 3.1.57  
#### 新增游戏  
- R.E.P.O  
- Gang Beasts  

#### 修复内容  
- 缓存的 Thunderstore 模组列表刷新现在再次在后台立即执行。  
- 防止某些模组因缓存未更新而未被包含在配置文件导入中。  

### 3.1.56  
#### 新增游戏  
- Subterror  
- MiSide  
- Dale & Dawson  
- I Am Your Beast  
- Bomb Rush Cyberfunk  
  - 增加了“其他”选项  
- Aloft  
  - 使用正确的 AppID 在 Steam 上启动  

#### 显著更新  
- 更快地访问配置文件选择界面  

### 3.1.55  
#### 新增游戏  
- ATLYSS  
- STRAFTAT  
- Peaks of Yore  

### 3.1.54  
#### Bug 修复  
- 修复了导入配置文件时在 ZIP 内部找不到条目的问题  

### 3.1.53  
#### 新增游戏  
- WEBFISHING  
- SULFUR  

#### 其他更新  
- BONELAB 支持现已正常运行  
  - 启动时不再重新生成程序集  
- “预加载修复”已替换为“重置 \<game\> 安装”  
  - 此操作将删除整个游戏目录，并提示 Steam 重新验证文件  
  - 预加载修复最初是用于解决旧版 Risk of Rain 2 模组安装（使用 SeikoML）无法启动的问题

### 3.1.52  
#### 小型体验优化  
- 从 Electron v11 升级至 v24  
  - 感谢 (@JonathanSteininger)  
- 来自配置文件的模组在导入时会保持其导出时的顺序  
- 修复了按下载数量排序的问题  
- 当模组依赖项较多时，安装时间显著缩短  

#### 新增游戏  
- 旧市场模拟器  
- 地下潜行者  

### 3.1.51  
#### 内存与性能优化  
TS 团队一直在努力提升以下方面：  
- 应用程序内存占用大幅减少。  
  - 预计管理器将至少节省 50% 的 RAM，多数情况下甚至更高。  
  - 这将解决部分用户报告的白色屏幕问题。  
- 模组安装性能  
- 配置文件导入性能  

#### 新增游戏  
- 卡牌商店模拟器  
- 距离  
- 《五夜惊魂：深入地底》  
- 坦克团队  
- 硬时间 III  
- Paquerette 下奔行  
- Shapez 2  
- Gorebox  

### 3.1.50  
#### 新增游戏  
- 风险之雨归来  
- Hades II  
- 《Among Us》  
- Ale & Tale 酒馆  
- Screw Drivers  
- Nine Sols  
- Goodbye Volcano High  
- Gloomwood  
- Below the Stone  
- Back to the Dawn  
- Supermarket Together  
- Betrayal Beach  
- Arcus Chroma  
- Deep Rock Galactic: Survivor  

#### 其他变更  
- 导入配置文件时，可预先查看其中包含的模组。  
- 模态窗口的视觉效果得到优化  

### 3.1.49  
#### 新增游戏  
- Gladio Mori  
- Slipstream: Rogue Space  
- Panicore  
- Magicraft  
- 另一个螃蟹的宝藏  
- Bopl 战斗  
- Vertigo 2  
- Against the Storm  
- Lycans  
- Castle Story  
- Balatro  

#### 通用修复  
- 在线模组过滤效果更佳  
- 可按最后更新日期筛选本地模组列表  
  - 此功能仅适用于本版本及之后更新的模组  
- 其他小规模修复与调整  

#### BONEWORKS / BONELAB  
- BONEWORKS 和 BONELAB 现在拥有推荐的模组加载器版本，以提升使用体验  

#### Titanfall 2 / Northstar  
- Northstar 模组加载器包现在可以正确安装  

### 3.1.48  
- 新增游戏：  
  - 内容警告  
  - 等离子体  
  - Bomb Rush Cyberfunk 增加 MS Store 支持  
- 配置文件导出现在有大小限制  
- 修复了《虚空之声》的问题  
- 其他一般性修复与改进  

_贡献者：_  
- _anttimaki_  
- _cspotcode_  
- _Oksamies_  
- _ethangreen-dev_  

### 3.1.47  
- 性能显著提升  
- 新增游戏：  
  - Palworld  
  - Voices of the Void  
  - Cult of the Lamb 增加 GOG 支持  
  - 20 Minutes Till Dawn 增加 EGS 支持  
- 通用体验优化  

_贡献者：_  
- _anttimaki_  
- _Lordmau5_  
- _IngoHHacks_  

### 3.1.46  
- 新增游戏：  
  - Sailwind  
  - Meeple Station  
  - Void Crew  
- 修复问题：  
  - 点击版本号不再跳转到失效链接  
  - 分页区域占用屏幕空间更小  
- 现在可以卸载/禁用依赖项，而无需移除依赖的模组  

### 3.1.45  
- 新增游戏：  
  - Cities: Skylines II  
  - Lethal Company  
  - DREDGE  
  - Last Train Outta' Wormtown  
  - Wizard With a Gun  
    - 现在启动完整游戏而非演示版（感谢 @RandomWolf）  

### 3.1.44  
- 新增游戏：  
    - Atomicrops  
    - Erenshor  
    - Sunkenland  
    - Wizard with a Gun（感谢 @RandomWolf）  

### 3.1.43  
本版本的所有变更均由 Thunderstore 团队完成  

- 新增游戏：  
  - Wizard of Legend  
  - Will You Snail?  
  - Garfield Kart - Furious Racing  
  - Techtonica  
  - Thronefall  
  - We Love Katamari REROLL + Royal Reverie  
  - Bomb Rush Cyberfunk  
  - Touhou: Lost Branch of Legend  

### 3.1.42  
- 使用 MelonLoader 的游戏现在在首次启动时能正确生成程序集（例如：BONEWORKS / BONELABS）  
- 数据目录现在可以（终于）重新设置为更早的路径，而无需清除设置  
- （Thunderstore 团队）新增游戏：  
    - Sun Haven  
    - Wild Frost  
    - Shadows of Doubt  
    - Receiver 2  
    - The Planet Crafter  
    - Patch Quest  
    - Shadows Over Loathing  
    - West of Loathing  
- 配置编辑器现在支持 YAML 格式  
  - 此外，编辑非 .cfg 文件时光标问题也已修复（感谢 [sheybey](https://github.com/sheybey)）  
- 其他小规模修复  

### 3.1.41  
- 修复了 Risk of Rain 2 在选择 EGS 平台时无法启动的问题  

### 3.1.40  
- 增加 Wrestling Empire 的支持  
- Dome Keeper 现在会调用 GodotML 配置脚本  

### 3.1.39  
- 增加对更多游戏的支持：  
  - RUMBLE  
  - Dome Keeper  
  - Skul: The Hero Slayer  
  - Sons Of The Forest  
  - The Ouroboros King  
- 修复了从某些界面点击后游戏启动模态窗口未正常出现的问题  
- _注意：本版本所有变更的版权归属于 Thunderstore 团队_  

### 3.1.38  
- Outward Definitive Edition 现在可以正确启动  
    - 之前的 bug 导致 Steam 在启动时提示参数并错误地丢弃这些参数，即使用户已接受  

### 3.1.37  
- Proton 检测现在应能在 Linux 平台正确工作  
    - 如果管理器未能及时更新仓库，则会提供合理的降级方案  
- 新增游戏：  
    - Brotato  
    - Ancient Dungeon VR  
    - Atrio: The Dark Wild  
    - Ultimate Chicken Horse  
- BONELAB 的 Oculus 可执行文件现在可被选择  
- 在线搜索不再需要名称中包含下划线（感谢 MSchmoecker）  

### 3.1.36  
- 为强制在 Linux 系统上使用 Proton 提供临时解决方案  
    - 在游戏目录中放置一个 `.forceproton` 文件，直到问题方案开发完成  
- 游戏选择界面的空白屏幕问题有另一个潜在修复方案  
- BONELAB 现在可应用于从 Oculus Store 复制的游戏  
- 增加 ULTRAKILL 的支持  
- `dotnet` 文件将不再出现在 BepInEx 6 BE 构建的配置编辑器中  

### 3.1.35  
- 在线搜索性能得到提升  
- 配置名称若过长则会自动换行  
- 已弃用的模组在在线部分默认隐藏  
    - 可通过在线部分的筛选模态窗口将其显示出来  
- 空白游戏选择界面问题的潜在修复方案  
- 增加 Hard Bullet 对 .npc 文件的支持  
- 内部重构  

### 3.1.34  
- 配置文件导出现在使用 Thunderstore 提供的主机地址  
- 在线标签中的搜索性能显著提升  
- 游戏选择服务器标签已修改，以减少用户误操作  

### 3.1.33

- 新增支持的游戏：
    - Ravenfield
    - Aloft
    - Lamb 的教派
    - Chrono Ark（64 位）
    - BONELAB
    - Trombone Champ
    - Rogue : Genesia
    - Across the Obelisk
- 支持 Xbox Game Pass
    - 感谢 starfi5h 的贡献
- 其他改进得益于 Thunderstore 团队：
    - 现可在不重启应用的情况下更改游戏
    - 内部重构与优化


技术标识：`QoL`
