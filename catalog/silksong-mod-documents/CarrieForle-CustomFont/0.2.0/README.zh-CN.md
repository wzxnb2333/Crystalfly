# CustomFont

用一个 OpenType 或 TrueType 字体替换字体。适用于玩家和开发者。

## 限制

并非所有字体都可以替换。具体来说，游戏菜单和暂停菜单中的字体无法替换，其余部分均可以。

## 玩家使用方式

安装该模组（可通过手动方式或使用模组管理器），并将一个名为 `font.otf` 或 `font.ttf` 的字体文件放置在 DLL 所在的同一文件夹中。您的文件夹结构应如下所示：

```
.
└── BepinEx/
    └── plugins/
        └── CarrieForle-CustomFont/
            ├── icon.png
            ├── font.otf <- your font
            ├── manifest.json
            ├── README.md
            ├── CustomFont.dll
            └── CustomFont.pdb
```

如果你使用了模组管理器（e.g，r2modman），上述结构应位于配置文件夹内。如果你手动安装了 BepinEx，它应该位于丝之歌的安装文件夹中。

### 选项

前往 选项 > 模组 > 自定义字体

#### 所有角色地图集

生成的字体图集包含游戏中未出现的字符。如果设置错误，则只会包含游戏中实际出现的字符。该更改将在加载存档时生效。

> [!WARNING]
启用此选项如果字体文件较大（>1MB），将会导致明显的加载延迟，可能需要几秒到几分钟的时间。

#### 字体缩放

字体大小的倍数 e.g，1.5 表示比原大小大 150%。

#### 替换字体模式

自定义字体的应用位置：
- 禁用：不应用自定义字体
- 受语言影响：仅对受语言影响的文本应用自定义字体，例如对话内容。物品数量、按钮提示等文本不会被应用 e.g。
- 所有项目：为所有可能的文本应用自定义字体（实验性）

> [!NOTE]
即使你使用了“全部”，菜单中的文字以及一些场景对话仍保持不变，因为这些内容使用了本模组未涵盖的不同 UI 框架。

## 开发者使用说明

见 [documentation](https://carrieforle.github.io/CustomFontSilksong/)。

## 编译

需要 .NET 10。

在 `CustomFont` 下创建 `SilksongPath.props`。复制并粘贴以下内容，并根据需要进行编辑。

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

### 编译文档

```sh
dotnet tool restore
dotnet docfx docfx/docfx.json --serve
```
