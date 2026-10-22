# CanvasUtil

一个用于在《空洞骑士：丝之歌》中创建游戏内 UIs 的工具。这是从 CanvasUtil 库直接移植而来的 [Hollow Knight Modding API](https://github.com/hk-modding/api/blob/master/Assembly-CSharp/CanvasUtil.cs)。

如果你不打算调整面板大小，那么使用这个工具应该相当简单。
通常你将从创建一个全屏画布开始，例如：
```
GameObject canvas = CanvasUtil.CanvasUtil.CreateCanvas(RenderMode.ScreenSpaceOverlay, new Vector2(1920, 1080));
```

请注意，CanvasUtil.CanvasUtil 在开头出现，并且重复了 CanvasUtil。你可能希望在文件顶部添加类似 `using CUtil = CanvasUtil.CanvasUtil;` 的内容，以便调用 `CUtil.CreateCanvas`。

如果你有一个画布或面板 GameObject `parent`，并想添加一个子面板，则需要构造一个 RectData 作为传递给 CreatePanel 方法的参数：
* RectSizeDelta 应该是子面板的大小。
* AnchorPivot 应代表父面板上的一个点。坐标范围是从 (0, 0) 到 (1, 1)，其中 (0, 0) 是左下角，(1, 1) 是右上角。
* AnchorMin = AnchorMax，且两者都应代表子面板上的一个点。坐标范围是从 (0, 0) 到 (1, 1)，与父面板相同。
* AnchorPosition 是从父面板的锚点到子面板锚点的偏移量。

例如，要将一个子面板放置在父面板的右下角，可以设置锚点、锚点最小值和锚点最大值为 (1, 0) —— 这代表右下角（最大 x，最小 y）。锚点位置应设为 (0, 0)，以表示父面板的锚点（右下角）和子面板的锚点（右下角）是同一个点。

## DebugPanel

一个用于辅助模组开发的工具。有时你想要获取每帧都可能变化的信息，而日志记录可能并不是最有效的方式。CanvasUtil 提供了一个名为 DebugPanel 的实用工具来帮助解决这个问题。

最简单的方法是直接执行以下代码：

```
// In your plugin class, define a function to get the text to display
private static string GetDebugPanelText()
{
    // Code goes here. This function will be called each frame
    // to figure out what the text to display should be.
}

// In your plugin Awake method
CanvasUtil.DebugPanel.Create(GetDebugPanelText);
```

另一种方法可以如下所示：
```
// In your plugin class, define a function to update the text and an event raised when updating the text
private static event System.Action<string>? OnUpdateDebugPanelText;
internal static void UpdateDebugPanelText(string newText) => OnUpdateDebugPanelText?.Invoke(newText);

// In your plugin Awake method
var dp = CanvasUtil.DebugPanel.Create();
OnUpdateDebugPanelText += dp.SetText;

// Anywhere else in your mod
MyPlugin.UpdateDebugPanelText("the new text to display");
```

该功能专为模组开发时使用设计，不建议在发布的模组版本中使用。  
在上述两个示例代码中，只需删除“在你的插件 Awake 方法中”部分的代码，即可停止调试面板的创建，而不会影响模组的其他任何功能。
