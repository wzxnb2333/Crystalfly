# Silksong.FsmUtil

这是一个用于模组的库，旨在使FSM编辑更加简便。

## 安装

要将 Silksong.FsmUtil 添加到你的模组中，请在 .csproj 文件中添加以下内容：
```
<PackageReference Include="Silksong.FsmUtil" Version="0.3.17" />
```
 最新的版本号可从 [Nuget](https://www.nuget.org/packages/Silksong.FsmUtil) 获取。

你还需要在你的 thunderstore.toml 中添加一个依赖项：
```
silksong_modding-FsmUtil = "0.3.17"
```
 版本号并不关键，但最新的版本号可从  
[Thunderstore](https://thunderstore.io/c/hollow-knight-silksong/p/silksong_modding/FsmUtil/) 获取。  
如果手动上传，请从 Thunderstore 链接复制依赖字符串。

建议通过在插件类中添加以下属性，将 FsmUtil 作为 BepInEx 依赖项：
```
[BepInDependency("org.silksong-modding.fsmutil")]
```
  
（该属性应放置在 BepInAutoPlugin 属性之后。）

# EUPL
                      Copyright (c) 2026 silksong-modding
                      根据 EUPL-1.2 许可
https://joinup.ec.europa.eu/collection/eupl/eupl-text-eupl-12
