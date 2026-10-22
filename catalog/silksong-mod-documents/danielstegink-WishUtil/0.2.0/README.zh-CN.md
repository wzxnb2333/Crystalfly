# 祝愿工具
该库提供了创建自定义祝愿所需的功能。

## 创建你的愿望

首先，你需要通过继承 CustomQuest 类来创建自定义的愿望对象。

构造函数需要传入一个名称，作为该愿望在游戏中的唯一标识符，以及用于显示愿望名称和位置的 LocalisedStrings。

你的对象还需要覆盖 3 属性：
- GiveAtStart 表示游戏是否在启动时将该愿望分配给玩家。
- QuestType 定义该愿望的类型：狩猎、采集等。
	- 你可以通过调用 GetQuestType.GetType 使用默认的愿望类型。
	- 或者，你可以使用 GetQuestType.BuildCustomType 提交自定义类型。你需要提供该类型显示名称的 LocalisedString、该类型的文本颜色，以及 4
		各种图标对应的 Sprites。
		- 这些图标示例可以参考本模组 Git 项目中的 Resources 文件夹（Thunderstore 链接）。
- GetDescription 提供愿望的文本描述，该描述会在任务日志中显示。

## 将你的愿望添加到丝之歌  
创建完你的愿望后，你需要将其添加到游戏里。在你模组的 Start 方法中，创建一个你愿望的实例，并使用 QuestData.AddQuest 将其添加到 WishUtil 的自定义愿望列表中。

你可能应该将愿望作为变量存储在模组中，以便方便引用，但你也可以通过名称使用 QuestData.GetQuest 来查找它。

## 与丝之歌中的愿望互动  
愿望已加入游戏，你可以在 HarmonyPatches 中对其进行操作。  
CustomQuest 目前提供了 3 方法，可用于更改愿望的状态：  
- Accept() 将愿望添加到任务日志中。如果你将 GiveAtStart 设置为 true，则可以跳过此步骤。  
- Update() 向玩家提示愿望已取得进展。  
- Complete() 告诉游戏将该愿望标记为已完成。

## 特别致谢  
the.ant.57 和 MistDZX 对测试与反馈的贡献