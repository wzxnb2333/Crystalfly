
# Infinite Tools Mod

A BepInEx mod for Hollow Knight: Silksong that provides infinite resources for selected tools.\
You can also choose when your tools get refilled : each time you use your tool, each time you sit on a bench or each time you get healed.

## Configuration

After the first launch, edit `BepInEx/config/com.dream.InfiniteTools.cfg` in your Silksong folder to select which tools should have infinite resources.\
Just change the false in front of the tools you want into true if you want to activate it.\
If you want to choose when your tools get refilled, just change this line : ```Replenishment Method = EveryUse```. You will have some comments to explain you how to do it above\
Note that you will have to set "Infinite use for all" to false if you want to have some tools cheats disabled.

### Example Config

```ini
[Infinite Tools]
Replenishment Method = EveryUse
Enable Infinite Use for All Tools = false
Enable Infinite Use for Cogfly = true
Enable Infinite Use for Cogwork Wheel = false
```

Will only refill cogfly without paying each time it get used.

## Installation

```ini
1.Install BepInEx
2.Download the zip and extract the .dll into your BepInEx/plugins folder.
3.Setup you config file as you want.
4.Enjoy!
```