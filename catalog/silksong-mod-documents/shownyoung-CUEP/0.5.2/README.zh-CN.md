# CUEP

有一个非常有用的模组叫做 UnityExplorerPlus（在 [there](https://github.com/Pinkuburu/UnityExplorerPlus) 中找到它），它是为 HollowKnight 制作的。该模组将其中部分功能移植到了丝之歌，并可能对模组开发者有所帮助。

此外，我还参考了 [FSMExpress](https://github.com/nesrak1/FSMExpress) 来实现本模组中的游戏内FSM查看器，这可以帮助你确切地了解FSM是如何运行的。

## 实现的功能

- 在列表视图中显示 Fsm 信息。
- 增加碰撞体/敌人/渲染器的检测功能。
- 显示并导出提取的精灵图和 tk2dsprite。
- 在材质的检视器中显示着色器信息。
- 游戏内 Fsm 查看器。
- ...

## 用于 FSM 查看器

* 当您检查一个 PlayMakerFSM 时，右上角会出现一个标记为“打开 Fsm 检查器”的按钮。点击它即可打开查看器。
* “状态”、“事件”和“变量”页面的功能与在 FSMExpress 中对应的页面完全相同。
* “操作”页面显示当前选定的状态和转换。当“记录”功能开启时，还可以记录 FSM 的转换过程。
* 有时连接线可能会出现在错误的位置。重新打开查看器通常可以解决此问题。如果您知道如何永久修复，请随时提交一个 Pull Request。

## 参考资料

- We have used the code of original UnityExplorerPlus. It uses the MIT license, and the license can be found [here](https://github.com/MCXGK3/UEP/blob/For_CUE/Licenses/LICENSE(UnityExplorerPlus))
