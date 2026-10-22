# SkongGamemodes

A lightweight framework for creating custom gamemodes in **HK: Silksong**

---

## Features

* Initialize multiple gamemodes with custom names, colors, and descriptions.
* Easily Initialized in just a few lines

---

## Installation

1. Download `SkongGamemodes.dll`.
2. Add `SkongGamemodes.dll` to your BepInEx `plugins` folder **alongside your main plugin DLL**.
3. Reference it in your main mod project (Don't forget to add `using SkongGamemodes;`).
4. When you publish your mod to Thunderstore, don't forget to add it as a dependency in `manifest.json`, the custom modes won't load without it being in `plugins` folder

---

## Usage

### 1. Initialize a game mode

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

### 2. Accessing mode state

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

### 3. Creating multiple modes

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

## Notes

* Make sure your **main plugin references this DLL** and includes `using SkongGamemodes;`.
* Do **not** add multiple instances of `GameModeManager`—it’s a singleton.
---

## License

MIT License – feel free to use, modify, or integrate into other mods.
