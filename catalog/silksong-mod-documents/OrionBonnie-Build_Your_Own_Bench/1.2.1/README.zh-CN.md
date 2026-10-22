## 这个模组的作用

该模组允许玩家通过持有 Z 键（可配置）持续 2 秒来使用贝壳碎片在任意位置建造一个长椅。默认情况下，建造一个长椅需要 150 个贝壳碎片。每个场景（非区域）最多可以拥有 2（可配置）个自定义长椅（被摧毁的长椅也计入总数）。玩家也可以通过按住与建造相同的按钮持续一秒来摧毁附近的自定义长椅。如果被摧毁的长椅是当前存档点，它不会消失，而会留在当前场景中作为临时重生点。当玩家坐在另一个长椅上时，该被摧毁的长椅将被移除。自定义长椅会在地图上以自定义标记（为原版长椅标记的蓝色版本）进行固定显示。

所有自定义长椅数据共享于各个存档之间，因为它们使用同一个数据文件。

该模组内置了游戏内界面（默认按 F2 打开），显示当前场景名称，并允许玩家管理自己创建的自定义长椅（删除和传送）。蓝色标识表示该自定义长椅目前是你的重生/存档点。当一个自定义长椅作为当前存档点时，无法直接被删除，删除时会自动变为被摧毁状态以确保安全。不建议频繁使用传送功能，否则可能导致游戏崩溃。玩家可以通过在界面中点击长椅名称来重命名，以便更好地管理。

**如果要卸载此模组，请先坐在原版长椅上，否则在加载存档后你将重生到第一个场景（即大黄蜂笼子所在的场景）**。

所有自定义长椅的数据均保存在 **BepInEx\config\BenchesBuilt.json** 中，请不要随意删除或修改该文件，除非你知道自己在做什么。建议通过界面来删除不需要的自定义长椅。

关于可能引发的问题，在我测试过程中，此模组未导致任何严重问题（如游戏崩溃等）。唯一出现的问题是存档缩略图设置错误，原因未知，例如：如果你坐在《骨髓》中创建的自定义长椅上，存档缩略图会显示为其他场景。

如果你使用手柄，请务必编辑配置文件以将 BuildKey 修改为用于建造和摧毁功能。以下是手柄映射：

```
For PlayerStation Controller:
Cross = JoystickButton0
Circle = JoystickButton1
Square = JoystickButton2
Triangle = JoystickButton3
L1 = JoystickButton4
R1 = JoystickButton5
L2 = JoystickButton6
R2 = JoystickButton7
Share/Create = JoystickButton8
Options = JoystickButton9
LS(Left Stick Click) = JoystickButton10
RS = JoystickButton11
PS-Button = JoystickButton12
Touchpad = JoystickButton13

For Xbox Controller:
A = JoystickButton0
B = JoystickButton1
X = JoystickButton2
Y = JoystickButton3
LB = JoystickButton4
RB = JoystickButton5
Back/View = JoystickButton6
Start/Menu = JoystickButton7
LS(Left Stick Click) = JoystickButton8
RS = JoystickButton9
LT and RT are not recognized
```
