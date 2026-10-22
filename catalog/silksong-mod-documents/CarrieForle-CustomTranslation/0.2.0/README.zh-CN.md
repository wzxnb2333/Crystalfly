# CustomTranslation

在丝之歌中加载自定义翻译/本地化，以支持未支持（或已支持）的语言。

它与 [I18N](https://thunderstore.io/c/hollow-knight-silksong/p/silksong_modding/I18N/) 兼容，允许你在不支持的语言中翻译模组。

## 安装

建议使用 Thunderstore 模组管理器（e.g，[r2modman](https://r2modman.com/)）来简化安装过程。

您也可以手动安装。首先 [install BepinEx](https://docs.bepinex.dev/articles/user_guide/installation/index.html#installing-bepinex-1)。下载模组。前往丝之歌的安装文件夹（您应已安装 BepinEx 的位置），并在 `BepinEx/plugins` 目录下解压模组 zip 文件。此外，还需要安装依赖项，这些依赖项可在 Thunderstore 上找到。

您的文件夹结构应如下所示：

```
.
└── BepinEx/
    └── plugins/
        ├── CarrieForle-CustomTranslation/
        │   ├── CustomTranslation.dll
        │   ├── CustomTranslation.pdb
        │   └── ...
        └── ...
```

## 使用方法

见 [here](https://github.com/CarrieForle/CustomTranslationSilksong/tree/main/docs)。

## 编译

需要 .NET 10。

创建 `SilksongPath.props`。复制并粘贴以下内容，并根据需要进行编辑。

```xml
<Project>
  <PropertyGroup>
    <SilksongFolder>SilksongInstallPath</SilksongFolder>
    <!-- If you use a mod manager rather than manually installing BepInEx, this should be a profile directory for that mod manager. -->
    <SilksongPluginsFolder>$(SilksongFolder)/BepInEx/plugins</SilksongPluginsFolder>
  </PropertyGroup>
</Project>
```

```sh
dotnet build -c Release
```
