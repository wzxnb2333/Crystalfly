![BepInEx logo](https://avatars2.githubusercontent.com/u/39589027?s=256)

# BepInExPack-丝之歌

这是专为《空洞骑士：丝之歌》定制的 BepInEx 版本。

确切版本是 `BepInEx_win_x64_5.4.23.4.zip` 来自 https://github.com/BepInEx/BepInEx/releases/tag/v5.4.23.4，并针对更好的跨平台支持进行了优化（具体变更如下）。

BepInEx 是一个通用的 Unity 模组框架。  
BepInEx 包含工具和库，可用于

* 在启动时将自定义代码（以下简称 *插件*）加载进游戏；
* 不修改原始游戏文件的情况下，对游戏中的方法、类甚至整个程序集进行修补；
* 配置插件并将其日志输出到指定目标，如控制台或文件；
* 管理插件依赖关系。

BepInEx 当前为 [one of the most popular modding tools for Unity on GitHub](https://github.com/topics/modding?o=desc&s=stars)。

## 本包的内容

本包已预配置，适用于 x64 Unity 游戏。  
特别地，它包含一个预配置的 `BepInEx.cfg` 文件，内含我们推荐的默认配置。插件开发者也可能希望将 Logging.Console.Enabled 设置为 true。

此外，还提供了一个启动脚本（run_bepinex.sh），其可执行文件名称已设置为适用于 Linux 和 Mac，并附带了这些平台所需的原生代码，以及 Windows。

提供了一个预配置的 steam_appid.txt，以确保 Steam 用户无需通过 Steam 启动游戏。

## 安装（模组管理器）

这是在游戏上安装 BepInEx 的推荐方式。

1. 下载并安装 [Thunderstore Mod Manager](https://www.overwolf.com/app/Thunderstore-Thunderstore_Mod_Manager) 或其他兼容的模组管理器  
2. 在网站页面上点击 **使用模组管理器安装** 按钮  
3. 通过模组管理器运行游戏

## 手动安装

如果您是手动安装此模组，请执行以下步骤：

1. 将压缩包提取到一个文件夹中。**不要将内容提取至游戏文件夹内。**
2. 将 `BepInExPack` 文件夹中的内容移动到游戏文件夹（即游戏可执行文件所在的位置）。
3. 启动游戏。如果一切正常，您将在桌面看到 BepInEx 控制台的弹出窗口。

## 有用链接

* [BepInEx: writing basic plugin walkthrough](https://docs.bepinex.dev/articles/dev_guide/plugin_tutorial/index.html)  
* [BepInEx: useful plugins for modding](https://docs.bepinex.dev/articles/dev_guide/dev_tools.html)  
* [BepInEx: patching game methods at runtime](https://docs.bepinex.dev/articles/dev_guide/runtime_patching.html)

## 问题、疑问等

目前您可以使用以下渠道寻求帮助：
* [Hollow Knight/Silksong Modding Discord](https://discord.gg/VDsg3HmWuB)

由于我们使用了自定义版本的 BepInEx，请勿直接前往 BepInEx 失落之境，他们可能无法提供支持。
