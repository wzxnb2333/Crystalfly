# SkongGamemodes

一个用于在 **空洞骑士：丝之歌** 中创建自定义游戏模式的轻量级框架

## 特性

* 使用自定义名称、颜色和描述初始化多个游戏模式。
* 仅需几行代码即可轻松初始化。

## 安装

1. 下载 `SkongGamemodes.dll`。
2. 将 `SkongGamemodes.dll` 添加到你的 BepInEx `plugins` 文件夹中，与主插件 DLL 放在同一目录下。
3. 在你的主模组项目中引用它（别忘了添加 `using SkongGamemodes;`）。
4. 当你将模组发布到 Thunderstore 时，请记得在 `manifest.json` 中将其作为依赖项添加，否则自定义模式将无法加载，且该文件必须存在于 `plugins` 文件夹中

## 使用方法

### 1. 初始化游戏模式  

```csharp
using UnityEngine;
using SkongGamemodes;
[BepInDependency("dervorce.hkss.gamemodemanager", BepInDependency.DependencyFlags.HardDependency)] // Add this to make sure it loads before your plugin
[BepInPlugin("dervorce.hkss.mypluh", "My Plugin", "1.0.0")]
public class MyPlugin : BaseUnityPlugin
{
    private GameModeManager.GameModeData myMode;

    private void Awake()
    {
        myMode = GameModeManagerPlugin.Instance.Manager.Init(
            this,
            "Bound Soul",                 // Mode Name
            "Enable The Binder by Pressing H",  // Description
            new Color(0.941f, 0.780f, 0.255f, 0.8f)  // UI Color (Nullable, if you put in null, it will not modify the color)
        );
    }
}
```
  
### 2. 访问模式状态  

```csharp

    [HarmonyPatch(typeof(PlayerData), "TakeHealth")]
    public static class Patch_GlassyBoy
    {
        [HarmonyPrefix]
        public static void glAss(PlayerData __instance, ref int amount)
        {
            if (BoundSilkPlugin.glassBoundMode.Enabled) // if GlassBoundMode is enabled
            {
                amount = __instance.CurrentMaxHealth; // Die in one hit
            }
        }
    }
```
  
### 3. 创建多个模式  

```csharp
var steelBoundMode =  GameModeManagerPlugin.Instance.Manager.Init(
    this,
    "SteelBound Soul",
    "Bound by Pantheon, Chained by Steel",
    null, // color but nulled
    steel: true // if steel is enabled, this will make it based off steel soul mode instead of normal mode, which means steel soul exclusive stuff and permadeath being part of your gamemode
    sprite // you can add in a UnityEngine.Sprite which will override the regular image
);
```
  
---

## 注意事项

* 请确保你的**主插件引用此 DLL**，并包含 `using SkongGamemodes;`。
* 不要多次添加 `GameModeManager`——它是一个单例。

## License

MIT License – feel free to use, modify, or integrate into other mods.
