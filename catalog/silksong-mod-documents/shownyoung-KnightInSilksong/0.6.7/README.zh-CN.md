# KnightInSilkSong

## 描述

它允许你在游戏中在小骑士和大黄蜂之间切换。

## 安装

1. 如果尚未安装，请为《空洞骑士：丝之歌》安装 [BepInEx](https://github.com/BepInEx/BepInEx/releases)。如果没有为《空洞骑士：丝之歌》下载过BepInEx模组，请下载[BepInEx](https://github.com/BepInEx/BepInEx/releases)。
2. 从 [Releases](https://github.com/MCXGK3/KnightInSilkSong/releases) 页面下载最新的空洞骑士模组版本。从 [Releases](https://github.com/MCXGK3/KnightInSilkSong/releases) 界面获取最新的发行版。
3. 解压下载的 ZIP 文件，并将其内容复制到BepInEx's `plugins`文件夹。将下载的zip文件解压，并复制到BepInEx的`plugins`文件夹。
4. 如果你使用 ThunderStore，你可以直接从 [there](https://thunderstore.io/c/hollow-knight-silksong/p/shownyoung/KnightInSilksong/) 下载，而无需遵循上述步骤。
如果你使用 ThunderStore，那么你可以跳过以上步骤，直接在这里下载。

> 开发并测试于 Windows 平台。在其他平台上的兼容性无法保证。
在Windows平台上进行开发与测试，其他平台的兼容性无法保证。

## 基础使用方法

1. 启动丝之歌版空洞骑士并加载 BepInEx。打开安装了BepInEx与本模组的游戏本体。
2. 在游戏过程中，按你设定的按键（默认为F5）来在小骑士和大黄蜂之间切换。
3. 要调整骑士的状态（例如钉子伤害等），你可以修改的`BepInEx\config\SlotX\PlayerData.json`（在保存一次后出现），以及其他设置（例如伤害倍率等）可以通过编辑 ‘Hollow Knight’SilksongBepInExconfigio.github.shownyoung.knightinsilksong.cfg'.要对小骑士的状态（如骨钉伤害等）进行调整，可以修改 `BepInEx\config\SlotX\PlayerData.json`文件（保存一次后出现），其他设置（如伤害修正等）可以修改 `Hollow Knight Silksong\BepInEx\config\io.github.shownyoung.knightinsilksong.cfg`来完成。

4同步能力的选项也设置在`BepInEx\config\SlotX\SlotSetting.cfg`. 同步能力的选项也在 `BepInEx\config\SlotX\SlotSetting.cfg`中设置。

5. 本模组现已支持皮肤模组，其在 customknight 中的皮肤图片对应关系如下（以 Patchwork 为例）。但请注意，在丝之歌中使用皮肤前，请务必获得皮肤创作者的许可。

| 空洞骑士中的名称 | Patchwork 的路径                                                  |
| -------------- | -------------------------------------------------------------------- |
| Knight.png     | 补丁工作坊\精灵图集\空洞巢穴\atlas0.png                 |
| Hud.png        | Patchwork\Spritesheets\HUD Cln(空洞巢穴)\atlas0.png                |
| Geo.png        | Patchwork\Spritesheets\Geo(空洞巢穴)\atlas0.png                    |
| Hatchling.png  | 补丁工作坊\精灵图集\空洞骑士幼体(Hollownest)\atlas0.png  |
| Wraiths.png    | Patchwork\Spritesheets\法术效果 2(空巢)\atlas0.png       |
| Grimm.png      | 补丁工作坊\精灵图集\空巢之子 Cln(空洞巢穴)\atlas0.png        |
| VoidSpells.png | 补丁工作坊\精灵图集\中立法术效果（空巢）\atlas0.png |
| Unn.png        | Patchwork\Spritesheets\\Knight Slug Cln(空洞巢)\\atlas0.png       |
| VS.png         | 补丁工作坊\精灵图集\法术效果（空巢）\atlas0.png         |
| Baldur.png     | 补丁工作坊\精灵图集\护身符阻断器 Cln(空巢)\atlas0.png     |
| Shield.png     | 补丁工作坊\精灵图集\轨道护盾 Cln(空巢)\atlas0.png      |
| Weaver.png     | 补丁工作坊\精灵图集\空巢编织者(Hollownest)\atlas0.png        |
| Liquid.png     | Patchwork\Spritesheets\\HUD_Soulorb_fills(空洞巢穴)\atlas0.png     |
| Sprint.png     | 补丁工作坊\精灵图集\空洞骑士梦境之门Cln(空洞巢)\atlas0.png |
| OrbFull.png    | 补丁工作坊\精灵\T2D\soul_orb_glow0000.png                          |

## 特别致谢

感谢@olvior为使小骑士能够正常完成游戏所做的努力。

感谢@ManicJamie帮助更新了与 DebugMod 1.0 的兼容性。

## 未来计划

- 修复许多漏洞。  
- 实现结局 2 和 3 所需的处理过程。

## 注意事项

- 此模组不得移植至盗版版本，例如丝之歌的移动版。


技术标识：`ManicJamie`、`DebugMod`
