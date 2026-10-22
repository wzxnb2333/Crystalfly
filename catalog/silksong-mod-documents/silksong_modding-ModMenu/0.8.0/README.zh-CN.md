# Silksong.ModMenu

丝之歌的标准化菜单修改模组。它为模组提供了简洁而强大的功能集。

该模组默认会在主选项菜单中添加一个“模组选项”菜单。

其余内容面向模组开发者。如果你不是模组开发者，现在就可以停止阅读了。

## 模组选项

本模组的主要功能是在“选项”菜单底部新增一个“模组”子菜单。其他插件可以根据需要向该菜单中添加内容。

默认情况下，任何已安装的插件的 [`ConfigFile`](https://docs.bepinex.dev/api/BepInEx.Configuration.ConfigFile.html) 数据将被用于生成游戏内的菜单，以修改这些配置选项。目前仅支持布尔值、枚举类型和显式值列表；对于更复杂的配置（如任意整数和浮点数），需要手动构建菜单。

插件可以通过手动使用 [`Registry`](Registry.cs) 类向主菜单添加子菜单，或通过实现其中一个继承自 [`IModMenuInterface`](Plugin/IModMenuInterface.cs) 的 [Plugin](Plugin) 接口来简化开发流程。

插件可以选择不自动创建菜单，方法如下：
* 在其插件类中添加 [`[ModMenuIgnore]`](Models/Attributes.cs) 属性。
* 创建一个名为 `ModMenuIgnoreAttribute` 的属性并添加到其插件类中。  
这可以通过将以下代码粘贴到项目中实现：  
```cs
internal class ModMenuIgnoreAttribute : System.Attribute { }
```
，然后在插件类上方（或下方）的 `[BepInAutoPlugin(...)]` 行之后放置 `[ModMenuIgnore]`。

## API

空洞骑士的 ModMenu API 可以分为三个独立且层级分明的类别。

### 屏幕

[Screens](Screens) 是单个菜单界面，玩家每次只能看到一个。对于大多数使用场景，仅需 [`SimpleMenuScreen`](Screens/SimpleMenuScreen.cs) 即可支持基本配置；但对于更大、更复杂、内容更密集的菜单，则需要考虑 [`AbstractMenuScreen`](Screens/AbstractMenuScreen.cs) 的替代实现。

创建一个新的屏幕对象会实例化所有必要的 Unity 预制体，将其作为 `UIManager` 实例的子对象，并保持其处于非激活状态。要使你的菜单显示出来，需要通过 [`Registry`](Registry.cs) 注入一个入口点，从而调用 [`MenuScreenNavigation.Show(...)`](Screens/MenuScreenNavigation.cs)。`MenuScreenNavigation` 是用于在自定义菜单中前后导航的标准静态类。

每当 UIManager 被重新创建时，菜单界面都会通过注册表 APIs 重新生成，这可能在一次游戏会话中发生多次。在菜单创建过程中需注意避免事件订阅者泄漏。

开发者可根据需要实现自己的 [`AbstractMenuScreen`](Screens/AbstractMenuScreen.cs) 子类以定义新的布局算法。

### 元素

[Elements](Elements) 是填充菜单界面的单个资源，例如按钮、标签、选择器和滑块。屏幕负责管理添加到其中的元素的组织方式以及控制器导航行为。

为了保证一致的用户体验，元素还定义了标准的 [Colors](Elements/Colors.cs) 和 [Font sizes](Elements/FontSizes.cs) 来表示不同状态，如 true/false 按钮和无效数据。

开发者可根据需要实现自己的 [`MenuElement`](Elements/MenuElement.cs) 子类以提供新机制。但请注意，如果这些元素需要支持控制器导航，则必须明确地被设计为 [`SelectableElement`](Elements/SelectableElement.cs)。

### 模型

[Models](Model) 是低层级的数据容器和监听器，与任何 Unity 依赖解耦。它们是简单且可扩展的类和接口，用于管理 [`MenuElement`](Elements/MenuElement.cs) 中表示的数据。

例如，一个 [`ChoiceElement`](Elements/ChoiceElement.cs) 的数据由一个 [`IChoiceModel`](Models/IChoiceModel.cs) 模型进行建模，该模型决定了底层数据类型以及用户向左或向右导航时会发生什么。在大多数情况下，使用 [`ListChoiceModel`](Models/ListChoiceModel.cs) 已足够，但在高级用例中可能需要自定义行为，例如动态过滤或无限滚动。低层级的模型接口支持此类需求。

模型是具有状态的，会记住当前表示的数据值，因此你应该为每项数据创建新的实例；i.e。如果配置包含 3 个不同的 `boolean` 参数，则需要 3 个独立的模型来表示它们，除非你确实希望在 UI 的多个部分中显示相同的数据，否则不应复用现有的模型。

建议（但非强制）每次菜单被重新生成时都创建新的模型，并在前一个菜单被销毁时移除旧的订阅者。

## 生成菜单

任何数据类都可以通过添加 `[GenerateMenu]` 属性，自动为每个关联字段生成元素，该过程通过源码时生成实现。所有公开的字段和属性（支持类型）都将被处理。

非公开字段或属性可以通过添加 `[GenerateMenu]` 注解包含进来。公开属性可以通过添加 `[ModMenuIgnore]` 注解忽略。各种约束和自定义设置可通过在 [`Generator/Attributes.cs`](Generator/Attributes.cs) 中文档化的注解提供。

## 未来工作

直到发布版本 1.0 之前，ModMenu 模组应被视为 _不稳定_。为实现 1.0 而增加功能的过程中，可能会出现破坏 API 的变更。

可选的未来工作：
*   在“新游戏”菜单中添加新的游戏模式。
*   新的界面内容面板实现：
	*   多行显示
*   新的菜单元素实现：
	*   图标/图片
