# 丝之歌预补丁

适用于空洞骑士：丝之歌的通用汇编修改预补丁。

# PrepatcherPlugin

该插件暴露了事件以监控玩家数据访问，并在不修改底层存档数据的情况下控制游戏接收到的值。

使用时，需订阅 `PlayerDataVariableEvents<T>.OnGetVariable` 或 `PlayerDataVariableEvents<T>.OnSetVariable` 事件，其中 `T` 表示字段类型。

订阅者接收参数为玩家数据对象、字段名以及未修改的值（若有多个订阅者，则为之前的值），并应返回修改后的值；在 GetVariable 情况下，未修改的值是字段的实际值，返回值是游戏代码所看到的值；而在 SetVariable 情况下，未修改的值是游戏代码试图设置的值，返回值才是实际被写入的值。

为了方便起见，已在非泛型的 PlayerDataVariableEvents 类中添加了一些常用事件。这些事件的行为与对应的泛型版本相同，例如 `PlayerDataVariableEvents.OnGetBool` 与 `PlayerDataVariableEvents<bool>.OnGetVariable` 是等效的。

示例用法：

```cs
using GlobalEnums;
using PrepatcherPlugin;

// In your mod's Awake class

PlayerDataVariableEvents.OnSetBool += MonitorBoolSets;
PlayerDataVariableEvents<CaravanTroupeLocations>.OnGetVariable += OnGetCaravanLocation;

// Defining the functions

private bool MonitorBoolSets(PlayerData pd, string fieldName, bool current)
{
	Logger.LogInfo($"Setting bool {fieldName} to {current}");
	
	// Make sure to return current if you don't want to modify the value that would be set.
	return current;
}

private CaravanTroupeLocations OnGetCaravanLocation(PlayerData pd, string fieldName, CaravanTroupeLocations current)
{
    // If the caravan is in the Marrow, make the game think it's in Greymoor. Otherwise no change.
	return current == CaravanTroupeLocations.Bone ? CaravanTroupeLocations.Greymoor : current;
}
```


为了方便起见，PlayerDataAccess 类提供了属性，用于通过上述事件从 PlayerData.instance 对象中获取和设置值（使用未修改代码中定义的 Get/Set 函数），以保持与监控存档数据访问的模组兼容性。例如，以下两种写法是等效的：

```cs
bool b = PlayerData.instance.GetBool(nameof(PlayerData.hasBrolly));
PlayerData.instance.SetBool(nameof(PlayerData.hasWalljump), true);
```
 和
```cs
bool b = PlayerDataAccess.hasBrolly;
PlayerDataAccess.hasWalljump = true;
```

# Prepatcher

以下补丁由预补丁工具生成：

* 通过 Get/Set 变量函数路由 PlayerData 字段访问。
这样做的目的是为了让模组能够更方便地监控游戏在获取或设置时的情况。
PlayerData 中的字段值，并控制对这些访问的输出/效果
通过在 PrepatcherPlugin 中定义的事件进行。
特别是，这使得相互作用的模组之间能够实现兼容性。
PlayerData以不同方式。

* 将某些对 Assembly.GetTypes 的调用替换为对安全方法的调用。
特别是，新方法在遇到不可加载的类型时不会抛出异常
存在于程序集中的，并会忽略 MMHOOK_ 程序集（其中可能包含）
多种类型，其中没有一种对基版本中定义的函数感兴趣
程序集（assemblies）
为了验证预补丁程序在此处所做的更改是否恰当，请检查以下内容
未安装预补丁程序（可通过简单的 MonoMod Hook 完成）：
- 记录 `NestedFadeGroup.AddMissingBridgeComponents` 方法的执行时长在
`TeamCherry.NestedFadeGroup.dll`. 此操作在启动时执行一次，但非常
如果游戏需要从大量程序集检查大量类型，则会变慢。
- 记录长时间的持续时间（i.e超过 1ms 的调用会触发`ReflectionUtils.GetGlobalType`
在 `PlayMaker.dll` 中，尤其是在进入 Bone_04、Bone_10 和 Greymoor_08 等场景时
（以及其他情况）。这些特定情况由 ReflectionUtilsPatcher 处理，但
通常情况下，GetTypes 替换会加快函数的运行速度。

* 将某些对 Type.IsAssignableFrom 的调用替换为对安全方法的调用。
出于某种原因，在某些情况下类型将可以通过
GetTypes 和非 null，但在参数为时会抛出错误
Type.IsAssignableFrom. 这仅在某些情况下出现，当模组缺失时
软依赖项。


### 检查已修补的程序集

为了检查已修补的程序集，请设置 DumpAssemblies 配置变量
在 BepInEx.cfg 到 true 之间。
