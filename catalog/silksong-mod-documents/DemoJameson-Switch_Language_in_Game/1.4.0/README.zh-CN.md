# 游戏内切换语言  
一款适用于《空洞骑士：丝之歌》的模组，用于支持游戏内的语言切换功能。

## 特性

* 支持游戏内的语言切换。  
* 支持通过快捷键进行语言切换。（需先手动配置）

## 配置
建议使用 [BepInEx.ConfigurationManager](https://thunderstore.io/c/hollow-knight-silksong/p/jakobhellermann/BepInExConfigurationManager/) 以方便在游戏中自定义设置。
如果您已安装（游戏中按 F1 打开菜单），请前往“游戏内切换语言”部分直接调整选项——无需编辑文件。

 alternatively，编辑位于 `BepInEx\config\com.demojameson.switchlanguageingame.cfg` 的配置文件。

## 安装方法

### Thunderstore
一切都会被自动处理。

### 手动安装
首先将 BepInEx 安装到你的丝之歌文件夹中，  
（注意：这将导致 Thunderstore 的功能失效）

你可以从以下位置找到它：  
https://github.com/BepInEx/BepInEx/releases  
当前最新稳定版本为 5.4.23.4

解压后，运行游戏一次，以生成 BepInEx 的文件夹结构  
（即：文件夹中除了 `core` 之外，还应存在其他文件夹）

然后将此 DLL 文件，或包含 DLL 的文件夹，放入  
`Hollow Knight Silksong\BepInEx\plugins`

## 源码  
GitHub: [https://github.com/DemoJameson/Silksong.SwitchLanguageInGame](https://github.com/DemoJameson/Silksong.SwitchLanguageInGame)