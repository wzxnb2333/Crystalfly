# CanvasUtil

A utility for making in-game UIs in Hollow Knight: Silksong. This is a direct port of the CanvasUtil
library from the [Hollow Knight Modding API](https://github.com/hk-modding/api/blob/master/Assembly-CSharp/CanvasUtil.cs).

If you are not planning to resize panels, then this should be fairly straightforward to use.
Typically you will start by creating a whole-screen canvas with something like
```
GameObject canvas = CanvasUtil.CanvasUtil.CreateCanvas(RenderMode.ScreenSpaceOverlay, new Vector2(1920, 1080));
```

Note that CanvasUtil.CanvasUtil appears at the start with CanvasUtil repeated. You may want to put something
like `using CUtil = CanvasUtil.CanvasUtil;` at the top of your file, so you can call `CUtil.CreateCanvas`.

If you have a canvas or panel GameObject `parent` and you want to add a child panel, then to construct the
RectData to pass into the CreatePanel methods:
* RectSizeDelta should be the size of the child panel.
* AnchorPivot should represent a point on the parent. The coordinates are from (0, 0) to (1, 1), where (0, 0) is the
bottom left corner and (1, 1) is the top right.
* AnchorMin = AnchorMax, and both should represent a point on the child panel. The coordinates are from (0, 0) to (1, 1),
as with the parent.
* AnchorPosition is the offset from the pivot point on the parent to the anchor point on the child.

For example, to put a child in the bottom-right corner of the parent, you can set the anchor pivot, anchor min and anchor max to (1, 0) - this
represents the bottom right corner (max x, min y). The anchor position should be (0, 0) to represent the fact that the anchor pivot
(bottom right corner of parent) and anchor (bottom right corner of child) are the same point.


## DebugPanel

A utility added to help in mod development. Sometimes you want to know information that may change
each frame, so logging is probably not the most helpful way to do it. CanvasUtil provides a utility
called DebugPanel to help with this.

The easiest way to do this is to simply do the following:

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

An alternative approach can be as follows:
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

This is designed to be used while developing mods and is not expected to be used in published versions of mods.
In both of the example codes, simply deleting the code from the "In your plugin Awake method" section
will stop the debug panel from being created without breaking any other aspect of your mod.
