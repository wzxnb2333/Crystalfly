SilksongRooms: 可共享房间加载器

本仓库包含一个模板（`content/SilksongRooms`），其中提供了一个可复用的房间加载器和一个小型集成组件。它允许任何模组加载房间 JSON 文件（`*.room.json`）并实现 Unity AssetBundles。

亮点：
- 将房间文件放入 `BepInEx/plugins/<YourMod>/Rooms/` 目录中，它们将被 SilksongRooms 插件自动拾取。
- 其他模组可通过一个简单的 API 在运行时注册自己的 Rooms 文件夹。

在其他模组中使用：

```csharp
using BepInEx;
using SilksongRooms._1;

[BepInDependency("io.github.silksongrooms__1")]
[BepInPlugin("your.guid.here", "Your Mod", "1.0.0")]
public class YourMod : BaseUnityPlugin
{
	private void Awake()
	{
		var myRooms = System.IO.Path.Combine(BepInEx.Paths.PluginPath, Info.Metadata.Name, "Rooms");
		RoomsApi.RegisterRoomsFolder(myRooms);
		// Or single file:
		// RoomsApi.RegisterRoomJson(System.IO.Path.Combine(myRooms, "my_room.room.json"));
	}
}
```