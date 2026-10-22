# SaveFileManagerMod

一个空洞骑士：丝之歌模组。为保存文件的命名和归档添加按钮。

## 特性

### 重命名存档文件

- 该模组在每个存档文件下方添加了一个“重命名”按钮，可为存档文件设置自定义名称，便于你管理存档。
- 名称会同步至你的 Steam 云存储。

### 归档存档文件

- 该模组在每个存档文件下方添加了一个“归档”按钮，允许你：
  - 将存档移动到归档目录中，从而释放一个宝贵的 4 存储槽位。
  - 将归档中的存档重新加载到空闲的存档槽中。
  - 交换当前激活的存档与归档中的某个存档。
- 归档后的存档仍会同步至你的 Steam 云存储。
- 如果你安装了 [More Saves] 模组，归档将位于存档列表的后续页面。在这种情况下，本模组仅支持在不同页面之间移动存档。

## 依赖项

- [BepInEx]
- [I18N]（可选）
  - 模组默认以英文显示。如果安装了 I18N，将提供额外的语言支持。
  - 需要注意的是，目前仅对英语和德语进行了人工验证。其余语言由 MTL/AI 提供。

## 安装

### 管理式安装

本模组应与您选择的模组管理工具兼容。如果其中任何一个工具无法正常使用，请打开 [an issue on GitHub](https://github.com/mich101mich/Silksong.SaveFileManagerMod/issues)。

### 手动安装

将下载并解压后的文件放入游戏文件夹中的 `Bepinex/plugins` 文件夹内。

为了使国际化翻译功能正常工作，必须确保 `languages` 文件夹直接位于 `SaveFileManagerMod.dll` 文件的相邻位置，并且两者均处于插件子文件夹（例如 `Bepinex/plugins/SaveFileManagerMod`）中。如果文件直接放置在插件文件夹内，则翻译功能将无法生效。


[More Saves]: https://thunderstore.io/c/hollow-knight-silksong/p/Clazex/MoreSaves/
[BepInEx]: https://thunderstore.io/c/hollow-knight-silksong/p/silksong_modding/BepInExPack_Silksong/
[I18N]: https://thunderstore.io/c/hollow-knight-silksong/p/silksong_modding/I18N/
