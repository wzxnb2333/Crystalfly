# Glissando
或：「即便是巨龙也会转身」

一款空洞骑士：丝之歌模组，完全用翻转重力替代跳跃功能。

受Terry Cavanagh所创作游戏《VVVVVV》的启发。由TherminatorX赞助并进行实机测试。

双跳也在本模组中被修改了。为了使移动更加合理，一旦浮空和双跳都被解锁，你始终可以按住向下键并在空中跳跃以实现浮空。

模组选项中包含强制使危险区域重新生成的功能，可通过菜单按钮或键位触发，以防大黄蜂被困在某处或超出边界。

动作 1 和 2 已经过测试，但你仍可能遇到一些问题。如果发现漏洞，请 [open an issue on the Github repository](https://github.com/kaycodes13/Silksong.Glissando/issues)。

**热的挑战：** 在禁用“Mount Fay”升级的情况下，能击败这个模组中的多少内容？

捐赠链接：https://ko-fi.com/kaykao

## 安装

通过 r2modman 或 Thunderstore 模组管理器进行安装。Glissando 的依赖项将自动安装。

对于手动安装，请解压 `.zip` 文件，并将生成的 **文件夹** 放入您 Silksong 安装目录下的 `BepInEx/plugins` 目录中。此外，您还需要手动安装：
* [FsmUtil](<https://thunderstore.io/c/hollow-knight-silksong/p/silksong_modding/FsmUtil/>)
* [I18N](<https://thunderstore.io/c/hollow-knight-silksong/p/silksong_modding/I18N/>)
* [ModMenu](<https://thunderstore.io/c/hollow-knight-silksong/p/silksong_modding/ModMenu/>) 及其依赖项

## 针对模组开发者

如果你开发的模组在任何时刻会修改大黄蜂的速度，你设置的 y 方向速度**可能不会自动翻转**，当“丝之歌”切换重力时。这一点对于自定义纹章和工具尤其重要，因为你在其中添加的任何新 FSM 行为**都不会受到该模组的影响**。

如果你想让自己的模组与“丝之歌”完全兼容，可以检查静态布尔属性 `GlissandoPlugin.GravityIsFlipped` 的值，并在其为 true 时翻转相关的 y 方向速度或移动行为。
