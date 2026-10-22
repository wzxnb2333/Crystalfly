# 说明文档

本模组为丝之歌添加了 bingo 模式，玩家将在一个方格网格上完成一系列小任务（e.g。“完成 7 的愿望”或“击败骷髅暴君”）。

你可以与他人加入一个 bingo 房间，从而追踪已完成的目标。游戏有多种玩法方式，例如先达成三行、锁死模式，或合作熄灯模式。  
如果你使用由丝之歌 bingo 社区维护的自定义目标集，模组将自动标记完成的任务方格。不属于该集合的目标会标注 (M)，表示“手动标记”，你需要通过点击方格来自行标记。由于其他插件模组触发而自动标记的目标则会在目标名称后加上 * 符号。

# 键位绑定  
B：显示或隐藏当前界面。在游戏过程中仅显示棋盘（或完全隐藏界面）时非常有用。  
O：调整当前界面的透明度。有助于同时看到游戏画面和棋盘。  
(未绑定)：显示当前棋盘，实际上开始 bingo 游戏。你也可以手动点击按钮完成。  
（以上所有键位均可在配置选项中重新绑定）

# 设置  
首先，你需要创建一个 bingo 房间。这里是一个 [video walkthrough for creating one](https://youtu.be/mearSngmZoU)。  
然后打开游戏，输入房间链接、你的昵称以及房间密码。  
一旦加入房间并准备就绪，创建一个新的文件，揭示游戏板，开始享受乐趣吧！

如果你有任何疑问，或者想了解更多关于丝之歌 bingo 的信息，请查看我们的 [community Discord](https://discord.gg/fG3uDT36Py)！我们非常包容，为新手玩家提供了大量学习如何游戏的资源。

你也可以在 discord 中提问，如果发现任何错误，或者想要提出新增功能/建议！

# 添加自定义目标  
模组开发者可以在自己的模组中添加对目标的自定义自动评分支持！  
为了设置依赖关系，请按照以下步骤操作：  
1. 打开你的 .csproj 文件，并通过添加 `<PackageReference Include="Silksong.VogsBingoMod" Version="1.4.0"/>` 来引入 NuGet 包，建议使用当时最新的版本。  
2. 在你的 thunderstore.toml 中添加对 `AtlasOfSouls-VogsBingoMod = "1.4.0"` 的依赖。  
3. 最后，在插件类中位于 BepInAutoPlugin 属性下方添加这一行：`[BepInDependency(Silksong.VogsBingoMod.VogsBingoModPlugin.Id)]`  

完成上述设置后，你可以通过使用 `CustomGoal.Create(string goalName)` 来注册一个自定义目标，该方法将返回一个 `CustomGoal` 对象，调用 `myCustomGoalObject.TryMark()` 即可将其标记在面板上。目标将按名称进行注册，当调用 `TryMark()` 时，所有名称匹配的面板目标都将被标记。

Credits:  
Abby, for curating the goalset and hosting caravan  
Bingosync, for hosting the servers that the mod connects to  
The Hollow Knight/Silksong Modding Discord, for various help getting me started with modding Silksong  
The Silksong Bingo Discord, for helping with feedback and suggestions  
Everyone who helped with playtesting before release

The lack of an explicit license on this project is intentional.  
© 2026 AtlasOfSouls
