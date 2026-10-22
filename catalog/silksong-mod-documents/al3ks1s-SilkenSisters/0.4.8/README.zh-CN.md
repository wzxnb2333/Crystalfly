# 丝之姐妹 - 测试版本

#### /!\ 免责声明：本模组部分功能可能不稳定，因该版本为测试版。请将发现的任何问题提交至 https://github.com/al3ks1s/SilkenSisters/issues

一款为《空洞骑士：丝之歌》添加的回忆战斗模组，战斗中将同时出现 Lace 与 Phantom。

要进入战斗，请满足以下条件：
- 当前处于第 3 关卡
- 已击败 Lace 2 与 Phantom
- 拥有“深海挽歌”技能

通过在 Phantom 的坦克处使用“深海挽歌”技能即可进入战斗。

目前，首领的战斗行为仍沿用原版设定，未来版本中将进行调整。

## 手动安装

### 不建议进行手动安装，因为该模组需要依赖项。建议通过 Thunderstore、r2modman 或 Gale 安装模组，以避免问题。

该模组需要以下模组才能正常运行：  
- [BepinEx version](https://thunderstore.io/c/hollow-knight-silksong/p/BepInEx/BepInExPack_Silksong/)  
- [FSMUtils](https://thunderstore.io/c/hollow-knight-silksong/p/silksong_modding/FsmUtil/)  
- [I18N](https://thunderstore.io/c/hollow-knight-silksong/p/silksong_modding/I18N/)  
- [UnityHelper](https://thunderstore.io/c/hollow-knight-silksong/p/silksong_modding/UnityHelper/)  
- [AssetHelper](https://thunderstore.io/c/hollow-knight-silksong/p/silksong_modding/AssetHelper/)  
- [DataManager](https://thunderstore.io/c/hollow-knight-silksong/p/silksong_modding/DataManager/)  
- [SilksongPrepatcher](https://thunderstore.io/c/hollow-knight-silksong/p/silksong_modding/SilksongPrepatcher/)  
- [MonoDetour BepInEx 5](https://thunderstore.io/c/hollow-knight-silksong/p/MonoDetour/MonoDetour_BepInEx_5/)  
- [MonoDetour](https://thunderstore.io/c/hollow-knight-silksong/p/MonoDetour/MonoDetour/)  

1. 下载 BepinEx 并将其解压到游戏目录中  
2. 运行游戏一次以生成必要的文件  
3. 下载并安装 FSMUtils 模组到 BepinEx/plugins 文件夹中  
4. 下载并安装 I18N 模组到 BepinEx/plugins 文件夹中  
5. 下载并安装本模组到 BepinEx/plugins 文件夹中的独立 "SilkenSisters" 文件夹内  

预期的文件结构如下：  
```
BepInEx/
	plugins/
		SilkenSisters/
			<SilkenSisters files>
		silksong_modding-I18N-1.0.2/
			<I18N files>
		silksong_modding-FsmUtil-0.3.5/
			<FSMUtil files>
		silksong_modding-AssetHelper-1.0.1/
			<AssetHelper files>
		silksong_modding-UnityHelper-1.1.1/
			<UnityHelper files>
		<Other mods>
	patchers/
		<monodetour bepinex 5 dlls directly in the folder>
		<Silksong Prepatchers files from /patchers>
	core/
		<monodetour dlls directly in the folder
```