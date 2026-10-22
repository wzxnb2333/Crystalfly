# Prop Hunt

## 什么是物品猎杀？
物品猎杀与捉迷藏非常相似。
玩家会隐藏在地图上的各种物体（道具）中，而搜寻者则试图找到你。一旦被找到，你就会变成搜寻者。最后剩下的那个人获胜！更多详情请查看 [Gameplay](#gameplay) 部分。

## 安装说明  
本模组需要 [SSMP](https://thunderstore.io/c/hollow-knight-silksong/p/SSMP/SSMP/) 才能支持多人游戏。

### Thunderstore  
你可以使用任何兼容的安装工具或模组管理器来安装此模组。

### 手动安装  
1) 下载并解压模组文件  
2) 导航至你的 BepInEx 插件文件夹  
3) 将解压后的模组文件夹复制到插件文件夹中（示例：`Plugins/PropHunt/PropHuntMod.dll`）

> 如需模组支持，你可以在 [HK Modding](https://discord.gg/hollow-knight-modding-879125729936298015) 和 [HKMP](https://discord.gg/6nzXWhPpEf) 的 Discord 服务器中找到我（BobbyTheCatfish），也可以联系我的 [personal one](https://discord.gg/drv64CKZh4)。

## 游戏玩法  
在回合之间（详见 [commands](#commands) 部分），玩家可以自由切换道具并摧毁其他玩家的道具。每个房间都提供一组不同的可隐藏道具。  
> 每个房间可用道具的完整列表请参见 [here](https://docs.google.com/spreadsheets/d/1pxumrYVEBxFdBz4NKolcoCLyxjkwwWMmx7_CYVWZ0Qc)。  
你可以使用类似 [QuickWarp](https://thunderstore.io/c/hollow-knight-silksong/p/hk_speedrunning/QuickWarp/) 的模组轻松跳转到特定房间。

> 需要注意的是，某些房间（尤其是记忆房间）可能没有道具，或仅有极少数道具。  
若你遇到此类情况，请在该模组的 [GitHub](https://github.com/BobbyTheCatfish/PropHuntMod/issues) 上创建问题。

在开始游戏前，建议制定一些规则并配置相关设置（详见 [commands](#commands) 部分）：  
- 隐藏者被选中后，搜寻者何时开始行动？（默认为 30 秒）  
- 隐藏者是否可以进入其他房间？（仅推荐在人数较多的游戏中使用）  
- 应该有多少名搜寻者？（默认为 1 名）  
- 搜寻者是否需要攻击冷却，若有，请设置冷却时间？（默认无冷却）

一旦通过 [/ph start](#commands) 启动一轮游戏，至少会选出 1 名玩家作为搜寻者，其余所有玩家则为隐藏者。

#### 隐藏者  
一旦被选为隐藏者，你将随机化地变成一个道具并被隐藏。如果你希望，可以更改该道具并将其移动到其他位置。（详见 [keybinds](#keybinds)）

请在房间内某处（或允许的情况下，在邻近房间）隐藏起来，并与周围环境融为一体。  
**如果搜寻者经过并攻击你的道具，你将暴露身份并转为搜寻者。**  
如果你是最后一个被找到的玩家，你将获胜！

> 如果你和大量玩家一起游戏，可能建议允许在邻近房间及子区域中隐藏。

#### 搜寻者  
一旦被选为搜寻者，你的屏幕将在 30 秒内（默认值）变黑，并且无法移动。隐藏者们将利用这段时间进行隐藏。  
等待期结束后，你可以开始搜索隐藏者。**攻击一个道具会暴露其下方的玩家。**  
一旦找到一名隐藏者，该玩家即转为搜寻者。

## 使用方法  
> 有关如何设置或加入服务器的详细信息，请参见 [SSMP usage documentation](https://thunderstore.io/c/hollow-knight-silksong/p/SSMP/SSMP/)

### 常见问题解答  
- 我使用的存档文件有关系吗？  
	- 没有！你可以在任何关卡，拥有任意进度，其他玩家也应该能够正常看到你的道具  
	- 不过，建议使用一个基本完成的关卡 2 存档文件  

### 快捷键  
本模组包含几个快捷键，可在 BepInEx 配置管理器中进行修改。

`P` → `Prop`（更改你的道具）  

`R` → `Reset`（暴露自己并移除你的道具）  

`Numpad 5` → `Reset Prop Position`  

在隐藏状态下，你可以通过几种方式移动你的道具，但存在一定的限制。有多种方法可以移动道具，你可以在配置中选择使用的方法。  
#### 数字键盘  
```
7 8 9      forward         up          back
4 5 6       left      center/reset     right  
1 2 3     rot. left       down       rot. right
-   +     scale down                  scale up 
```
  
#### 控制器左右摇杆  
按下选定的摇杆以切换模式（在二维空间移动、前后移动、旋转、缩放、停止移动），并移动摇杆来移动道具。靠近生命条附近会出现一个指示器，显示当前处于哪种移动模式。

#### 键盘移动  
与控制器摇杆方法类似，此方式使用你的移动按键来移动道具。  
你可以通过按下 `taunt` 按键在不同模式间切换。

### 命令  
这些命令可通过按下 `Y` 打开 SSMP 聊天框  
- `/ph start`（开始道具寻宝一轮）  
- `/ph stop`（结束当前的道具寻宝轮次）  
- `/ph config <seekerwait|seekercount|attackcooldown>`（设置搜寻者参数）  
- `/sync`（同步其他玩家道具的状态）

## 计划  
这些只是我未来希望添加的一些功能  
- 添加游戏配置  
  - 限制每位玩家每轮的道具交换次数

## 提交问题  
目前这个模组仍处于早期测试阶段。我还没有机会充分测试多人游戏功能，因此请经常检查你的日志记录。  
请将你看到的任何错误（无论是日志中的内容还是你注意到的现象）提交至该模组的 [GitHub repository](https://github.com/BobbyTheCatfish/PropHuntMod/issues)。  
非常感谢！我希望这个模组能让每一位玩家都能享受乐趣。