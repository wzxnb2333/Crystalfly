# RandomPingSilksong

在随机时刻播放 Discord 提醒音效。

## 安装

手动安装时，首先 [install BepinEx](https://docs.bepinex.dev/articles/user_guide/installation/index.html#installing-bepinex-1)。下载模组。前往丝之歌安装文件夹（你应该已安装 BepinEx 的位置），并将模组 zip 文件解压至 `BepinEx/plugins`。

## 编译

需要 .NET 10。

在 `RandomPing` 目录下创建 `SilksongPath.props`。复制并粘贴以下内容，并根据需要进行编辑。

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