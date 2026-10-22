# FilteredLogs

筛选日志，仅包含指定日志来源的日志。

该模组仅在调用方以调试模式编译时生效。

## 使用方法

该模组应通过在 csproj 文件中添加以下行声明为包引用：

```
<PackageReference Include="Silksong.FilteredLogs" Version="1.0.0" />
```

请参见 [the nuget page](https://www.nuget.org/packages/Silksong.FilteredLogs) 以获取最新版本。

该模组通常应被声明为软依赖项，以确保其在你的模组加载前启动。为此，请在插件类的 BepInAutoPlugin 属性下方添加以下属性：

```
[BepInDependency("io.github.flibber-hk.filteredlogs", BepInDependency.DependencyFlags.SoftDependency)]
```

该模组通常不应在 Thunderstore 中声明为依赖项；编译时在发布模式下，编译器会移除对本模组 API 的所有引用，因此你的模组在其他用户设备上将正常运行。当然，你需要自行下载此模组。

通常，你应该在插件的 awake 方法开头添加以下代码行：

```
FilteredLogs.API.ApplyFilter(Name);
```

这将过滤掉非你模组产生的日志。（当然，在发布模式下编译时，该行会被编译器忽略。）

ApplyFilter 的重载提供了多种方式来过滤日志；更多详情请查阅文档。
