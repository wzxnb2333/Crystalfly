# Silksong.Modlist

一个用于丝之歌的模组列表。就这样！

## 对用户

配置可以在 ModMenu、BepInExConfigurationManager 中更改，或通过编辑 `BepInEx/config/org.silksong-modding.modlist.cfg` 实现。  
- `DisplayMode` 设置主菜单中模组列表的显示方式：  
  - `Full` 始终显示所有模组，`Reduced` 显示模组数量及依赖关系紧密的模组，`Hidden` 隐藏模组列表。  
  - 在 `Full` 和 `Reduced` 设置下，游戏暂停时会显示简化版的模组列表。  
- `LogModFiles` 设置是否在日志中追加简化的文件树结构，有助于问题排查。

## 开发者指南

鼓励速通玩家使用的模组依赖于 ModList，以确保其在补丁文本中显示，从而为速通比赛中的作弊行为增加额外障碍。

要实现这一点，请在插件定义中添加对 Modlist 的依赖（并相应地更新 `thunderstore.toml`）：

```csharp
[BepInAutoPlugin(id: "your_mod's_guid")]
[BepInDependency("org.silksong-modding.modlist")]  // Add this line!
public class YourPlugin : BaseUnityPlugin 
{
    ...
}
```
```toml
# dependencies are specified in the format AuthorName-PackageName = "version". You should always have at least BepInExPack_Silksong.
[package.dependencies]
BepInEx-BepInExPack_Silksong = "5.4.2304"
silksong_modding-Modlist = "0.3.0"  # Add this line!
```
需要注意的是，这将导致您的模组在未启用 Modlist 时无法加载——这是有意为之的。
