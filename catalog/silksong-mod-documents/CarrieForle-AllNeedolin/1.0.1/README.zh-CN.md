# AllNeedolin

一次性显示所有需要线消息。

![](screenshots/1.png)
![](screenshots/2.png)

## 安装

安装 BepinEx。下载模组并解压到 plugins 文件夹中。

```
.
└── BepinEx/
    └── plugins/
        └── CarrieForle-AllNeedolin/
            ├── ...
            ├── AllNeedolin.dll
            └── AllNeedolin.pdb
```

如果你使用了模组管理器（e.g.、r2modman），上述结构应位于配置文件夹内。如果手动安装了 BepinEx，它应该位于丝之歌的安装文件夹中。

## 编译

需要 .NET 10。

在 `AllNeedolin` 下创建 `SilksongPath.props`。复制并粘贴以下内容，并根据需要进行编辑。

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