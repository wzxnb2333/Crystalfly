# Silksong.Benchwarp

本模组将《空洞骑士》的 [Benchwarp mod](https://github.com/homothetyhk/HollowKnight.BenchwarpMod) 迁移到《空洞骑士：丝之歌》中。Benchwarp 功能支持：
- 通过菜单设置重生点为任意长椅。
- 通过菜单传送至当前重生点。
- 通过菜单传送至一个过渡点。

Benchwarp 提供多种模式，可在配置文件或模组菜单中选择：
- StandardBenchwarp：仅传送至已访问的长椅。
- WarpOnly：仅传送至当前重生点。
- UnlockAll：可传送至任意长椅。
- Doorwarp：传送至任意门过渡点。

在菜单中，长椅状态显示如下：
- 黄色：当前重生点。
- 白色：曾访问过的长椅。
- 红色：未访问的长椅。
- 紫色：锁定（仅用于与其他模组集成时使用）。

## 依赖项

- BepInEx 及核心丝之歌模组库：DataManager、I18N、ModMenu、Prepatcher、FsmUtil、AssetHelper。
- Benchwarp 的发布版本：[Thunderstore](https://thunderstore.io/c/hollow-knight-silksong/p/homothety/Benchwarp/)。建议使用基于 Thunderstore 的安装程序来管理依赖项和更新。

## 特性  
### 快捷键  
Benchwarp 支持快捷键，可在游戏暂停时替代菜单按钮使用。所有快捷键均由两个连续的按键操作组成，按键无需同时按下。快捷键分为两种类型：  
1. 字母后接一个数字：从菜单中选择一个长椅，并传送到该位置（如果该长椅不可选，则传送到当前重生点）。当启用快捷键时，这些组合的标签会显示出来。  
2. 一对字母，执行特殊命令，例如：  
- **LB**：**L**ast **B**ench（等同于直接点击“传送”按钮）。  
- **SB**：**S**tart **B**ench（等同于点击“设置起点”，然后“传送”）。  
- **DW**：**D**oor **W**arp（等同于将菜单模式设为“门传送”）。如果当前已启用门传送，则切换至标准长椅传送模式。  
- **NP**：**N**ext **P**age（等同于点击页面按钮）。  
- **DB**：**D**eploy **B**ench（等同于点击“部署”按钮）。  
- **WD**：**W**arp **D**eploy（等同于点击“设置部署”，然后“传送”）。  

提示：默认情况下，某些字母（如 **A**）被绑定到可取消菜单的操作。若要输入包含这些字母的快捷键，可以将鼠标移开以取消选择“继续”按钮。或者，可以几乎同时按下两个按键来输入快捷键。

### 部署  
- 当启用 `EnableDeploy` 配置选项时，可在屏幕左下角找到部署子菜单。  
- “部署”按钮会在大黄蜂当前位置放置一个长椅或重生标记。即使退出房间或退出游戏，该重生点也会持续存在。  
- “设置”按钮会将已部署的重生点设为大黄蜂当前的重生位置，可用于返回该重生点。  
- “销毁”按钮会删除已部署的重生点。如果当前重生点正是该部署点，则大黄蜂的当前重生点将被设为“起点”。  
- “样式”按钮会打开下拉菜单以选择部署重生点的类型。目前选项包括：“地面”（重生时大黄蜂从地面起身），以及“骨质长椅”（用于骨底和莫斯之家大门处的长椅风格）。  
- 若因部署的重生点位于不安全位置而导致无法加载存档，请参阅下文关于 RecoveryMode 的说明。

## 兼容性
- 如果 DebugMod 的 *NoClip* 模式已启用，传送可能导致大黄蜂变得无法移动。此问题可通过再次切换 *NoClip* 来解决。
- 如果存档文件变为无法加载（例如 e.g. respawn 设置为一个不存在的重生点），请在长椅传送配置（或模组菜单）中激活 RecoveryMode。在该功能启用期间，从主菜单加载的任何现有存档都将被载入 Tut_01（即游戏中的第一个房间：苔藓洞穴）。这样你可以在安全的长椅位置保存游戏，之后应关闭 RecoveryMode。

## Credits

- Huge special thanks to **Phenomenol** for leading the effort to clean transition data and handle obstacles for Doorwarp mode.
- Thanks to Flibber, Nerthul, and Annalythic for contributions to the transition data cleaning effort and to Posneg for contributions to obstacle handling.
- Special thanks to GamingInfinite (VoidBaroness) for bench research and UI design!

## License

Benchwarp is licensed under LGPL. Source code and license can be found at https://github.com/homothetyhk/Silksong.Benchwarp/.