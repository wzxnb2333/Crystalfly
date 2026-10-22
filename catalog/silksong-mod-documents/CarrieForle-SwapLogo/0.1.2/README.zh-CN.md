# SwapLogo

将开场画面的Team Cherry标志替换为另一张图片。

![Demonstration](https://github.com/user-attachments/assets/aabb6f8d-7bbd-4d17-8992-d3eaac57b6b0)

## 使用方法

安装模组（手动或通过模组管理器），并将一个名为 `logo.png` 或 `logo.jpg` 的图像文件放置在 DLL 所在的同一文件夹中。您的文件夹结构应如下所示：

```
.
└── BepinEx/
    └── plugins/
        └── CarrieForle-SwapLogo/
            ├── icon.png
            ├── logo.png <- your image
            ├── manifest.json
            ├── README.md
            ├── SwapLogo.dll
            └── SwapLogo.pdb
```

如果您使用模组管理器（如 e.g 或 r2modman），上述结构应位于配置文件文件夹内。如果手动安装了 BepinEx，则应放置在丝之歌的安装文件夹中。

支持 png 和 jpg 格式。

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
