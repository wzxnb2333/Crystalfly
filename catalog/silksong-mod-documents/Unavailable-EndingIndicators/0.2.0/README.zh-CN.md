# EndingIndicators

一个显示每个存档槽位已完成结局的 `Hollow Knight: Silksong` 模组。

**Github 发布页**：
https://github.com/UserIsntAvailable/Silksong.EndingIndicators/releases/latest

**Thunderstore**：
https://thunderstore.io/c/hollow-knight-silksong/p/Unavailable/EndingIndicators

## 安装（手动）

1. 下载并安装 BepInEx。
2. 确保游戏已至少运行一次，以便生成一个 `plugins`
目录在 `BepInEx` 目录中生成，除了游戏之外
可执行文件。
3. 从上述列出的任一来源下载 .zip 文件。
4. 将.zip文件解压到生成的 `plugins` 目录中。
5. 启动游戏并享受吧！

## 配置

首次加载插件时，将在 `<SAVEFILES>/plugins/<PLUGINGUID>/config.dat` 下创建一个配置文件。你可以使用 [ConfigurationManager] 在游戏中更新并重新加载配置；如果你更倾向于手动修改配置文件，则需要关闭并重新打开游戏才能看到更改。

（以下为当前可配置的选项）

<!-- TODO(Unavailable): Automatically generate this section and make
it prettier (add some screenshots?). -->

```ini
## Plugin GUID: unavailable.ending-indicators

[Options]

## How the indicators should be displayed
## Endings that are not yet completed, will be shown in a more transparent color
## if `ShowAllIfAnyCompleted` or `ShowAll` are used.
# Setting type: EndingDisplayOpts
# Default value: OnlyCompleted
# Acceptable values: OnlyCompleted, ShowAllIfAnyCompleted, ShowAll
EndingDisplay = OnlyCompleted

## Reverse the order of the endings to match their natural order of unlocking
# Setting type: Boolean
# Default value: false
NaturalOrder = false
```

[Download and install BepInEx]: https://docs.bepinex.dev/articles/user_guide/installation/index.html
[ConfigurationManager]: https://github.com/BepInEx/BepInEx.ConfigurationManager
