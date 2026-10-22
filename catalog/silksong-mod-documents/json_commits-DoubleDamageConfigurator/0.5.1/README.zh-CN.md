# DoubleDamageConfigurator

<!-- WIP_LISTING -->  
**WIP。此列表尚未完成且存在已知Bug。请勿将其视为最终版本。**  
<!-- /WIP_LISTING -->

一个《空洞骑士：丝之歌》BepInEx插件。它在“选项 > 插件”中添加了一个敌人列表。每行对应一名敌人，其被导出的预制体`DamageHero`造成的伤害不低于2层护甲。当记录匹配时，该敌人的猎人日志头像会显示在其名称左侧。未匹配的名称则保留一个深色空方块。

将敌人的护甲上限设为1层护甲。关闭状态则保持原版伤害。新安装后，所有列出的敌人默认处于开启状态。来自这些敌人的攻击在你将其关闭之前，护甲上限均被限制在1层护甲。菜单中会在此处显示该设定项。

## 使用 r2modman 安装

1. 打开 r2modman，选择 空洞骑士：丝之歌。
2. 打开或创建一个配置文件。本仓库中的本地构建文件会复制到开发配置文件中。
3. 如果尚未在配置文件中安装，请安装 [BepInExPack_Silksong](https://thunderstore.io/c/hollow-knight-silksong/p/silksong_modding/BepInExPack_Silksong/)、[ModMenu](https://thunderstore.io/c/hollow-knight-silksong/p/silksong_modding/ModMenu/)、[I18N](https://thunderstore.io/c/hollow-knight-silksong/p/silksong_modding/I18N/) 和 [AssetHelper](https://thunderstore.io/c/hollow-knight-silksong/p/silksong_modding/AssetHelper/)。AssetHelper 在没有 I18N 的情况下无法加载。
4. 安装本包，或将构建好的文件复制到 `BepInEx/plugins/json_commits-DoubleDamageConfigurator/`。
5. 从 r2modman 启动游戏，请勿使用 Steam 的播放按钮。

## 使用敌人列表

1. 打开 **选项**，然后 **模组**，再选择 **双倍伤害配置器**。  
2. 在扫描过程中，列表会显示百分比和文件数量（已检查 / 队列中），而非敌方单位行。在扫描完成前无法更改“关闭”选项。扫描完成后会重新加载当前菜单。  
3. 使用 **显示** 选择全部、敌人、首领或其他类型。敌人是指与日志匹配的非“其他”敌人。“其他”包括日志中无记录的dump名称。首领是其预制体FSM中带有首领标题的敌人。  
4. 在 **搜索** 中输入dump名称或日志名称以进行匹配。  
5. 将某行保持为 **开启** 状态，以限制该敌人的伤害值至 1 掩码。将其切换为 **关闭** 状态则保留原版伤害。滚动列表可查看所有匹配项。  
6. 游戏更新后或之前扫描被中断时，请按 **重新生成列表**。重新生成会从已安装的游戏内容中重建名称和伤害数据，不会清除“关闭”选项。

该目录保存至 `BepInEx/config/DoubleDamageConfigurator.enemy-catalog.json`。关闭选项保存至 `BepInEx/config/DoubleDamageConfigurator.enemy-settings.json`。这两个文件均不包含在zip包中。

行名称在日志记录匹配时使用猎人日志中的名称，否则使用dump GameObject的名称。共享同一日志名称的日志匹配变体将共用一个开启/关闭行，该开关对组内所有dump ID均生效。当 2 掩码命中但仅存在于PlayMaker中的敌人，在后续扫描中才会被识别出来。

## 本地构建

你需要 .NET 10 SDK。从仓库根目录开始：

```
dotnet build
```

构建过程会将 DLL 文件复制到 r2modman 的配置文件中，当 `SilksongPath.props` 存在时。该文件被 Git 忽略。请使用 `dotnet new silksongpath` 创建一个文件，并设置 `SilksongFolder` 为配置目录路径，例如 `C:/Users/json/AppData/Roaming/r2modmanPlus-local/HollowKnightSilksong/profiles/Development`。

打包使用 NuGet `tcli` 工具（仅支持 `init`、`build`、`publish`）。生成的 zip 文件会保存在 `thunderstore/dist`。

## BepInEx GUID 仍为模板值

`DoubleDamageConfiguratorPlugin.cs` 仍使用模板 GUID `io.github.change_me.doubledamageconfigurator`。首次公开发布后，不要更改该 GUID。
