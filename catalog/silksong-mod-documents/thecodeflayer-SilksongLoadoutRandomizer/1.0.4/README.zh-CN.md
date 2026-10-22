# 丝之歌装备随机化

一个为 *空洞骑士：丝之歌* 设计的 BepInEx 插件，可随机化你当前装备的工具与丝线技能。该模组受到 MyPetCactus 及其在 [YouTube](https://www.youtube.com/watch?v=zL1Bqa6Tgqs) 上进行的随机配置运行的启发。

## 特性
- **长椅随机化**：每次在长椅上休息时，都会随机生成你的全套装备。
- **首领击败随机化**：当你对首领造成最终一击时，自动随机化你的装备配置。
- **按需快捷键**：按下 F10（可配置）即可随时随机调整你的装备搭配。
- **游戏内通知**：使用丝之歌原生的banner界面，在发生随机化时向你发出提示。
- **安全持久化**：内置存档保护机制拦截游戏的后台线程，确保非法或非所有权物品**永远不会**被永久写入你的存档文件。

## 配置  
所有选项均可通过游戏内的 BepInEx ConfigurationManager 菜单（默认快捷键 F1）进行配置。  
你可以精确地自定义哪些内容会被随机生成：  
- 包含/排除纹章、丝线技能、红色工具、蓝色工具或黄色工具。  
- 绕过解锁条件，允许随机生成你尚未找到的物品！  
- 对所有 48 首领战斗提供强大的支持，能够妥善处理多首领战斗而不会出现错误。

## 安装

**推荐方式：** 通过 [Thunderstore / r2modman](https://thunderstore.io/c/hollow-knight-silksong/p/thecodeflayer/SilksongLoadoutRandomizer/) 自动下载并安装。

**手动安装：**
1. 安装 [BepInEx 5](https://github.com/BepInEx/BepInEx)（空洞骑士：丝之歌）。
2. 将 `SilksongLoadoutRandomizer.dll` 放入你的 `BepInEx/plugins` 文件夹中。
3. 启动游戏，享受混乱吧！

## 源码与支持  
如果您遇到任何错误、有功能需求，或想查看源代码，请访问 [GitHub Repository](https://github.com/thecodeflayer/SilksongLoadoutRandomizer)。
